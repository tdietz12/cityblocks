using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay
{
    public class ScoreEvent : UnityEvent<int> { }

    public class TowerController : MonoBehaviour
    {
        internal enum TowerState { queue, grid }
        internal TowerState state;

        public TextMeshProUGUI levelText;
        internal QueueController queue;
        public int level;
        private int currentLevel;
        internal GridController grid;
        public int col;
        public int row;
        internal bool drop;
        internal bool merge;
        internal GameObject currentTowerPrefab;
        public GameObject levelUpEffectPrefab;
        public GameObject fallingTowerPrefab;

        public delegate void TowerLevelUpEvent(int towerLevel);
        public event TowerLevelUpEvent onTowerLevelUpEvent;

        private void Update()
        {
            if (currentLevel != level) DisplayTowerForCurrentLevel(level);

            if (state == TowerState.queue)
            {
                int index = queue.towerQueue.IndexOf(gameObject);
                transform.localScale = index > 0 ? Vector3.one * 0.5f : Vector3.one;
                transform.position = index >= queue.visibleCount
                    ? new Vector3(0, -1000, 0)
                    : index > 0
                        ? new Vector3((index * -0.5f) + 1.75f, 0, -1.5f)
                        : new Vector3(2, 0, -1.5f);
                return;
            }

            Vector3 target = grid.WorldPosition(col, row);
            transform.position = Vector3.Distance(transform.position, target) < 0.1f
                ? target : Vector3.Lerp(transform.position, target, 0.5f);
            if (drop) StartCoroutine(Drop());
            else if (merge) StartCoroutine(Merge());
        }

        private void DisplayTowerForCurrentLevel(int towerLevel)
        {
            if (currentTowerPrefab != null) Destroy(currentTowerPrefab);
            GameObject prefab = Resources.Load<GameObject>(towerLevel.ToString());
            if (prefab == null)
            {
                Debug.LogError("No tower prefab for level " + towerLevel);
                currentLevel = towerLevel;
                return;
            }
            currentTowerPrefab = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            if (levelText != null) levelText.text = towerLevel.ToString();
            currentLevel = towerLevel;
        }

        private IEnumerator Drop()
        {
            drop = false;
            if (!grid.towersInPlay.Contains(gameObject)) grid.towersInPlay.Add(gameObject);

            int landingRow = grid.FindLandingRow(col, row);
            if (landingRow != row)
            {
                int oldRow = row;
                grid.grid[col, row] = null;
                row = landingRow;
                grid.grid[col, row] = gameObject;
                for (int higherRow = oldRow + 1; higherRow < grid.totalRows; higherRow++)
                    if (grid.grid[col, higherRow] != null)
                        grid.grid[col, higherRow].GetComponent<TowerController>().drop = true;
                yield return new WaitForSeconds(0.25f);
            }
            merge = true;
        }

        private IEnumerator Merge()
        {
            merge = false;
            if (!grid.towersInPlay.Contains(gameObject)) grid.towersInPlay.Add(gameObject);

            List<TowerController> matches = new List<TowerController>();
            AddMatchingNeighbour(matches, col, row - 1);
            AddMatchingNeighbour(matches, col, row + 1);
            AddMatchingNeighbour(matches, col - 1, row);
            AddMatchingNeighbour(matches, col + 1, row);

            if (matches.Count == 0)
            {
                grid.towersInPlay.Remove(gameObject);
                yield break;
            }

            bool removedBelow = false;
            HashSet<TowerController> above = new HashSet<TowerController>();
            HashSet<int> gravityColumns = new HashSet<int>();
            List<Vector2Int> levelUpCells = new List<Vector2Int> { new Vector2Int(col, row) };
            foreach (TowerController neighbour in matches)
            {
                levelUpCells.Add(new Vector2Int(neighbour.col, neighbour.row));
                gravityColumns.Add(neighbour.col);
                if (neighbour.col == col && neighbour.row < row) removedBelow = true;
                for (int higherRow = neighbour.row + 1; higherRow < grid.totalRows; higherRow++)
                {
                    GameObject higher = grid.grid[neighbour.col, higherRow];
                    if (higher != null && higher != gameObject)
                        above.Add(higher.GetComponent<TowerController>());
                }
                RemoveMergedTower(neighbour);
            }

            level += matches.Count;
            foreach (int affectedColumn in grid.DamageAdjacentBreakables(levelUpCells, false))
                gravityColumns.Add(affectedColumn);
            if (levelUpEffectPrefab != null) Instantiate(levelUpEffectPrefab, transform.position, Quaternion.identity);
            onTowerLevelUpEvent?.Invoke(level);
            yield return new WaitForSeconds(0.25f);

            foreach (int affectedColumn in gravityColumns)
                grid.ResolveGravity(affectedColumn);
            foreach (TowerController higher in above)
                if (higher != null) higher.drop = true;
            if (removedBelow || grid.FindLandingRow(col, row) != row) drop = true;
            else merge = true;
        }

        private void AddMatchingNeighbour(List<TowerController> matches, int column, int targetRow)
        {
            if (!grid.IsAvailable(column, targetRow)) return;
            GameObject other = grid.grid[column, targetRow];
            if (other == null || other == gameObject) return;
            TowerController neighbour = other.GetComponent<TowerController>();
            if (neighbour.level == level) matches.Add(neighbour);
        }

        private void RemoveMergedTower(TowerController neighbour)
        {
            grid.grid[neighbour.col, neighbour.row] = null;
            grid.towersInPlay.Remove(neighbour.gameObject);
            if (fallingTowerPrefab != null && neighbour.currentTowerPrefab != null)
            {
                GameObject falling = Instantiate(fallingTowerPrefab, neighbour.transform.position, Quaternion.identity);
                neighbour.currentTowerPrefab.transform.SetParent(falling.transform);
                falling.GetComponent<FallingTowerController>().mergeTarget = transform.position;
            }
            Destroy(neighbour.gameObject);
        }
    }
}