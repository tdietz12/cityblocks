using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace CityBlocks.CloudCode
{
    public class LevelRewardsModule
    {
        private readonly IGameApiClient api;
        private readonly ILogger<LevelRewardsModule> logger;
        public LevelRewardsModule(IGameApiClient api, ILogger<LevelRewardsModule> logger)
        {
            this.api = api;
            this.logger = logger;
        }

        // The caller reports a win, not reward amounts. Board simulation is not validated here.
        [CloudCodeFunction("AwardLevelRewards")]
        public async Task<LevelRewardResult> AwardLevelRewards(IExecutionContext context, int levelNumber)
        {
            var result = await LevelRewardClaim.ExecuteAsync(new Store(api, context), RewardCatalog.Deployed,
                levelNumber, DateTimeOffset.UtcNow);
            logger.LogInformation("First-win reward for {PlayerId} level {LevelNumber}: {Status}",
                context.PlayerId, levelNumber, result.Status);
            return result;
        }

        internal sealed class Store : ILevelRewardStore
        {
            private readonly IGameApiClient api;
            private readonly IExecutionContext context;
            private readonly string playerId;
            public Store(IGameApiClient api, IExecutionContext context)
            {
                this.api = api;
                this.context = context;
                playerId = context.PlayerId ?? throw new InvalidOperationException("A signed-in player is required.");
            }
            public async Task<JournalRead?> ReadAsync()
            {
                var response = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new List<string> { Rules.WelcomeGrantedKey });
                var item = response.Data.Results.FirstOrDefault(i => i.Key == Rules.WelcomeGrantedKey);
                if (item == null) return null;
                string value = item.Value?.ToString() ?? "";
                var journal = value.TrimStart().StartsWith("{")
                    ? JsonSerializer.Deserialize<PowerUpJournal>(value) ?? throw new InvalidOperationException("Invalid journal.")
                    : new PowerUpJournal { WelcomeGrantedAt = value };
                return new JournalRead { Journal = journal, WriteLock = item.WriteLock };
            }
            public async Task<string> WriteAsync(PowerUpJournal journal, string writeLock)
            {
                var response = await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new Unity.Services.CloudSave.Model.SetItemBody(
                        Rules.WelcomeGrantedKey, JsonSerializer.Serialize(journal), writeLock));
                return response.Data.WriteLock;
            }
            public async Task<long> IncrementAsync(string currencyId, long amount)
            {
                var response = await api.EconomyCurrencies.IncrementPlayerCurrencyBalanceAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, currencyId,
                    new Unity.Services.Economy.Model.CurrencyModifyBalanceRequest(currencyId, amount, null!));
                return response.Data.Balance;
            }
        }
    }
}
