using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class LevelRunData
    {
        public int levelNumber;
        public int attemptNumber;
        public int score;
        public int highestTowerLevel;
        public int movesRemaining;
        public int queueIndex;
        public int firstInQueue;
        public int nextInQueue;
        public int thirdInQueue;
        public int previewTurnsRemaining;
        public bool previewUsed;
        public List<Vector3> board = new List<Vector3>();
        public bool obstacleStateInitialized;
        public List<ObstacleSaveData> obstacles = new List<ObstacleSaveData>();
    }

    [Serializable]
    public class ObstacleSaveData
    {
        public BoardObstacleType type;
        public int column;
        public int row;
        public int durability;
    }

    [Serializable]
    public class LevelProgressData
    {
        public int highestUnlockedLevel = 1;
        public List<int> completedLevels = new List<int>();
        public LevelRunData activeRun;
        public bool firstLevelTutorialComplete;
        public PowerUpInventory powerUps = new PowerUpInventory();
        public bool adsRemoved;
        public bool hasMadePurchase;
        public List<LevelAttemptCounter> levelAttempts = new List<LevelAttemptCounter>();
        public List<string> processedPurchaseIds = new List<string>();

        public int BeginAttempt(int levelNumber)
        {
            if (levelAttempts == null) levelAttempts = new List<LevelAttemptCounter>();
            LevelAttemptCounter counter = levelAttempts.Find(item => item.levelNumber == levelNumber);
            if (counter == null)
            {
                counter = new LevelAttemptCounter { levelNumber = levelNumber };
                levelAttempts.Add(counter);
            }
            return ++counter.attemptNumber;
        }

        public bool IsUnlocked(int levelNumber)
        {
            return levelNumber > 0 && levelNumber <= highestUnlockedLevel;
        }

        public void Complete(int levelNumber, int nextLevelNumber)
        {
            if (!completedLevels.Contains(levelNumber)) completedLevels.Add(levelNumber);
            if (nextLevelNumber > 0)
                highestUnlockedLevel = Mathf.Max(highestUnlockedLevel, nextLevelNumber);
            activeRun = null;
        }
    }

    [Serializable]
    public class LevelAttemptCounter
    {
        public int levelNumber;
        public int attemptNumber;
    }

    [Serializable]
    public class PowerUpInventory
    {
        public int deleteUses;
        public int deleteRowUses;
        public int levelUpUses;
        public int extraTurnsUses;

        public int Get(GameController.PowerUpType type)
        {
            switch (type)
            {
                case GameController.PowerUpType.delete: return deleteUses;
                case GameController.PowerUpType.lookAhead: return deleteRowUses;
                case GameController.PowerUpType.levelUp: return levelUpUses;
                case GameController.PowerUpType.extraTurns: return extraTurnsUses;
                default: return 0;
            }
        }

        public bool TryUse(GameController.PowerUpType type)
        {
            if (Get(type) <= 0) return false;
            switch (type)
            {
                case GameController.PowerUpType.delete: deleteUses--; break;
                case GameController.PowerUpType.lookAhead: deleteRowUses--; break;
                case GameController.PowerUpType.levelUp: levelUpUses--; break;
                case GameController.PowerUpType.extraTurns: extraTurnsUses--; break;
                default: return false;
            }
            return true;
        }

        public void Add(PowerUpInventory amount)
        {
            if (amount == null) return;
            deleteUses += Mathf.Max(0, amount.deleteUses);
            deleteRowUses += Mathf.Max(0, amount.deleteRowUses);
            levelUpUses += Mathf.Max(0, amount.levelUpUses);
            extraTurnsUses += Mathf.Max(0, amount.extraTurnsUses);
        }
    }
}