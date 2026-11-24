using System;
using System.Globalization;
using System.Threading.Tasks;

namespace CityBlocks.CloudCode
{
    public static class DailyRewardMigration
    {
        public static async Task<DailyRewardResult> ImportAsync(ILevelRewardStore store, RewardCatalog catalog,
            int streakDay, string lastClaimUtcDate, DateTimeOffset now)
        {
            var read = await store.ReadAsync();
            if (read == null) return DailyRewardClaim.Preview(null, catalog, now);
            if (read.Journal.LegacyDailyStateImported) return DailyRewardClaim.Preview(read.Journal, catalog, now);
            if (read.Journal.HasPendingOperation)
            {
                var pending = DailyRewardClaim.Preview(read.Journal, catalog, now);
                pending.Status = "pending";
                return pending;
            }
            // Legacy data is inherently client-reported. Import only once, cap streak at seven,
            // ignore invalid/future dates, and never issue retroactive rewards or override cloud claims.
            if (string.IsNullOrEmpty(read.Journal.LastDailyClaimUtcDate) && read.Journal.DailyClaims.Count == 0 &&
                streakDay > 0 && DateTime.TryParseExact(lastClaimUtcDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime last) && last.Date <= now.UtcDateTime.Date)
            {
                read.Journal.LastDailyClaimUtcDate = lastClaimUtcDate;
                read.Journal.DailyStreakDay = Math.Min(7, streakDay);
            }
            read.Journal.LegacyDailyStateImported = true;
            try
            {
                if (string.IsNullOrEmpty(read.WriteLock)) throw new InvalidOperationException("Missing migration write lock.");
                await store.WriteAsync(read.Journal, read.WriteLock);
            }
            catch (Exception)
            {
                var pending = DailyRewardClaim.Preview(read.Journal, catalog, now);
                pending.Status = "pending";
                pending.LegacyStateImported = false;
                return pending;
            }
            return DailyRewardClaim.Preview(read.Journal, catalog, now);
        }
    }
}
