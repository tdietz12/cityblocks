using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CloudServices;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class ProgressAccountTests
    {
        [MenuItem("Tools/City Blocks/Tests/Progress Account Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("ProgressAccountTests.Passed", false);
            int count = 0;
            Action<bool, string> check = (value, message) =>
            {
                if (!value) throw new Exception(message);
                count++;
            };
            var legacy = new CloudProgressData
            {
                highestUnlockedLevel = 20, completedLevels = new List<int> { 1, 7 },
                endlessRecordScore = 900, endlessRecordLevel = 4, levelTutorialComplete = true
            };
            string before = JsonUtility.ToJson(legacy);
            var first = ProgressOwnerJournal.Restore("", "", legacy);
            check(first.owners.Count == 0, "Unsigned legacy progress was prematurely attributed.");
            var firstCandidate = first.Bind("A", legacy);
            check(JsonUtility.ToJson(firstCandidate) == before && first.Get("A").pending,
                "First sign-in did not retain local level 20 / records / tutorial.");
            var journal = ProgressOwnerJournal.Restore("", "A", legacy);
            check(journal.activeOwner == "A" && journal.Get("A").pending &&
                JsonUtility.ToJson(journal.Get("A").progress) == before, "Step 27 owner migration lost pending progress.");
            var target = new CloudProgressData
            {
                highestUnlockedLevel = 10, completedLevels = new List<int> { 2 },
                endlessRecordScore = 100, endlessRecordLevel = 12
            };
            journal.Record("B", target, false);
            string oldPending = JsonUtility.ToJson(journal.Get("A"));
            var candidate = journal.Bind("B", legacy);
            check(JsonUtility.ToJson(journal.Get("A")) == oldPending,
                "Switch relabeled or cleared old-owner pending work.");
            check(candidate.highestUnlockedLevel == 20 && candidate.endlessRecordScore == 900 &&
                candidate.endlessRecordLevel == 12 && string.Join(",", candidate.completedLevels) == "1,2,7" &&
                candidate.levelTutorialComplete, "Existing target merge lost a permanent progress field.");
            candidate.completedLevels.Add(99);
            check(!journal.Get("B").progress.completedLevels.Contains(99), "Candidate aliases retained owner checkpoint.");
            journal.Record("B", journal.Get("B").progress, false);
            check(journal.Get("A").pending && !journal.Get("B").pending,
                "Confirming B cleared A's pending checkpoint.");
            var relaunched = ProgressOwnerJournal.Restore(JsonUtility.ToJson(journal), "ignored", legacy);
            check(relaunched.activeOwner == "B" && relaunched.Get("A").pending && !relaunched.Get("B").pending,
                "Relaunch lost owner-specific pending state.");
            var returned = relaunched.Bind("A", target);
            check(returned.highestUnlockedLevel == 20 && returned.endlessRecordLevel == 12 &&
                relaunched.Get("B") != null, "Return to A lost archived/offline progress.");
            check(JsonUtility.ToJson(legacy) == before, "Migration mutated input local progress.");
            bool invalid = false;
            try { ProgressOwnerJournal.Restore("{\"schemaVersion\":99}", "A", legacy); }
            catch (InvalidOperationException) { invalid = true; }
            check(invalid, "Unknown owner journal version did not fail closed.");
            string payload = JsonUtility.ToJson(relaunched);
            check(!payload.Contains("Currency") && !payload.Contains("powerUps") && !payload.Contains("Purchase") &&
                !payload.Contains("activeRun") && !payload.Contains("receipt"), "Owner journal contains economic/run data.");

            string directory = Path.Combine(Path.GetTempPath(), "CityBlocksAccounts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string[] keys = { "cityblocks_progress_owner_" + CloudBootstrap.EnvironmentName,
                "cityblocks_progress_journal_" + CloudBootstrap.EnvironmentName };
            var existed = new bool[keys.Length];
            var values = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) { existed[i] = PlayerPrefs.HasKey(keys[i]); values[i] = PlayerPrefs.GetString(keys[i], ""); }
            var type = typeof(CloudProgressSync);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var publish = typeof(AccountService).GetMethod("PublishSession", BindingFlags.Static | BindingFlags.NonPublic);
            var revision = typeof(AccountService).GetField("<SessionRevision>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            long originalRevision = AccountService.SessionRevision;
            GameObject go = null;
            int sessionEvents = 0;
            int kindEvents = 0;
            Action<string> sessionListener = id => sessionEvents++;
            Action<string> kindListener = kind => kindEvents++;
            try
            {
                var files = new Data_Persistence.FileHandler(directory, "run", "records");
                files.Save(new Data_Persistence.GameSaveData { score = 42 },
                    new Data_Persistence.ProgressionData { recordScore = 777, recordLevel = 8, softCurrency = 999 });
                string runBefore = File.ReadAllText(Path.Combine(directory, "run"));
                go = new GameObject("ProgressAccountChecks");
                go.SetActive(false);
                var runner = go.AddComponent<CloudProgressSync>();
                type.GetField("files", flags).SetValue(runner, files);
                type.GetField("journal", flags).SetValue(runner, new ProgressOwnerJournal());
                var onSession = type.GetMethod("OnSessionChanged", flags);
                var current = type.GetMethod("IsCurrentRequest", flags);
                Action<string> bind = id => onSession.Invoke(runner, new object[] { id });
                AccountService.SessionChanged += sessionListener;
                AccountService.KindChanged += kindListener;
                type.GetMethod("OnEnable", flags).Invoke(runner, null);
                publish.Invoke(null, new object[] { "A" });
                long aGeneration = (long)type.GetField("generation", flags).GetValue(runner);
                long aRevision = AccountService.SessionRevision;
                Func<bool> isOriginalA = () => (bool)current.Invoke(runner, new object[] { "A", aGeneration, aRevision, "A" });
                check(isOriginalA(), "Current authenticated A request was rejected.");
                var aJournal = (ProgressOwnerJournal)type.GetField("journal", flags).GetValue(runner);
                check(aJournal.activeOwner == "A" && aJournal.Get("A").progress.endlessRecordScore == 777,
                    "Session event did not bind permanent device progress.");

                publish.Invoke(null, new object[] { null });
                check(!isOriginalA() && aJournal.activeOwner == "A", "Sign-out adopted a target or kept a stale request valid.");
                string afterFailure = JsonUtility.ToJson(aJournal);
                // A failed/cancelled provider sign-in emits no SignedIn event. Nothing may bind a target.
                check(aJournal.Get("B") == null && JsonUtility.ToJson(aJournal) == afterFailure,
                    "Failed switch manufactured target ownership.");
                publish.Invoke(null, new object[] { "B" });
                check(aJournal.activeOwner == "B" && aJournal.Get("A").pending && aJournal.Get("B").pending,
                    "Successful switch did not keep separate pending owners.");
                publish.Invoke(null, new object[] { null });
                publish.Invoke(null, new object[] { "A" });
                check(!isOriginalA(), "Switch-away-and-back accepted the first A response.");
                check(sessionEvents == 5 && kindEvents == 0, "Same-kind switches depend on KindChanged.");
                long currentGeneration = (long)type.GetField("generation", flags).GetValue(runner);
                long currentRevision = AccountService.SessionRevision;
                check((bool)current.Invoke(runner, new object[] { "A", currentGeneration, currentRevision, "A" }),
                    "New A request remained blocked after returning.");
                // Linking an identity without sign-out/sign-in leaves the session generation valid.
                check((long)type.GetField("generation", flags).GetValue(runner) == currentGeneration &&
                    AccountService.SessionRevision == currentRevision, "Unchanged-PlayerId link changed session identity.");
                check(!(bool)current.Invoke(runner, new object[] { "A", currentGeneration, currentRevision, null }),
                    "Signed-out response was applicable.");
                check(File.ReadAllText(Path.Combine(directory, "run")) == runBefore &&
                    files.LoadProgressionData().softCurrency == 999, "Account boundary modified board or currency data.");
                var disk = ProgressOwnerJournal.Restore(PlayerPrefs.GetString(keys[1]), "", legacy);
                check(disk.activeOwner == "A" && disk.Get("B").pending && disk.Get("A").pending,
                    "Runtime switch checkpoint did not survive a journal reload.");

                // The real transport core must reject A -> B -> A while a read is outstanding.
                var core = typeof(CloudProgressService).GetMethod("SynchronizeCoreAsync", BindingFlags.Static | BindingFlags.NonPublic);
                var read = new TaskCompletionSource<(CloudProgressData, string)>();
                long transportRevision = AccountService.SessionRevision;
                int writes = 0;
                var task = (Task<CloudProgressData>)core.Invoke(null, new object[]
                {
                    legacy, new Func<Task<(CloudProgressData, string)>>(() => read.Task),
                    new Func<CloudProgressData, string, Task<bool>>((data, writeLock) => { writes++; return Task.FromResult(true); }),
                    new Func<bool>(() => AccountService.SessionRevision == transportRevision)
                });
                check(!task.IsCompleted, "Deferred read test did not reach its await boundary.");
                publish.Invoke(null, new object[] { null });
                publish.Invoke(null, new object[] { "B" });
                publish.Invoke(null, new object[] { null });
                publish.Invoke(null, new object[] { "A" });
                read.SetResult((target, "lock"));
                bool canceled = false;
                try { task.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { canceled = true; }
                check(canceled && writes == 0, "Stale deferred A read wrote after switch-away-and-back.");
                SessionState.SetBool("ProgressAccountTests.Passed", true);
                Debug.Log("[ProgressAccountTests] " + count + " checks passed. Fake auth events/transport; temporary records; original progress prefs restored. No real accounts, cloud, wallet, reward queue or campaign saves mutated.");
            }
            finally
            {
                if (go != null)
                {
                    type.GetMethod("OnDisable", flags).Invoke(go.GetComponent<CloudProgressSync>(), null);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                AccountService.SessionChanged -= sessionListener;
                AccountService.KindChanged -= kindListener;
                revision.SetValue(null, originalRevision);
                for (int i = 0; i < keys.Length; i++)
                    if (existed[i]) PlayerPrefs.SetString(keys[i], values[i]); else PlayerPrefs.DeleteKey(keys[i]);
                PlayerPrefs.Save();
                Directory.Delete(directory, true);
            }
        }
    }
}
