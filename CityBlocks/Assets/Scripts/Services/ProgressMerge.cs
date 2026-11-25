using System.Collections.Generic;

namespace CloudServices
{
    /// <summary>Pure merge rules. Does not read/write saves or call cloud services.</summary>
    public static class ProgressMerge
    {
        /// <summary>Copies only supported permanent progress from the existing local models.</summary>
        public static CloudProgressData Capture(Gameplay.LevelProgressData levels,
            Data_Persistence.ProgressionData endless)
        {
            var snapshot = new CloudProgressData
            {
                highestUnlockedLevel = levels == null ? 1 : levels.highestUnlockedLevel,
                completedLevels = levels == null ? null : levels.completedLevels,
                levelTutorialComplete = levels != null && levels.levelTutorialComplete,
                endlessRecordScore = endless == null ? 0 : endless.recordScore,
                endlessRecordLevel = endless == null ? 0 : endless.recordLevel
            };
            // Normalize and copy the list; never retain a reference into mutable gameplay data.
            return Merge(snapshot, null);
        }

        /// <summary>
        /// Max unlocked/independent records, union completed levels, OR tutorial completion.
        /// Null/legacy lists and invalid negative values are normalized without changing inputs.
        /// </summary>
        public static CloudProgressData Merge(CloudProgressData local, CloudProgressData cloud)
        {
            var completed = new SortedSet<int>();
            if (local != null && local.completedLevels != null)
                foreach (int level in local.completedLevels)
                    if (level > 0) completed.Add(level);
            if (cloud != null && cloud.completedLevels != null)
                foreach (int level in cloud.completedLevels)
                    if (level > 0) completed.Add(level);

            return new CloudProgressData
            {
                highestUnlockedLevel = System.Math.Max(1, System.Math.Max(
                    local == null ? 1 : local.highestUnlockedLevel,
                    cloud == null ? 1 : cloud.highestUnlockedLevel)),
                completedLevels = new List<int>(completed),
                endlessRecordScore = System.Math.Max(0, System.Math.Max(
                    local == null ? 0 : local.endlessRecordScore,
                    cloud == null ? 0 : cloud.endlessRecordScore)),
                endlessRecordLevel = System.Math.Max(0, System.Math.Max(
                    local == null ? 0 : local.endlessRecordLevel,
                    cloud == null ? 0 : cloud.endlessRecordLevel)),
                levelTutorialComplete = (local != null && local.levelTutorialComplete) ||
                    (cloud != null && cloud.levelTutorialComplete)
            };
        }
    }
}
