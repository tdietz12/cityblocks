using System.Collections.Generic;

namespace CityBlocks.CloudCode
{
    public class LevelRewardReceipt
    {
        public int LevelNumber { get; set; }
        public string Status { get; set; } = "pending";
        public string ClaimedAtUtc { get; set; } = "";
        // Freeze the configured bundle when reserved, so redeploys cannot change an in-flight grant.
        public Dictionary<string, long> Grants { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> BalancesAfter { get; set; } = new Dictionary<string, long>();
    }

    public class LevelRewardResult
    {
        // applied, already_claimed, pending, invalid, not_initialized
        public string Status { get; set; } = "pending";
        public int LevelNumber { get; set; }
        public string ServerTimeUtc { get; set; } = "";
        public Dictionary<string, long> Granted { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> BalancesAfter { get; set; } = new Dictionary<string, long>();
    }
}
