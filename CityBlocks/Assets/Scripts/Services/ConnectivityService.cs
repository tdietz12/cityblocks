using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Owns the game's online / offline state (backend option B: no heartbeat).
    /// Online = device reachable + signed in + the last cloud call succeeded. Checked on launch, on resume, when
    /// reachability changes and on request (EnsureOnlineAsync, before store / power-up use); while offline it retries
    /// every 30 s. Optimistic: it only reports offline on evidence (no network, failed sign-in or a failed cloud call),
    /// so a slow first call (Cloud Code cold start) never locks anything. Each successful check refreshes PlayerWallet.
    /// </summary>
    public class ConnectivityService : MonoBehaviour
    {
        // Runtime-created singleton (no scene object), so tuning values are constants.
        private const float OfflineRetrySeconds = 30f;
        // EnsureOnlineAsync trusts a successful check this recent instead of calling the cloud again.
        private const float RecentSuccessSeconds = 60f;

        private static ConnectivityService instance;
        private static bool isOnline = true;
        private static float stateChangedAt;
        private static float lastSuccessAt = -1000f;
        private static Task<bool> checkTask;

        /// <summary>Current state; also the value of the analytics `is_online` common param.</summary>
        public static bool IsOnline => isOnline && Application.internetReachability != NetworkReachability.NotReachable;

        /// <summary>Raised on the main thread when the state flips (true = back online).</summary>
        public static event Action<bool> OnlineChanged;

        private NetworkReachability lastReachability;
        private float nextRetryAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isOnline = true;
            stateChangedAt = 0f;
            lastSuccessAt = -1000f;
            checkTask = null;
            OnlineChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("ConnectivityService");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ConnectivityService>();
        }

        private void Awake()
        {
            lastReachability = Application.internetReachability;
            stateChangedAt = Time.realtimeSinceStartup;
            isOnline = lastReachability != NetworkReachability.NotReachable;
        }

        private void Start() => _ = CheckAsync("launch");

        private void Update()
        {
            NetworkReachability reachability = Application.internetReachability;
            if (reachability != lastReachability)
            {
                lastReachability = reachability;
                _ = CheckAsync("reachability");
                return;
            }
            if (!isOnline && Time.realtimeSinceStartup >= nextRetryAt && (checkTask == null || checkTask.IsCompleted))
                _ = CheckAsync("retry");
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) _ = CheckAsync("resume");
        }

        /// <summary>
        /// Call before anything that needs the cloud (store, power-ups, claims). Returns immediately when a check
        /// succeeded in the last minute; otherwise runs a check. False = offline (the badge is already showing).
        /// </summary>
        public static Task<bool> EnsureOnlineAsync(string reason = "action")
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                SetOnline(false, reason);
                return Task.FromResult(false);
            }
            if (isOnline && Time.realtimeSinceStartup - lastSuccessAt < RecentSuccessSeconds) return Task.FromResult(true);
            return CheckAsync(reason);
        }

        /// <summary>Lets other cloud calls report their outcome so the state follows real traffic.</summary>
        public static void ReportCloudResult(bool success, string reason = "call")
        {
            if (success) lastSuccessAt = Time.realtimeSinceStartup;
            SetOnline(success, reason);
        }

        private static Task<bool> CheckAsync(string reason)
        {
            if (checkTask == null || checkTask.IsCompleted) checkTask = RunCheckAsync(reason);
            return checkTask;
        }

        private static async Task<bool> RunCheckAsync(string reason)
        {
            bool ok = false;
            try
            {
                if (Application.internetReachability != NetworkReachability.NotReachable &&
                    await CloudBootstrap.EnsureSignedInAsync())
                {
                    // Bootstrap doubles as the connectivity probe and refreshes balances.
                    ok = await PlayerWallet.RefreshAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ConnectivityService] Check failed: " + exception.Message);
            }
            if (ok) lastSuccessAt = Time.realtimeSinceStartup;
            SetOnline(ok, reason);
            if (!ok && instance != null) instance.nextRetryAt = Time.realtimeSinceStartup + OfflineRetrySeconds;
            return ok;
        }

        private static void SetOnline(bool online, string reason)
        {
            if (online == isOnline) return;
            float now = Time.realtimeSinceStartup;
            int secondsInPreviousState = Mathf.RoundToInt(now - stateChangedAt);
            isOnline = online;
            stateChangedAt = now;
            if (!online && instance != null) instance.nextRetryAt = now + OfflineRetrySeconds;
            Debug.Log("[ConnectivityService] " + (online ? "Back online" : "Offline") + " (" + reason + ")");
            Gameplay.LevelAnalytics.OfflineModeChanged(online ? "online" : "offline", reason, secondsInPreviousState);
            try { OnlineChanged?.Invoke(online); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
