using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CityBlocks.CloudCode
{
    public class PowerUpUseResult
    {
        // applied, insufficient, pending, invalid, not_initialized
        public string Status { get; set; } = "pending";
        public string UseId { get; set; } = "";
        public string PowerUp { get; set; } = "";
        public long BalanceAfter { get; set; }
        public bool Replay { get; set; }
    }

    public class PowerUpReceipt
    {
        public string UseId { get; set; } = "";
        public string PowerUp { get; set; } = "";
        public long IssuedAt { get; set; }
        public string Status { get; set; } = "pending";
        public long BalanceAfter { get; set; }
    }

    public class PowerUpJournal
    {
        // Preserves the original welcome marker: Bootstrap still tests only whether its key exists.
        public string WelcomeGrantedAt { get; set; } = "";
        public List<PowerUpReceipt> Receipts { get; set; } = new List<PowerUpReceipt>();
        public List<LevelRewardReceipt> LevelClaims { get; set; } = new List<LevelRewardReceipt>();
        public List<DailyRewardReceipt> DailyClaims { get; set; } = new List<DailyRewardReceipt>();
        public string LastDailyClaimUtcDate { get; set; } = "";
        public int DailyStreakDay { get; set; }

        public bool HasPendingOperation => Receipts.Any(r => r.Status == "pending") ||
            LevelClaims.Any(r => r.Status == "pending") || DailyClaims.Any(r => r.Status == "pending");
    }

    public class JournalRead
    {
        public PowerUpJournal Journal { get; set; } = new PowerUpJournal();
        public string WriteLock { get; set; } = "";
    }

    public interface IPowerUpUseStore
    {
        Task<JournalRead?> ReadAsync();
        // Must compare the supplied Cloud Save write lock; never force-overwrite.
        Task<string> WriteAsync(PowerUpJournal journal, string writeLock);
        Task<long> ReadBalanceAsync(string currencyId);
        Task<long> DecrementAsync(string currencyId);
    }

    public static class PowerUpUse
    {
        // IDs encode their creation time so expired IDs cannot be replayed after receipts are pruned.
        // Format: unixMilliseconds:GUID-N. Step 26 must persist and reuse the SAME ID when retrying.
        public static string CreateUseId(DateTimeOffset now) =>
            now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N");

        public static bool TryValidate(string powerUp, string useId, DateTimeOffset now, out long issuedAt)
        {
            issuedAt = 0;
            if (!EconomyIds.PowerUpCurrencies.ContainsKey(powerUp ?? "") || string.IsNullOrEmpty(useId)) return false;
            string[] parts = useId.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out issuedAt) ||
                !Guid.TryParseExact(parts[1], "N", out _)) return false;
            long age = now.ToUnixTimeMilliseconds() - issuedAt;
            return age >= -(long)TimeSpan.FromMinutes(5).TotalMilliseconds && age <= (long)TimeSpan.FromDays(1).TotalMilliseconds;
        }

        public static async Task<PowerUpUseResult> ExecuteAsync(IPowerUpUseStore store, string powerUp, string useId, DateTimeOffset now)
        {
            powerUp ??= "";
            useId ??= "";
            var result = new PowerUpUseResult { UseId = useId, PowerUp = powerUp };
            if (!TryValidate(powerUp, useId, now, out long issuedAt)){ result.Status = "invalid"; return result; }
            JournalRead? read = await store.ReadAsync();
            if (read == null || string.IsNullOrEmpty(read.WriteLock)) { result.Status = "not_initialized"; return result; }

            PowerUpReceipt? existing = read.Journal.Receipts.FirstOrDefault(r => r.UseId == useId);
            if (existing != null)
            {
                if (existing.PowerUp != powerUp) { result.Status = "invalid"; return result; }
                result.Status = existing.Status;
                result.BalanceAfter = existing.BalanceAfter;
                result.Replay = true;
                return result;
            }
            // Any unresolved Economy operation blocks new uses: no guessed success or concurrent balance mutation.
            if (read.Journal.HasPendingOperation) return result;

            string currency = EconomyIds.PowerUpCurrencies[powerUp];
            long balance = await store.ReadBalanceAsync(currency);
            var receipt = new PowerUpReceipt { UseId = useId, PowerUp = powerUp, IssuedAt = issuedAt,
                Status = balance > 0 ? "pending" : "insufficient", BalanceAfter = balance };
            long cutoff = now.ToUnixTimeMilliseconds() - (long)TimeSpan.FromDays(2).TotalMilliseconds;
            read.Journal.Receipts.RemoveAll(r => r.Status != "pending" && r.IssuedAt < cutoff);
            // Fail closed before exceeding the Cloud Save document size; never evict still-valid IDs.
            if (read.Journal.Receipts.Count >= 4096) return result;
            read.Journal.Receipts.Add(receipt);

            string reservedLock;
            try { reservedLock = await store.WriteAsync(read.Journal, read.WriteLock); }
            catch (Exception) { return result; } // Conflict or lost reservation response: do NOT debit.
            if (string.IsNullOrEmpty(reservedLock)) return result;

            if (receipt.Status == "insufficient"){ result.Status = "insufficient"; result.BalanceAfter = balance; return result; }
            try
            {
                receipt.BalanceAfter = await store.DecrementAsync(currency);
                receipt.Status = "applied";
                await store.WriteAsync(read.Journal, reservedLock);
                result.Status = "applied";
                result.BalanceAfter = receipt.BalanceAfter;
            }
            catch (Exception)
            {
                // The Economy request or receipt commit might have succeeded. Leave the durable pending intent;
                // retry reads it, never decrements again. Support recovery must inspect the server records.
            }
            return result;
        }
    }
}
