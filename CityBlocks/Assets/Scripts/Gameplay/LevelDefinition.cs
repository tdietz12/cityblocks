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

    [Serializable]
    public class LevelGridRow
    {
        public string[] cells = new string[0];

        public LevelGridRow() { }

        public LevelGridRow(int cols)
        {
            cells = new string[cols];
            for (int i = 0; i < cols; i++) cells[i] = string.Empty;
        }

        public LevelGridRow Clone()
        {
            LevelGridRow copy = new LevelGridRow(cells != null ? cells.Length : 0);
            if (cells != null)
            {
                for (int i = 0; i < cells.Length; i++) copy.cells[i] = cells[i];
            }
            return copy;
        }
    }

    [CreateAssetMenu(fileName = "Level", menuName = "City Blocks/Level Definition")]
    public class LevelDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        [Min(1)] public int levelNumber = 1;
        public LevelDifficulty difficulty;
        [Range(3, 7)] public int columns = 4;
        [Range(3, 7)] public int rows = 6;
        public List<LevelGridRow> grid = new List<LevelGridRow>();
        // Listed cells are holes: they do not accept blocks, and falling blocks pass through them.
        public List<Vector2Int> unavailableCells = new List<Vector2Int>();
        public List<StartingBlock> startingBlocks = new List<StartingBlock>();
        public List<LevelObstacle> obstacles = new List<LevelObstacle>();
        public List<QueueEntry> queue = new List<QueueEntry>();
        [Min(1)] public int randomMaximumLevel = 6;
        [Min(0)] public int moveLimit;
        public List<LevelObjective> objectives = new List<LevelObjective>();
        public List<GameObject> tutorialSteps = new List<GameObject>();
        public bool showAdAfterWin;

        public void ResizeGrid(int newCols, int newRows)
        {
            newCols = Mathf.Clamp(newCols, 3, 7);
            newRows = Mathf.Clamp(newRows, 3, 7);

            if (grid == null) grid = new List<LevelGridRow>();

            while (grid.Count < newRows)
            {
                grid.Add(new LevelGridRow(newCols));
            }
            while (grid.Count > newRows)
            {
                grid.RemoveAt(grid.Count - 1);
            }

            for (int r = 0; r < newRows; r++)
            {
                if (grid[r] == null) grid[r] = new LevelGridRow(newCols);
                string[] oldCells = grid[r].cells ?? new string[0];
                if (oldCells.Length != newCols)
                {
                    string[] newCells = new string[newCols];
                    for (int c = 0; c < newCols; c++)
                    {
                        newCells[c] = c < oldCells.Length ? (oldCells[c] ?? string.Empty) : string.Empty;
                    }
                    grid[r].cells = newCells;
                }
                else
                {
                    for (int c = 0; c < newCols; c++)
                    {
                        if (grid[r].cells[c] == null) grid[r].cells[c] = string.Empty;
                    }
                }
            }
        }

        public void SyncFromGrid()
        {
            ResizeGrid(columns, rows);

            if (startingBlocks == null) startingBlocks = new List<StartingBlock>();
            else startingBlocks.Clear();

            if (obstacles == null) obstacles = new List<LevelObstacle>();
            else obstacles.Clear();

            if (unavailableCells == null) unavailableCells = new List<Vector2Int>();
            else unavailableCells.Clear();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    string raw = grid[r].cells[c];
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string trimmed = raw.Trim();
                    if (trimmed == "." || trimmed == "-") continue;

                    if (int.TryParse(trimmed, out int towerLevel))
                    {
                        if (towerLevel >= 1)
                        {
                            startingBlocks.Add(new StartingBlock { column = c, row = r, towerLevel = towerLevel });
                        }
                    }
                    else if (trimmed.StartsWith("Fixed Breakable", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.StartsWith("Breakable", StringComparison.OrdinalIgnoreCase))
                    {
                        int durability = 1;
                        string rest = trimmed.StartsWith("Fixed Breakable", StringComparison.OrdinalIgnoreCase)
                            ? trimmed.Substring("Fixed Breakable".Length).Trim()
                            : trimmed.Substring("Breakable".Length).Trim();
                        if (!string.IsNullOrEmpty(rest))
                        {
                            rest = rest.Trim('(', ')', ':', '-', ' ');
                            if (int.TryParse(rest, out int d))
                            {
                                durability = Mathf.Clamp(d, 1, 4);
                            }
                        }
                        obstacles.Add(new LevelObstacle
                        {
                            type = BoardObstacleType.Breakable,
                            column = c,
                            row = r,
                            durability = durability
                        });
                    }
                    else if (string.Equals(trimmed, "Fixed Unbreakable", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmed, "Fixed", StringComparison.OrdinalIgnoreCase))
                    {
                        obstacles.Add(new LevelObstacle
                        {
                            type = BoardObstacleType.FixedUnbreakable,
                            column = c,
                            row = r,
                            durability = 0
                        });
                    }
                    else if (string.Equals(trimmed, "Falling Unbreakable", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmed, "Falling", StringComparison.OrdinalIgnoreCase))
                    {
                        obstacles.Add(new LevelObstacle
                        {
                            type = BoardObstacleType.FallingUnbreakable,
                            column = c,
                            row = r,
                            durability = 0
                        });
                    }
                    else if (string.Equals(trimmed, "Hole", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmed, "X", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmed, "Unavailable", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(trimmed, "Blocked", StringComparison.OrdinalIgnoreCase))
                    {
                        unavailableCells.Add(new Vector2Int(c, r));
                    }
                }
            }
        }

        public void SyncToGrid()
        {
            ResizeGrid(columns, rows);

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    grid[r].cells[c] = string.Empty;

            if (unavailableCells != null)
            {
                foreach (Vector2Int hole in unavailableCells)
                {
                    if (hole.y >= 0 && hole.y < rows && hole.x >= 0 && hole.x < columns)
                    {
                        grid[hole.y].cells[hole.x] = "Hole";
                    }
                }
            }

            if (startingBlocks != null)
            {
                foreach (StartingBlock block in startingBlocks)
                {
                    if (block.row >= 0 && block.row < rows && block.column >= 0 && block.column < columns)
                    {
                        grid[block.row].cells[block.column] = block.towerLevel.ToString();
                    }
                }
            }

            if (obstacles != null)
            {
                foreach (LevelObstacle obstacle in obstacles)
                {
                    if (obstacle.row >= 0 && obstacle.row < rows && obstacle.column >= 0 && obstacle.column < columns)
                    {
                        switch (obstacle.type)
                        {
                            case BoardObstacleType.Breakable:
                                grid[obstacle.row].cells[obstacle.column] = obstacle.durability > 1
                                    ? "Fixed Breakable " + obstacle.durability
                                    : "Fixed Breakable";
                                break;
                            case BoardObstacleType.FixedUnbreakable:
                                grid[obstacle.row].cells[obstacle.column] = "Fixed Unbreakable";
                                break;
                            case BoardObstacleType.FallingUnbreakable:
                                grid[obstacle.row].cells[obstacle.column] = "Falling Unbreakable";
                                break;
                        }
                    }
                }
            }
        }

        public bool HasPopulatedGrid()
        {
            if (grid == null || grid.Count != rows) return false;
            for (int r = 0; r < rows; r++)
            {
                if (grid[r] == null || grid[r].cells == null || grid[r].cells.Length != columns) return false;
                for (int c = 0; c < columns; c++)
                {
                    if (!string.IsNullOrWhiteSpace(grid[r].cells[c])) return true;
                }
            }
            return false;
        }

        public void OnBeforeSerialize()
        {
            if (HasPopulatedGrid())
            {
                SyncFromGrid();
            }
        }

        public void OnAfterDeserialize()
        {
        }

        private void OnValidate()
        {
            columns = Mathf.Clamp(columns, 3, 7);
            rows = Mathf.Clamp(rows, 3, 7);

            if (HasPopulatedGrid())
            {
                SyncFromGrid();
            }
            else if ((startingBlocks != null && startingBlocks.Count > 0) ||
                     (obstacles != null && obstacles.Count > 0) ||
                     (unavailableCells != null && unavailableCells.Count > 0))
            {
                SyncToGrid();
            }
            else
            {
                ResizeGrid(columns, rows);
            }
        }

        public bool IsAvailable(int column, int row)
        {
            return column >= 0 && column < columns && row >= 0 && row < rows &&
                   !unavailableCells.Contains(new Vector2Int(column, row));
        }

        public bool IsValid(out string error)
        {
            if (HasPopulatedGrid())
            {
                SyncFromGrid();
            }

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