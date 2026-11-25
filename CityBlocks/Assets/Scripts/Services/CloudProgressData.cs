using System;
using System.Collections.Generic;

namespace CloudServices
{
    /// <summary>
    /// Progress-only payload for the Cloud Save progress key. Economy balances, entitlements,
    /// reward/purchase receipts and active boards remain outside this snapshot.
    /// </summary>
    [Serializable]
    public sealed class CloudProgressData
    {
        public int highestUnlockedLevel = 1;
        public List<int> completedLevels = new List<int>();
        public int endlessRecordScore;
        public int endlessRecordLevel;
        public bool levelTutorialComplete;
    }
}
