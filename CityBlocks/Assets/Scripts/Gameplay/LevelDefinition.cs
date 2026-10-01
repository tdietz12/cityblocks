using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    public enum LevelDifficulty { Normal, Hard, VeryHard }
    public enum QueueEntryType { Exact, RandomAny, RandomRange }
    public enum LevelObjectiveType { ReachTowerLevel, ReachScore }
    public enum BoardObstacleType { Breakable, FixedUnbreakable, FallingUnbreakable }

    [Serializable]
    public class LevelObstacle
    {
        public BoardObstacleType type;
        public int column;
        public int row;
        [Range(1, 4)] public int durability = 1;
    }

    [Serializable]
    public class StartingBlock
    {
        public int column;
        public int row;
        [Min(1)] public int towerLevel = 1;
    }

    [Serializable]
    public class QueueEntry
    {
        public QueueEntryType type;
        [Min(1)] public int exactLevel = 1;
        [Min(1)] public int minimumLevel = 1;
        [Min(1)] public int maximumLevel = 6;
    }

    [Serializable]
    public class LevelObjective
    {
        public LevelObjectiveType type;
        [Min(1)] public int target = 1;
    }

    [CreateAssetMenu(fileName = "Level", menuName = "City Blocks/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Min(1)] public int levelNumber = 1;
        public LevelDifficulty difficulty;
        [Range(3, 7)] public int columns = 4;
        [Range(3, 7)] public int rows = 6;
        // Listed cells are holes: they do not accept blocks, and falling blocks pass through them.
        public List<Vector2Int> unavailableCells = new List<Vector2Int>();
        public List<StartingBlock> startingBlocks = new List<StartingBlock>();
        public List<LevelObstacle> obstacles = new List<LevelObstacle>();
        public List<QueueEntry> queue = new List<QueueEntry>();
        [Min(1)] public int randomMaximumLevel = 6;
        [Min(0)] public int moveLimit;
        public List<LevelObjective> objectives = new List<LevelObjective>();
        [TextArea(2, 4)] public List<string> tutorialSteps = new List<string>();
        public bool showAdAfterWin;

        public bool IsAvailable(int column, int row)
        {
            return column >= 0 && column < columns && row >= 0 && row < rows &&
                   !unavailableCells.Contains(new Vector2Int(column, row));
        }

        public bool IsValid(out string error)
        {
            if (levelNumber < 1 || columns < 3 || columns > 7 || rows < 3 || rows > 7)
            {
                error = "Level number or board size is outside the supported range.";
                return false;
            }

            HashSet<Vector2Int> holes = new HashSet<Vector2Int>();
            foreach (Vector2Int hole in unavailableCells)
            {
                if (hole.x < 0 || hole.x >= columns || hole.y < 0 || hole.y >= rows || !holes.Add(hole))
                {
                    error = "An unavailable cell is outside the board or duplicated.";
                    return false;
                }
            }
            if (holes.Count == columns * rows)
            {
                error = "A level needs at least one playable cell.";
                return false;
            }

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            foreach (StartingBlock block in startingBlocks)
            {
                Vector2Int position = new Vector2Int(block.column, block.row);
                if (!IsAvailable(block.column, block.row) || block.towerLevel < 1 || !occupied.Add(position))
                {
                    error = "A starting block is invalid, unavailable, or duplicated.";
                    return false;
                }
            }

            if (obstacles != null) foreach (LevelObstacle obstacle in obstacles)
            {
                Vector2Int position = new Vector2Int(obstacle.column, obstacle.row);
                if (!IsAvailable(obstacle.column, obstacle.row) || !occupied.Add(position) ||
                    !Enum.IsDefined(typeof(BoardObstacleType), obstacle.type) ||
                    (obstacle.type == BoardObstacleType.Breakable &&
                     (obstacle.durability < 1 || obstacle.durability > 4)))
                {
                    error = "An obstacle is invalid, unavailable, or overlaps another board object.";
                    return false;
                }
            }

            if (randomMaximumLevel < 1 || moveLimit < 0)
            {
                error = "Random level maximum or move limit is invalid.";
                return false;
            }

            foreach (QueueEntry entry in queue)
            {
                if ((entry.type == QueueEntryType.Exact && entry.exactLevel < 1) ||
                    (entry.type == QueueEntryType.RandomRange &&
                     (entry.minimumLevel < 1 || entry.maximumLevel < entry.minimumLevel)))
                {
                    error = "A queue entry has an invalid tower level range.";
                    return false;
                }
            }

            if (objectives == null || objectives.Count == 0)
            {
                error = "A level needs at least one win objective.";
                return false;
            }
            foreach (LevelObjective objective in objectives)
            {
                if (objective.target < 1)
                {
                    error = "An objective target must be positive.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}