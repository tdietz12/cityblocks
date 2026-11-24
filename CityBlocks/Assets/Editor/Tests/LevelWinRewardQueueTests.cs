using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class LevelWinRewardQueueTests
    {
        [MenuItem("Tools/City Blocks/Tests/Level Win Reward Queue Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Run queue persistence checks in Edit Mode.");
                return;
            }
            const string queueKey = "cityblocks_level_reward_queue";
            const string walletKey = "cityblocks_wallet_cache";
            bool hadQueue = PlayerPrefs.HasKey(queueKey), hadWallet = PlayerPrefs.HasKey(walletKey);
            string oldQueue = PlayerPrefs.GetString(queueKey, ""), oldWallet = PlayerPrefs.GetString(walletKey, "");
            var field = typeof(CloudServices.LevelWinRewardQueue).GetField("pending", BindingFlags.NonPublic | BindingFlags.Static);
            object oldPending = field.GetValue(null);
            try
            {
                PlayerPrefs.DeleteKey(queueKey);
                PlayerPrefs.SetString(walletKey, "{\"playerId\":\"queue-test-A\"}");
                field.SetValue(null, null);
                CloudServices.LevelWinRewardQueue.Enqueue(10);
                CloudServices.LevelWinRewardQueue.Enqueue(10);
                CloudServices.LevelWinRewardQueue.Enqueue(11);
                var first = JObject.Parse(PlayerPrefs.GetString(queueKey));
                Check(((JArray)first["entries"]).Count == 2, "Repeated wins must deduplicate per player/level.");
                Check((string)first["entries"][0]["playerId"] == "queue-test-A", "Offline wins must retain their last known player ID.");
                field.SetValue(null, null); // Simulate restart reading the persisted queue.
                PlayerPrefs.SetString(walletKey, "{\"playerId\":\"queue-test-B\"}");
                CloudServices.LevelWinRewardQueue.Enqueue(10);
                var second = JObject.Parse(PlayerPrefs.GetString(queueKey));
                Check(((JArray)second["entries"]).Count == 3, "Different accounts must have separate level claims.");
                Check((string)second["entries"][2]["playerId"] == "queue-test-B", "Queued claims must stay bound to the earning account.");
                CloudServices.LevelWinRewardQueue.Enqueue(0);
                Check(((JArray)JObject.Parse(PlayerPrefs.GetString(queueKey))["entries"]).Count == 3, "Invalid levels must not enqueue.");
                SessionState.SetBool("LevelWinRewardQueueTests.Passed", true);
                Debug.Log("[LevelWinRewardQueueTests] 5 persistence checks passed; no cloud calls made.");
            }
            finally
            {
                if (hadQueue) PlayerPrefs.SetString(queueKey, oldQueue); else PlayerPrefs.DeleteKey(queueKey);
                if (hadWallet) PlayerPrefs.SetString(walletKey, oldWallet); else PlayerPrefs.DeleteKey(walletKey);
                PlayerPrefs.Save();
                field.SetValue(null, oldPending);
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
