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
            string priorOwner = AccountService.PlayerId;
            long priorRevision = AccountService.SessionRevision;
            if (Application.internetReachability == NetworkReachability.NotReachable || !await CloudBootstrap.EnsureSignedInAsync()) return null;
            if (!string.IsNullOrEmpty(priorOwner) && !IsCurrentSession(priorOwner, priorRevision)) return null;
            string owner = AccountService.PlayerId;
            long revision = AccountService.SessionRevision;
            return await CheckAndClaimForSessionAsync(owner, revision,
                () => IsCurrentSession(owner, revision), CloudFunctions.GetDailyRewardStatusAsync,
                CloudFunctions.MigrateDailyRewardStateAsync, CloudFunctions.ClaimDailyRewardAsync, PlayerWallet.RefreshAsync);
        }

        internal static bool IsCurrentSession(string owner, long revision) =>
            !string.IsNullOrEmpty(owner) && AccountService.PlayerId == owner && AccountService.SessionRevision == revision;

        // The same workflow is exercised with deferred, non-network transports by Editor regression checks.
        internal static async Task<DailyRewardResponse> CheckAndClaimForSessionAsync(string owner, long revision,
            Func<bool> isCurrent, Func<Task<DailyRewardResponse>> getStatus,
            Func<int, string, Task<DailyRewardResponse>> migrate, Func<Task<DailyRewardResponse>> claim,
            Func<Task<bool>> refreshWallet)
        {
            if (!isCurrent()) return null;
            var status = await getStatus();
            if (!isCurrent() || status == null) return null;
            if (status.Status == "not_initialized")
            {
                if (!await refreshWallet() || !isCurrent()) return null;
                status = await getStatus();
                if (!isCurrent() || status == null) return null;
            }
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
                status = await migrate(streak, date);
                if (!isCurrent() || status == null) return null;
            }
            Store(status, owner);
            if (status.Status != "available") return status;
            var claimed = await claim();
            if (!isCurrent() || claimed == null) return null;
            Store(claimed, owner);
            if (claimed.Status == "applied")
            {
                LogConfirmedGrants(claimed);
                await refreshWallet();
                if (!isCurrent()) return null;
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
