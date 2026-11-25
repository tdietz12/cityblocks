using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CloudServices;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class ProgressLifecycleTests
    {
        [MenuItem("Tools/City Blocks/Tests/Progress Lifecycle Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool("ProgressLifecycleTests.Passed", false);
            int count = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new Exception(message);
                count++;
            };
            string directory = Path.Combine(Path.GetTempPath(), "CityBlocksProgress-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            GameObject go = null;
            int savedEvents = 0;
            Action onSaved = () => savedEvents++;
            Data_Persistence.FileHandler.ProgressionSaved += onSaved;
            try
            {
                var files = new Data_Persistence.FileHandler(directory, "run", "records");
                files.Save(new Data_Persistence.GameSaveData { score = 123 },
                    new Data_Persistence.ProgressionData
                    { recordScore = 500, recordLevel = 8, xp = 30, softCurrency = 99, hardCurrency = 10 });
                check(savedEvents == 1, "Successful progression save did not notify dirty tracking.");
                string runBefore = File.ReadAllText(Path.Combine(directory, "run"));
                check(files.MergeCloudRecords(1000, 4), "Records-only cloud merge failed.");
                var records = files.LoadProgressionData();
                check(records.recordScore == 1000 && records.recordLevel == 8,
                    "Records merge downgraded an independent record.");
                check(records.xp == 30 && records.softCurrency == 99 && records.hardCurrency == 10,
                    "Cloud merge touched non-record progression fields.");
                check(File.ReadAllText(Path.Combine(directory, "run")) == runBefore,
                    "Cloud records merge altered the active Endless board file.");
                check(savedEvents == 2, "Cloud records save did not notify listeners.");

                var run = new Gameplay.LevelRunData { attemptId = "held-attempt", score = 42 };
                var inventory = new Gameplay.PowerUpInventory { deleteUses = 7 };
                var levels = new Gameplay.LevelProgressData
                {
                    highestUnlockedLevel = 5, completedLevels = new List<int> { 1 },
                    activeRun = run, powerUps = inventory, adsRemoved = true,
                    processedPurchaseIds = new List<string> { "keep-receipt" }
                };
                var apply = typeof(CloudProgressSync).GetMethod("ApplyLevels", BindingFlags.Static | BindingFlags.NonPublic);
                apply.Invoke(null, new object[] { levels, new CloudProgressData
                { highestUnlockedLevel = 12, completedLevels = new List<int> { 2, 3 }, levelTutorialComplete = true } });
                check(levels.highestUnlockedLevel == 12 && levels.completedLevels.Count == 3 && levels.levelTutorialComplete,
                    "Restored permanent level data was not applied.");
                check(ReferenceEquals(levels.activeRun, run) && run.score == 42 && run.attemptId == "held-attempt",
                    "Restoration replaced or modified the live campaign run.");
                check(ReferenceEquals(levels.powerUps, inventory) && inventory.deleteUses == 7 && levels.adsRemoved &&
                    levels.processedPurchaseIds[0] == "keep-receipt", "Restoration altered wallet/receipt data.");

                go = new GameObject("ProgressLifecycleChecks");
                go.SetActive(false);
                var runner = go.AddComponent<CloudProgressSync>();
                var type = typeof(CloudProgressSync);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("files", flags).SetValue(runner, files);
                var capture = type.GetMethod("CaptureLocal", flags);
                type.GetField("snapshot", flags).SetValue(runner, capture.Invoke(runner, null));
                type.GetField("dirty", flags).SetValue(runner, false);
                type.GetMethod("OnEnable", flags).Invoke(runner, null);
                var notify = type.GetMethod("OnLocalSaved", flags);
                notify.Invoke(runner, null);
                check(!(bool)type.GetField("dirty", flags).GetValue(runner),
                    "Unchanged progress/board-only save was marked dirty.");
                files.Save(new Data_Persistence.GameSaveData { score = 999 }, records);
                check(!(bool)type.GetField("dirty", flags).GetValue(runner),
                    "Changing only board state triggered a progress upload.");
                records.recordScore = 1500;
                files.Save(new Data_Persistence.GameSaveData { score = 999 }, records);
                check((bool)type.GetField("dirty", flags).GetValue(runner), "New Endless record was not marked dirty.");
                check((float)type.GetField("dueAt", flags).GetValue(runner) > Time.realtimeSinceStartup,
                    "Progress change was not debounced.");
                type.GetField("downloadNeeded", flags).SetValue(runner, false);
                type.GetMethod("OnApplicationPause", flags).Invoke(runner, new object[] { false });
                check((bool)type.GetField("downloadNeeded", flags).GetValue(runner) &&
                    (float)type.GetField("dueAt", flags).GetValue(runner) == 0f,
                    "Resume did not schedule a fresh download.");
                SessionState.SetBool("ProgressLifecycleTests.Passed", true);
                Debug.Log("[ProgressLifecycleTests] " + count +
                    " checks passed. Temporary files only; no player saves, rewards or cloud requests modified.");
            }
            finally
            {
                Data_Persistence.FileHandler.ProgressionSaved -= onSaved;
                if (go != null)
                {
                    typeof(CloudProgressSync).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(go.GetComponent<CloudProgressSync>(), null);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                Directory.Delete(directory, true);
            }
        }
    }
}
