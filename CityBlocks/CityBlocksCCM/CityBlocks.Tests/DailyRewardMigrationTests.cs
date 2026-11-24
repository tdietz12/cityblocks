using System.Text.Json;
using CityBlocks.CloudCode;

namespace CityBlocks.Tests
{
    public class DailyRewardMigrationTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-02T12:00:00Z");
        private static RewardCatalog Catalog() => RewardCatalog.Parse("{\"schemaVersion\":1,\"firstWinOnly\":true,\"levels\":[{\"levelNumber\":1,\"rewards\":{}}],\"daily\":[{},{},{},{},{},{},{}]}");
        private sealed class Store : ILevelRewardStore
        {
            public PowerUpJournal Journal = new PowerUpJournal();
            public int Writes;
            public Task<JournalRead?> ReadAsync() => Task.FromResult<JournalRead?>(new JournalRead
            {
                Journal = JsonSerializer.Deserialize<PowerUpJournal>(JsonSerializer.Serialize(Journal))!, WriteLock = "lock"
            });
            public Task<string> WriteAsync(PowerUpJournal journal, string writeLock) { Journal = journal; Writes++; return Task.FromResult("new-lock"); }
            public Task<long> IncrementAsync(string id, long amount) => throw new Exception("Migration must never grant rewards.");
        }
        [Test]
        public async Task TodayImportedWithoutSecondClaim()
        {
            var store = new Store();
            var result = await DailyRewardMigration.ImportAsync(store, Catalog(), 3, "2026-01-02", Now);
            Assert.That(result.Status, Is.EqualTo("already_claimed"));
            Assert.That(result.StreakDay, Is.EqualTo(3));
            Assert.That(result.LegacyStateImported, Is.True);
            Assert.That(result.Granted, Is.Empty);
            Assert.That(result.Schedule.Length, Is.EqualTo(7));
        }
        [Test]
        public async Task YesterdayImportedAndStreakCapped()
        {
            var store = new Store();
            var result = await DailyRewardMigration.ImportAsync(store, Catalog(), 999, "2026-01-01", Now);
            Assert.That(result.Status, Is.EqualTo("available"));
            Assert.That(result.StreakDay, Is.EqualTo(7));
            await DailyRewardMigration.ImportAsync(store, Catalog(), 1, "2026-01-02", Now);
            Assert.That(store.Writes, Is.EqualTo(1));
        }
        [Test]
        public async Task ExistingCloudHistoryNotOverwritten()
        {
            var store = new Store();
            store.Journal.LastDailyClaimUtcDate = "2026-01-01";
            store.Journal.DailyStreakDay = 5;
            await DailyRewardMigration.ImportAsync(store, Catalog(), 1, "2026-01-02", Now);
            Assert.That(store.Journal.LastDailyClaimUtcDate, Is.EqualTo("2026-01-01"));
            Assert.That(store.Journal.DailyStreakDay, Is.EqualTo(5));
        }
        [Test]
        public async Task FutureOrInvalidDateIgnored()
        {
            foreach (var date in new[] { "2030-01-01", "invalid" })
            {
                var store = new Store();
                var result = await DailyRewardMigration.ImportAsync(store, Catalog(), 7, date, Now);
                Assert.That(result.StreakDay, Is.EqualTo(1));
                Assert.That(store.Journal.LastDailyClaimUtcDate, Is.Empty);
            }
        }
        [Test]
        public async Task PendingOperationPreventsMigration()
        {
            var store = new Store();
            store.Journal.Receipts.Add(new PowerUpReceipt { Status = "pending" });
            var result = await DailyRewardMigration.ImportAsync(store, Catalog(), 7, "2026-01-02", Now);
            Assert.That(result.Status, Is.EqualTo("pending"));
            Assert.That(result.LegacyStateImported, Is.False);
            Assert.That(store.Writes, Is.Zero);
        }
    }
}
