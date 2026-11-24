using System.Collections.Generic;

namespace CloudServices
{
    [System.Serializable]
    public class DailyRewardResponse
    {
        public string Status;
        public string UtcDate;
        public string ServerTimeUtc;
        public string NextClaimUtc;
        public int StreakDay;
        public int PreviousStreak;
        public int DaysMissed;
        public Gameplay.RewardAmounts Reward;
        public Gameplay.RewardAmounts[] Schedule;
        public bool LegacyStateImported;
        public Dictionary<string, long> Granted;
        public Dictionary<string, long> BalancesAfter;
    }
}
