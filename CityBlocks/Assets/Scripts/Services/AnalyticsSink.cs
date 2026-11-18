using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Sends gameplay events to Unity Analytics (UGS). Events recorded before services are ready are buffered
    /// and flushed once CloudBootstrap finishes its first sign-in attempt (so events carry the PlayerId).
    /// Events recorded while offline are cached by the Analytics SDK and uploaded later.
    /// Unregistered event names/parameters are rejected by UGS: register them in Dashboard ▸ Analytics ▸ Event Manager.
    /// </summary>
    public static class AnalyticsSink
    {
        private const int MaxBufferedEvents = 200;
        private const string SessionNumberKey = "cityblocks_analytics_session_number";

        private static readonly Queue<CustomEvent> Pending = new Queue<CustomEvent>();
        private static bool collectionStarted;
        private static int sessionNumber;

        // Session IDs: UGS stamps its standard `sessionID` on every event, so no custom session_id parameter is sent.

        /// <summary>Launch counter persisted in PlayerPrefs. Step 6 (SessionTracker) replaces this with resume-aware sessions.</summary>
        public static int SessionNumber
        {
            get
            {
                if (sessionNumber == 0)
                {
                    sessionNumber = PlayerPrefs.GetInt(SessionNumberKey, 0) + 1;
                    PlayerPrefs.SetInt(SessionNumberKey, sessionNumber);
                    PlayerPrefs.Save();
                }
                return sessionNumber;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pending.Clear();
            collectionStarted = false;
            sessionNumber = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookCloudBootstrap()
        {
            CloudBootstrap.StateChanged += state => { if (state == CloudState.Online) TryStartCollection(); };
            CloudBootstrap.Ready.ContinueWith(_ => TryStartCollection(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static void Record(string eventName, IDictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            CustomEvent customEvent = new CustomEvent(eventName);
            if (parameters != null)
            {
                foreach (KeyValuePair<string, object> parameter in parameters)
                    AddParameter(customEvent, parameter.Key, parameter.Value);
            }

            if (!collectionStarted) TryStartCollection();
            if (collectionStarted)
            {
                Send(customEvent);
                return;
            }

            if (Pending.Count >= MaxBufferedEvents) Pending.Dequeue();
            Pending.Enqueue(customEvent);
        }

        private static void TryStartCollection()
        {
            if (collectionStarted) return;
            if (UnityServices.State != ServicesInitializationState.Initialized || !CloudBootstrap.Ready.IsCompleted) return;
            try
            {
                // Consent: collection starts for everyone for now; the in-app opt-out toggle arrives in Step 46.
                AnalyticsService.Instance.StartDataCollection();
                collectionStarted = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AnalyticsSink] Could not start Unity Analytics data collection: " + exception.Message);
                return;
            }

            while (Pending.Count > 0) Send(Pending.Dequeue());
        }

        private static void Send(CustomEvent customEvent)
        {
            try { AnalyticsService.Instance.RecordEvent(customEvent); }
            catch (Exception exception) { Debug.LogWarning("[AnalyticsSink] RecordEvent failed: " + exception.Message); }
        }

        private static void AddParameter(CustomEvent customEvent, string key, object value)
        {
            if (string.IsNullOrEmpty(key) || value == null) return;
            switch (value)
            {
                case string _:
                case int _:
                case long _:
                case float _:
                case double _:
                case bool _:
                    customEvent.Add(key, value);
                    break;
                case short shortValue: customEvent.Add(key, (int)shortValue); break;
                case byte byteValue: customEvent.Add(key, (int)byteValue); break;
                case Enum enumValue: customEvent.Add(key, enumValue.ToString()); break;
                default: customEvent.Add(key, value.ToString()); break;
            }
        }
    }
}
