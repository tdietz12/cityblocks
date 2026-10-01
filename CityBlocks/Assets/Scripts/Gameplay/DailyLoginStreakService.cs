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
        private static readonly int[] DailyCredits = { 100, 150, 200, 250, 300, 400, 500 };

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

        public static int RewardForDay(int day) => DailyCredits[Mathf.Clamp(day, 1, DailyCredits.Length) - 1];

        public static bool Claim(out int day, out int credits)
        {
            DailyLoginStreakData data = Load();
            long today = DateTime.Now.Date.Ticks;
            day = data.lastClaimedLocalDateTicks == today ? data.streakDay :
                PreviousClaimWasYesterday(data) ? Mathf.Min(7, data.streakDay + 1) : 1;
            credits = 0;
            if (data.lastClaimedLocalDateTicks == today) return false;

            credits = RewardForDay(day);
            data.streakDay = day;
            data.lastClaimedLocalDateTicks = today;
            data.rewardCredits += credits;
            Save(data);
            LevelAnalytics.Record("daily_reward_claimed", "streak_day", day);
            return true;
        }

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