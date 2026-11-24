using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "DailyRewardSchedule", menuName = "City Blocks/Daily Reward Schedule")]
    public class DailyRewardSchedule : ScriptableObject
    {
        [Tooltip("Entries 0 through 6 represent streak days 1 through 7. Each day can contain coins, power-ups or both. Export and deploy after editing.")]
        public RewardAmounts[] days = new RewardAmounts[7]
        {
            new RewardAmounts { coins = 100, deleteUses = 1 },
            new RewardAmounts { coins = 150, levelUpUses = 1 },
            new RewardAmounts { coins = 200, lookAheadUses = 1 },
            new RewardAmounts { coins = 250, deleteUses = 1, levelUpUses = 1 },
            new RewardAmounts { coins = 300, lookAheadUses = 2 },
            new RewardAmounts { coins = 400, deleteUses = 2, lookAheadUses = 1 },
            new RewardAmounts { coins = 500, deleteUses = 2, levelUpUses = 2, lookAheadUses = 2 }
        };

        public bool IsValid
        {
            get
            {
                if (days == null || days.Length != 7) return false;
                foreach (var reward in days) if (reward == null || !reward.IsValid) return false;
                return true;
            }
        }

        public RewardAmounts ForDay(int day) => days[Mathf.Clamp(day, 1, 7) - 1];
    }
}
