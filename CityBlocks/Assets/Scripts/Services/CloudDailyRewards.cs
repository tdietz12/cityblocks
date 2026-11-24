using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CloudServices
{
    public static class CloudDailyRewards
    {
        private const string CacheKey = "cityblocks_daily_server_cache";
        private const string LegacyOwnerKey = "cityblocks_daily_legacy_owner";
        [Serializable] private class Cache { public string playerId; public DailyRewardResponse response; }
        private static Cache cache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cache = null;

        public static DailyRewardResponse Cached
        {
            get
            {
                if (cache == null)
                {
                    try { cache = JsonUtility.FromJson<Cache>(PlayerPrefs.GetString(CacheKey, "")); }
                    catch (Exception) { }
                }
                string player = AccountService.PlayerId;
                return cache != null && (string.IsNullOrEmpty(player) || player == cache.playerId) ? cache.response : null;
            }
        }

        public static async Task<DailyRewardResponse> CheckAndClaimAsync()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable || !await CloudBootstrap.EnsureSignedInAsync()) return null;
            string owner = AccountService.PlayerId;
            var status = await CloudFunctions.GetDailyRewardStatusAsync();
            if (status != null && status.Status == "not_initialized")
            {
                if (!await PlayerWallet.RefreshAsync()) return null;
                status = await CloudFunctions.GetDailyRewardStatusAsync();
            }
            if (AccountService.PlayerId != owner || status == null) return null;
            if (!status.LegacyStateImported)
            {
                string legacyOwner = PlayerPrefs.GetString(LegacyOwnerKey, "");
                if (string.IsNullOrEmpty(legacyOwner))
                {
                    legacyOwner = owner;
                    PlayerPrefs.SetString(LegacyOwnerKey, owner);
                    PlayerPrefs.Save();
                }
                int streak = 0; string date = "";
                if (legacyOwner == owner) Gameplay.DailyLoginStreakService.ReadLegacyState(out streak, out date);
                status = await CloudFunctions.MigrateDailyRewardStateAsync(streak, date);
                if (status == null || AccountService.PlayerId != owner) return null;
            }
            Store(status, owner);
            if (status.Status != "available") return status;
            var claimed = await CloudFunctions.ClaimDailyRewardAsync();
            if (claimed == null || AccountService.PlayerId != owner) return null;
            Store(claimed, owner);
            if (claimed.Status == "applied")
            {
                LogConfirmedGrants(claimed);
                await PlayerWallet.RefreshAsync();
            }
            return claimed;
        }

        private static void Store(DailyRewardResponse response, string owner)
        {
            cache = new Cache { playerId = owner, response = response };
            PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(cache));
            PlayerPrefs.Save();
        }

        private static void LogConfirmedGrants(DailyRewardResponse response)
        {
            if (response.PreviousStreak > 0 && response.DaysMissed > 0)
                Gameplay.LevelAnalytics.DailyStreakBroken(response.PreviousStreak, response.DaysMissed);
            var powerUps = response.Reward?.ToPowerUps();
            Gameplay.LevelAnalytics.DailyRewardClaimed(response.StreakDay, Gameplay.DailyLoginStreakService.TotalUses(powerUps));
            if (response.Granted == null || response.BalancesAfter == null) return;
            foreach (var grant in response.Granted)
            {
                if (grant.Value <= 0 || !response.BalancesAfter.TryGetValue(grant.Key, out long balance)) continue;
                int amount = (int)Math.Min(int.MaxValue, grant.Value);
                int after = (int)Math.Min(int.MaxValue, balance);
                if (grant.Key == "COINS") Gameplay.LevelAnalytics.CoinsEarned(amount, "daily", after);
                else
                {
                    Gameplay.GameController.PowerUpType type;
                    switch (grant.Key)
                    {
                        case "PU_DELETE": type = Gameplay.GameController.PowerUpType.delete; break;
                        case "PU_LEVELUP": type = Gameplay.GameController.PowerUpType.levelUp; break;
                        case "PU_LOOKAHEAD": type = Gameplay.GameController.PowerUpType.lookAhead; break;
                        case "PU_EXTRATURNS": type = Gameplay.GameController.PowerUpType.extraTurns; break;
                        default: continue;
                    }
                    Gameplay.LevelAnalytics.PowerUpGranted(type, amount, "daily", after);
                }
            }
        }
    }
}
