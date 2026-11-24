using Unity.Services.CloudCode.Core;

namespace CityBlocks.CloudCode
{
    public class RewardCatalogStatus
    {
        public int SchemaVersion { get; set; } = 1;
        public bool FirstWinOnly { get; set; } = true;
        public int LevelCount { get; set; }
        public int DailyCount { get; set; }
    }

    public class RewardCatalogModule
    {
        [CloudCodeFunction("GetRewardCatalogStatus")]
        public RewardCatalogStatus GetRewardCatalogStatus() => new RewardCatalogStatus
        {
            LevelCount = RewardCatalog.Deployed.LevelCount,
            DailyCount = RewardCatalog.Deployed.DailyCount
        };
    }
}
