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

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _slideDuration = 0.14f;
        [SerializeField, Min(0f)] private float _absorptionDuration = 0.12f;
        [Tooltip("Pause before triggering any merge.")]
        [SerializeField, Min(0f)] private float _preMergeDelay = 0.1f;
        [Tooltip("Pause before another merge when a horizontal-only batch leaves this tower stationary and no gravity animation is pending.")]
        [SerializeField, Min(0f)] private float _horizontalChainDelay = 0.15f;
        [Tooltip("Visual space below a full-column top tower while it slides into the incoming survivor.")]
        [SerializeField, Range(0.1f, 0.8f)] private float _topMergeContactOffset = 0.45f;
        private bool _isAnimating;
        private bool _reservedForAbsorption;
        internal bool IsAnimating => _isAnimating || _reservedForAbsorption;

        [Header("Level Badge")]
        [SerializeField] private UnityEngine.UI.Image _levelColorPanel;
        [Tooltip("Dark panel colors indexed by the level's ones digit (0–9). The number stays white.")]
        [SerializeField] private Color[] _levelDigitColors =
        {
            new Color32(55, 68, 84, 255),
            new Color32(117, 87, 22, 255),
            new Color32(139, 51, 47, 255),
            new Color32(137, 69, 22, 255),
            new Color32(76, 98, 31, 255),
            new Color32(25, 101, 70, 255),
            new Color32(23, 97, 110, 255),
            new Color32(38, 73, 132, 255),
            new Color32(91, 56, 133, 255),
            new Color32(127, 45, 96, 255)
        };

        private void Awake()
        {
            RefreshLevelBadge(level);
        }

        private void RefreshLevelBadge(int towerLevel)
        {
            if (levelText != null)
            {
                levelText.text = towerLevel.ToString();
                levelText.color = Color.white;
            }
            int digit = Mathf.Abs(towerLevel % 10);
            if (_levelColorPanel != null && _levelDigitColors != null && digit < _levelDigitColors.Length)
                _levelColorPanel.color = _levelDigitColors[digit];
        }

        private void Update()
        {
            if (currentLevel != level) DisplayTowerForCurrentLevel(level);

            if (state == TowerState.queue)
            {
                int index = queue.towerQueue.IndexOf(gameObject);
                transform.localScale = index > 0 ? Vector3.one * 0.5f : Vector3.one;
                transform.position = index >= queue.visibleCount
                    ? new Vector3(0, -1000, 0)
                    : queue.PositionForSlot(index);
                return;
            }

            if (IsAnimating || grid == null || !grid.CanAnimate(this)) return;
            if (_placementPending && grid.TryBeginMerge(this)) StartCoroutine(RunMotion(PlaceAtFullColumn()));
            else if (drop) StartCoroutine(RunMotion(Drop()));
            else if (merge && !grid.HasPendingMovement(this) && grid.TryBeginMerge(this))
                StartCoroutine(RunMotion(Merge()));
            else if (!merge) transform.position = grid.WorldPosition(col, row);
        }

        private IEnumerator RunMotion(IEnumerator motion)
        {
            _isAnimating = true;
            if (!grid.towersInPlay.Contains(gameObject)) grid.towersInPlay.Add(gameObject);
            yield return motion;
            grid.EndMerge(this);
            _isAnimating = false;
            if (!drop && !merge) grid.towersInPlay.Remove(gameObject);
        }

        private IEnumerator SlideTo(Vector3 target)
        {
            Vector3 start = transform.position;
            float duration = _slideDuration * Mathf.Clamp(Vector3.Distance(start, target), 0.8f, 1.2f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.LerpUnclamped(start, target, t * t * (3f - 2f * t));
                yield return null;
            }
            transform.position = target;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (transform.parent != null && transform.parent.GetComponent<FallingTowerController>() != null)
            {
                transform.SetParent(null, true);
            }
            foreach (FallingTowerController motion in _absorptions)
            {
                if (motion == null) continue;
                TowerController absorbed = motion.GetComponentInChildren<TowerController>(true);
                if (absorbed != null && absorbed != this && grid != null && grid.grid != null &&
                    grid.grid[absorbed.col, absorbed.row] == absorbed.gameObject)
                    grid.grid[absorbed.col, absorbed.row] = null;
                Destroy(motion.gameObject);
            }
            _absorptions.Clear();
            _placementPending = false;
            _forcedTopMatch = null;
            drop = false;
            merge = false;
            _isAnimating = false;
            _reservedForAbsorption = false;
            if (grid != null)
            {
                grid.EndMerge(this);
                grid.towersInPlay.Remove(gameObject);
            }
        }

        internal void DisplayTowerForCurrentLevel(int towerLevel)
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
            RefreshLevelBadge(towerLevel);
            currentLevel = towerLevel;
        }

        private readonly List<FallingTowerController> _absorptions = new List<FallingTowerController>();
        private bool _placementPending;
        private TowerController _forcedTopMatch;

        internal void BeginPlacement(TowerController topMatch)
        {
            transform.localScale = Vector3.one;
            transform.position = grid.VisualEntryPosition(col);
            _forcedTopMatch = topMatch;
            _placementPending = topMatch != null;
            drop = topMatch == null;
            merge = false;
            if (!grid.towersInPlay.Contains(gameObject)) grid.towersInPlay.Add(gameObject);
        }

        private IEnumerator PlaceAtFullColumn()
        {
            _placementPending = false;
            TowerController top = _forcedTopMatch;
            _forcedTopMatch = null;
            if (top == null) { drop = true; yield break; }
            int topRow = top.row;
            Vector3 targetPosition = grid.WorldPosition(col, topRow);
            // Slide to contact just above the top tower, pause 0.1s, then merge down into it while shrinking the top tower.
            yield return SlideTo(targetPosition - Vector3.forward * _topMergeContactOffset);
            if (_preMergeDelay > 0f && _slideDuration > 0f)
            {
                yield return new WaitForSeconds(_preMergeDelay);
            }
            yield return Absorb(new List<TowerController> { top }, slideSelfDownToTarget: true, selfSlideDestination: targetPosition, shrinkTargetDuringSelfSlide: top);
            grid.grid[col, row] = null;
            row = topRow;
            grid.grid[col, row] = gameObject;
            transform.position = targetPosition;
            level++;
            DisplayTowerForCurrentLevel(level);
            grid.game.AwardMergeScore(level, transform.position);
            onTowerLevelUpEvent?.Invoke(level);
            grid.DamageAdjacentBreakables(col, row);
            grid.ResolveGravity(col);
            PlayMergeFeedback();
            drop = true;
        }

        private IEnumerator Drop()
        {
            drop = false;
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
            }
            yield return SlideTo(grid.WorldPosition(col, row));
            merge = true;
        }

        private IEnumerator Merge()
        {
            merge = false;
            List<TowerController> matches = new List<TowerController>();
            AddMatchingNeighbour(matches, col, row - 1);
            AddMatchingNeighbour(matches, col, row + 1);
            AddMatchingNeighbour(matches, col - 1, row);
            AddMatchingNeighbour(matches, col + 1, row);
            if (matches.Count == 0) yield break;

            if (_preMergeDelay > 0f && _absorptionDuration > 0f)
            {
                yield return new WaitForSeconds(_preMergeDelay);
                matches.Clear();
                AddMatchingNeighbour(matches, col, row - 1);
                AddMatchingNeighbour(matches, col, row + 1);
                AddMatchingNeighbour(matches, col - 1, row);
                AddMatchingNeighbour(matches, col + 1, row);
                if (matches.Count == 0) yield break;
            }

            TowerController lowerNeighbour = null;
            HashSet<int> gravityColumns = new HashSet<int> { col };
            List<Vector2Int> levelUpCells = new List<Vector2Int> { new Vector2Int(col, row) };
            foreach (TowerController neighbour in matches)
            {
                if (neighbour.col == col && neighbour.row == row - 1) lowerNeighbour = neighbour;
                levelUpCells.Add(new Vector2Int(neighbour.col, neighbour.row));
                gravityColumns.Add(neighbour.col);
            }

            Vector3 mergeDestination = lowerNeighbour != null ? grid.WorldPosition(col, lowerNeighbour.row) : transform.position;
            // Vertical merge: slide the upper tower down into the lower tower while shrinking the lower tower.
            // Horizontal merge: shrink horizontal neighbours into the falling/surviving tower.
            yield return Absorb(matches, slideSelfDownToTarget: lowerNeighbour != null, selfSlideDestination: mergeDestination, shrinkTargetDuringSelfSlide: lowerNeighbour);
            if (lowerNeighbour != null)
            {
                int oldRow = row;
                int targetRow = lowerNeighbour.row;
                if (grid.grid[col, oldRow] == gameObject) grid.grid[col, oldRow] = null;
                row = targetRow;
                grid.grid[col, row] = gameObject;
                transform.position = mergeDestination;
            }
            level += matches.Count;
            DisplayTowerForCurrentLevel(level);
            foreach (int affectedColumn in grid.DamageAdjacentBreakables(levelUpCells, false))
                gravityColumns.Add(affectedColumn);
            grid.game.AwardMergeScore(level, transform.position);
            onTowerLevelUpEvent?.Invoke(level);
            PlayMergeFeedback();
            foreach (int affectedColumn in gravityColumns) grid.ResolveGravity(affectedColumn);
            if (grid.FindLandingRow(col, row) != row) drop = true;
            else merge = true;
        }

        private IEnumerator Absorb(List<TowerController> matches, bool slideSelfDownToTarget = false, Vector3 selfSlideDestination = default, TowerController shrinkTargetDuringSelfSlide = null)
        {
            grid.NotifyMergeStarted();
            Vector3 destination = slideSelfDownToTarget ? selfSlideDestination : transform.position;
            Transform originalParent = transform.parent;

            if (slideSelfDownToTarget)
            {
                GameObject selfProxy = fallingTowerPrefab != null
                    ? Instantiate(fallingTowerPrefab, transform.position, Quaternion.identity)
                    : new GameObject("Tower vertical merge");
                selfProxy.transform.position = transform.position;
                FallingTowerController selfMotion = selfProxy.GetComponent<FallingTowerController>();
                if (selfMotion == null) selfMotion = selfProxy.AddComponent<FallingTowerController>();
                transform.SetParent(selfProxy.transform, true);
                selfMotion.BeginSlide(
                    destination,
                    _absorptionDuration,
                    shrinkSelf: false,
                    shrinkTarget: shrinkTargetDuringSelfSlide != null ? shrinkTargetDuringSelfSlide.transform : null,
                    hideOnComplete: false);
                _absorptions.Add(selfMotion);
            }

            foreach (TowerController neighbour in matches)
            {
                neighbour.enabled = false;
                neighbour.drop = false;
                neighbour.merge = false;
                neighbour._reservedForAbsorption = true;

                if (slideSelfDownToTarget && neighbour == shrinkTargetDuringSelfSlide)
                {
                    // Stationary lower tower stays in place and shrinks via selfMotion while the sliding tower merges down into it.
                    continue;
                }

                bool isVerticalUpper = neighbour.col == col && neighbour.row > row;
                GameObject proxy = fallingTowerPrefab != null
                    ? Instantiate(fallingTowerPrefab, neighbour.transform.position, Quaternion.identity)
                    : new GameObject("Tower absorption");
                proxy.transform.position = neighbour.transform.position;
                FallingTowerController motion = proxy.GetComponent<FallingTowerController>();
                if (motion == null) motion = proxy.AddComponent<FallingTowerController>();
                // Move the entire root, including its number canvas, not just the building model.
                neighbour.transform.SetParent(proxy.transform, true);
                motion.BeginSlide(
                    destination,
                    _absorptionDuration,
                    shrinkSelf: !isVerticalUpper,
                    shrinkTarget: isVerticalUpper && !slideSelfDownToTarget ? transform : null,
                    hideOnComplete: true);
                _absorptions.Add(motion);
            }
            bool waiting = true;
            while (waiting)
            {
                waiting = false;
                foreach (FallingTowerController motion in _absorptions)
                    if (motion != null && !motion.IsComplete) { waiting = true; break; }
                if (waiting) yield return null;
            }
            if (transform.parent != originalParent)
            {
                transform.SetParent(originalParent, true);
            }
            transform.localScale = Vector3.one;
            foreach (TowerController neighbour in matches)
            {
                if (neighbour == null) continue;
                if (grid.grid[neighbour.col, neighbour.row] == neighbour.gameObject)
                    grid.grid[neighbour.col, neighbour.row] = null;
                grid.towersInPlay.Remove(neighbour.gameObject);
                Destroy(neighbour.gameObject);
            }
            foreach (FallingTowerController motion in _absorptions)
                if (motion != null) Destroy(motion.gameObject);
            _absorptions.Clear();
        }

        private void PlayMergeFeedback()
        {
            if (levelUpEffectPrefab != null) Instantiate(levelUpEffectPrefab, transform.position, Quaternion.identity);
            grid.NotifyMergeCompleted();
        }

        private void AddMatchingNeighbour(List<TowerController> matches, int column, int targetRow)
        {
            if (!grid.IsAvailable(column, targetRow)) return;
            GameObject other = grid.grid[column, targetRow];
            if (other == null || other == gameObject) return;
            TowerController neighbour = other.GetComponent<TowerController>();
            if (neighbour.enabled && !neighbour.IsAnimating && neighbour.level == level) matches.Add(neighbour);
        }
}
}