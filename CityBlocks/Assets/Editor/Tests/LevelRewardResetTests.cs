using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class LevelRewardResetTests
    {
        [MenuItem("Tools/City Blocks/Tests/Level Reward Reset Checks")]
        private static void Run()
        {
            const string original = "{\"WelcomeGrantedAt\":\"original\",\"Receipts\":[],\"LevelClaims\":[{\"LevelNumber\":10,\"Status\":\"applied\",\"Grants\":{\"COINS\":20}},{\"LevelNumber\":11,\"Status\":\"applied\"}],\"FutureField\":{\"keep\":true}}";
            string updated = EditorTools.LevelRewardResetAdmin.ResetClaimJson(original, 10, out bool changed);
            JObject journal = JObject.Parse(updated);
            Check(changed && ((JArray)journal["LevelClaims"]).Count == 1 && (int)journal["LevelClaims"][0]["LevelNumber"] == 11, "Only level 10 should be removed.");
            Check((string)journal["WelcomeGrantedAt"] == "original" && (bool)journal["FutureField"]["keep"], "Other journal fields must survive.");
            EditorTools.LevelRewardResetAdmin.ResetClaimJson(updated, 10, out bool changedAgain);
            Check(!changedAgain, "Repeated reset must be a no-op.");
            ExpectRefusal(() => EditorTools.LevelRewardResetAdmin.ResetClaimJson(original.Replace("\"Status\":\"applied\"", "\"Status\":\"pending\""), 10, out _));
            ExpectRefusal(() => EditorTools.LevelRewardResetAdmin.ResetClaimJson(original.Replace("\"Receipts\":[]", "\"Receipts\":[{\"Status\":\"pending\"}]"), 10, out _));
            ExpectRefusal(() => EditorTools.LevelRewardResetAdmin.ResetAsync("player", 10, "", "").GetAwaiter().GetResult());
            ExpectRefusal(() => EditorTools.LevelRewardResetAdmin.ResetClaimJson(original.Replace("\"Receipts\":[]", "\"Receipts\":[],\"DailyClaims\":[{\"Status\":\"pending\"}]"), 10, out _));
            SessionState.SetBool("LevelRewardResetTests.Passed", true);
            Debug.Log("[LevelRewardResetTests] 7 checks passed. No cloud or local player data was changed.");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
        private static void ExpectRefusal(Action action)
        {
            bool refused = false;
            try { action(); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "Unsafe reset was not refused.");
        }
    }
}
