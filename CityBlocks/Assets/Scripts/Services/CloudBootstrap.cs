using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace CloudServices
{
    public enum CloudState { Offline, Initializing, SigningIn, Online }

    /// <summary>
    /// Initializes Unity Gaming Services and signs the player in.
    /// Boot order: UnityServices.InitializeAsync (environment chosen by build type) → cached session or anonymous sign-in.
    /// Step 15 replaces the anonymous fallback with Game Center / Sign in with Apple.
    /// </summary>
    public sealed class CloudBootstrap : MonoBehaviour
    {
        // Editor and development builds talk to "development"; release builds to "production".
        // Each environment uses its own auth profile so cached session tokens never cross environments (EnvironmentMismatch).
        private const string DevelopmentEnvironment = "development";
        private const string ProductionEnvironment = "production";

        private static CloudBootstrap instance;
        private static TaskCompletionSource<bool> readySource = new TaskCompletionSource<bool>();
        private static Task<bool> signInTask;

        /// <summary>Completes when the first sign-in attempt finishes: true = signed in, false = offline fallback.</summary>
        public static Task<bool> Ready => readySource.Task;
        public static CloudState State { get; private set; } = CloudState.Offline;
        public static event Action<CloudState> StateChanged;

        public static string EnvironmentName => Application.isEditor || Debug.isDebugBuild ? DevelopmentEnvironment : ProductionEnvironment;
        public static bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
        public static string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Keeps statics correct when Domain Reload is disabled in Enter Play Mode Options.
            instance = null;
            readySource = new TaskCompletionSource<bool>();
            signInTask = null;
            State = CloudState.Offline;
            StateChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            GameObject bootstrap = new GameObject(nameof(CloudBootstrap));
            DontDestroyOnLoad(bootstrap);
            instance = bootstrap.AddComponent<CloudBootstrap>();
        }

        private async void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            AccountService.KindChanged += _ => UpdateCrashReportMetadata();

            bool signedIn = await EnsureSignedInAsync();
            readySource.TrySetResult(signedIn);
        }

        private void Update() => AccountService.Tick();

        /// <summary>
        /// Initializes services if needed and signs in (cached session first, anonymous otherwise).
        /// Safe to call repeatedly; concurrent callers share one attempt. Returns false when offline or on failure.
        /// </summary>
        public static Task<bool> EnsureSignedInAsync()
        {
            if (IsSignedIn) return Task.FromResult(true);
            if (signInTask == null || signInTask.IsCompleted) signInTask = InitializeAndSignInAsync();
            return signInTask;
        }

        private static async Task<bool> InitializeAndSignInAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    SetState(CloudState.Initializing);
                    InitializationOptions options = new InitializationOptions()
                        .SetEnvironmentName(EnvironmentName)
                        .SetProfile(EnvironmentName);
                    await UnityServices.InitializeAsync(options);
                }
                else if (UnityServices.State == ServicesInitializationState.Initializing)
                {
                    // Another package started initialization first; our environment/profile options were not applied.
                    Debug.LogWarning("[CloudBootstrap] Unity Services were initialized by another system before CloudBootstrap.");
                    while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
                }
                SubscribeAuthEvents();

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    SetState(CloudState.Online);
                    return true;
                }

                SetState(CloudState.SigningIn);
                // Cached session → Game Center (iOS) → anonymous; see AccountService.
                await AccountService.SignInAsync();
                Debug.Log("[CloudBootstrap] Signed in. Environment=" + EnvironmentName + " PlayerId=" +
                          AuthenticationService.Instance.PlayerId + " Account=" + AccountKind);
                SetState(CloudState.Online);
                return true;
            }
            catch (AuthenticationException exception)
            {
                Debug.LogWarning("[CloudBootstrap] Sign-in failed (code " + exception.ErrorCode + "): " + exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("[CloudBootstrap] Unity Services unavailable, continuing offline (code " + exception.ErrorCode + "): " + exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            SetState(CloudState.Offline);
            return false;
        }

        private static bool authEventsSubscribed;

        private static void SubscribeAuthEvents()
        {
            if (authEventsSubscribed) return;
            authEventsSubscribed = true;
            AuthenticationService.Instance.Expired += () => Debug.Log("[CloudBootstrap] Access token expired; SDK is refreshing.");
            AuthenticationService.Instance.SignInFailed += exception =>
            {
                Debug.LogWarning("[CloudBootstrap] Token refresh/sign-in failed: " + exception.Message);
                SetState(CloudState.Offline);
            };
            AuthenticationService.Instance.SignedOut += () => SetState(CloudState.Offline);
            // Switching to an existing Apple account signs out and back in; return to Online when that completes.
            AuthenticationService.Instance.SignedIn += () => { if (State != CloudState.Initializing) SetState(CloudState.Online); };
        }

        private static void SetState(CloudState state)
        {
            if (State == state) return;
            State = state;
            UpdateCrashReportMetadata();
            try { StateChanged?.Invoke(state); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>Account type sent with analytics and crash reports: gamecenter, apple, anonymous or signed_out.</summary>
        public static string AccountKind => AccountService.Kind;

        /// <summary>
        /// Tags Cloud Diagnostics crash/exception reports so they can be matched to the same player in Analytics.
        /// </summary>
        private static void UpdateCrashReportMetadata()
        {
            try
            {
                UnityEngine.CrashReportHandler.CrashReportHandler.SetUserMetadata("environment", EnvironmentName);
                UnityEngine.CrashReportHandler.CrashReportHandler.SetUserMetadata("player_id", PlayerId ?? "");
                UnityEngine.CrashReportHandler.CrashReportHandler.SetUserMetadata("account_kind", AccountKind);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CloudBootstrap] Could not set crash report metadata: " + exception.Message);
            }
        }
    }
}
