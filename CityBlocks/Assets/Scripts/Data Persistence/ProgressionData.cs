namespace Data_Persistence
{
    [System.Serializable]
    public class ProgressionData
    {
        public int recordScore;
        public int recordLevel;
        public int xp;
        public int softCurrency;
        public int hardCurrency;

        public ProgressionData()
        {
            this.recordScore = 0;
            this.recordLevel = 0;
            this.xp = 0;
            this.softCurrency = 0;
            this.hardCurrency = 0;
        }
    }
}
