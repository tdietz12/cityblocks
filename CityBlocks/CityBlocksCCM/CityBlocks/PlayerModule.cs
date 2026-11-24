using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Unity.Services.Economy.Model;

namespace CityBlocks.CloudCode
{
    /// <summary>
    /// City Blocks player functions (module name "CityBlocks", from Assets/CloudCode/CityBlocks.ccmr).
    /// Option B backend: only these functions may change Economy balances; the client reads them.
    /// </summary>
    public class PlayerModule
    {
        private readonly IGameApiClient gameApiClient;
        private readonly ILogger<PlayerModule> logger;

        public PlayerModule(IGameApiClient gameApiClient, ILogger<PlayerModule> logger)
        {
            this.gameApiClient = gameApiClient;
            this.logger = logger;
        }

        /// <summary>Current server time in UTC, ISO 8601 round-trip format (e.g. 2026-01-31T09:15:00.0000000Z).</summary>
        [CloudCodeFunction("GetServerTime")]
        public string GetServerTime() => FormatUtc(DateTime.UtcNow);

        /// <summary>
        /// Called on launch / resume. Grants the welcome bundle once (500 coins) and, when the client sends its local
        /// power-up counts, moves them into Economy once (capped). Returns balances, no-ads and server time.
        /// All Economy writes use the service token (Access Control refuses player-token writes).
        /// </summary>
        [CloudCodeFunction("Bootstrap")]
        public async Task<BootstrapResult> Bootstrap(IExecutionContext context, Dictionary<string, int>? legacyPowerUps = null)
        {
            string token = context.ServiceToken;
            string projectId = context.ProjectId;
            string playerId = context.PlayerId ?? throw new InvalidOperationException("Bootstrap needs a signed-in player.");
            var result = new BootstrapResult();

            var flags = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(context, token, projectId, playerId,
                new List<string> { Rules.WelcomeGrantedKey, Rules.LegacyMigratedKey });
            var done = new HashSet<string>(flags.Data.Results.Select(item => item.Key));

            if (!done.Contains(Rules.WelcomeGrantedKey))
            {
                await IncrementAsync(context, token, projectId, playerId, EconomyIds.Coins, Rules.WelcomeCoins);
                await SetFlagAsync(context, token, projectId, playerId, Rules.WelcomeGrantedKey);
                result.WelcomeCoinsGranted = Rules.WelcomeCoins;
                logger.LogInformation("Welcome bundle granted to {PlayerId}", playerId);
            }

            if (legacyPowerUps != null && !done.Contains(Rules.LegacyMigratedKey))
            {
                foreach (var grant in Rules.PlanMigration(legacyPowerUps))
                {
                    await IncrementAsync(context, token, projectId, playerId, EconomyIds.PowerUpCurrencies[grant.Key], grant.Value);
                    result.Migrated[grant.Key] = grant.Value;
                }
                await SetFlagAsync(context, token, projectId, playerId, Rules.LegacyMigratedKey);
                logger.LogInformation("Legacy power-ups migrated for {PlayerId}: {Count} types", playerId, result.Migrated.Count);
            }

            var balances = await gameApiClient.EconomyCurrencies.GetPlayerCurrenciesAsync(context, token, projectId, playerId);
            foreach (var balance in balances.Data.Results)
            {
                if (balance.CurrencyId == EconomyIds.Coins) result.Coins = balance.Balance;
                foreach (var powerUp in EconomyIds.PowerUpCurrencies)
                    if (powerUp.Value == balance.CurrencyId) result.PowerUps[powerUp.Key] = balance.Balance;
            }
            foreach (var powerUp in EconomyIds.PowerUpCurrencies.Keys)
                if (!result.PowerUps.ContainsKey(powerUp)) result.PowerUps[powerUp] = 0;

            var inventory = await gameApiClient.EconomyInventory.GetPlayerInventoryAsync(context, token, projectId, playerId,
                inventoryItemIds: new List<string> { EconomyIds.NoAds });
            result.AdsRemoved = inventory.Data.Results.Any(item => item.InventoryItemId == EconomyIds.NoAds);

            result.ServerTimeUtc = FormatUtc(DateTime.UtcNow);
            return result;
        }

        private Task IncrementAsync(IExecutionContext context, string token, string projectId, string playerId, string currencyId, long amount)
            => gameApiClient.EconomyCurrencies.IncrementPlayerCurrencyBalanceAsync(context, token, projectId, playerId, currencyId,
                new CurrencyModifyBalanceRequest(currencyId, amount, null!));

        private Task SetFlagAsync(IExecutionContext context, string token, string projectId, string playerId, string key)
            => gameApiClient.CloudSaveData.SetProtectedItemAsync(context, token, projectId, playerId,
                new SetItemBody(key, FormatUtc(DateTime.UtcNow), null!));

        public static string FormatUtc(DateTime utc) => utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }
}
