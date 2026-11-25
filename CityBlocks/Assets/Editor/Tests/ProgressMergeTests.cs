using System;
using System.Collections.Generic;
using CloudServices;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class ProgressMergeTests
    {
        [MenuItem("Tools/City Blocks/Tests/Progress Merge Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Run progress merge checks in Edit Mode.");
                return;
            }
            SessionState.SetBool("ProgressMergeTests.Passed", false);
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new Exception(message);
                checks++;
            };
            var empty = ProgressMerge.Merge(null, null);
            check(empty.highestUnlockedLevel == 1 && empty.completedLevels.Count == 0 &&
                empty.endlessRecordScore == 0 && empty.endlessRecordLevel == 0 &&
                !empty.levelTutorialComplete, "Empty progress must have safe defaults.");
            var legacy = JsonUtility.FromJson<CloudProgressData>("{\"highestUnlockedLevel\":12}");
            check(ProgressMerge.Merge(legacy, null).highestUnlockedLevel == 12,
                "Legacy partial snapshot lost unlocked progress.");
            var invalid = new CloudProgressData
            {
                highestUnlockedLevel = -5, completedLevels = null,
                endlessRecordScore = -9, endlessRecordLevel = -2
            };
            var normalized = ProgressMerge.Merge(invalid, null);
            check(normalized.highestUnlockedLevel == 1 && normalized.completedLevels.Count == 0 &&
                normalized.endlessRecordScore == 0 && normalized.endlessRecordLevel == 0,
                "Invalid values/null lists were not normalized.");
            check(invalid.highestUnlockedLevel == -5 && invalid.completedLevels == null,
                "Normalization mutated its input.");

            var local = new CloudProgressData
            {
                highestUnlockedLevel = 20, completedLevels = new List<int> { 7, 1, 7, 0, -3 },
                endlessRecordScore = 900, endlessRecordLevel = 4
            };
            var cloud = new CloudProgressData
            {
                highestUnlockedLevel = 10, completedLevels = new List<int> { 2, 7, 9 },
                endlessRecordScore = 100, endlessRecordLevel = 12, levelTutorialComplete = true
            };
            string localBefore = JsonUtility.ToJson(local);
            string cloudBefore = JsonUtility.ToJson(cloud);
            var merged = ProgressMerge.Merge(local, cloud);
            check(merged.highestUnlockedLevel == 20, "Unlocked progress went backwards.");
            check(string.Join(",", merged.completedLevels) == "1,2,7,9",
                "Completed levels must be a sorted positive union without duplicates.");
            check(merged.endlessRecordScore == 900 && merged.endlessRecordLevel == 12,
                "Endless records must use independent maxima.");
            check(merged.levelTutorialComplete, "Cloud tutorial completion was lost.");
            check(JsonUtility.ToJson(local) == localBefore && JsonUtility.ToJson(cloud) == cloudBefore,
                "Merge mutated its inputs.");
            string mergedJson = JsonUtility.ToJson(merged);
            check(JsonUtility.ToJson(ProgressMerge.Merge(cloud, local)) == mergedJson,
                "Merge must be commutative.");
            check(JsonUtility.ToJson(ProgressMerge.Merge(merged, merged)) == mergedJson,
                "Merge must be idempotent.");
            check(JsonUtility.ToJson(ProgressMerge.Merge(merged, local)) == mergedJson,
                "Repeated stale progress must not downgrade merged progress.");
            var third = new CloudProgressData
            {
                highestUnlockedLevel = 25, completedLevels = new List<int> { 8 },
                endlessRecordScore = 1000, endlessRecordLevel = 3
            };
            check(JsonUtility.ToJson(ProgressMerge.Merge(ProgressMerge.Merge(local, cloud), third)) ==
                JsonUtility.ToJson(ProgressMerge.Merge(local, ProgressMerge.Merge(cloud, third))),
                "Merge must be associative.");
            check(ProgressMerge.Merge(new CloudProgressData { levelTutorialComplete = true }, null)
                .levelTutorialComplete, "Local tutorial completion was lost.");
            merged.completedLevels.Add(100);
            check(!local.completedLevels.Contains(100) && !cloud.completedLevels.Contains(100),
                "Merged list aliases an input list.");

            var levels = new Gameplay.LevelProgressData
            {
                highestUnlockedLevel = 6, completedLevels = new List<int> { 1, 3 },
                levelTutorialComplete = true, adsRemoved = true, hasMadePurchase = true,
                activeRun = new Gameplay.LevelRunData { score = 123 },
                powerUps = new Gameplay.PowerUpInventory { deleteUses = 99 },
                processedPurchaseIds = new List<string> { "private-receipt" }
            };
            var endless = new Data_Persistence.ProgressionData
            {
                recordScore = 1500, recordLevel = 15, xp = 30, softCurrency = 500, hardCurrency = 100
            };
            var captured = ProgressMerge.Capture(levels, endless);
            check(captured.highestUnlockedLevel == 6 && captured.levelTutorialComplete &&
                captured.endlessRecordScore == 1500 && captured.endlessRecordLevel == 15,
                "Capture lost permanent local progress.");
            captured.completedLevels.Add(5);
            check(!levels.completedLevels.Contains(5), "Capture aliases local completed levels.");
            string payload = JsonUtility.ToJson(captured);
            check(!payload.Contains("powerUps") && !payload.Contains("adsRemoved") &&
                !payload.Contains("activeRun") && !payload.Contains("Purchase") &&
                !payload.Contains("Currency") && !payload.Contains("private-receipt") &&
                !payload.Contains("xp"), "Progress payload contains wallet/private/run data.");
            check(typeof(CloudProgressData).GetFields().Length == 5,
                "Progress DTO scope changed; review excluded data before extending it.");
            var roundTrip = JsonUtility.FromJson<CloudProgressData>(payload);
            check(JsonUtility.ToJson(ProgressMerge.Merge(roundTrip, null)) == payload,
                "Progress JSON round-trip failed.");
            check(JsonUtility.ToJson(ProgressMerge.Capture(null, null)) == JsonUtility.ToJson(empty),
                "Empty local capture failed.");
            SessionState.SetBool("ProgressMergeTests.Passed", true);
            Debug.Log("[ProgressMergeTests] " + checks +
                " checks passed. No saves, preferences, cloud requests or rewards modified.");
        }
    }
}
