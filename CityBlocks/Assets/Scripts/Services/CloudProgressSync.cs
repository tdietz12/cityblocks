using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CloudServices
{
    /// <summary>Local files are the durable offline queue. Network traffic occurs only for pending work or lifecycle checks.</summary>
    public sealed class CloudProgressSync : MonoBehaviour
    {
        private const float DebounceSeconds = 2f;
        private const float RetrySeconds = 30f;
        private const string OwnerKeyPrefix = "cityblocks_progress_owner_";
        private const string JournalKeyPrefix = "cityblocks_progress_journal_";
        private static CloudProgressSync instance;
        private Data_Persistence.FileHandler files;
        private CloudProgressData snapshot;
        private ProgressOwnerJournal journal;
        private long generation;
        private string owner;
        private bool dirty;
        private bool downloadNeeded = true;
        private bool sending;
        private bool applying;
        private bool stopping;
        private float dueAt;

        public static string LastSyncUtc { get; private set; }
        public static bool HasPendingChanges => instance != null && instance.dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; LastSyncUtc = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("CloudProgressSync");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CloudProgressSync>();
        }

        private void Awake()
        {
            files = new Data_Persistence.FileHandler(Application.persistentDataPath, "SaveGameData", "ProgressionData");
            snapshot = CaptureLocal();
            try
            {
                journal = ProgressOwnerJournal.Restore(
                    PlayerPrefs.GetString(JournalKeyPrefix + CloudBootstrap.EnvironmentName, ""),
                    PlayerPrefs.GetString(OwnerKeyPrefix + CloudBootstrap.EnvironmentName, ""), snapshot);
                owner = journal.activeOwner;
                dirty = true; // Local files may contain offline progress newer than the last checkpoint.
                PersistJournal();
            }
            catch (Exception exception)
            {
                stopping = true; // Fail closed rather than overwrite an unreadable owner checkpoint.
                Debug.LogWarning("[CloudProgress] " + exception.Message);
            }
        }

        private void OnEnable()
        {
            Gameplay.LevelProgressStore.Saved += OnLocalSaved;
            Data_Persistence.FileHandler.ProgressionSaved += OnLocalSaved;
            ConnectivityService.OnlineChanged += OnOnlineChanged;
            AccountService.SessionChanged += OnSessionChanged;
        }
        private void OnDisable()
        {
            stopping = true;
            generation++;
            Gameplay.LevelProgressStore.Saved -= OnLocalSaved;
            Data_Persistence.FileHandler.ProgressionSaved -= OnLocalSaved;
            ConnectivityService.OnlineChanged -= OnOnlineChanged;
            AccountService.SessionChanged -= OnSessionChanged;
        }

        private CloudProgressData CaptureLocal() => ProgressMerge.Capture(
            Gameplay.LevelProgressStore.Load(), files.LoadProgressionData());

        private void PersistJournal()
        {
            if (journal == null) return;
            PlayerPrefs.SetString(JournalKeyPrefix + CloudBootstrap.EnvironmentName, JsonUtility.ToJson(journal));
            PlayerPrefs.SetString(OwnerKeyPrefix + CloudBootstrap.EnvironmentName, journal.activeOwner ?? "");
            PlayerPrefs.Save();
        }

        private void CheckpointOwner()
        {
            if (journal == null || string.IsNullOrEmpty(owner)) return;
            journal.Record(owner, CaptureLocal(), dirty || downloadNeeded);
            PersistJournal();
        }

        private void OnSessionChanged(string playerId)
        {
            if (stopping || journal == null) return;
            generation++;
            CheckpointOwner(); // Still attributed to the old owner even after SDK sign-out.
            if (!string.IsNullOrEmpty(playerId))
            {
                journal.Bind(playerId, CaptureLocal());
                owner = playerId;
                PersistJournal();
                dirty = true;
            }
            // Sign-out/failure does not adopt a target. Any successful fallback sign-in binds on its own event.
            snapshot = CaptureLocal();
            downloadNeeded = true;
            LastSyncUtc = null;
            dueAt = 0f;
        }

        private void OnLocalSaved()
        {
            if (applying || stopping) return;
            CloudProgressData latest = CaptureLocal();
            if (JsonUtility.ToJson(latest) == JsonUtility.ToJson(snapshot)) return;
            snapshot = latest;
            dirty = true;
            CheckpointOwner();
            dueAt = Time.realtimeSinceStartup + DebounceSeconds;
        }
        private void OnOnlineChanged(bool online) { if (online) dueAt = 0f; }

        private void Update()
        {
            if (!stopping && CloudBootstrap.IsSignedIn) AccountService.ObserveAuthentication();
            if (!stopping && CloudBootstrap.IsSignedIn && owner != CloudBootstrap.PlayerId)
                OnSessionChanged(CloudBootstrap.PlayerId);
            if (stopping || sending || (!dirty && !downloadNeeded) ||
                Time.realtimeSinceStartup < dueAt || !CloudBootstrap.IsSignedIn || !ConnectivityService.IsOnline) return;
            _ = FlushInternalAsync();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // Explicitly save before the flush: Unity does not define callback ordering between components.
                if (Gameplay.LevelFlow.ActiveSession != null) Gameplay.LevelFlow.ActiveSession.SaveRun();
                else if (Data_Persistence.DataPersistenceController.instance != null)
                    Data_Persistence.DataPersistenceController.instance.SaveGame();
                dueAt = 0f;
                _ = FlushInternalAsync();
            }
            else
            {
                downloadNeeded = true;
                dueAt = 0f;
            }
        }

        public static Task FlushAsync() => instance == null ? Task.CompletedTask : instance.FlushInternalAsync();

        private bool IsCurrentRequest(string requestOwner, long requestGeneration, long sessionRevision, string signedInPlayer)
        {
            return this != null && !stopping && generation == requestGeneration && owner == requestOwner &&
                !string.IsNullOrEmpty(signedInPlayer) && signedInPlayer == requestOwner &&
                AccountService.SessionRevision == sessionRevision;
        }

        private async Task FlushInternalAsync()
        {
            if (sending || stopping || (!dirty && !downloadNeeded)) return;
            if (CloudBootstrap.IsSignedIn && owner != CloudBootstrap.PlayerId)
                OnSessionChanged(CloudBootstrap.PlayerId);
            string requestOwner = owner;
            long requestGeneration = generation;
            long sessionRevision = AccountService.SessionRevision;
            Func<bool> isCurrent = () => IsCurrentRequest(requestOwner, requestGeneration, sessionRevision, CloudBootstrap.PlayerId);
            sending = true;
            bool success = false;
            try
            {
                if (!await ConnectivityService.EnsureOnlineAsync("progress_sync") || !isCurrent()) return;
                if (string.IsNullOrEmpty(requestOwner)) return;
                CloudProgressData sent = ProgressMerge.Merge(CaptureLocal(), journal.Get(requestOwner)?.progress);
                journal.Record(requestOwner, sent, true);
                PersistJournal(); // Retain uncertain/in-flight work across termination or switching accounts.
                CloudProgressData remote = await CloudProgressService.SynchronizeAsync(requestOwner, sent);
                if (!isCurrent()) return;
                CloudProgressData latest = CaptureLocal();
                CloudProgressData merged = ProgressMerge.Merge(latest, remote);
                applying = true;
                try
                {
                    if (JsonUtility.ToJson(merged) != JsonUtility.ToJson(latest))
                    {
                        var levels = Gameplay.LevelProgressStore.Load();
                        ApplyLevels(levels, merged);
                        // Keep the live session's held object current, without touching its run/board.
                        if (Gameplay.LevelFlow.ActiveSession != null)
                            ApplyLevels(Gameplay.LevelFlow.ActiveSession.Progress, merged);
                        Gameplay.LevelProgressStore.Save(levels);
                        if (!files.MergeCloudRecords(merged.endlessRecordScore, merged.endlessRecordLevel)) return;
                        if (Data_Persistence.DataPersistenceController.instance != null)
                            Data_Persistence.DataPersistenceController.instance.MergeCloudRecords(
                                merged.endlessRecordScore, merged.endlessRecordLevel);
                    }
                }
                finally { applying = false; }
                snapshot = CaptureLocal();
                // Gameplay may have progressed while the request was in flight; upload that later.
                dirty = JsonUtility.ToJson(snapshot) != JsonUtility.ToJson(remote);
                downloadNeeded = false;
                journal.Record(requestOwner, snapshot, dirty);
                PersistJournal();
                LastSyncUtc = DateTime.UtcNow.ToString("o");
                success = true;
                Debug.Log("[CloudProgress] Synchronized progress for " + requestOwner + ". Pending local changes=" + dirty);
            }
            catch (OperationCanceledException) { /* Keep all local files for the correct owner. */ }
            catch (Exception exception)
            {
                Debug.LogWarning("[CloudProgress] Local progress retained for retry: " + exception.Message);
            }
            finally
            {
                sending = false;
                if (this != null && !stopping)
                    dueAt = generation != requestGeneration || AccountService.SessionRevision != sessionRevision ? 0f :
                        Time.realtimeSinceStartup + (success ? DebounceSeconds : RetrySeconds);
            }
        }

        private static void ApplyLevels(Gameplay.LevelProgressData levels, CloudProgressData cloud)
        {
            if (levels == null) return;
            var merged = ProgressMerge.Merge(ProgressMerge.Capture(levels, null), cloud);
            levels.highestUnlockedLevel = merged.highestUnlockedLevel;
            levels.completedLevels = merged.completedLevels;
            levels.levelTutorialComplete = merged.levelTutorialComplete;
        }
    }
}
