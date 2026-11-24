using System.Text.Json;
using CityBlocks.CloudCode;

namespace CityBlocks.Tests
{
    public class PowerUpUseTests
    {
        private sealed class Store : IPowerUpUseStore
        {
            public long Balance = 3;
            public int Debits;
            public int Writes;
            public bool Initialized = true;
            public bool FailReserve;
            public bool LoseDebitResponse;
            public bool FailCommit;
            public bool LoseCommitResponse;
            private int version = 1;
            public bool SynchronizeBalanceReads;
            private int balanceReads;
            private readonly TaskCompletionSource<bool> balanceGate = new TaskCompletionSource<bool>();
            private PowerUpJournal journal = new PowerUpJournal();
            private static PowerUpJournal Copy(PowerUpJournal j) => JsonSerializer.Deserialize<PowerUpJournal>(JsonSerializer.Serialize(j))!;
            public Task<JournalRead?> ReadAsync() => Task.FromResult<JournalRead?>(Initialized
                ? new JournalRead { Journal = Copy(journal), WriteLock = version.ToString() } : null);
            public async Task<long> ReadBalanceAsync(string currencyId)
            {
                if (SynchronizeBalanceReads)
                {
                    if (++balanceReads == 2) balanceGate.TrySetResult(true);
                    await balanceGate.Task;
                }
                return Balance;
            }
            public Task<string> WriteAsync(PowerUpJournal j, string writeLock)
            {
                Writes++;
                if (FailReserve && Writes == 1 || FailCommit && Writes == 2 || writeLock != version.ToString())
                    throw new InvalidOperationException("write conflict/unavailable");
                journal = Copy(j);
                version++;
                if (LoseCommitResponse && Writes == 2) throw new InvalidOperationException("lost response");
                return Task.FromResult(version.ToString());
            }
            public Task<long> DecrementAsync(string currencyId)
            {
                Debits++;
                Balance--;
                if (LoseDebitResponse) throw new InvalidOperationException("lost response after debit");
                return Task.FromResult(Balance);
            }
        }
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        private static string Id() => PowerUpUse.CreateUseId(Now);

        [Test]
        public async Task SuccessAndReplay_DebitOnlyOnce()
        {
            var store = new Store(); string id = Id();
            var first = await PowerUpUse.ExecuteAsync(store, "delete", id, Now);
            var second = await PowerUpUse.ExecuteAsync(store, "delete", id, Now);
            Assert.That(first.Status, Is.EqualTo("applied"));
            Assert.That(first.BalanceAfter, Is.EqualTo(2));
            Assert.That(second.Status, Is.EqualTo("applied"));
            Assert.That(second.Replay, Is.True);
            Assert.That(store.Debits, Is.EqualTo(1));
        }
        [Test]
        public async Task ConcurrentDuplicates_OnlyOneDebit()
        {
            var store = new Store { SynchronizeBalanceReads = true };
            string id = Id();
            var a = PowerUpUse.ExecuteAsync(store, "delete", id, Now);
            var b = PowerUpUse.ExecuteAsync(store, "delete", id, Now);
            var results = await Task.WhenAll(a, b);
            Assert.That(store.Debits, Is.EqualTo(1));
            Assert.That(results.Count(r => r.Status == "applied"), Is.EqualTo(1));
        }

        [Test]
        public async Task Insufficient_DoesNotDebit()
        {
            var store = new Store { Balance = 0 };
            var result = await PowerUpUse.ExecuteAsync(store, "delete", Id(), Now);
            Assert.That(result.Status, Is.EqualTo("insufficient"));
            Assert.That(store.Debits, Is.Zero);
        }
        [Test]
        public async Task ReservationFailure_DoesNotDebit()
        {
            var store = new Store { FailReserve = true };
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", Id(), Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Debits, Is.Zero);
        }
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task AmbiguousFailure_RetryAndNewUseNeverDebitAgain(bool loseDebit, bool failCommit)
        {
            var store = new Store { LoseDebitResponse = loseDebit, FailCommit = failCommit }; string id = Id();
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", id, Now)).Status, Is.EqualTo("pending"));
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", id, Now)).Status, Is.EqualTo("pending"));
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", Id(), Now)).Status, Is.EqualTo("pending"));
            Assert.That(store.Debits, Is.EqualTo(1));
        }
        [Test]
        public async Task LostCommitResponse_ReplayRecoversAppliedResult()
        {
            var store = new Store { LoseCommitResponse = true }; string id = Id();
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", id, Now)).Status, Is.EqualTo("pending"));
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", id, Now)).Status, Is.EqualTo("applied"));
            Assert.That(store.Debits, Is.EqualTo(1));
        }
        [Test]
        public async Task WrongTypeForSameId_IsRejected()
        {
            var store = new Store(); string id = Id();
            await PowerUpUse.ExecuteAsync(store, "delete", id, Now);
            Assert.That((await PowerUpUse.ExecuteAsync(store, "levelUp", id, Now)).Status, Is.EqualTo("invalid"));
            Assert.That(store.Debits, Is.EqualTo(1));
        }
        [Test]
        public async Task InvalidOrExpiredIds_AreRejected()
        {
            var store = new Store();
            Assert.That((await PowerUpUse.ExecuteAsync(store, "unknown", Id(), Now)).Status, Is.EqualTo("invalid"));
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", "bad", Now)).Status, Is.EqualTo("invalid"));
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", PowerUpUse.CreateUseId(Now.AddDays(-3)), Now)).Status, Is.EqualTo("invalid"));
            Assert.That(store.Debits, Is.Zero);
        }
        [Test]
        public async Task BootstrapRequired_NoWritesOrDebits()
        {
            var store = new Store { Initialized = false };
            Assert.That((await PowerUpUse.ExecuteAsync(store, "delete", Id(), Now)).Status, Is.EqualTo("not_initialized"));
            Assert.That(store.Debits, Is.Zero);
            Assert.That(store.Writes, Is.Zero);
        }
    }
}
