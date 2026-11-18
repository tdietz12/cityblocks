using System;
using System.Globalization;
using Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudServices
{
    /// <summary>
    /// Tracks play sessions for retention analytics.
    /// A session starts on every cold start and when the app returns after more than <see cref="ResumeTimeoutSeconds"/>
    /// in the background. session_end fires each time the app goes to the background (iOS may kill a backgrounded app,
    /// so this is the last reliable moment); duration_s is cumulative foreground time, so a session that is backgrounded
    /// and resumed within the timeout sends session_end again with a larger duration — use the max per session.
    /// UGS's standard sessionID still groups events; session_number is the player-facing session counter.
    /// </summary>
    public sealed class SessionTracker : MonoBehaviour
    {
        public const float ResumeTimeoutSeconds = 300f;

        // Keeps the counter key from Step 4 so existing session counts continue.
        private const string SessionNumberKey = "cityblocks_analytics_session_number";
        private const string InstallTicksKey = "cityblocks_install_utc_ticks";
        private const string LastActivityTicksKey = "cityblocks_last_activity_utc_ticks";

        /// <summary>Clock used for all session math; replaceable in tests.</summary>
        internal static Func<DateTime> UtcNow = () => DateTime.UtcNow;

        private static SessionTracker instance;
        private static int sessionNumber;

        public static int SessionNumber
        {
            get
            {
                if (sessionNumber == 0) sessionNumber = Mathf.Max(1, PlayerPrefs.GetInt(SessionNumberKey, 0));
                return sessionNumber;
            }
        }

        /// <summary>True during the player's first ever session.</summary>
        public static bool IsFirstSession => SessionNumber == 1;

        private DateTime foregroundSinceUtc;
        private double foregroundSeconds;
        private DateTime? backgroundedAtUtc;
        private int levelsPlayed;
        private bool sessionActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            sessionNumber = 0;
            UtcNow = () => DateTime.UtcNow;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            GameObject tracker = new GameObject(nameof(SessionTracker));
            DontDestroyOnLoad(tracker);
            instance = tracker.AddComponent<SessionTracker>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            LevelAnalytics.EventRecorded += CountLevels;
        }

        private void Start() { StartSession(); }

        private void OnDestroy()
        {
            if (instance == this) LevelAnalytics.EventRecorded -= CountLevels;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) HandleBackground();
            else HandleForeground();
        }

        private void OnApplicationQuit() { HandleBackground(); }

        internal void HandleBackground()
        {
            if (!sessionActive || backgroundedAtUtc.HasValue) return;
            DateTime now = UtcNow();
            foregroundSeconds += Math.Max(0d, (now - foregroundSinceUtc).TotalSeconds);
            backgroundedAtUtc = now;
            SaveTicks(LastActivityTicksKey, now);
            LevelAnalytics.SessionEnded(Mathf.RoundToInt((float)foregroundSeconds), levelsPlayed, CurrentScreen());
        }

        internal void HandleForeground()
        {
            if (!backgroundedAtUtc.HasValue) return;
            DateTime now = UtcNow();
            double awaySeconds = (now - backgroundedAtUtc.Value).TotalSeconds;
            backgroundedAtUtc = null;
            if (awaySeconds > ResumeTimeoutSeconds) StartSession();
            else foregroundSinceUtc = now;
        }

        private void StartSession()
        {
            DateTime now = UtcNow();
            DateTime? install = LoadTicks(InstallTicksKey);
            if (!install.HasValue)
            {
                install = now;
                SaveTicks(InstallTicksKey, now);
            }
            DateTime? lastActivity = LoadTicks(LastActivityTicksKey);

            sessionNumber = PlayerPrefs.GetInt(SessionNumberKey, 0) + 1;
            PlayerPrefs.SetInt(SessionNumberKey, sessionNumber);
            SaveTicks(LastActivityTicksKey, now);
            PlayerPrefs.Save();

            foregroundSinceUtc = now;
            foregroundSeconds = 0d;
            backgroundedAtUtc = null;
            levelsPlayed = 0;
            sessionActive = true;

            int daysSinceInstall = Mathf.Max(0, (int)Math.Floor((now - install.Value).TotalDays));
            int daysSinceLastSession = lastActivity.HasValue ? Mathf.Max(0, (int)Math.Floor((now - lastActivity.Value).TotalDays)) : -1;
            LevelAnalytics.SessionStarted(sessionNumber == 1, daysSinceInstall, daysSinceLastSession);
        }

        private void CountLevels(string eventName, System.Collections.Generic.IDictionary<string, object> parameters)
        {
            if (eventName == "level_start" || eventName == "endless_level_start") levelsPlayed++;
        }

        private static string CurrentScreen()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return "menu";
            return LevelFlow.ActiveSession != null ? "level" : "endless";
        }

        private static DateTime? LoadTicks(string key)
        {
            string raw = PlayerPrefs.GetString(key, "");
            long ticks;
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return null;
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static void SaveTicks(string key, DateTime value)
        {
            PlayerPrefs.SetString(key, value.Ticks.ToString(CultureInfo.InvariantCulture));
        }
    }
}
