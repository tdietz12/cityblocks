using System.Collections.Generic;

namespace CloudServices
{
    public class LevelWinRewardResult
    {
        public string Status;
        public int LevelNumber;
        public string ServerTimeUtc;
        public Dictionary<string, long> Granted = new Dictionary<string, long>();
        public Dictionary<string, long> BalancesAfter = new Dictionary<string, long>();
    }
}
