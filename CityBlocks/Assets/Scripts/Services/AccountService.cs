using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Decides how the player signs in and which account type they have.
    /// Order: cached session (instant, any linked account) → Game Center (silent when the device is signed in;
    /// iOS shows its own "Welcome back" banner) → anonymous fallback. When the player ends up anonymous or on a
    /// cached session without Game Center, Game Center is linked in the background as soon as it authenticates,
    /// so a reinstall on the same Apple ID restores the same PlayerId.
    /// Game Center is only used on iOS devices (never in the Editor).
    /// </summary>
    public static class AccountService
    {
        public const string KindGameCenter = "gamecenter";
        public const string KindApple = "apple";
        public const string KindAnonymous = "anonymous";
        public const string KindGuest = "guest";
        public const string KindSignedOut = "signed_out";

        public enum GameCenterStatus { Unsupported, Pending, Authenticated, Failed }
        public enum AppleSignInResult { Success, Cancelled, Unsupported, Failed }

        // Game Center's sign-in sheet can stay open indefinitely, so sign-in only waits this long before falling back.
        private const int GameCenterWaitMilliseconds = 6000;
        private const string KindPrefsKey = "cityblocks_account_kind";
        // Set once the player picks Apple or Guest on the sign-in panel; the panel never shows again after that.
        private const string ChoicePrefsKey = "cityblocks_signin_choice";

        private static Task<global::Apple.GameKit.GKLocalPlayer> gameCenterAuth;
        private static string cachedKind;
        private static global::AppleAuth.AppleAuthManager appleAuth;

        /// <summary>Fires when the account kind changes (e.g. Game Center linked in the background).</summary>
        public static event Action<string> KindChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            gameCenterAuth = null;
            cachedKind = null;
            appleAuth = null;
            KindChanged = null;
        }

        public static bool GameCenterSupported => Application.platform == RuntimePlatform.IPhonePlayer;

        public static GameCenterStatus GameCenter
        {
            get
            {
                if (!GameCenterSupported) return GameCenterStatus.Unsupported;
                if (gameCenterAuth == null || !gameCenterAuth.IsCompleted) return GameCenterStatus.Pending;
                return gameCenterAuth.Status == TaskStatus.RanToCompletion && gameCenterAuth.Result != null &&
                       gameCenterAuth.Result.IsAuthenticated ? GameCenterStatus.Authenticated : GameCenterStatus.Failed;
            }
        }

        public static bool HasMadeSignInChoice => PlayerPrefs.HasKey(ChoicePrefsKey);

        /// <summary>
        /// True when the sign-in panel should be offered: signed in with no linked account, no earlier Apple/Guest
        /// choice, and Game Center has finished without signing in (cancelled / signed out on this device).
        /// </summary>
        public static bool ShouldOfferSignIn =>
            CloudBootstrap.IsSignedIn && Kind == KindAnonymous && !HasMadeSignInChoice &&
            (GameCenter == GameCenterStatus.Failed || GameCenter == GameCenterStatus.Unsupported);

        /// <summary>Drives Sign in with Apple callbacks; call every frame (CloudBootstrap does).</summary>
        public static void Tick() => appleAuth?.Update();

        /// <summary>Account type for analytics/crash metadata: gamecenter, apple, anonymous or signed_out.</summary>
        public static string Kind
        {
            get
            {
                if (!CloudBootstrap.IsSignedIn) return KindSignedOut;
                if (cachedKind == null) cachedKind = PlayerPrefs.GetString(KindPrefsKey, KindAnonymous);
                return cachedKind;
            }
        }

        /// <summary>
        /// Signs in after UnityServices is initialized. Throws like the Authentication SDK on failure
        /// (CloudBootstrap handles offline/failure fallback).
        /// </summary>
        public static async Task SignInAsync()
        {
            StartGameCenter();

            if (AuthenticationService.Instance.SessionTokenExists)
            {
                // Restores the same player silently, whatever accounts are linked to it.
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                LogSignedIn("session", false);
                _ = RefreshKindAndLinkGameCenterAsync();
                return;
            }

            global::Apple.GameKit.GKLocalPlayer localPlayer = await WaitForGameCenterAsync(GameCenterWaitMilliseconds);
            if (localPlayer != null)
            {
                try
                {
                    await SignInWithGameCenterAsync(localPlayer);
                    bool isNew = await IsNewPlayerAsync();
                    SetKind(KindGameCenter);
                    LogSignedIn(KindGameCenter, isNew);
                    return;
                }
                catch (Exception exception) when (!(exception is RequestFailedException) || exception is AuthenticationException)
                {
                    Debug.LogWarning("[AccountService] Game Center sign-in failed, falling back to anonymous: " + exception.Message);
                }
            }

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            SetKind(KindAnonymous);
            LogSignedIn(KindAnonymous, true);
            _ = RefreshKindAndLinkGameCenterAsync();
        }

        private static void StartGameCenter()
        {
            if (!GameCenterSupported || gameCenterAuth != null) return;
            try { gameCenterAuth = global::Apple.GameKit.GKLocalPlayer.Authenticate(); }
            catch (Exception exception) { Debug.LogWarning("[AccountService] Game Center unavailable: " + exception.Message); }
        }

        /// <summary>Returns the authenticated local player, or null if Game Center failed, was cancelled or is still pending.</summary>
        private static async Task<global::Apple.GameKit.GKLocalPlayer> WaitForGameCenterAsync(int timeoutMilliseconds)
        {
            if (gameCenterAuth == null) return null;
            if (timeoutMilliseconds > 0 && await Task.WhenAny(gameCenterAuth, Task.Delay(timeoutMilliseconds)) != gameCenterAuth) return null;
            try
            {
                global::Apple.GameKit.GKLocalPlayer player = await gameCenterAuth;
                return player != null && player.IsAuthenticated ? player : null;
            }
            catch (Exception exception)
            {
                Debug.Log("[AccountService] Game Center not signed in: " + exception.Message);
                return null;
            }
        }

        private static async Task SignInWithGameCenterAsync(global::Apple.GameKit.GKLocalPlayer player)
        {
            global::Apple.GameKit.Players.GKIdentityVerificationResponse items = await player.FetchItems();
            await AuthenticationService.Instance.SignInWithAppleGameCenterAsync(
                Convert.ToBase64String(items.GetSignature()), player.TeamPlayerId, items.PublicKeyUrl,
                Convert.ToBase64String(items.GetSalt()), items.Timestamp);
        }

        /// <summary>
        /// Reads the linked identities to set the account kind, then links Game Center if it authenticates
        /// (no timeout: the player may finish Apple's sign-in sheet later) and isn't linked yet.
        /// </summary>
        private static async Task RefreshKindAndLinkGameCenterAsync()
        {
            try
            {
                PlayerInfo info = await AuthenticationService.Instance.GetPlayerInfoAsync();
                bool hasGameCenter = !string.IsNullOrEmpty(info.GetAppleGameCenterId());
                SetKind(KindFrom(info));
                if (hasGameCenter) return;

                global::Apple.GameKit.GKLocalPlayer player = await WaitForGameCenterAsync(0);
                if (player == null || !CloudBootstrap.IsSignedIn) return;

                global::Apple.GameKit.Players.GKIdentityVerificationResponse items = await player.FetchItems();
                await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(
                    Convert.ToBase64String(items.GetSignature()), player.TeamPlayerId, items.PublicKeyUrl,
                    Convert.ToBase64String(items.GetSalt()), items.Timestamp);
                SetKind(KindGameCenter);
                Debug.Log("[AccountService] Linked Game Center to PlayerId=" + AuthenticationService.Instance.PlayerId);
            }
            catch (AuthenticationException exception) when (exception.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                // This Game Center account already belongs to another player. Never force-link; Step 17 offers
                // a "Switch to that account?" choice.
                Debug.LogWarning("[AccountService] Game Center is already linked to a different player; not linking.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AccountService] Could not refresh account / link Game Center: " + exception.Message);
            }
        }

        /// <summary>
        /// Shows Apple's sign-in sheet, then links the Apple ID to the current player (keeps PlayerId and progress).
        /// If that Apple ID already belongs to another player, switches to that player instead: the anonymous player
        /// created this launch has no linked account to lose yet, and local progress stays on the device.
        /// </summary>
        public static async Task<AppleSignInResult> SignInWithAppleAsync()
        {
            string idToken;
            try { idToken = await RequestAppleIdTokenAsync(); }
            catch (TaskCanceledException) { return AppleSignInResult.Cancelled; }
            catch (Exception exception)
            {
                Debug.LogWarning("[AccountService] Sign in with Apple failed: " + exception.Message);
                return AppleSignInResult.Failed;
            }
            if (idToken == null) return AppleSignInResult.Unsupported;

            try
            {
                bool isNewPlayer = false;
                if (CloudBootstrap.IsSignedIn)
                {
                    try
                    {
                        await AuthenticationService.Instance.LinkWithAppleAsync(idToken);
                        Debug.Log("[AccountService] Linked Apple ID to PlayerId=" + AuthenticationService.Instance.PlayerId);
                    }
                    catch (AuthenticationException exception) when (exception.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                    {
                        Debug.Log("[AccountService] Apple ID already has a player; switching to it.");
                        AuthenticationService.Instance.SignOut(true);
                        await AuthenticationService.Instance.SignInWithAppleAsync(idToken);
                    }
                }
                else
                {
                    await AuthenticationService.Instance.SignInWithAppleAsync(idToken);
                    isNewPlayer = await IsNewPlayerAsync();
                }

                SaveChoice(KindApple);
                SetKind(KindApple);
                LogSignedIn(KindApple, isNewPlayer);
                return AppleSignInResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AccountService] Could not sign in with the Apple ID: " + exception.Message);
                return AppleSignInResult.Failed;
            }
        }

        /// <summary>Keeps the current anonymous player and remembers the choice so the panel never shows again.</summary>
        public static void ContinueAsGuest()
        {
            SaveChoice(KindGuest);
            if (Kind == KindAnonymous) SetKind(KindGuest);
            LogSignedIn(KindGuest, false);
        }

        /// <summary>Returns the Apple identity token, null when Sign in with Apple isn't available, or throws/cancels.</summary>
        private static Task<string> RequestAppleIdTokenAsync()
        {
            if (!global::AppleAuth.AppleAuthManager.IsCurrentPlatformSupported) return Task.FromResult<string>(null);
            if (appleAuth == null) appleAuth = new global::AppleAuth.AppleAuthManager(new global::AppleAuth.Native.PayloadDeserializer());

            TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
            // No name/email requested: Unity Authentication only needs the identity token.
            appleAuth.LoginWithAppleId(new global::AppleAuth.AppleAuthLoginArgs(global::AppleAuth.Enums.LoginOptions.None),
                credential =>
                {
                    global::AppleAuth.Interfaces.IAppleIDCredential appleId = credential as global::AppleAuth.Interfaces.IAppleIDCredential;
                    if (appleId?.IdentityToken == null) completion.TrySetException(new Exception("Apple returned no identity token."));
                    else completion.TrySetResult(System.Text.Encoding.UTF8.GetString(appleId.IdentityToken));
                },
                error =>
                {
                    if (global::AppleAuth.Extensions.AppleErrorExtensions.GetAuthorizationErrorCode(error) == global::AppleAuth.Enums.AuthorizationErrorCode.Canceled)
                        completion.TrySetCanceled();
                    else completion.TrySetException(new Exception("Apple error " + error.Code + ": " + error.LocalizedDescription));
                });
            return completion.Task;
        }

        private static void SaveChoice(string choice)
        {
            PlayerPrefs.SetString(ChoicePrefsKey, choice);
            PlayerPrefs.Save();
        }

        private static string KindFrom(PlayerInfo info)
        {
            if (!string.IsNullOrEmpty(info.GetAppleGameCenterId())) return KindGameCenter;
            if (!string.IsNullOrEmpty(info.GetAppleId())) return KindApple;
            return PlayerPrefs.GetString(ChoicePrefsKey, "") == KindGuest ? KindGuest : KindAnonymous;
        }

        /// <summary>A player created within the last few minutes counts as new (Game Center may create or restore).</summary>
        private static async Task<bool> IsNewPlayerAsync()
        {
            try
            {
                PlayerInfo info = await AuthenticationService.Instance.GetPlayerInfoAsync();
                return info.CreatedAt.HasValue && (DateTime.UtcNow - info.CreatedAt.Value.ToUniversalTime()).TotalMinutes < 5;
            }
            catch (Exception) { return false; }
        }

        private static void SetKind(string kind)
        {
            if (kind == cachedKind) return;
            cachedKind = kind;
            PlayerPrefs.SetString(KindPrefsKey, kind);
            PlayerPrefs.Save();
            try { KindChanged?.Invoke(kind); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void LogSignedIn(string method, bool isNewPlayer)
        {
            Debug.Log("[AccountService] Signed in via " + method + ". PlayerId=" + AuthenticationService.Instance.PlayerId +
                      (isNewPlayer ? " (new player)" : ""));
            Gameplay.LevelAnalytics.SignIn(method, isNewPlayer);
        }
    }
}
