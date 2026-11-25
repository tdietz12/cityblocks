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
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(global::UI.DailyLoginStreakUI).GetMethod("Populate", flags).Invoke(ui, new object[] { 2, response.Reward.ToPowerUps(), 150 });
                Check(feedback.text.Contains("00:00 UTC"), "Daily popup must explain the UTC boundary.");
                var schedule = new Gameplay.RewardAmounts[7];
                typeof(global::UI.DailyLoginStreakUI).GetField("serverSchedule", flags).SetValue(ui, schedule);
                typeof(global::UI.DailyLoginStreakUI).GetMethod("OnSessionChanged", flags).Invoke(ui, new object[] { null });
                var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
                Check(panel != null && !panel.activeSelf && !ui.IsShowingOrPending, "Previous owner's popup remained open after sign-out.");
                Check(typeof(global::UI.DailyLoginStreakUI).GetField("serverSchedule", flags).GetValue(ui) == null, "Previous owner's schedule remained after sign-out.");
                Check(events == 0, "Session reset must not log a reward or dismissal for another account.");
                SessionState.SetBool("CloudDailyRewardTests.Passed", true);
                Debug.Log("[CloudDailyRewardTests] 11 checks passed. No cloud requests or reward grants made.");
            }
            finally
            {
                Gameplay.LevelAnalytics.EventRecorded -= hook;
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        [MenuItem("Tools/City Blocks/Tests/Cloud Daily Boundary Checks")]
        private static async void RunBoundaries()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized)
            {
                Debug.LogWarning("Run boundary checks after services have shut down; synthetic analytics must not reach live telemetry.");
                return;
            }
            SessionState.SetBool("CloudDailyBoundaryTests.Passed", false);
            var analyticsQueue = typeof(CloudServices.AnalyticsSink).GetField("Pending", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            var queueType = analyticsQueue.GetType();
            var savedAnalytics = (Array)queueType.GetMethod("ToArray").Invoke(analyticsQueue, null);
            string[] keys = { "cityblocks_daily_server_cache", "cityblocks_daily_legacy_owner" };
            var saved = new System.Collections.Generic.Dictionary<string, string>();
            foreach (string key in keys) saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            var cacheField = typeof(CloudServices.CloudDailyRewards).GetField("cache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            object savedCache = cacheField.GetValue(null);
            int events = 0, checks = 0;
            Action<string, System.Collections.Generic.IDictionary<string, object>> hook = (name, p) => { if (name == "daily_reward_claimed") events++; };
            Gameplay.LevelAnalytics.EventRecorded += hook;
            try
            {
                PlayerPrefs.SetString(keys[1], "old-owner");
                PlayerPrefs.SetString(keys[0], "unchanged");
                int claims = 0, migrations = 0, refreshes = 0;
                string player = "A"; long revision = 1;
                Func<bool> current = () => player == "A" && revision == 1;
                Func<System.Threading.Tasks.Task<bool>> refresh = () => { refreshes++; return System.Threading.Tasks.Task.FromResult(true); };
                Func<System.Threading.Tasks.Task<CloudServices.DailyRewardResponse>> claim = () => { claims++; return System.Threading.Tasks.Task.FromResult(Response("applied")); };
                Func<int, string, System.Threading.Tasks.Task<CloudServices.DailyRewardResponse>> migrate = (day, date) =>
                {
                    Check(day == 0 && date == "", "Old-owner migration state was transferred."); checks++;
                    migrations++; return System.Threading.Tasks.Task.FromResult(Response("available"));
                };

                var deferred = new System.Threading.Tasks.TaskCompletionSource<CloudServices.DailyRewardResponse>();
                var pending = Workflow(current, () => deferred.Task, migrate, claim, refresh);
                player = "B"; revision++; player = "A"; revision++;
                deferred.SetResult(Response("available"));
                Check(await pending == null && claims == 0 && migrations == 0 && refreshes == 0, "A-B-A status response continued into a mutation."); checks++;
                Check(PlayerPrefs.GetString(keys[0]) == "unchanged", "Stale status changed the cache."); checks++;

                revision = 1;
                var deferredMigration = new System.Threading.Tasks.TaskCompletionSource<CloudServices.DailyRewardResponse>();
                pending = Workflow(current, () => System.Threading.Tasks.Task.FromResult(Response("available", false)),
                    (day, date) => deferredMigration.Task, claim, refresh);
                revision++;
                deferredMigration.SetResult(Response("available"));
                Check(await pending == null && claims == 0 && events == 0, "Stale migration continued into a claim."); checks++;

                revision = 1;
                var deferredClaim = new System.Threading.Tasks.TaskCompletionSource<CloudServices.DailyRewardResponse>();
                pending = Workflow(current, () => System.Threading.Tasks.Task.FromResult(Response("available")), migrate, () => deferredClaim.Task, refresh);
                player = "B";
                deferredClaim.SetResult(Response("applied"));
                Check(await pending == null && events == 0 && refreshes == 0, "Old-account claim logged or refreshed the new account."); checks++;

                player = "A";
                var applied = await Workflow(current, () => System.Threading.Tasks.Task.FromResult(Response("available", false)), migrate, claim, refresh);
                Check(applied.Status == "applied" && claims == 1 && migrations == 1 && refreshes == 1 && events == 1, "Confirmed daily claim workflow failed."); checks++;
                Check(PlayerPrefs.GetString(keys[1]) == "old-owner", "Installation migration owner was relabeled."); checks++;
                foreach (string status in new[] { "already_claimed", "pending" })
                {
                    var response = await Workflow(current, () => System.Threading.Tasks.Task.FromResult(Response(status)), migrate, claim, refresh);
                    Check(response.Status == status && claims == 1 && events == 1, "Replay/pending status claimed or logged another reward."); checks++;
                }
                var offline = await Workflow(current, () => System.Threading.Tasks.Task.FromResult<CloudServices.DailyRewardResponse>(null), migrate, claim, refresh);
                Check(offline == null && claims == 1 && events == 1, "Offline status fabricated a claim."); checks++;
                var legacy = typeof(Gameplay.DailyLoginStreakService).GetMethod("Claim");
                object[] arguments = { 0, null };
                Check(!(bool)legacy.Invoke(null, arguments) && arguments[1] == null && events == 1, "Local clock claim still mutates rewards."); checks++;
                SessionState.SetBool("CloudDailyBoundaryTests.Passed", true);
                Debug.Log("[CloudDailyBoundaryTests] " + checks + " checks passed. Deferred fake transports only; preferences restored.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                Gameplay.LevelAnalytics.EventRecorded -= hook;
                foreach (string key in keys) { if (saved[key] == null) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, saved[key]); }
                PlayerPrefs.Save(); cacheField.SetValue(null, savedCache);
                queueType.GetMethod("Clear").Invoke(analyticsQueue, null);
                foreach (object entry in savedAnalytics) queueType.GetMethod("Enqueue").Invoke(analyticsQueue, new[] { entry });
            }
        }

        private static CloudServices.DailyRewardResponse Response(string status, bool imported = true) => new CloudServices.DailyRewardResponse
        {
            Status = status, StreakDay = 2, LegacyStateImported = imported,
            Reward = new Gameplay.RewardAmounts { coins = 150, levelUpUses = 1 }
        };

        private static System.Threading.Tasks.Task<CloudServices.DailyRewardResponse> Workflow(Func<bool> current,
            Func<System.Threading.Tasks.Task<CloudServices.DailyRewardResponse>> status,
            Func<int, string, System.Threading.Tasks.Task<CloudServices.DailyRewardResponse>> migrate,
            Func<System.Threading.Tasks.Task<CloudServices.DailyRewardResponse>> claim, Func<System.Threading.Tasks.Task<bool>> refresh)
        {
            var method = typeof(CloudServices.CloudDailyRewards).GetMethod("CheckAndClaimForSessionAsync", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return (System.Threading.Tasks.Task<CloudServices.DailyRewardResponse>)method.Invoke(null, new object[] { "A", 1L, current, status, migrate, claim, refresh });
        }
}
}
