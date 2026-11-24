using System;
using System.IO;
using System.Linq;
using Gameplay;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    public static class RewardConfigurationExport
    {
        public const string SchedulePath = "Assets/Gameplay/Rewards/DailyRewardSchedule.asset";
        public const string OutputPath = "CityBlocksCCM/CityBlocks/RewardConfiguration.json";

        [Serializable] private class LevelReward { public int levelNumber; public RewardAmounts rewards; }
        [Serializable] private class Configuration
        {
            public int schemaVersion = 1;
            public bool firstWinOnly = true;
            public LevelReward[] levels;
            public RewardAmounts[] daily;
        }

        [MenuItem("Tools/City Blocks/Cloud/Export Reward Configuration")]
        public static void Export()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<DailyRewardSchedule>(SchedulePath);
            if (schedule == null || !schedule.IsValid)
            {
                Debug.LogError("[Rewards] Daily schedule must contain exactly 7 valid, non-negative reward entries: " + SchedulePath);
                return;
            }
            var levels = AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets/Resources/Levels" })
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(l => l.levelNumber).ToArray();
            if (levels.Length == 0 || levels.Any(l => l.levelNumber < 1 || l.firstWinRewards == null || !l.firstWinRewards.IsValid) ||
                levels.GroupBy(l => l.levelNumber).Any(group => group.Count() != 1))
            {
                Debug.LogError("[Rewards] Export refused: invalid reward amounts, missing levels or duplicate level numbers. Previous export left untouched.");
                return;
            }
            var config = new Configuration
            {
                levels = levels.Select(l => new LevelReward { levelNumber = l.levelNumber, rewards = l.firstWinRewards }).ToArray(),
                daily = schedule.days
            };
            string path = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(config, true) + "\n");
            Debug.Log("[Rewards] Exported " + levels.Length + " first-win rewards and 7 daily rewards to " + OutputPath +
                ". Backend claim wiring is required before these values affect gameplay; redeploy after editing/exporting.");
        }
    }
}
