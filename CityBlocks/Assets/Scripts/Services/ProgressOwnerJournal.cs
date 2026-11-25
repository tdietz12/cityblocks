using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudServices
{
    /// <summary>Progress snapshots, not reward operations. Old owners retain their own pending checkpoint.</summary>
    [Serializable]
    public sealed class ProgressOwnerJournal
    {
        [Serializable]
        public sealed class Entry
        {
            public string playerId;
            public CloudProgressData progress;
            public bool pending;
        }

        public int schemaVersion = 1;
        public string activeOwner;
        public List<Entry> owners = new List<Entry>();

        public static ProgressOwnerJournal Restore(string json, string legacyOwner, CloudProgressData device)
        {
            ProgressOwnerJournal journal;
            if (string.IsNullOrEmpty(json))
            {
                journal = new ProgressOwnerJournal { activeOwner = legacyOwner };
                if (!string.IsNullOrEmpty(legacyOwner)) journal.Record(legacyOwner, device, true);
            }
            else
            {
                journal = JsonUtility.FromJson<ProgressOwnerJournal>(json);
                if (journal == null || journal.schemaVersion != 1 || journal.owners == null)
                    throw new InvalidOperationException("Invalid progress owner journal; local progress retained.");
                var ids = new HashSet<string>();
                foreach (var entry in journal.owners)
                    if (entry == null || string.IsNullOrEmpty(entry.playerId) || entry.progress == null || !ids.Add(entry.playerId))
                        throw new InvalidOperationException("Invalid progress owner entry; migration refused.");
                if (!string.IsNullOrEmpty(journal.activeOwner) && journal.Get(journal.activeOwner) == null)
                    throw new InvalidOperationException("Missing active progress owner; migration refused.");
            }
            return journal;
        }

        public Entry Get(string playerId) => owners.Find(entry => entry.playerId == playerId);

        public void Record(string playerId, CloudProgressData progress, bool pending)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            var entry = Get(playerId);
            if (entry == null)
            {
                entry = new Entry { playerId = playerId };
                owners.Add(entry);
            }
            entry.progress = ProgressMerge.Merge(entry.progress, progress);
            entry.pending = pending;
        }

        /// <summary>Only call for a successful authenticated identity. Device progress intentionally follows the player.</summary>
        public CloudProgressData Bind(string playerId, CloudProgressData device)
        {
            if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("A signed-in owner is required.");
            if (!string.IsNullOrEmpty(activeOwner) && activeOwner != playerId)
            {
                var old = Get(activeOwner);
                Record(activeOwner, device, old == null || old.pending ||
                    JsonUtility.ToJson(ProgressMerge.Merge(old.progress, device)) != JsonUtility.ToJson(old.progress));
            }
            activeOwner = playerId;
            var target = Get(playerId);
            CloudProgressData candidate = ProgressMerge.Merge(device, target == null ? null : target.progress);
            Record(playerId, candidate, true);
            return candidate;
        }
    }
}
