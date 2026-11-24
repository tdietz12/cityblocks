using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CityBlocks.CloudCode
{
    public interface ILevelRewardStore
    {
        Task<JournalRead?> ReadAsync();
        Task<string> WriteAsync(PowerUpJournal journal, string writeLock);
        Task<long> IncrementAsync(string currencyId, long amount);
    }

    public static class LevelRewardClaim
    {
        public static async Task<LevelRewardResult> ExecuteAsync(ILevelRewardStore store, RewardCatalog catalog,
            int levelNumber, DateTimeOffset now)
        {
            var result = new LevelRewardResult { LevelNumber = levelNumber, ServerTimeUtc = now.UtcDateTime.ToString("o") };
            RewardBundle bundle;
            try { bundle = catalog.ForLevel(levelNumber); }
            catch (ArgumentOutOfRangeException) { result.Status = "invalid"; return result; }

            var read = await store.ReadAsync();
            if (read == null || string.IsNullOrEmpty(read.WriteLock)) { result.Status = "not_initialized"; return result; }
            var existing = read.Journal.LevelClaims.FirstOrDefault(r => r.LevelNumber == levelNumber);
            if (existing != null)
            {
                result.Status = existing.Status == "applied" ? "already_claimed" : "pending";
                // No newly Granted amounts on a replay. Historical balances are not a current wallet snapshot.
                return result;
            }
            if (read.Journal.HasPendingOperation) return result;
            // Claim history is not pruned; clearing it would allow old levels to be claimed again.
            if (read.Journal.LevelClaims.Count >= 4096) return result;

            var receipt = new LevelRewardReceipt { LevelNumber = levelNumber, ClaimedAtUtc = result.ServerTimeUtc,
                Grants = bundle.Grants() };
            read.Journal.LevelClaims.Add(receipt);
            string reservedLock;
            try { reservedLock = await store.WriteAsync(read.Journal, read.WriteLock); }
            catch (Exception) { return result; } // A concurrent claimant wins, or the reservation response was lost.
            if (string.IsNullOrEmpty(reservedLock)) return result;

            try
            {
                foreach (var grant in receipt.Grants)
                    receipt.BalancesAfter[grant.Key] = await store.IncrementAsync(grant.Key, grant.Value);
                receipt.Status = "applied";
                await store.WriteAsync(read.Journal, reservedLock);
                result.Status = "applied";
                result.Granted = new Dictionary<string, long>(receipt.Grants);
                result.BalancesAfter = new Dictionary<string, long>(receipt.BalancesAfter);
            }
            catch (Exception)
            {
                // Multi-currency grants are not one transaction. A failure may follow a successful credit.
                // Keep the pending receipt; neither this level nor any other operation retries credits blindly.
                // Recovery must inspect Economy/server records, not accept client-provided grant amounts.
            }
            return result;
        }
    }
}
