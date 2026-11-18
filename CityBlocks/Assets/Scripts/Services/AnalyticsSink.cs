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
        private const string AccountKindKey = "account_kind";

        private struct PendingEvent
        {
            public string Name;
            public Dictionary<string, object> Parameters;
        }

        private static readonly Queue<PendingEvent> Pending = new Queue<PendingEvent>();
        private static bool collectionStarted;

        // Session IDs: UGS stamps its standard `sessionID` on every event, so no custom session_id parameter is sent.
        // session_number comes from SessionTracker.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pending.Clear();
            collectionStarted = false;
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
            PendingEvent pendingEvent = new PendingEvent
            {
                Name = eventName,
                Parameters = parameters != null ? new Dictionary<string, object>(parameters) : new Dictionary<string, object>()
            };

            if (!collectionStarted) TryStartCollection();
            if (collectionStarted)
            {
                Send(pendingEvent);
                return;
            }

            if (Pending.Count >= MaxBufferedEvents) Pending.Dequeue();
            Pending.Enqueue(pendingEvent);
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

        private static void Send(PendingEvent pendingEvent)
        {
            // account_kind describes the account the event is uploaded under, so it is resolved now rather than when
            // the event was recorded (events buffered before sign-in would otherwise say "signed_out").
            if (pendingEvent.Parameters.ContainsKey(AccountKindKey))
                pendingEvent.Parameters[AccountKindKey] = CloudBootstrap.AccountKind;

            CustomEvent customEvent = new CustomEvent(pendingEvent.Name);
            foreach (KeyValuePair<string, object> parameter in pendingEvent.Parameters)
                AddParameter(customEvent, parameter.Key, parameter.Value);

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
