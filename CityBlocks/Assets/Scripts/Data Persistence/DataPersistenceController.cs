using System.Collections.Generic;
using System.Linq;
using Gameplay;
using UnityEngine;

//See this tutorial: https://www.youtube.com/watch?v=aUi9aijvpgs

namespace Data_Persistence
{
    public class DataPersistenceController : MonoBehaviour
    {
        [Header("Save Game File Name")]
        [SerializeField] private string saveGameFileName;

        [Header("Progression Data File Name")]
        [SerializeField] private string progressionDataFileName;

        //Creates a singleton (only one per scene), get the instace publicly, can only be set privately
        public static DataPersistenceController instance { get; private set; }

        private GameSaveData gameSaveData;
        private ProgressionData progressionData;
        private List<DataPersistenceInterface> dataPersistenceObjects = new List<DataPersistenceInterface>();

        private FileHandler fileHandler;

        void Awake()
        {
            if (instance != null)
            {
                Debug.LogError("Found more than one GameObject with this class in the scene.");
            }
            instance = this;
        }

        void Start()
        {
            if (gameObject.scene.name == "EndlessMode" && LevelFlow.IsLevelMode) return;
            this.fileHandler = new FileHandler(Application.persistentDataPath, saveGameFileName, progressionDataFileName);
            this.dataPersistenceObjects = FindAllDataPersistenceObjects();
            LoadGame();
        }

        public void LoadGame()
        {
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
        }

        public void NewGame()
        {
            this.gameSaveData = new GameSaveData();
            this.progressionData = new ProgressionData();
        }

        public void SaveGame()
        {
            foreach (DataPersistenceInterface dataPersistenceInterface in dataPersistenceObjects)
            {
                dataPersistenceInterface.Save(ref gameSaveData, ref progressionData);
            }

            fileHandler.Save(gameSaveData, progressionData);
        }

        private void OnDisable()
        {
            if (fileHandler != null) SaveGame();
        }

        List<DataPersistenceInterface> FindAllDataPersistenceObjects()
        {
            //Use Linq function to find all gameObjects that use DataPersistenceInterface, add them to the list, and return the list
            IEnumerable<DataPersistenceInterface> dataPersistenceObjects = FindObjectsOfType<MonoBehaviour>().OfType<DataPersistenceInterface>();
            return new List<DataPersistenceInterface>(dataPersistenceObjects);
        }
    }
}
