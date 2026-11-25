using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Explicit Cloud Save transport for progress. Does not modify local saves, award rewards or
    /// subscribe to lifecycle events; dirty tracking and automatic synchronization follow separately.
    /// </summary>
    public static class CloudProgressService
    {
        private const string ProgressKey = "progress";
        private const int MaxSaveAttempts = 3;
        private static readonly SemaphoreSlim SyncGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Synchronizes a snapshot already bound to playerId and returns merged permanent progress.
        /// Call only after sign-in. Failure throws: callers must retain their dirty local progress.
        /// Callers must also verify the owner before applying the returned snapshot locally.
        /// </summary>
        public static async Task<CloudProgressData> SynchronizeAsync(string playerId, CloudProgressData local)
        {
            if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("A progress owner is required.", nameof(playerId));
            if (local == null) throw new ArgumentNullException(nameof(local));
            // Freeze at entry, not after another queued synchronization finishes.
            CloudProgressData snapshot = ProgressMerge.Merge(local, null);
            long sessionRevision = AccountService.SessionRevision;
            Func<bool> isOwner = () => CloudBootstrap.IsSignedIn && CloudBootstrap.PlayerId == playerId &&
                AccountService.SessionRevision == sessionRevision;
            EnsureOwner(isOwner);
            if (!ConnectivityService.IsOnline) throw new InvalidOperationException("Progress remains local while offline.");
            await SyncGate.WaitAsync();
            try
            {
                EnsureOwner(isOwner);
                var result = await SynchronizeCoreAsync(snapshot, ReadAsync, TrySaveAsync, isOwner);
                ConnectivityService.ReportCloudResult(true, "progress");
                return result;
            }
            catch (Unity.Services.CloudSave.CloudSaveException)
            {
                // An exhausted CAS conflict is not evidence of a lost connection.
                // The SDK's conflict subtype is caught in TrySaveAsync and retried by the core.
                if (isOwner()) ConnectivityService.ReportCloudResult(false, "progress");
                throw;
            }
            finally { SyncGate.Release(); }
        }

        private static async Task<(CloudProgressData data, string writeLock)> ReadAsync()
        {
            var items = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { ProgressKey },
                new Unity.Services.CloudSave.Models.Data.Player.LoadOptions(
                    new Unity.Services.CloudSave.Models.Data.Player.DefaultReadAccessClassOptions()));
            if (!items.TryGetValue(ProgressKey, out var item)) return (null, null);
            var data = item.Value.GetAs<CloudProgressData>();
            // Corrupt data is not equivalent to a missing key: never overwrite it with blank progress.
            if (data == null || string.IsNullOrEmpty(item.WriteLock))
                throw new InvalidOperationException("Cloud progress or its write lock is invalid; synchronization refused.");
            return (data, item.WriteLock);
        }

        private static async Task<bool> TrySaveAsync(CloudProgressData progress, string writeLock)
        {
            try
            {
                // SDK requires null for creation. Existing keys always use the lock returned by Load.
                // Concurrent first-ever creation cannot be CAS-protected by this client API.
                await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, Unity.Services.CloudSave.Models.SaveItem>
                    {
                        { ProgressKey, new Unity.Services.CloudSave.Models.SaveItem(progress, writeLock) }
                    }, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(
                        new Unity.Services.CloudSave.Models.Data.Player.DefaultWriteAccessClassOptions()));
                return true;
            }
            catch (Unity.Services.CloudSave.CloudSaveConflictException) { return false; }
        }

        // Delegate boundary allows deterministic transport tests without auth, network or real player mutations.
        private static async Task<CloudProgressData> SynchronizeCoreAsync(CloudProgressData snapshot,
            Func<Task<(CloudProgressData data, string writeLock)>> read,
            Func<CloudProgressData, string, Task<bool>> trySave, Func<bool> isOwner)
        {
            CloudProgressData pending = ProgressMerge.Merge(snapshot, null);
            for (int attempt = 0; attempt < MaxSaveAttempts; attempt++)
            {
                EnsureOwner(isOwner);
                var remote = await read();
                EnsureOwner(isOwner);
                pending = ProgressMerge.Merge(pending, remote.data);
                if (remote.data != null && JsonUtility.ToJson(pending) == JsonUtility.ToJson(remote.data))
                    return pending;
                bool saved = await trySave(pending, remote.writeLock);
                EnsureOwner(isOwner);
                if (saved) return pending;
                // Conflict: read the latest lock/data and merge again, never retry an old value blindly.
            }
            throw new InvalidOperationException("Cloud progress changed repeatedly; retain local changes and retry later.");
        }

        private static void EnsureOwner(Func<bool> isOwner)
        {
            if (!isOwner()) throw new OperationCanceledException("Progress account changed; stale synchronization canceled.");
        }
    }
}
