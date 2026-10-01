using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;

namespace Gameplay
{
    public class FirebaseBootstrap : MonoBehaviour
    {
        private static readonly Queue<KeyValuePair<string, Firebase.Analytics.Parameter[]>> PendingEvents =
            new Queue<KeyValuePair<string, Firebase.Analytics.Parameter[]>>();
        private static bool ready;

        public static void LogEvent(string name, Firebase.Analytics.Parameter[] parameters)
        {
            if (!ready) { PendingEvents.Enqueue(new KeyValuePair<string, Firebase.Analytics.Parameter[]>(name, parameters)); return; }
            Firebase.Analytics.FirebaseAnalytics.LogEvent(name, parameters);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            GameObject bootstrap = new GameObject(nameof(FirebaseBootstrap));
            DontDestroyOnLoad(bootstrap);
            bootstrap.AddComponent<FirebaseBootstrap>();
        }

        private void Awake()
        {
            if (FindObjectsByType<FirebaseBootstrap>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                if (task.Result != Firebase.DependencyStatus.Available)
                {
                    Debug.LogError("Firebase dependencies are unavailable: " + task.Result);
                    return;
                }
                _ = Firebase.FirebaseApp.DefaultInstance;
                Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                ready = true;
                while (PendingEvents.Count > 0)
                {
                    KeyValuePair<string, Parameter[]> queued = PendingEvents.Dequeue();
                    Firebase.Analytics.FirebaseAnalytics.LogEvent(queued.Key, queued.Value);
                }
            });
        }
    }
}
