using System.Collections.Generic;
using System.Linq;
using Gameplay;
using UI;
using UnityEngine;

//See this tutorial: https://www.youtube.com/watch?v=aUi9aijvpgs

namespace Data_Persistence
{
    public class DataPersistenceController : MonoBehaviour
    {
        [Header("Save Game File Name")]
        [SerializeField] private string saveGameFileName = "SaveGameData";

        [Header("Progression Data File Name")]
        [SerializeField] private string progressionDataFileName = "ProgressionData";

        //Creates a singleton (only one per scene), get the instace publicly, can only be set privately
        public static DataPersistenceController instance { get; private set; }

        public bool HasLoaded { get; private set; }

        private GameSaveData gameSaveData;
        private ProgressionData progressionData;
        private List<DataPersistenceInterface> dataPersistenceObjects = new List<DataPersistenceInterface>();

        private FileHandler fileHandler;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("Found more than one GameObject with this class in the scene.");
            }
            instance = this;
            fileHandler = new FileHandler(Application.persistentDataPath, saveGameFileName, progressionDataFileName);
        }

        private void Start()
        {
            if (gameObject.scene.name == "EndlessMode" && LevelFlow.IsLevelMode) return;
            if (!HasLoaded)
            {
                LoadGame();
            }
        }

        public void LoadGame()
        {
            if (fileHandler == null)
            {
                fileHandler = new FileHandler(Application.persistentDataPath, saveGameFileName, progressionDataFileName);
            }

            dataPersistenceObjects = FindAllDataPersistenceObjects();

            this.gameSaveData = fileHandler.LoadGameSaveData();
            this.progressionData = fileHandler.LoadProgressionData();

            if (this.gameSaveData == null || this.progressionData == null)
            {
                NewGame();
            }

            foreach (DataPersistenceInterface dataPersistenceInterface in dataPersistenceObjects)
            {
                dataPersistenceInterface.Load(gameSaveData, progressionData);
            }

            HasLoaded = true;

            LevelRulesUI rulesUI = LevelRulesUI.Instance ?? FindObjectsByType<LevelRulesUI>(FindObjectsInactive.Include).FirstOrDefault();
            if (rulesUI != null && !LevelFlow.IsLevelMode)
            {
                rulesUI.Refresh();
            }
        }

        public void NewGame()
        {
            this.gameSaveData = new GameSaveData();
            if (this.progressionData == null)
            {
                this.progressionData = new ProgressionData();
            }
        }

        public void SaveGame()
        {
            if (fileHandler == null)
            {
                fileHandler = new FileHandler(Application.persistentDataPath, saveGameFileName, progressionDataFileName);
            }

            // Never write before the save file was loaded in this scene, otherwise a blank/foreign
            // state (e.g. a campaign level session) would overwrite the stored endless board.
            if (!HasLoaded || gameSaveData == null) return;

            if (dataPersistenceObjects == null || dataPersistenceObjects.Count == 0)
            {
                dataPersistenceObjects = FindAllDataPersistenceObjects();
            }

            foreach (DataPersistenceInterface dataPersistenceInterface in dataPersistenceObjects)
            {
                dataPersistenceInterface.Save(ref gameSaveData, ref progressionData);
            }

            fileHandler.Save(gameSaveData, progressionData);
        }

        private void OnDisable()
        {
            if (fileHandler != null && !LevelFlow.IsLevelMode)
            {
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && fileHandler != null && !LevelFlow.IsLevelMode)
            {
                SaveGame();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && fileHandler != null && !LevelFlow.IsLevelMode)
            {
                SaveGame();
            }
        }

        private void OnApplicationQuit()
        {
            if (fileHandler != null && !LevelFlow.IsLevelMode)
            {
                SaveGame();
            }
        }

        private List<DataPersistenceInterface> FindAllDataPersistenceObjects()
        {
            // Find all active/inactive gameObjects implementing DataPersistenceInterface in the scene
            IEnumerable<DataPersistenceInterface> dataPersistenceInterfaces = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).OfType<DataPersistenceInterface>();
            return new List<DataPersistenceInterface>(dataPersistenceInterfaces);
        }
    }
}
