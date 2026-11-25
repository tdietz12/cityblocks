using System;
using System.IO;
using UnityEngine;

//See this tutorial: https://www.youtube.com/watch?v=aUi9aijvpgs

namespace Data_Persistence
{
    public class FileHandler
    {
        private string fileDirectory = "";
        private string saveGameDataFileName = "";
        private string progressionDataFileName = "";

        public static event Action ProgressionSaved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ProgressionSaved = null;

        /// <summary>Updates only permanent records; leaves the run file and other progression fields intact.</summary>
        public bool MergeCloudRecords(int score, int level)
        {
            try
            {
                var data = LoadProgressionData() ?? new ProgressionData();
                data.recordScore = Math.Max(data.recordScore, score);
                data.recordLevel = Math.Max(data.recordLevel, level);
                string path = Path.Combine(fileDirectory, progressionDataFileName);
                Directory.CreateDirectory(fileDirectory);
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".tmp", path);
                RaiseProgressionSaved();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not persist cloud records: " + exception);
                return false;
            }
        }

        private static void RaiseProgressionSaved()
        {
            try { ProgressionSaved?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public FileHandler(string directoryPath, string saveGameDataFileName, string progressionDataFileName)
        {
            this.fileDirectory = directoryPath;
            this.saveGameDataFileName = saveGameDataFileName;
            this.progressionDataFileName = progressionDataFileName;
        }

        public GameSaveData LoadGameSaveData()
        {
            string fullPath = Path.Combine(fileDirectory, saveGameDataFileName);

            GameSaveData loadedSaveGameData = null;

            if (File.Exists(fullPath))
            {
                try
                {
                    string saveGameDataToLoad = "";

                    using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            saveGameDataToLoad = reader.ReadToEnd();
                        }
                    }

                    loadedSaveGameData = JsonUtility.FromJson<GameSaveData>(saveGameDataToLoad);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error occured while trying to load data from file: " + fullPath + "\n" + e);
                }
            }
            return loadedSaveGameData;
        }

        public ProgressionData LoadProgressionData()
        {
            string fullPath = Path.Combine(fileDirectory, progressionDataFileName);

            ProgressionData loadedProgressionData = null;

            if (File.Exists(fullPath))
            {
                try
                {
                    string progressionDataToLoad = "";

                    using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            progressionDataToLoad = reader.ReadToEnd();
                        }
                    }

                    loadedProgressionData = JsonUtility.FromJson<ProgressionData>(progressionDataToLoad);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error occured while trying to load data from file: " + fullPath + "\n" + e);
                }
            }
            return loadedProgressionData;
        }

        public void Save(GameSaveData gameSaveData, ProgressionData progressionData)
        {
            string saveGameDatafullPath = Path.Combine(fileDirectory, saveGameDataFileName);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(saveGameDatafullPath));

                string gameSaveDataToSave = JsonUtility.ToJson(gameSaveData, true);

                using (FileStream stream = new FileStream(saveGameDatafullPath, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(gameSaveDataToSave);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error occured while trying to save save game data: " + saveGameDatafullPath + "\n" + e);
            }

            string progressionDatafullPath = Path.Combine(fileDirectory, progressionDataFileName);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(progressionDatafullPath));

                string progressionDataToSave = JsonUtility.ToJson(progressionData, true);

                using (FileStream stream = new FileStream(progressionDatafullPath, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(progressionDataToSave);
                    }
                }
                RaiseProgressionSaved();
            }
            catch (Exception e)
            {
                Debug.LogError("Error occured while trying to save progression data: " + progressionDatafullPath + "\n" + e);
            }
        }
    }
}
