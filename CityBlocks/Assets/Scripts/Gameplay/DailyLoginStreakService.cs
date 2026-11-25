using System;
using System.IO;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class DailyLoginStreakData
    {
        public long lastClaimedLocalDateTicks;
        public int streakDay;
        public int rewardCredits;
    }

    public static class DailyLoginStreakService
    {
        private const string FileName = "daily-login-streak.json";

        // Day 1..7 rewards as power-up uses (delete, deleteRowUses = look ahead, levelUp). Day 7 is the bonus day.
        private static readonly PowerUpInventory[] DailyRewards =
        {
            new PowerUpInventory { deleteUses = 1 },
            new PowerUpInventory { levelUpUses = 1 },
            new PowerUpInventory { deleteRowUses = 1 },
            new PowerUpInventory { deleteUses = 1, levelUpUses = 1 },
            new PowerUpInventory { deleteRowUses = 2 },
            new PowerUpInventory { deleteUses = 2, deleteRowUses = 1 },
            new PowerUpInventory { deleteUses = 2, levelUpUses = 2, deleteRowUses = 2 },
        };

        // Day 1..7 coin amounts. DISPLAY ONLY for now: there is no coin wallet yet, so coins are shown on the
        // reward track but not granted. Granting moves server-side with the cloud wallet (plan Steps 23 / 32).
        private static readonly int[] DailyCoins = { 100, 150, 200, 250, 300, 400, 500 };

        /// <summary>Coins shown for a streak day (1..7). Not granted yet — see <see cref="DailyCoins"/>.</summary>
        public static int CoinsForDay(int day) => DailyCoins[Mathf.Clamp(day, 1, DailyCoins.Length) - 1];

        public static int CurrentStreakDay
        {
            get
            {
                DailyLoginStreakData data = Load();
                if (data.lastClaimedLocalDateTicks == DateTime.Now.Date.Ticks) return data.streakDay;
                return PreviousClaimWasYesterday(data) ? Mathf.Min(7, data.streakDay + 1) : 1;
            }
        }

        public static bool CanClaim => Load().lastClaimedLocalDateTicks != DateTime.Now.Date.Ticks;

        public const int StreakLength = 7;

        /// <summary>Reads old local claim state for one-time cloud migration; never grants anything.</summary>
        public static void ReadLegacyState(out int streakDay, out string lastClaimUtcDate)
        {
            DailyLoginStreakData data = Load();
            streakDay = Mathf.Clamp(data.streakDay, 0, StreakLength);
            lastClaimUtcDate = "";
            if (data.lastClaimedLocalDateTicks <= 0) return;
            try
            {
                DateTime localDate = new DateTime(data.lastClaimedLocalDateTicks, DateTimeKind.Local);
                lastClaimUtcDate = localDate.ToUniversalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException) { streakDay = 0; }
        }

        /// <summary>Power-up uses grantedfor a streak day (1..7). Returns a copy.</summary>
        public static PowerUpInventory RewardForDay(int day)
        {
            PowerUpInventory source = DailyRewards[Mathf.Clamp(day, 1, DailyRewards.Length) - 1];
            return new PowerUpInventory
            {
                deleteUses = source.deleteUses, deleteRowUses = source.deleteRowUses,
                levelUpUses = source.levelUpUses, extraTurnsUses = source.extraTurnsUses
            };
        }

        public static int TotalUses(PowerUpInventory reward) =>
            reward == null ? 0 : reward.deleteUses + reward.deleteRowUses + reward.levelUpUses + reward.extraTurnsUses;

        /// <summary>Legacy compatibility entry point. Daily rewards require a confirmed server claim.</summary>
        [Obsolete("Use CloudServices.CloudDailyRewards.CheckAndClaimAsync; local daily claims are disabled.")]
        public static bool Claim(out int day, out PowerUpInventory reward)
        {
            day = 0;
            reward = null;
            return false;
        }

        #if UNITY_EDITOR
                // ---------------- Editor-only test helpers (never compiled into builds) ----------------
                private const string EveryMenuVisitIsNewDayKey = "CityBlocks.Debug.DailyReward.EveryMenuVisitIsNewDay";

                /// <summary>When on, every main menu visit in the Editor behaves like a new day (walk days 1→7 quickly).</summary>
                public static bool DebugEveryMenuVisitIsNewDay
                {
                    get => UnityEditor.EditorPrefs.GetBool(EveryMenuVisitIsNewDayKey, false);
                    set => UnityEditor.EditorPrefs.SetBool(EveryMenuVisitIsNewDayKey, value);
                }

                /// <summary>Last claim date, or null if never claimed.</summary>
                public static DateTime? DebugLastClaimDate
                {
                    get
                    {
                        long ticks = Load().lastClaimedLocalDateTicks;
                        return ticks > 0 ? new DateTime(ticks) : (DateTime?)null;
                    }
                }

                /// <summary>Streak day stored with the last claim (0 if never claimed).</summary>
                public static int DebugStoredStreakDay => Load().streakDay;

                /// <summary>Makes today's reward claimable as streak day <paramref name="nextDay"/> (1-7) by recording the previous claim as yesterday.</summary>
                public static void DebugSetNextClaimDay(int nextDay)
                {
                    nextDay = Mathf.Clamp(nextDay, 1, StreakLength);
                    if (nextDay == 1) { DebugReset(); return; }
                    Save(new DailyLoginStreakData { lastClaimedLocalDateTicks = DateTime.Now.Date.AddDays(-1).Ticks, streakDay = nextDay - 1 });
                }

                /// <summary>Records the last claim as 3 days ago (keeping the streak length) so the next claim breaks the streak.</summary>
                public static void DebugBreakStreak()
                {
                    int streak = Mathf.Max(1, Load().streakDay);
                    if (streak < 2) streak = 3;
                    Save(new DailyLoginStreakData { lastClaimedLocalDateTicks = DateTime.Now.Date.AddDays(-3).Ticks, streakDay = streak });
                }

                /// <summary>Deletes the streak file: next claim is Day 1, available today.</summary>
                public static void DebugReset()
                {
                    try
                    {
                        string path = Path.Combine(Application.persistentDataPath, FileName);
                        if (File.Exists(path)) File.Delete(path);
                    }
                    catch (Exception exception) { Debug.LogError("Could not reset daily login streak: " + exception); }
                }

                /// <summary>
                /// If today's reward was already claimed, moves that claim to yesterday (keeping the streak) so the next
                /// claim is the following streak day. Otherwise leaves the data as is (already claimable).
                /// </summary>
                public static void DebugMakeClaimableToday()
                {
                    DailyLoginStreakData data = Load();
                    if (data.lastClaimedLocalDateTicks != DateTime.Now.Date.Ticks) return;
                    data.lastClaimedLocalDateTicks = DateTime.Now.Date.AddDays(-1).Ticks;
                    Save(data);
                }

                /// <summary>The day the next claim would be after today's claim (for "every visit is a new day").</summary>
                public static int DebugNextDayAfterToday => Mathf.Min(StreakLength, Mathf.Max(1, Load().streakDay) + 1);
        #endif

                private static bool PreviousClaimWasYesterday(DailyLoginStreakData data)
        {
            if (data.lastClaimedLocalDateTicks <= 0) return false;
            return DateTime.Now.Date.Ticks - data.lastClaimedLocalDateTicks == TimeSpan.TicksPerDay;
        }

        private static DailyLoginStreakData Load()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, FileName);
                if (!File.Exists(path)) return new DailyLoginStreakData();
                return JsonUtility.FromJson<DailyLoginStreakData>(File.ReadAllText(path)) ?? new DailyLoginStreakData();
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not read daily login streak: " + exception);
                return new DailyLoginStreakData();
            }
        }

        private static void Save(DailyLoginStreakData data)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, FileName);
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not save daily login streak: " + exception);
            }
        }
    }
}