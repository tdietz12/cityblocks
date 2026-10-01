using System;
using System.IO;
using UnityEngine;

namespace Gameplay
{
    public static class LevelProgressStore
    {
        private const string FileName = "level-progress.json";

        public static LevelProgressData Load()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, FileName);
                if (!File.Exists(path)) return new LevelProgressData();
                LevelProgressData data = JsonUtility.FromJson<LevelProgressData>(File.ReadAllText(path));
                if (data == null) return new LevelProgressData();
                data.highestUnlockedLevel = Mathf.Max(1, data.highestUnlockedLevel);
                if (data.completedLevels == null) data.completedLevels = new System.Collections.Generic.List<int>();
                if (data.levelAttempts == null) data.levelAttempts = new System.Collections.Generic.List<LevelAttemptCounter>();
                if (data.processedPurchaseIds == null) data.processedPurchaseIds = new System.Collections.Generic.List<string>();
                if (data.activeRun != null && data.activeRun.levelNumber <= 0)
                    data.activeRun = null;
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not read level progress: " + exception);
                return new LevelProgressData();
            }
        }

        public static void Save(LevelProgressData data)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, FileName);
                Directory.CreateDirectory(Application.persistentDataPath);
                string temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not save level progress: " + exception);
            }
        }
    }
}
