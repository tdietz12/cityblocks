using System.Collections.Generic;

namespace CityBlocks.CloudCode
{
    public class DailyRewardReceipt
    {
        public string UtcDate { get; set; } = "";
        public string Status { get; set; } = "pending";
        public int StreakDay { get; set; }
        public int PreviousStreak { get; set; }
        public int DaysMissed { get; set; }
        public Dictionary<string, long> Grants { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> BalancesAfter { get; set; } = new Dictionary<string, long>();
    }

    public class DailyRewardResult
    {
        // available (read-only preview), applied, already_claimed, pending, not_initialized
        public string Status { get; set; } = "pending";
        public string UtcDate { get; set; } = "";
        public string ServerTimeUtc { get; set; } = "";
        public string NextClaimUtc { get; set; } = "";
        public int StreakDay { get; set; } = 1;
        public int PreviousStreak { get; set; }
        public int DaysMissed { get; set; }
        public RewardBundle Reward { get; set; } = new RewardBundle();
        public Dictionary<string, long> Granted { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> BalancesAfter { get; set; } = new Dictionary<string, long>();
    }
}
