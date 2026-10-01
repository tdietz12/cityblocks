using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    public static class LevelCatalog
    {
        private static List<LevelDefinition> levels;

        public static IReadOnlyList<LevelDefinition> Levels
        {
            get
            {
                if (levels == null) Load();
                return levels;
            }
        }

        public static LevelDefinition Get(int number)
        {
            foreach (LevelDefinition level in Levels)
                if (level.levelNumber == number) return level;
            return null;
        }

        public static LevelDefinition Next(int number)
        {
            foreach (LevelDefinition level in Levels)
                if (level.levelNumber > number) return level;
            return null;
        }

        private static void Load()
        {
            levels = new List<LevelDefinition>();
            HashSet<int> numbers = new HashSet<int>();
            foreach (LevelDefinition level in Resources.LoadAll<LevelDefinition>("Levels"))
            {
                if (!level.IsValid(out string error) || !numbers.Add(level.levelNumber))
                {
                    Debug.LogError("Invalid level definition " + level.name + ": " + (error ?? "duplicate level number"));
                    continue;
                }
                levels.Add(level);
            }
            levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
        }
    }
}
