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
    public class PowerUpModule
    {
        private readonly IGameApiClient api;
        private readonly ILogger<PowerUpModule> logger;

        public PowerUpModule(IGameApiClient api, ILogger<PowerUpModule> logger)
        {
            this.api = api;
            this.logger = logger;
        }

        [CloudCodeFunction("UsePowerUp")]
        public async Task<PowerUpUseResult> UsePowerUp(IExecutionContext context, string powerUp, string useId)
        {
            if (string.IsNullOrEmpty(context.PlayerId)) throw new InvalidOperationException("A signed-in player is required.");
            var result = await PowerUpUse.ExecuteAsync(new Store(api, context), powerUp, useId, DateTimeOffset.UtcNow);
            logger.LogInformation("Power-up operation {UseId}: {Status}, replay={Replay}", useId, result.Status, result.Replay);
            return result;
        }

        private sealed class Store : IPowerUpUseStore
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
                // Reuse the already-existing welcome marker as a CAS anchor; this avoids unsafe create-if-absent
                // races for a new lock document. It remains a welcome marker and Bootstrap never rewrites it.
                var response = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new List<string> { Rules.WelcomeGrantedKey });
                var item = response.Data.Results.FirstOrDefault(i => i.Key == Rules.WelcomeGrantedKey);
                if (item == null) return null;
                string value = item.Value?.ToString() ?? "";
                PowerUpJournal journal;
                if (value.TrimStart().StartsWith("{"))
                    journal = JsonSerializer.Deserialize<PowerUpJournal>(value) ?? throw new InvalidOperationException("Invalid power-up journal.");
                else
                    journal = new PowerUpJournal { WelcomeGrantedAt = value };
                return new JournalRead { Journal = journal, WriteLock = item.WriteLock };
            }

            public async Task<string> WriteAsync(PowerUpJournal journal, string writeLock)
            {
                var response = await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken,
                    context.ProjectId, playerId,
                    new Unity.Services.CloudSave.Model.SetItemBody(Rules.WelcomeGrantedKey, JsonSerializer.Serialize(journal), writeLock));
                return response.Data.WriteLock;
            }

            public async Task<long> ReadBalanceAsync(string currencyId)
            {
                var response = await api.EconomyCurrencies.GetPlayerCurrenciesAsync(context, context.ServiceToken,
                    context.ProjectId, playerId);
                return response.Data.Results.FirstOrDefault(b => b.CurrencyId == currencyId)?.Balance ?? 0;
            }

            public async Task<long> DecrementAsync(string currencyId)
            {
                var response = await api.EconomyCurrencies.DecrementPlayerCurrencyBalanceAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, currencyId,
                    new Unity.Services.Economy.Model.CurrencyModifyBalanceRequest(currencyId, 1, null!));
                return response.Data.Balance;
            }
        }
    }
}
