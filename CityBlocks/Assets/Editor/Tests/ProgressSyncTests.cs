using System;
using System.Reflection;
using System.Threading.Tasks;
using CloudServices;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class ProgressSyncTests
    {
        [MenuItem("Tools/City Blocks/Tests/Progress Sync Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Run progress sync checks in Edit Mode.");
                return;
            }
            SessionState.SetBool("ProgressSyncTests.Passed", false);
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new Exception(message);
                checks++;
            };
            var local = new CloudProgressData { highestUnlockedLevel = 10 };
            int writes = 0;
            var created = Sync(local, () => Task.FromResult<(CloudProgressData, string)>((null, null)),
                (data, writeLock) =>
                {
                    check(writeLock == null && data.highestUnlockedLevel == 10,
                        "Missing progress key must create the local snapshot without a fabricated lock.");
                    writes++;
                    return Task.FromResult(true);
                });
            check(writes == 1 && created.highestUnlockedLevel == 10, "Creation did not return confirmed progress.");

            writes = 0;
            var remote = new CloudProgressData { highestUnlockedLevel = 20, endlessRecordScore = 500 };
            var downloaded = Sync(local, () => Task.FromResult((remote, "lock-1")),
                (data, writeLock) => { writes++; return Task.FromResult(true); });
            check(writes == 0 && downloaded.highestUnlockedLevel == 20 && downloaded.endlessRecordScore == 500,
                "Already-current cloud progress must return without an unnecessary save.");
            check(local.highestUnlockedLevel == 10 && local.endlessRecordScore == 0,
                "Download mutated local progress before caller confirmation.");

            int reads = 0;
            writes = 0;
            var richerLocal = new CloudProgressData { highestUnlockedLevel = 10, endlessRecordScore = 900 };
            var conflictResult = Sync(richerLocal,
                () =>
                {
                    reads++;
                    return Task.FromResult((new CloudProgressData
                    {
                        highestUnlockedLevel = reads == 1 ? 5 : 30,
                        endlessRecordLevel = reads == 1 ? 2 : 12
                    }, "lock-" + reads));
                },
                (data, writeLock) =>
                {
                    writes++;
                    check(writeLock == "lock-" + writes, "Retry reused a stale write lock.");
                    return Task.FromResult(writes > 1);
                });
            check(reads == 2 && writes == 2 && conflictResult.highestUnlockedLevel == 30 &&
                conflictResult.endlessRecordScore == 900 && conflictResult.endlessRecordLevel == 12,
                "Conflict reload/merge lost local or newer cloud progress.");

            reads = 0;
            writes = 0;
            bool exhausted = false;
            try
            {
                Sync(local, () => { reads++; return Task.FromResult((new CloudProgressData(), "lock")); },
                    (data, writeLock) => { writes++; return Task.FromResult(false); });
            }
            catch (InvalidOperationException) { exhausted = true; }
            check(exhausted && reads == 3 && writes == 3, "Conflicts must stop after three save attempts.");

            bool owner = false;
            reads = 0;
            bool canceled = false;
            try
            {
                Sync(local, () => { reads++; return Task.FromResult((remote, "lock")); },
                    (data, writeLock) => Task.FromResult(true), () => owner);
            }
            catch (OperationCanceledException) { canceled = true; }
            check(canceled && reads == 0, "Wrong owner must be refused before reading cloud data.");
            owner = true;
            writes = 0;
            canceled = false;
            try
            {
                Sync(local, () => { owner = false; return Task.FromResult((new CloudProgressData(), "lock")); },
                    (data, writeLock) => { writes++; return Task.FromResult(true); }, () => owner);
            }
            catch (OperationCanceledException) { canceled = true; }
            check(canceled && writes == 0, "Account change during read must prevent a stale write.");
            owner = true;
            canceled = false;
            try
            {
                Sync(local, () => Task.FromResult((new CloudProgressData(), "lock")),
                    (data, writeLock) => { owner = false; return Task.FromResult(true); }, () => owner);
            }
            catch (OperationCanceledException) { canceled = true; }
            check(canceled, "Account change during save must not return data for local application.");

            bool failed = false;
            writes = 0;
            try
            {
                Sync(local, () => Task.FromException<(CloudProgressData, string)>(new Exception("read failed")),
                    (data, writeLock) => { writes++; return Task.FromResult(true); });
            }
            catch (Exception exception) { failed = exception.Message == "read failed"; }
            check(failed && writes == 0, "Read failure must never be treated as a missing key.");
            failed = false;
            writes = 0;
            try
            {
                Sync(local, () => Task.FromResult((new CloudProgressData(), "lock")),
                    (data, writeLock) => { writes++; return Task.FromException<bool>(new Exception("save failed")); });
            }
            catch (Exception exception) { failed = exception.Message == "save failed"; }
            check(failed && writes == 1 && local.highestUnlockedLevel == 10,
                "Save failure must propagate without blind retry or local mutation.");

            var mutable = new CloudProgressData { highestUnlockedLevel = 7 };
            var frozen = Sync(mutable,
                () => { mutable.highestUnlockedLevel = 99; return Task.FromResult((new CloudProgressData(), "lock")); },
                (data, writeLock) => Task.FromResult(true));
            check(frozen.highestUnlockedLevel == 7, "In-flight gameplay mutation changed the upload snapshot.");
            SessionState.SetBool("ProgressSyncTests.Passed", true);
            Debug.Log("[ProgressSyncTests] " + checks +
                " checks passed. Fake transport only; no cloud requests, local saves or rewards modified.");
        }

        private static CloudProgressData Sync(CloudProgressData local,
            Func<Task<(CloudProgressData data, string writeLock)>> read,
            Func<CloudProgressData, string, Task<bool>> save, Func<bool> owner = null)
        {
            var method = typeof(CloudProgressService).GetMethod("SynchronizeCoreAsync",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new Exception("Progress sync test boundary is missing.");
            var task = (Task<CloudProgressData>)method.Invoke(null,
                new object[] { local, read, save, owner ?? (() => true) });
            // Fake operations are already completed; these checks never wait on the network/editor loop.
            return task.GetAwaiter().GetResult();
        }
    }
}
