using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Client calls to the City Blocks Cloud Code module (CityBlocksCCM, deployed from Assets/CloudCode/CityBlocks.ccmr).
    /// Each call makes sure the player is signed in first and returns null instead of throwing when offline / failing.
    /// </summary>
    public static class CloudFunctions
    {
        /// <summary>Module name = file name of the .ccmr reference.</summary>
        public const string ModuleName = "CityBlocks";

        /// <summary>
        /// Calls Bootstrap: grants the welcome bundle once (server side), migrates local power-ups once when
        /// <paramref name="legacyPowerUps"/> is given (game name → count; sent from Step 26 on), and returns balances.
        /// Logs coins_earned / powerup_granted for anything this call granted. Null if offline / failed.
        /// </summary>
        public static async Task<PlayerSnapshot> BootstrapAsync(Dictionary<string, int> legacyPowerUps = null)
        {
            if (!await CloudBootstrap.EnsureSignedInAsync()) return null;
            var args = new Dictionary<string, object>();
            if (legacyPowerUps != null) args["legacyPowerUps"] = legacyPowerUps;
            try
            {
                PlayerSnapshot snapshot = await CloudCodeService.Instance.CallModuleEndpointAsync<PlayerSnapshot>(ModuleName, "Bootstrap", args);
                if (snapshot == null) return null;
                if (snapshot.WelcomeCoinsGranted > 0)
                    Gameplay.LevelAnalytics.CoinsEarned((int)snapshot.WelcomeCoinsGranted, "welcome", (int)snapshot.Coins);
                if (snapshot.Migrated != null)
                {
                    foreach (var moved in snapshot.Migrated)
                    {
                        if (moved.Value <= 0 || !Enum.TryParse(moved.Key, true, out Gameplay.GameController.PowerUpType type)) continue;
                        Gameplay.LevelAnalytics.PowerUpGranted(type, (int)moved.Value, "migration", (int)snapshot.PowerUp(moved.Key));
                    }
                }
                return snapshot;
            }
            catch (CloudCodeException exception)
            {
                Debug.LogWarning("[CloudFunctions] Bootstrap failed (" + exception.Reason + "): " + exception.Message);
                return null;
            }
        }

        /// <summary>Reports a campaign win. The server selects amounts and prevents repeated rewards.</summary>
        public static async Task<LevelWinRewardResult> AwardLevelRewardsAsync(int levelNumber)
        {
            if (!await CloudBootstrap.EnsureSignedInAsync()) return null;
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<LevelWinRewardResult>(ModuleName, "AwardLevelRewards",
                    new Dictionary<string, object> { { "levelNumber", levelNumber } });
            }
            catch (CloudCodeException exception)
            {
                Debug.LogWarning("[CloudFunctions] AwardLevelRewards failed (" + exception.Reason + "): " + exception.Message);
                return null;
            }
        }

        public static Task<DailyRewardResponse> GetDailyRewardStatusAsync() => CallDailyAsync("GetDailyRewardStatus", null);
        public static Task<DailyRewardResponse> ClaimDailyRewardAsync() => CallDailyAsync("ClaimDailyReward", null);
        public static Task<DailyRewardResponse> MigrateDailyRewardStateAsync(int streakDay, string lastClaimUtcDate)
            => CallDailyAsync("MigrateDailyRewardState", new Dictionary<string, object>
            {
                { "streakDay", streakDay }, { "lastClaimUtcDate", lastClaimUtcDate ?? "" }
            });

        private static async Task<DailyRewardResponse> CallDailyAsync(string endpoint, Dictionary<string, object> args)
        {
            if (!await CloudBootstrap.EnsureSignedInAsync()) return null;
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<DailyRewardResponse>(ModuleName, endpoint, args);
            }
            catch (CloudCodeException exception)
            {
                Debug.LogWarning("[DailyRewards] " + endpoint + " failed (" + exception.Reason + "): " + exception.Message);
                return null;
            }
        }

        /// <summary>Server time in UTC, or null if offline / the call failed.</summary>
        public static async Task<DateTime?> GetServerTimeAsync()
        {
            if (!await CloudBootstrap.EnsureSignedInAsync()) return null;
            try
            {
                string value = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(ModuleName, "GetServerTime");
                return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
            }
            catch (CloudCodeException exception)
            {
                Debug.LogWarning("[CloudFunctions] GetServerTime failed (" + exception.Reason + "): " + exception.Message);
                return null;
            }
            catch (FormatException exception)
            {
                Debug.LogWarning("[CloudFunctions] GetServerTime returned an unexpected value: " + exception.Message);
                return null;
            }
        }
    }
}
