using System;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class RewardAmounts
    {
        [Min(0)] public int coins;
        [Min(0)] public int deleteUses;
        [Min(0)] public int levelUpUses;
        [Tooltip("Look Ahead uses.")]
        [Min(0)] public int lookAheadUses;
        [Min(0)] public int extraTurnsUses;

        public bool IsValid => coins >= 0 && deleteUses >= 0 && levelUpUses >= 0 && lookAheadUses >= 0 && extraTurnsUses >= 0;

        public PowerUpInventory ToPowerUps() => new PowerUpInventory
        {
            deleteUses = deleteUses,
            levelUpUses = levelUpUses,
            deleteRowUses = lookAheadUses,
            extraTurnsUses = extraTurnsUses
        };
    }
}
