using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class CloudDailyRewardTests
    {
        [MenuItem("Tools/City Blocks/Tests/Cloud Daily Reward Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Run daily display checks in Edit Mode."); return; }
            var response = JsonConvert.DeserializeObject<CloudServices.DailyRewardResponse>(
                "{\"Status\":\"applied\",\"StreakDay\":2,\"Reward\":{\"Coins\":150,\"DeleteUses\":2,\"LevelUpUses\":3,\"LookAheadUses\":4,\"ExtraTurnsUses\":5},\"Schedule\":[{\"Coins\":100}],\"LegacyStateImported\":true}");
            Check(response.Reward.coins == 150 && response.Reward.lookAheadUses == 4 && response.Reward.extraTurnsUses == 5, "Server reward field mapping failed.");
            var cached = JsonUtility.FromJson<CloudServices.DailyRewardResponse>(JsonUtility.ToJson(response));
            Check(cached.StreakDay == 2 && cached.Reward.coins == 150 && cached.LegacyStateImported, "Daily display cache failed.");
            Check(global::UI.DailyLoginStreakUI.Describe(response.Reward.ToPowerUps(), response.Reward.coins).Contains("EXTRA TURNS"), "Mixed reward summary lost extra turns.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DailyLoginRewards.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            int events = 0;
            Action<string, System.Collections.Generic.IDictionary<string, object>> hook = (name, p) => events++;
            Gameplay.LevelAnalytics.EventRecorded += hook;
            try
            {
                var ui = instance.GetComponent<global::UI.DailyLoginStreakUI>();
                Check(ui.DebugForceOpen(), "Daily preview could not open.");
                var so = new SerializedObject(ui);
                Check(so.FindProperty("rewardSchedule").objectReferenceValue != null, "Editable schedule reference missing.");
                var feedback = so.FindProperty("feedbackLabel").objectReferenceValue as TMPro.TMP_Text;
                Check(feedback != null && feedback.text.Contains("NO REWARD GRANTED"), "Preview must clearly say it does not grant.");
                Check(events == 0, "Editor preview must not send reward analytics.");
                SessionState.SetBool("CloudDailyRewardTests.Passed", true);
                Debug.Log("[CloudDailyRewardTests] 7 checks passed. No cloud requests or reward grants made.");
            }
            finally
            {
                Gameplay.LevelAnalytics.EventRecorded -= hook;
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
