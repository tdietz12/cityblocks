using System.Collections.Generic;
using Data_Persistence;
using UI;
using UnityEngine;

namespace Gameplay
{
    public class GridController : MonoBehaviour, DataPersistenceInterface
    {
        public static GridController instance { get; private set; }

        public GameController game;
        public GameObject cell;
        public BoardObstacle breakableObstaclePrefab;
        public BoardObstacle fixedObstaclePrefab;
        public BoardObstacle fallingObstaclePrefab;

        internal GameObject[,] grid;
        private BoardObstacle[,] obstacles;
        internal int totalColumns = 4;
        internal int totalRows = 7; // Includes the temporary spawn row.
        [SerializeField] private Vector2 boardCenter = new Vector2(2f, 3f);

        /// <summary>World-space origin for the configured board, centered on the street backdrop.</summary>
        public Vector3 WorldOffset => transform.position + new Vector3(
            boardCenter.x - (totalColumns - 1) * 0.5f,
            0f,
            boardCenter.y - (totalRows - 2) * 0.5f);

        private bool[,] available;
        public List<GameObject> towersInPlay = new List<GameObject>();

        public bool IsSettled()
        {
            if (towersInPlay.Count > 0 || grid == null) return false;
            for (int column = 0; column < totalColumns; column++)
            for (int row = 0; row < totalRows; row++)
            {
                if (obstacles != null && obstacles[column, row] != null &&
                    obstacles[column, row].IsMoving) return false;
                GameObject block = grid[column, row];
                if (block == null) continue;
                TowerController tower = block.GetComponent<TowerController>();
                if (tower.drop || tower.merge) return false;
            }
            return true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
                Debug.LogError("Found more than one GridController in the scene.");
            instance = this;
        }

        public void Configure(LevelDefinition definition)
        {
            totalColumns = Mathf.Clamp(definition.columns, 3, 7);
            totalRows = Mathf.Clamp(definition.rows, 3, 7) + 1;
            available = new bool[totalColumns, totalRows - 1];
            for (int column = 0; column < totalColumns; column++)
            for (int row = 0; row < totalRows - 1; row++)
                available[column, row] = definition.IsAvailable(column, row);
        }

        public bool IsAvailable(int column, int row)
        {
            return column >= 0 && column < totalColumns && row >= 0 && row < totalRows - 1 &&
                   (available == null || available[column, row]);
        }

        public int TopPlayableRow(int column)
        {
            for (int row = totalRows - 2; row >= 0; row--)
                if (IsAvailable(column, row)) return row;
            return -1;
        }

        public int FindLandingRow(int column, int fromRow)
        {
            int landingRow = fromRow;
            for (int row = fromRow - 1; row >= 0; row--)
            {
                if (!IsAvailable(column, row)) continue;
                if (IsOccupied(column, row)) break;
                landingRow = row;
            }
            return landingRow;
        }

        public bool IsOccupied(int column, int row)
        {
            return grid[column, row] != null ||
                   (obstacles != null && obstacles[column, row] != null);
        }

        public bool HasObstacle(int column, int row)
        {
            return obstacles != null && obstacles[column, row] != null;
        }

        public List<ObstacleSaveData> CaptureObstacles()
        {
            List<ObstacleSaveData> state = new List<ObstacleSaveData>();
            if (obstacles == null) return state;
            for (int column = 0; column < totalColumns; column++)
            for (int row = 0; row < totalRows - 1; row++)
            {
                BoardObstacle obstacle = obstacles[column, row];
                if (obstacle == null) continue;
                state.Add(new ObstacleSaveData
                {
                    type = obstacle.Type, column = column, row = row,
                    durability = obstacle.Durability
                });
            }
            return state;
        }

        public void LoadObstacles(List<ObstacleSaveData> state)
        {
            obstacles = new BoardObstacle[totalColumns, totalRows];
            if (state == null) return;
            foreach (ObstacleSaveData entry in state)
            {
                if (!IsAvailable(entry.column, entry.row) ||
                    IsOccupied(entry.column, entry.row))
                {
                    Debug.LogWarning("Skipped invalid saved obstacle position.");
                    continue;
                }
                BoardObstacle prefab = entry.type == BoardObstacleType.Breakable
                    ? breakableObstaclePrefab : entry.type == BoardObstacleType.FixedUnbreakable
                        ? fixedObstaclePrefab : fallingObstaclePrefab;
                if (prefab == null)
                {
                    Debug.LogError("Missing obstacle prefab for " + entry.type);
                    continue;
                }
                BoardObstacle obstacle = Instantiate(prefab, WorldPosition(entry.column, entry.row),
                    Quaternion.identity);
                obstacle.Initialize(entry.type, entry.column, entry.row, entry.durability,
                    WorldPosition(entry.column, entry.row));
                obstacles[entry.column, entry.row] = obstacle;
            }
        }

        public List<int> DamageAdjacentBreakables(int column, int row, bool dropTowers = true)
        {
            return DamageAdjacentBreakables(new List<Vector2Int> { new Vector2Int(column, row) }, dropTowers);
        }

        /// <summary>
        /// A level-up may merge several blocks. Check the cells occupied by all of them,
        /// but damage any single neighboring obstacle only once for this level-up.
        /// </summary>
        public List<int> DamageAdjacentBreakables(IList<Vector2Int> levelUpCells, bool dropTowers = true)
        {
            HashSet<int> affectedColumns = new HashSet<int>();
            HashSet<Vector2Int> checkedObstacles = new HashSet<Vector2Int>();
            if (levelUpCells != null)
            {
                foreach (Vector2Int cell in levelUpCells)
                {
                    DamageBreakable(cell.x - 1, cell.y, checkedObstacles, affectedColumns);
                    DamageBreakable(cell.x + 1, cell.y, checkedObstacles, affectedColumns);
                    DamageBreakable(cell.x, cell.y - 1, checkedObstacles, affectedColumns);
                    DamageBreakable(cell.x, cell.y + 1, checkedObstacles, affectedColumns);
                }
            }
            foreach (int affectedColumn in affectedColumns)
                ResolveGravity(affectedColumn, dropTowers);
            return new List<int>(affectedColumns);
        }

        private void DamageBreakable(int column, int row, HashSet<Vector2Int> checkedObstacles,
            HashSet<int> affectedColumns)
        {
            Vector2Int position = new Vector2Int(column, row);
            if (!IsAvailable(column, row) || obstacles == null || !checkedObstacles.Add(position)) return;
            BoardObstacle obstacle = obstacles[column, row];
            if (obstacle == null || !obstacle.Damage()) return;
            obstacles[column, row] = null;
            if (Application.isPlaying) Destroy(obstacle.gameObject);
            else DestroyImmediate(obstacle.gameObject);
            affectedColumns.Add(column);
        }

        public void ResolveGravity(int column, bool dropTowers = true)
        {
            if (column < 0 || column >= totalColumns) return;
            // Falling obstacles settle first, so towers above can land on their new positions.
            for (int row = 0; row < totalRows - 1; row++)
            {
                BoardObstacle obstacle = obstacles == null ? null : obstacles[column, row];
                if (obstacle == null || obstacle.Type != BoardObstacleType.FallingUnbreakable) continue;
                int landing = row;
                for (int below = row - 1; below >= 0; below--)
                {
                    if (!IsAvailable(column, below)) continue;
                    if (IsOccupied(column, below)) break;
                    landing = below;
                }
                if (landing == row) continue;
                obstacles[column, row] = null;
                obstacles[column, landing] = obstacle;
                obstacle.MoveTo(landing, WorldPosition(column, landing));
            }
            if (!dropTowers) return;
            for (int row = 0; row < totalRows; row++)
                if (grid[column, row] != null && FindLandingRow(column, row) != row)
                    grid[column, row].GetComponent<TowerController>().drop = true;
        }

        public Vector3 WorldPosition(int column, int row)
        {
            Vector3 offset = WorldOffset;
            return new Vector3(column + offset.x, offset.y, totalRows - 2 - row + offset.z);
        }

        public void Load(GameSaveData gameSaveData, ProgressionData progressionData)
        {
            GenerateNewGrid();
            LoadBoardState(gameSaveData.boardState);
        }

        public void Save(ref GameSaveData gameSaveData, ref ProgressionData progressionData)
        {
            gameSaveData.boardState = CaptureBoardState();
        }

        public List<Vector3> CaptureBoardState()
        {
            List<Vector3> state = new List<Vector3>();
            if (grid == null) return state;
            for (int column = 0; column < totalColumns; column++)
            for (int row = 0; row < totalRows; row++)
                if (grid[column, row] != null)
                    state.Add(new Vector3(column, row, grid[column, row].GetComponent<TowerController>().level));
            return state;
        }

        private void GenerateNewGrid()
        {
            grid = new GameObject[totalColumns, totalRows];
            obstacles = new BoardObstacle[totalColumns, totalRows];
            for (int column = 0; column < totalColumns; column++)
            {
                for (int row = 0; row < totalRows - 1; row++)
                {
                    if (!IsAvailable(column, row)) continue;
                    GameObject newCell = Instantiate(cell, WorldPosition(column, row), Quaternion.identity);
                    Cell cellComponent = newCell.GetComponent<Cell>();
                    cellComponent.col = column;
                    cellComponent.row = row;
                    cellComponent.onCellHitEvent += GetCellHit;
                }
            }
        }

        private void LoadBoardState(List<Vector3> state)
        {
            if (state == null) return;
            foreach (Vector3 block in state)
            {
                int column = (int)block.x;
                int row = (int)block.y;
                bool spawnRow = row == totalRows - 1;
                if (column < 0 || column >= totalColumns || row < 0 || row >= totalRows ||
                    (!spawnRow && !IsAvailable(column, row)) || block.z < 1 || IsOccupied(column, row))
                {
                    Debug.LogWarning("Skipped invalid saved block at " + block);
                    continue;
                }

                GameObject newTower = Instantiate(game.tower, WorldPosition(column, row), Quaternion.identity);
                grid[column, row] = newTower;
                TowerController tower = newTower.GetComponent<TowerController>();
                tower.state = TowerController.TowerState.grid;
                tower.queue = game.queue;
                tower.grid = this;
                tower.onTowerLevelUpEvent += IncrementScore;
                tower.onTowerLevelUpEvent += IncrementLevel;
                tower.level = (int)block.z;
                tower.col = column;
                tower.row = row;
                tower.drop = spawnRow;
            }
        }

        public void GetCellHit(int column, int row) { game.GetCellHit(column, row); }
        public void IncrementScore(int towerLevel) { game.IncrementScore(towerLevel); }
        public void IncrementLevel(int towerLevel) { game.IncrementLevel(towerLevel); }
    }
}
