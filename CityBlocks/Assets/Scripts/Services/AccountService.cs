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
        public enum LinkResult { Linked, AlreadyLinked, Cancelled, Unsupported, Failed }

        // Game Center's sign-in sheet can stay open indefinitely, so sign-in only waits this long before falling back.
        private const int GameCenterWaitMilliseconds = 6000;
        private const string KindPrefsKey = "cityblocks_account_kind";
        // Set once the player picks Apple or Guest on the sign-in panel; the panel never shows again after that.
        private const string ChoicePrefsKey = "cityblocks_signin_choice";

        private static Task<global::Apple.GameKit.GKLocalPlayer> gameCenterAuth;
        private static string cachedKind;
        private static global::AppleAuth.AppleAuthManager appleAuth;
        private static bool appleLinked;
        private static bool gameCenterLinked;
        // Game Center is signed in on the device but already belongs to a different player (never force-linked).
        private static bool gameCenterConflict;
        // Apple identity token kept after an AccountAlreadyLinked result so the player can choose to switch.
        private static string pendingAppleToken;

        /// <summary>Fires when the account kind changes (e.g. Game Center linked in the background).</summary>
        public static event Action<string> KindChanged;

        // A PlayerId alone is insufficient: A -> B -> A must invalidate the first A request.
        public static long SessionRevision { get; private set; }
        public static event Action<string> SessionChanged;
        private static Unity.Services.Authentication.IAuthenticationService observedAuth;

        internal static void ObserveAuthentication()
        {
            if (observedAuth != null) return;
            observedAuth = Unity.Services.Authentication.AuthenticationService.Instance;
            observedAuth.SignedIn += OnSessionSignedIn;
            observedAuth.SignedOut += OnSessionSignedOut;
        }

        private static void OnSessionSignedIn() => PublishSession(observedAuth.PlayerId);
        private static void OnSessionSignedOut() => PublishSession(null);
        private static void PublishSession(string playerId)
        {
            SessionRevision++;
            try { SessionChanged?.Invoke(playerId); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (observedAuth != null)
            {
                observedAuth.SignedIn -= OnSessionSignedIn;
                observedAuth.SignedOut -= OnSessionSignedOut;
                observedAuth = null;
            }
            SessionRevision++;
            SessionChanged = null;
            gameCenterAuth = null;
            cachedKind = null;
            appleAuth = null;
            appleLinked = false;
            gameCenterLinked = false;
            gameCenterConflict = false;
            pendingAppleToken = null;
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

        public static bool IsAppleLinked => appleLinked;
        public static bool IsGameCenterLinked => gameCenterLinked;
        /// <summary>True when Game Center is signed in on this device but its account belongs to another player.</summary>
        public static bool HasGameCenterConflict => gameCenterConflict && !gameCenterLinked;
        public static string PlayerId => CloudBootstrap.IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

        /// <summary>The Game Center nickname when Game Center is signed in on this device, otherwise null.</summary>
        public static string GameCenterAlias =>
            GameCenter == GameCenterStatus.Authenticated ? gameCenterAuth.Result.Alias : null;

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
            ObserveAuthentication();
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
                gameCenterLinked = hasGameCenter;
                appleLinked = !string.IsNullOrEmpty(info.GetAppleId());
                SetKind(KindFrom(info));
                if (hasGameCenter) return;

                global::Apple.GameKit.GKLocalPlayer player = await WaitForGameCenterAsync(0);
                if (player == null || !CloudBootstrap.IsSignedIn) return;

                global::Apple.GameKit.Players.GKIdentityVerificationResponse items = await player.FetchItems();
                await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(
                    Convert.ToBase64String(items.GetSignature()), player.TeamPlayerId, items.PublicKeyUrl,
                    Convert.ToBase64String(items.GetSalt()), items.Timestamp);
                gameCenterLinked = true;
                gameCenterConflict = false;
                SetKind(KindGameCenter);
                Gameplay.LevelAnalytics.AccountLinked(KindGameCenter, "background");
                Debug.Log("[AccountService] Linked Game Center to PlayerId=" + AuthenticationService.Instance.PlayerId);
            }
            catch (AuthenticationException exception) when (exception.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                // This Game Center account already belongs to another player. Never force-link; Account Settings
                // offers a "Switch to that account?" choice.
                gameCenterConflict = true;
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
                ObserveAuthentication();
                bool isNewPlayer = false;
                if (CloudBootstrap.IsSignedIn)
                {
                    try
                    {
                        await AuthenticationService.Instance.LinkWithAppleAsync(idToken);
                        appleLinked = true;
                        Gameplay.LevelAnalytics.AccountLinked(KindApple, "signin_panel");
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

        /// <summary>
        /// Account Settings: links Sign in with Apple to the current player. Unlike the first-run panel this never
        /// switches automatically; if the Apple ID already belongs to another player it returns AlreadyLinked and keeps
        /// the token so SwitchToAppleAccountAsync can be offered.
        /// </summary>
        public static async Task<LinkResult> LinkAppleAsync()
        {
            string idToken;
            try { idToken = await RequestAppleIdTokenAsync(); }
            catch (TaskCanceledException) { return LinkResult.Cancelled; }
            catch (Exception exception)
            {
                Debug.LogWarning("[AccountService] Sign in with Apple failed: " + exception.Message);
                return LinkResult.Failed;
            }
            if (idToken == null) return LinkResult.Unsupported;
            if (!CloudBootstrap.IsSignedIn) return LinkResult.Failed;

            try
            {
                await AuthenticationService.Instance.LinkWithAppleAsync(idToken);
                appleLinked = true;
                pendingAppleToken = null;
                SaveChoice(KindApple);
                SetKind(gameCenterLinked ? KindGameCenter : KindApple);
                Gameplay.LevelAnalytics.AccountLinked(KindApple, "settings");
                Debug.Log("[AccountService] Linked Apple ID to PlayerId=" + AuthenticationService.Instance.PlayerId);
                return LinkResult.Linked;
            }
            catch (AuthenticationException exception) when (exception.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                pendingAppleToken = idToken;
                return LinkResult.AlreadyLinked;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AccountService] Could not link the Apple ID: " + exception.Message);
                return LinkResult.Failed;
            }
        }

        /// <summary>Signs out of the current player and into the player that owns the Apple ID from LinkAppleAsync.</summary>
        public static Task<bool> SwitchToAppleAccountAsync()
        {
            string token = pendingAppleToken;
            if (string.IsNullOrEmpty(token)) return Task.FromResult(false);
            return SwitchAsync(KindApple, () => AuthenticationService.Instance.SignInWithAppleAsync(token));
        }

        /// <summary>Signs out of the current player and into the player that owns this device's Game Center account.</summary>
        public static async Task<bool> SwitchToGameCenterAccountAsync()
        {
            global::Apple.GameKit.GKLocalPlayer player = await WaitForGameCenterAsync(0);
            if (player == null) return false;
            return await SwitchAsync(KindGameCenter, () => SignInWithGameCenterAsync(player));
        }

        /// <summary>
        /// Replaces the current player with another existing one. The current player's credentials are cleared, so a
        /// guest account is left behind; local progress stays on the device. If signing in to the other player fails,
        /// falls back to the normal sign-in so the game is never left signed out.
        /// </summary>
        private static async Task<bool> SwitchAsync(string kind, Func<Task> signIn)
        {
            ObserveAuthentication();
            string previousPlayer = PlayerId;
            try
            {
                AuthenticationService.Instance.SignOut(true);
                await signIn();
                pendingAppleToken = null;
                if (kind == KindGameCenter) { gameCenterConflict = false; gameCenterLinked = true; }
                if (kind == KindApple) appleLinked = true;
                SaveChoice(kind);
                SetKind(kind);
                Debug.Log("[AccountService] Switched from PlayerId=" + previousPlayer + " to " + AuthenticationService.Instance.PlayerId + " via " + kind);
                LogSignedIn(kind, false);
                _ = RefreshKindAndLinkGameCenterAsync();
                _ = PlayerWallet.RefreshAsync();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AccountService] Could not switch account: " + exception.Message);
                if (!CloudBootstrap.IsSignedIn) await CloudBootstrap.EnsureSignedInAsync();
                return false;
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
