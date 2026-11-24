using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace CityBlocks.CloudCode
{
    public class DailyRewardsModule
    {
        private readonly IGameApiClient api;
        private readonly ILogger<DailyRewardsModule> logger;
        public DailyRewardsModule(IGameApiClient api, ILogger<DailyRewardsModule> logger)
        {
            this.api = api;
            this.logger = logger;
        }

        [CloudCodeFunction("GetDailyRewardStatus")]
        public async Task<DailyRewardResult> GetDailyRewardStatus(IExecutionContext context)
        {
            var read = await new LevelRewardsModule.Store(api, context).ReadAsync();
            return DailyRewardClaim.Preview(read?.Journal, RewardCatalog.Deployed, DateTimeOffset.UtcNow);
        }

        [CloudCodeFunction("ClaimDailyReward")]
        public async Task<DailyRewardResult> ClaimDailyReward(IExecutionContext context)
        {
            var result = await DailyRewardClaim.ExecuteAsync(new LevelRewardsModule.Store(api, context),
                RewardCatalog.Deployed, DateTimeOffset.UtcNow);
            logger.LogInformation("Daily reward for {PlayerId} UTC day {UtcDate}, streak {StreakDay}: {Status}",
                context.PlayerId, result.UtcDate, result.StreakDay, result.Status);
            return result;
        }
    }
}
