using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CityBlocks.CloudCode
{
    public static class DailyRewardClaim
    {
        public static DailyRewardResult Preview(PowerUpJournal? journal, RewardCatalog catalog, DateTimeOffset now)
        {
            DateTime today = now.UtcDateTime.Date;
            var result = new DailyRewardResult
            {
                UtcDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ServerTimeUtc = now.UtcDateTime.ToString("o"),
                NextClaimUtc = today.AddDays(1).ToString("o"),
                Status = journal == null ? "not_initialized" : "available"
            };
            if (journal == null) { result.Reward = catalog.ForStreakDay(1); return result; }
            var existing = journal.DailyClaims.FirstOrDefault(c => c.UtcDate == result.UtcDate);
            if (existing != null)
            {
                result.Status = existing.Status == "applied" ? "already_claimed" : "pending";
                result.StreakDay = existing.StreakDay;
                result.PreviousStreak = existing.PreviousStreak;
                result.DaysMissed = existing.DaysMissed;
            }
            else if (!string.IsNullOrEmpty(journal.LastDailyClaimUtcDate))
            {
                DateTime last = DateTime.ParseExact(journal.LastDailyClaimUtcDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                int elapsedDays = (today - last.Date).Days;
                if (elapsedDays < 0) throw new InvalidOperationException("Daily claim history is ahead of server time.");
                if (elapsedDays == 0) { result.Status = "already_claimed"; result.StreakDay = journal.DailyStreakDay; }
                else if (elapsedDays == 1) result.StreakDay = Math.Min(7, journal.DailyStreakDay + 1);
                else { result.PreviousStreak = journal.DailyStreakDay; result.DaysMissed = elapsedDays - 1; }
            }
            if (journal.HasPendingOperation) result.Status = "pending";
            result.Reward = catalog.ForStreakDay(result.StreakDay);
            return result;
        }

        public static async Task<DailyRewardResult> ExecuteAsync(ILevelRewardStore store, RewardCatalog catalog, DateTimeOffset now)
        {
            var read = await store.ReadAsync();
            var result = Preview(read?.Journal, catalog, now);
            if (read == null || string.IsNullOrEmpty(read.WriteLock)) { result.Status = "not_initialized"; return result; }
            if (result.Status != "available") return result;

            var receipt = new DailyRewardReceipt
            {
                UtcDate = result.UtcDate, StreakDay = result.StreakDay,
                PreviousStreak = result.PreviousStreak, DaysMissed = result.DaysMissed,
                Grants = result.Reward.Grants()
            };
            // Keep a small recent history; LastDailyClaimUtcDate retains the authoritative streak across pruning.
            string cutoff = now.UtcDateTime.Date.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            read.Journal.DailyClaims.RemoveAll(r => r.Status == "applied" && string.CompareOrdinal(r.UtcDate, cutoff) < 0);
            read.Journal.DailyClaims.Add(receipt);
            result.Status = "pending";
            string reservedLock;
            try { reservedLock = await store.WriteAsync(read.Journal, read.WriteLock); }
            catch (Exception) { return result; }
            if (string.IsNullOrEmpty(reservedLock)) return result;

            try
            {
                foreach (var grant in receipt.Grants)
                    receipt.BalancesAfter[grant.Key] = await store.IncrementAsync(grant.Key, grant.Value);
                receipt.Status = "applied";
                read.Journal.LastDailyClaimUtcDate = result.UtcDate;
                read.Journal.DailyStreakDay = result.StreakDay;
                await store.WriteAsync(read.Journal, reservedLock);
                result.Status = "applied";
                result.Granted = new Dictionary<string, long>(receipt.Grants);
                result.BalancesAfter = new Dictionary<string, long>(receipt.BalancesAfter);
            }
            catch (Exception)
            {
                // No blind retries: some credits may already exist. Durable pending intent blocks new operations
                // until server-side inspection/recovery confirms what happened.
            }
            return result;
        }
    }
}
