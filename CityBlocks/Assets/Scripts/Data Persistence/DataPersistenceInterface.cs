

//See this tutorial: https://www.youtube.com/watch?v=aUi9aijvpgs

namespace Data_Persistence
{
    public interface DataPersistenceInterface
    {
        void Load(GameSaveData gameSaveData, ProgressionData progressionData);
        void Save(ref GameSaveData gameSaveData, ref ProgressionData progressionData);
        //Ref is called here bacause the SaveData method is for modifyiung the data, whereas LoadData method is for just reading the data
    }
}
