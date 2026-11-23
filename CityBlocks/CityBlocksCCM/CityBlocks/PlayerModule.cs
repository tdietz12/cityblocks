using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

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

        public static string FormatUtc(DateTime utc) => utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }
}
