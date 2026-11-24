using System.Text.Json;
using CityBlocks.CloudCode;

namespace CityBlocks.Tests
{
    public class DailyRewardClaimTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        private static RewardCatalog Catalog(string reward = "{\"coins\":100,\"deleteUses\":1}") => RewardCatalog.Parse(
            "{\"schemaVersion\":1,\"firstWinOnly\":true,\"levels\":[{\"levelNumber\":1,\"rewards\":{}}],\"daily\":[" + string.Join(",", Enumerable.Repeat(reward, 7)) + "]}");

        private sealed class Store : ILevelRewardStore
        {
            public PowerUpJournal Journal = new PowerUpJournal { WelcomeGrantedAt = "original" };
            public int Credits;
            public int Writes;
            public bool Initialized = true;
            public bool FailReserve;
            public bool LoseCreditResponse;
            public bool LoseCommitResponse;
            public bool FailCommit;
            public bool ConcurrentReads;
            private int version = 1;
            private int reads;
            private readonly TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>();
            private static PowerUpJournal Copy(PowerUpJournal journal) => JsonSerializer.Deserialize<PowerUpJournal>(JsonSerializer.Serialize(journal))!;
            public async Task<JournalRead?> ReadAsync()
            {
                if (!Initialized) return null;
                var read = new JournalRead { Journal = Copy(Journal), WriteLock = version.ToString() };
                if (ConcurrentReads && reads < 2) { if (++reads == 2) gate.TrySetResult(true); await gate.Task; }
                return read;
            }
            public Task<string> WriteAsync(PowerUpJournal journal, string writeLock)
            {
                Writes++;
                if (FailReserve && Writes == 1 || FailCommit && Writes == 2 || version.ToString() != writeLock)
                    throw new InvalidOperationException("conflict/unavailable");
                Journal = Copy(journal); version++;
                if (LoseCommitResponse && Writes == 2) throw new InvalidOperationException("lost response");
                return Task.FromResult(version.ToString());
            }
            public Task<long> IncrementAsync(string currencyId, long amount)
            {
                Credits++;
                if (LoseCreditResponse) throw new InvalidOperationException("response lost after credit");
                return Task.FromResult(amount);
            }
        }

        [Test]
        public void PreviewNeverMutatesJournal()
        {
            var store = new Store();
            var status = DailyRewardClaim.Preview(store.Journal, Catalog(), Now);
            Assert.That(status.Status, Is.EqualTo("available"));
            Assert.That(status.StreakDay, Is.EqualTo(1));
            Assert.That(store.Journal.DailyClaims, Is.Empty);
            Assert.That(store.Credits, Is.Zero);
        }

        [Test]
        public async Task MixedRewardOncePerDay()
        {
            var store = new Store();
            var first = await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now);
            var replay = await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now.AddHours(1));
            Assert.That(first.Status, Is.EqualTo("applied"));
            Assert.That(first.Granted["COINS"], Is.EqualTo(100));
            Assert.That(first.Granted["PU_DELETE"], Is.EqualTo(1));
            Assert.That(replay.Status, Is.EqualTo("already_claimed"));
            Assert.That(replay.Granted, Is.Empty);
            Assert.That(store.Credits, Is.EqualTo(2));
            Assert.That(store.Journal.WelcomeGrantedAt, Is.EqualTo("original"));
        }

        [Test]
        public async Task ConsecutiveDaysAdvanceAndStayAtSeven()
        {
            var store = new Store();
            for (int day = 0; day < 9; day++)
            {
                var result = await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now.AddDays(day));
                Assert.That(result.Status, Is.EqualTo("applied"));
                Assert.That(result.StreakDay, Is.EqualTo(Math.Min(7, day + 1)));
            }
            Assert.That(store.Journal.DailyClaims.Count, Is.LessThanOrEqualTo(8));
        }

        [Test]
        public async Task MissedDaysResetStreakAndReportBreak()
        {
            var store = new Store();
            await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now);
            await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now.AddDays(1));
            var result = await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now.AddDays(4));
            Assert.That(result.StreakDay, Is.EqualTo(1));
            Assert.That(result.PreviousStreak, Is.EqualTo(2));
            Assert.That(result.DaysMissed, Is.EqualTo(2));
        }

        [Test]
        public void DayBoundaryUsesUtcNotSuppliedOffset()
        {
            var result = DailyRewardClaim.Preview(new PowerUpJournal(), Catalog(), DateTimeOffset.Parse("2026-01-02T01:00:00+03:00"));
            Assert.That(result.UtcDate, Is.EqualTo("2026-01-01"));
            Assert.That(result.NextClaimUtc, Does.StartWith("2026-01-02T00:00:00"));
        }

        [Test]
        public async Task ConcurrentClaimOnlyOnePays()
        {
            var store = new Store { ConcurrentReads = true };
            var a = DailyRewardClaim.ExecuteAsync(store, Catalog(), Now);
            var b = DailyRewardClaim.ExecuteAsync(store, Catalog(), Now);
            var results = await Task.WhenAll(a, b);
            Assert.That(results.Count(r => r.Status == "applied"), Is.EqualTo(1));
            Assert.That(store.Credits, Is.EqualTo(2));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task UncertainGrantNeverRepeatsCredits(bool lostCredit, bool failedCommit)
        {
            var store = new Store { LoseCreditResponse = lostCredit, FailCommit = failedCommit };
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("pending"));
            int credits = store.Credits;
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("pending"));
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now.AddDays(1))).Status, Is.EqualTo("pending"));
            Assert.That(store.Credits, Is.EqualTo(credits));
            Assert.That(store.Journal.LastDailyClaimUtcDate, Is.Empty);
        }

        [Test]
        public async Task LostCommitResponseReturnsAlreadyClaimedOnRetry()
        {
            var store = new Store { LoseCommitResponse = true };
            await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now);
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("already_claimed"));
            Assert.That(store.Credits, Is.EqualTo(2));
        }

        [Test]
        public async Task PendingLevelOrDebitBlocksDailyClaim()
        {
            var store = new Store();
            store.Journal.LevelClaims.Add(new LevelRewardReceipt { Status = "pending" });
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("pending"));
            store.Journal.LevelClaims.Clear();
            store.Journal.Receipts.Add(new PowerUpReceipt { Status = "pending" });
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Credits, Is.Zero);
        }

        [Test]
        public async Task ReservationFailureAndUninitializedPlayerDoNotPay()
        {
            var store = new Store { FailReserve = true };
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Credits, Is.Zero);
            store.Initialized = false;
            Assert.That((await DailyRewardClaim.ExecuteAsync(store, Catalog(), Now)).Status, Is.EqualTo("not_initialized"));
        }

        [Test]
        public async Task CoinsOnlyPowerUpsOnlyAndZeroRewardsWork()
        {
            foreach (string rewards in new[] { "{\"coins\":100}", "{\"lookAheadUses\":2}", "{}" })
            {
                var result = await DailyRewardClaim.ExecuteAsync(new Store(), Catalog(rewards), Now);
                Assert.That(result.Status, Is.EqualTo("applied"));
            }
        }
    }
}
