using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class FullPlayerTestResetTests
    {
        [MenuItem("Tools/City Blocks/Tests/Full Player Test Reset Checks")]
        private static void Run()
        {
            string reserved = EditorTools.FullPlayerTestReset.PrepareResetJournal("original-welcome");
            var journal = JObject.Parse(reserved);
            Check((string)journal["WelcomeGrantedAt"] == "original-welcome", "Welcome marker should survive reservation.");
            Check((string)journal["Receipts"][0]["Status"] == "pending", "Reset must block new cloud operations.");
            string retried = EditorTools.FullPlayerTestReset.PrepareResetJournal(reserved);
            Check(((JArray)JObject.Parse(retried)["Receipts"]).Count == 1, "Reset reservation must be retryable without duplicates.");
            bool refused = false;
            try { EditorTools.FullPlayerTestReset.PrepareResetJournal("{\"DailyClaims\":[{\"Status\":\"pending\"}]}"); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "Unresolved grants must be refused.");
            refused = false;
            try { EditorTools.FullPlayerTestReset.ResetAsync("player", "", "", null).GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "Missing credentials must be refused.");
            Check(EditorTools.FullPlayerTestReset.AuthKey("project", "session_token") == "project.development.unity.services.authentication.session_token", "Authentication key must be scoped to development.");
            SessionState.SetBool("FullPlayerTestResetTests.Passed", true);
            Debug.Log("[FullPlayerTestResetTests] 6 safety checks passed. No real local or cloud data changed.");
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
