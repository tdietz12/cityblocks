using System.Text.Json;
using CityBlocks.CloudCode;

namespace CityBlocks.Tests
{
    public class LevelRewardClaimTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        private static RewardCatalog Catalog(string rewards = "{\"coins\":20,\"deleteUses\":1}") => RewardCatalog.Parse(
            "{\"schemaVersion\":1,\"firstWinOnly\":true,\"levels\":[{\"levelNumber\":1,\"rewards\":" + rewards +
            "},{\"levelNumber\":2,\"rewards\":" + rewards + "}],\"daily\":[{},{},{},{},{},{},{}]}");

        private sealed class Store : ILevelRewardStore, IPowerUpUseStore
        {
            public PowerUpJournal Journal = new PowerUpJournal { WelcomeGrantedAt = "original-welcome-date" };
            public Dictionary<string, long> Balances = new Dictionary<string, long>();
            public int Credits;
            public int Debits;
            public int Writes;
            public bool Initialized = true;
            public bool FailReserve;
            public bool FailCommit;
            public bool LoseCreditResponse;
            public bool LoseCommitResponse;
            public bool ConcurrentReads;
            private int reads;
            private int version = 1;
            private readonly TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>();

            public async Task<JournalRead?> ReadAsync()
            {
                if (!Initialized) return null;
                var read = new JournalRead { Journal = JsonSerializer.Deserialize<PowerUpJournal>(JsonSerializer.Serialize(Journal))!, WriteLock = version.ToString() };
                if (ConcurrentReads && reads < 2)
                {
                    if (++reads == 2) gate.TrySetResult(true);
                    await gate.Task;
                }
                return read;
            }
            public Task<string> WriteAsync(PowerUpJournal journal, string writeLock)
            {
                Writes++;
                if (FailReserve && Writes == 1 || FailCommit && Writes == 2 || writeLock != version.ToString())
                    throw new InvalidOperationException("conflict or unavailable");
                Journal = JsonSerializer.Deserialize<PowerUpJournal>(JsonSerializer.Serialize(journal))!;
                version++;
                if (LoseCommitResponse && Writes == 2) throw new InvalidOperationException("lost commit response");
                return Task.FromResult(version.ToString());
            }
            public Task<long> IncrementAsync(string currencyId, long amount)
            {
                Credits++;
                Balances.TryGetValue(currencyId, out var old);
                Balances[currencyId] = old + amount;
                if (LoseCreditResponse) throw new InvalidOperationException("lost credit response");
                return Task.FromResult(old + amount);
            }
            public Task<long> ReadBalanceAsync(string currencyId) => Task.FromResult(Balances.TryGetValue(currencyId, out long value) ? value : 0);
            public Task<long> DecrementAsync(string currencyId)
            {
                Debits++;
                return Task.FromResult(--Balances[currencyId]);
            }
        }

        [Test]
        public async Task FirstWinPaysConfiguredBundle_ReplayDoesNotPay()
        {
            var store = new Store();
            var first = await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now);
            var replay = await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now);
            Assert.That(first.Status, Is.EqualTo("applied"));
            Assert.That(first.Granted["COINS"], Is.EqualTo(20));
            Assert.That(first.Granted["PU_DELETE"], Is.EqualTo(1));
            Assert.That(replay.Status, Is.EqualTo("already_claimed"));
            Assert.That(replay.Granted, Is.Empty);
            Assert.That(store.Credits, Is.EqualTo(2));
            Assert.That(store.Journal.WelcomeGrantedAt, Is.EqualTo("original-welcome-date"));
        }

        [Test]
        public async Task DifferentLevelsCanEachPayOnce()
        {
            var store = new Store();
            await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now);
            await LevelRewardClaim.ExecuteAsync(store, Catalog(), 2, Now);
            Assert.That(store.Balances["COINS"], Is.EqualTo(40));
            Assert.That(store.Journal.LevelClaims.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ConcurrentSameLevel_OnlyOneBundlePays()
        {
            var store = new Store { ConcurrentReads = true };
            var a = LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now);
            var b = LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now);
            var results = await Task.WhenAll(a, b);
            Assert.That(results.Count(r => r.Status == "applied"), Is.EqualTo(1));
            Assert.That(store.Credits, Is.EqualTo(2));
        }

        [Test]
        public async Task ReservationFailure_NeverCredits()
        {
            var store = new Store { FailReserve = true };
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Credits, Is.Zero);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task UncertainGrantBlocksRetriesAndOtherLevels(bool loseCreditResponse, bool failCommit)
        {
            var store = new Store { LoseCreditResponse = loseCreditResponse, FailCommit = failCommit };
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("pending"));
            int credits = store.Credits;
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("pending"));
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 2, Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Credits, Is.EqualTo(credits));
        }

        [Test]
        public async Task LostCommitResponse_RecordedClaimRecoversWithoutMoreCredits()
        {
            var store = new Store { LoseCommitResponse = true };
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("pending"));
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("already_claimed"));
            Assert.That(store.Credits, Is.EqualTo(2));
        }

        [Test]
        public async Task PendingDebitBlocksReward_AndPendingRewardBlocksDebit()
        {
            var store = new Store();
            store.Journal.Receipts.Add(new PowerUpReceipt { UseId = PowerUpUse.CreateUseId(Now), PowerUp = "delete", Status = "pending" });
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Credits, Is.Zero);
            store.Journal.Receipts.Clear();
            store.Journal.LevelClaims.Add(new LevelRewardReceipt { LevelNumber = 1, Status = "pending" });
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", PowerUpUse.CreateUseId(Now), Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Debits, Is.Zero);
        }

        [Test]
        public async Task ZeroRewardStillMarksFirstWinClaimed()
        {
            var store = new Store();
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog("{}"), 1, Now)).Status, Is.EqualTo("applied"));
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("already_claimed"));
            Assert.That(store.Credits, Is.Zero);
        }

        [Test]
        public async Task InvalidLevelAndUninitializedPlayerDoNotWrite()
        {
            var store = new Store();
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 99, Now)).Status, Is.EqualTo("invalid"));
            store.Initialized = false;
            Assert.That((await LevelRewardClaim.ExecuteAsync(store, Catalog(), 1, Now)).Status, Is.EqualTo("not_initialized"));
            Assert.That(store.Writes, Is.Zero);
        }
    }
}
