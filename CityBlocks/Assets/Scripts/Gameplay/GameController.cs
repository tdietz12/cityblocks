using System.Collections;
using Data_Persistence;
using TMPro;
using UI;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Core gameplay coordinator for CityBlocks.
    /// Manages the game lifecycle, placement flow, tower merging, power-ups, queue generation, and scoring.
    /// </summary>
    public class GameController : MonoBehaviour, DataPersistenceInterface
    {
        // Singleton pattern: allows global access to this instance via GameController.instance
        public static GameController instance { get; private set; }

        public enum GameState
        {
            start,
            play,
            levelUp,
            powerUp,
            lose,
            win
        }
        public GameState state;

        public enum PowerUpType
        {
            delete,
            lookAhead,
            levelUp,
            extraTurns
        }
        public PowerUpType powerUp;

        [Header("Gameplay References")]
        public GameObject tower;
        public QueueController queue;
        public GridController grid;

        [Header("UI Panels (Endless / Legacy Fallback)")]
        public GameObject playPanel;
        public GameObject levelUpPanel;
        public GameObject powerUpPanel_Delete;
        public GameObject powerUpPanel_LookAhead;
        public GameObject powerUpPanel_LevelUp;
        public GameObject losePanel;

        [Header("Score HUD")]
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI recordScoreText;
        [SerializeField] private ScoreFeedbackController _scoreFeedback;
        [SerializeField] private Camera _gameplayCamera;
        public Camera GameplayCamera => _gameplayCamera;
        public int DisplayedScore => _scoreFeedback != null && _scoreFeedback.isActiveAndEnabled ? _scoreFeedback.DisplayedScore : score;
        public int DisplayedRecordScore => _scoreFeedback != null && _scoreFeedback.isActiveAndEnabled ? _scoreFeedback.DisplayedRecordScore : recordScore;
        public bool HasPendingScoreFeedback => _scoreFeedback != null && _scoreFeedback.isActiveAndEnabled && _scoreFeedback.HasPendingFeedback;
        public bool HasPendingComboCelebration => _scoreFeedback != null && _scoreFeedback.isActiveAndEnabled && _scoreFeedback.HasPendingComboCelebration;

        public void BindScoreFeedback(ScoreFeedbackController feedback, Camera camera)
        {
            if (_scoreFeedback != null) _scoreFeedback.onDisplayedScoreChanged -= UpdateScoreUI;
            _scoreFeedback = feedback;
            if (camera != null) _gameplayCamera = camera;
            if (_scoreFeedback != null)
            {
                _scoreFeedback.Bind(this, _gameplayCamera);
                _scoreFeedback.onDisplayedScoreChanged += UpdateScoreUI;
            }
            UpdateScoreUI();
        }

        [Header("State & Tracking")]
        public int score;
        public int level;
        public int recordScore;
        public int recordLevel;

        // Endless record as it stood when the current run started (recordScore/recordLevel update live during play).
        private int _endlessRecordScoreAtStart;
        private int _endlessRecordLevelAtStart;
        private bool _endlessRecordReported;

        /// <summary>Sends endless_new_record once per Endless run if this run beat the record it started with.</summary>
        public void ReportEndlessRecordIfBeaten()
        {
            if (_endlessRecordReported || levelDefinition != null || LevelFlow.ActiveSession != null) return;
            if (score <= _endlessRecordScoreAtStart && level <= _endlessRecordLevelAtStart) return;
            _endlessRecordReported = true;
            LevelAnalytics.EndlessNewRecord(score, level, _endlessRecordScoreAtStart, _endlessRecordLevelAtStart, EndlessAttemptNumber);
        }

        public int QueueIndex => queueIndex;
        public bool QueueReady => queue != null && queue.towerQueue.Count >= 2;
        private bool powerUpRequestPending;
        public bool IsPowerUpRequestPending => powerUpRequestPending;
        public bool IsResolvingMove => moveResolving || powerUpRequestPending;
        public int LookAheadTurnsRemaining => lookAheadTurnsRemaining;
        public bool LookAheadUsed => lookAheadUsed;
        public bool HasActiveRun => hasActiveRun;
        public int TotalMoves { get; private set; }
        public int EndlessAttemptNumber { get; private set; }

        // Tutorial / gameplay event hooks
        public event System.Action<int> onTowerPlaced;
        public event System.Action onMoveResolved;
        public event System.Action<PowerUpType> onPowerUpUsed;
        public System.Func<int, bool> PlacementFilter;

        private bool hasActiveRun;
        private int firstInQueue;
        private int nextInQueue;
        private int savedThirdInQueue;
        private int queueIndex;
        private int lookAheadTurnsRemaining;
        private bool lookAheadUsed;
        private bool endlessFinished;
        private bool prepLevelUpPanel = false;
        private bool moveResolving;
        private int _turnMergeCount;
        private bool _comboTurnActive;
        private bool _isPlacementComboTurn;
        private int _comboTurnId;
        public int CurrentMergeMultiplier => _comboTurnActive ? Mathf.Max(1, _turnMergeCount) : 1;
        public int TurnMergeCount => _turnMergeCount;
        public bool IsPlacementComboTurn => _isPlacementComboTurn;
        public int ComboTurnId => _comboTurnId;
        public event System.Action onComboTurnStarted;
        public event System.Action onComboTurnEnded;
        public event System.Action<int> onPlacementComboCompleted;

        private void BeginComboTurn() => BeginComboTurn(true);

        private void BeginComboTurn(bool isTowerPlacement)
        {
            _turnMergeCount = 0;
            _comboTurnActive = true;
            _isPlacementComboTurn = isTowerPlacement;
            _comboTurnId++;
            onComboTurnStarted?.Invoke();
        }

        private void EndComboTurn()
        {
            _turnMergeCount = 0;
            _comboTurnActive = false;
            _isPlacementComboTurn = false;
            onComboTurnEnded?.Invoke();
        }

        internal void CompleteComboTurnAfterSettlement()
        {
            int completedMerges = _turnMergeCount;
            bool wasPlacementTurn = _comboTurnActive && _isPlacementComboTurn;
            EndComboTurn();
            if (wasPlacementTurn && completedMerges >= 3)
            {
                onPlacementComboCompleted?.Invoke(completedMerges);
            }
        }

        private LevelDefinition levelDefinition;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("Found more than one GameController in the scene.");
            }
            instance = this;
            if (_scoreFeedback != null) BindScoreFeedback(_scoreFeedback, _gameplayCamera);
        }

        private void Start()
        {
            // If in Endless mode, ensure DataPersistenceController has loaded before generating initial queue or board
            if (levelDefinition == null && LevelFlow.ActiveSession == null)
            {
                if (DataPersistenceController.instance != null && !DataPersistenceController.instance.HasLoaded)
                {
                    DataPersistenceController.instance.LoadGame();
                }
            }

            UpdateScoreUI();
            InitializeTowerQueue();

            if (state != GameState.lose && state != GameState.play)
            {
                state = GameState.start;
            }

            // Track attempts when running Endless mode (no campaign level definition or active level session)
            if (levelDefinition == null && LevelFlow.ActiveSession == null)
            {
                EndlessAttemptNumber = PlayerPrefs.GetInt("cityblocks_endless_attempt", 0) + 1;
                PlayerPrefs.SetInt("cityblocks_endless_attempt", EndlessAttemptNumber);
                PlayerPrefs.Save();
                _endlessRecordScoreAtStart = recordScore;
                _endlessRecordLevelAtStart = recordLevel;
                _endlessRecordReported = false;
                LevelAnalytics.EndlessStarted(EndlessAttemptNumber);

                // Save whenever the player enters endless mode
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.SaveGame();
                }
            }
        }

        private void Update()
        {
            if (state == GameState.play)
            {
                // Only evaluate game state transitions when all animations and physics drops have finished settling
                if (!moveResolving && grid.IsSettled())
                {
                    HandleLevelUpCheck();
                    HandleLossCheck();
                }
            }

            // Continuously cache the top two queue levels for save persistence
            if (state != GameState.lose && queue.towerQueue.Count >= 2)
            {
                firstInQueue = queue.towerQueue[0].GetComponent<TowerController>().level;
                nextInQueue = queue.towerQueue[1].GetComponent<TowerController>().level;
            }
        }

        /// <summary>
        /// Loads run and progression data from saved files.
        /// </summary>
        public void Load(GameSaveData gameSaveData, ProgressionData progressionData)
        {
            EndComboTurn();
            if (_scoreFeedback != null) _scoreFeedback.ClearAndSynchronize();
            recordScore = progressionData != null ? progressionData.recordScore : 0;
            recordLevel = progressionData != null ? progressionData.recordLevel : 0;
            if (gameSaveData != null)
            {
                if (gameSaveData.recordScore > recordScore) recordScore = gameSaveData.recordScore;
                if (gameSaveData.recordLevel > recordLevel) recordLevel = gameSaveData.recordLevel;
            }

            bool isEndless = levelDefinition == null && LevelFlow.ActiveSession == null;
            if (isEndless)
            {
                bool hasData = gameSaveData != null && (gameSaveData.hasActiveRun || gameSaveData.score > 0 || (gameSaveData.boardState != null && gameSaveData.boardState.Count > 0));
                if (hasData)
                {
                    hasActiveRun = true;
                    score = gameSaveData.score;
                    level = gameSaveData.level;
                    if (score > recordScore) recordScore = score;
                    if (level > recordLevel) recordLevel = level;

                    firstInQueue = gameSaveData.firstInQueue;
                    nextInQueue = gameSaveData.nextInQueue;
                    savedThirdInQueue = gameSaveData.thirdInQueue;
                    lookAheadTurnsRemaining = gameSaveData.lookAheadTurnsRemaining;
                    lookAheadUsed = gameSaveData.lookAheadUsed;

                    if (gameSaveData.isLost)
                    {
                        state = GameState.lose;
                    }

                    RebuildQueueFromSaved();
                    UpdateScoreUI();
                }
                else
                {
                    hasActiveRun = false;
                    score = 0;
                    level = 0;
                    firstInQueue = 0;
                    nextInQueue = 0;
                    savedThirdInQueue = 0;
                    lookAheadTurnsRemaining = 0;
                    lookAheadUsed = false;

                    RebuildQueueFromSaved();
                    UpdateScoreUI();
                }
            }
            else
            {
                if (gameSaveData != null)
                {
                    score = gameSaveData.score;
                    level = gameSaveData.level;
                    firstInQueue = gameSaveData.firstInQueue;
                    nextInQueue = gameSaveData.nextInQueue;
                }
            }
        }

        /// <summary>
        /// Populates save data structures before writing to disk.
        /// </summary>
        public void Save(ref GameSaveData gameSaveData, ref ProgressionData progressionData)
        {
            if (gameSaveData == null) gameSaveData = new GameSaveData();

            if (score > recordScore) recordScore = score;
            if (level > recordLevel) recordLevel = level;

            bool isEndless = levelDefinition == null && LevelFlow.ActiveSession == null;
            if (isEndless)
            {
                // ReferenceEquals: during scene teardown the GridController may already be destroyed,
                // but it still returns its last intact snapshot instead of an empty board.
                var capturedBoard = !ReferenceEquals(grid, null) ? grid.CaptureBoardState() : null;
                bool hasBoardPieces = capturedBoard != null && capturedBoard.Count > 0;
                bool shouldSaveRun = hasActiveRun && (score > 0 || level > 0 || hasBoardPieces);

                if (shouldSaveRun)
                {
                    gameSaveData.hasActiveRun = true;
                    gameSaveData.score = score;
                    gameSaveData.level = level;
                    gameSaveData.recordScore = recordScore;
                    gameSaveData.recordLevel = recordLevel;
                    gameSaveData.isLost = (state == GameState.lose);
                    gameSaveData.boardState = capturedBoard ?? new System.Collections.Generic.List<Vector3>();

                    gameSaveData.firstInQueue = (queue != null && queue.towerQueue.Count > 0 && queue.towerQueue[0] != null)
                        ? queue.towerQueue[0].GetComponent<TowerController>().level
                        : firstInQueue;
                    gameSaveData.nextInQueue = (queue != null && queue.towerQueue.Count > 1 && queue.towerQueue[1] != null)
                        ? queue.towerQueue[1].GetComponent<TowerController>().level
                        : nextInQueue;
                    gameSaveData.thirdInQueue = (queue != null && queue.towerQueue.Count > 2 && queue.towerQueue[2] != null)
                        ? queue.towerQueue[2].GetComponent<TowerController>().level
                        : savedThirdInQueue;
                    gameSaveData.lookAheadTurnsRemaining = lookAheadTurnsRemaining;
                    gameSaveData.lookAheadUsed = lookAheadUsed;
                }
                else
                {
                    gameSaveData.hasActiveRun = false;
                    gameSaveData.score = 0;
                    gameSaveData.level = 0;
                    gameSaveData.recordScore = recordScore;
                    gameSaveData.recordLevel = recordLevel;
                    gameSaveData.isLost = false;
                    gameSaveData.boardState = new System.Collections.Generic.List<Vector3>();
                    gameSaveData.firstInQueue = 0;
                    gameSaveData.nextInQueue = 0;
                    gameSaveData.thirdInQueue = 0;
                    gameSaveData.lookAheadTurnsRemaining = 0;
                    gameSaveData.lookAheadUsed = false;
                }
            }
            else
            {
                gameSaveData.score = score;
                gameSaveData.level = level;
                gameSaveData.recordScore = recordScore;
                gameSaveData.recordLevel = recordLevel;
                gameSaveData.firstInQueue = firstInQueue;
                gameSaveData.nextInQueue = nextInQueue;
            }

            if (progressionData != null)
            {
                progressionData.recordScore = recordScore;
                progressionData.recordLevel = recordLevel;
            }
        }

        /// <summary>
        /// Configures a campaign level with its definition and saved queue/booster state.
        /// </summary>
        public void ConfigureLevel(LevelDefinition definition, int nextQueueIndex, int thirdQueueLevel,
            int savedLookAheadTurns, bool savedLookAheadUsed)
        {
            levelDefinition = definition;
            queueIndex = Mathf.Max(0, nextQueueIndex);
            savedThirdInQueue = thirdQueueLevel;
            lookAheadTurnsRemaining = Mathf.Max(0, savedLookAheadTurns);
            lookAheadUsed = savedLookAheadUsed;
            if (queue != null)
            {
                queue.visibleCount = lookAheadTurnsRemaining > 0 ? 3 : 2;
            }
        }

        /// <summary>
        /// Retrieves the tower levels currently occupying the first 3 queue slots.
        /// </summary>
        public void GetQueueLevels(out int first, out int next, out int third)
        {
            first = queue.towerQueue.Count > 0 ? queue.towerQueue[0].GetComponent<TowerController>().level : 0;
            next = queue.towerQueue.Count > 1 ? queue.towerQueue[1].GetComponent<TowerController>().level : 0;
            third = queue.towerQueue.Count > 2 ? queue.towerQueue[2].GetComponent<TowerController>().level : 0;
        }

        /// <summary>
        /// Attempts to enter power-up mode for the specified booster.
        /// </summary>
        private bool CloudPowerUpAvailable(PowerUpType type)
        {
            if (PowerUpStore.CanUse) return true;
            if (!CloudServices.ConnectivityService.IsOnline)
                LevelAnalytics.BoosterBlockedOffline(LevelAnalytics.BoosterId(type), LevelFlow.ActiveSession != null ? LevelFlow.ActiveSession.Definition.levelNumber : level, LevelFlow.ActiveSession == null);
            return false;
        }

        public bool TryBeginPowerUp(PowerUpType type)
        {
            if (!CloudPowerUpAvailable(type)) return false;
            if (state != GameState.play || moveResolving || powerUpRequestPending || HasPendingComboCelebration || PowerUpStore.Uses(type) <= 0) return false;

            if (type == PowerUpType.lookAhead)
            {
                return ActivateLookAhead();
            }

            powerUp = type;
            state = GameState.powerUp;
            return true;
        }

        /// <summary>
        /// Activates the Look Ahead power-up to display 3 upcoming pieces for 3 turns.
        /// </summary>
        public bool ActivateLookAhead()
        {
            if (!CloudPowerUpAvailable(PowerUpType.lookAhead) || powerUpRequestPending) return false;
            if (state != GameState.play || moveResolving || HasPendingComboCelebration || lookAheadTurnsRemaining > 0) return false;
            if (levelDefinition != null && levelDefinition.moveLimit > 0 && LevelFlow.ActiveSession != null &&
                LevelFlow.ActiveSession.MovesRemaining <= 0) return false;
            if (PowerUpStore.Uses(PowerUpType.lookAhead) <= 0) return false;
            _ = ApplyLookAheadAsync();
            return true;
        }

        private string PowerUpContextId => LevelFlow.ActiveSession != null ? LevelFlow.ActiveSession.AttemptId : "endless:" + EndlessAttemptNumber;

        private async System.Threading.Tasks.Task ApplyLookAheadAsync()
        {
            powerUpRequestPending = true;
            string requestContext = PowerUpContextId;
            try
            {
                if (!await PowerUpStore.TryUseAsync(PowerUpType.lookAhead, requestContext)) return;
                if (this == null || state != GameState.play || PowerUpContextId != requestContext) return;
                powerUp = PowerUpType.lookAhead;
                RecordBoosterUse();
                lookAheadUsed = true;
                lookAheadTurnsRemaining = 3;
                queue.visibleCount = 3;
                while (queue.towerQueue.Count < 3) AddTowerToQueue(GetNextQueueLevel());
                LevelFlow.ActiveSession?.SaveRun();
            }
            finally { powerUpRequestPending = false; }
        }

        /// <summary>
        /// Called when a grid cell is tapped/clicked.
        /// Routes action depending on whether the game is in normal play or targeting a power-up.
        /// </summary>
        public void GetCellHit(int col, int row)
        {
            if (powerUpRequestPending) return;
            if (!grid.IsAvailable(col, row)) return;

            if (state == GameState.play)
            {
                HandlePlayCellHit(col);
            }
            else if (state == GameState.powerUp)
            {
                HandlePowerUpCellHit(col, row);
            }
        }

        /// <summary>
        /// Handles tower placement when a player taps a column in normal play mode.
        /// </summary>
        private void HandlePlayCellHit(int col)
        {
            // Do not allow moves if moves are exhausted in a level mode
            if (levelDefinition != null && LevelFlow.ActiveSession != null && levelDefinition.moveLimit > 0 &&
                LevelFlow.ActiveSession.MovesRemaining <= 0) return;

            if (moveResolving || HasPendingComboCelebration || !grid.IsSettled() || queue.towerQueue.Count == 0) return;

            if (PlacementFilter != null && !PlacementFilter(col)) return;

            int topRow = grid.TopPlayableRow(col);
            if (topRow < 0) return;

            // If the column top is blocked by an obstacle, placement is invalid
            if (grid.HasObstacle(col, topRow)) return;

            GameObject incomingTower = GetTowerFromQueue();
            int matchStatus = CheckForValidColumn(grid.grid[col, topRow], incomingTower);

            if (matchStatus == 0) // Empty column
            {
                PlayTower(col, incomingTower, isTopOfColumn: false);
            }
            else if (matchStatus == 1) // Matches top tower level -> merge
            {
                PlayTower(col, incomingTower, isTopOfColumn: true);
            }
            // matchStatus == 2 means mismatched levels and cannot place/merge
        }

        /// <summary>
        /// Handles applying the active power-up to the selected cell.
        /// </summary>
        private async void HandlePowerUpCellHit(int col, int row)
        {
            if (powerUpRequestPending || grid.towersInPlay.Count > 0) return;
            PowerUpType selected = powerUp;
            if ((selected == PowerUpType.delete || selected == PowerUpType.levelUp) && grid.grid[col, row] == null) return;
            if (selected == PowerUpType.extraTurns && LevelFlow.ActiveSession == null) return;
            powerUpRequestPending = true;
            string requestContext = PowerUpContextId;
            try
            {
                if (!await PowerUpStore.TryUseAsync(selected, requestContext))
                {
                    if (this != null && PowerUpContextId == requestContext)
                    {
                        if (playPanel != null) UI.UIPanelAnimator.SetAnimatedActive(playPanel, true);
                        if (powerUpPanel_Delete != null) UI.UIPanelAnimator.SetAnimatedActive(powerUpPanel_Delete, false);
                        if (powerUpPanel_LevelUp != null) UI.UIPanelAnimator.SetAnimatedActive(powerUpPanel_LevelUp, false);
                        state = GameState.play;
                    }
                    return;
                }
                if (this == null || state != GameState.powerUp || powerUp != selected || PowerUpContextId != requestContext) return;
                switch (selected)
                {
                    case PowerUpType.delete: ApplyDeletePowerUp(col, row); break;
                    case PowerUpType.levelUp: ApplyLevelUpPowerUp(col, row); break;
                    case PowerUpType.extraTurns: ApplyExtraTurnsPowerUp(); break;
                }
                onPowerUpUsed?.Invoke(selected);
                state = GameState.play;
            }
            finally { powerUpRequestPending = false; }
        }

        private void ApplyDeletePowerUp(int col, int row)
        {
            if (grid.grid[col, row] == null) return;

            BeginComboTurn(false);
            moveResolving = true;
            RecordBoosterUse();
            GameObject removed= grid.grid[col, row];
            grid.grid[col, row] = null;
            Destroy(removed);
            grid.ResolveGravity(col);

            if (playPanel != null) UI.UIPanelAnimator.SetAnimatedActive(playPanel, true);
            if (powerUpPanel_Delete != null) UI.UIPanelAnimator.SetAnimatedActive(powerUpPanel_Delete, false);

            StartCoroutine(SaveLevelAfterMove());
        }

        private void ApplyLevelUpPowerUp(int col, int row)
        {
            if (grid.grid[col, row] == null) return;

            BeginComboTurn(false);
            moveResolving = true;
            RecordBoosterUse();
            TowerController towerCtrl= grid.grid[col, row].GetComponent<TowerController>();
            towerCtrl.level++;
            towerCtrl.merge = true;
            grid.DamageAdjacentBreakables(col, row);

            if (playPanel != null) UI.UIPanelAnimator.SetAnimatedActive(playPanel, true);
            if (powerUpPanel_LevelUp != null) UI.UIPanelAnimator.SetAnimatedActive(powerUpPanel_LevelUp, false);

            StartCoroutine(SaveLevelAfterMove());
        }

        private void ApplyExtraTurnsPowerUp()
        {
            if (LevelFlow.ActiveSession == null) return;

            RecordBoosterUse();
            LevelFlow.ActiveSession.GrantExtraTurns(3);

            if (playPanel != null) UI.UIPanelAnimator.SetAnimatedActive(playPanel, true);
        }

        /// <summary>
        /// Places a tower into the top of a column and initiates its drop/merge sequence.
        /// </summary>
        private void PlayTower(int column, GameObject incomingTower, bool isTopOfColumn)
        {
            BeginComboTurn(true);
            TotalMoves++;
            moveResolving = true;
            hasActiveRun = true;

            // Remove tower from queue head
            queue.towerQueue.Remove(incomingTower);

            TowerController towerCtrl = incomingTower.GetComponent<TowerController>();

            TowerController topMatch = isTopOfColumn
                ? grid.grid[column, grid.TopPlayableRow(column)].GetComponent<TowerController>() : null;

            if (levelDefinition != null) IncrementLevel(towerCtrl.level);

            // Keep temporary logical entry occupancy; presentation enters from the near board edge.
            int entryRow = grid.totalRows - 1;
            grid.grid[column, entryRow] = incomingTower;
            towerCtrl.col = column;
            towerCtrl.row = entryRow;
            towerCtrl.state = TowerController.TowerState.grid;
            towerCtrl.BeginPlacement(topMatch);

            // Replenish queue
            AddTowerToQueue(GetNextQueueLevel());

            if (LevelFlow.ActiveSession != null)
            {
                LevelFlow.ActiveSession.RecordMove();
            }

            // Decrement active look-ahead turns if running
            if (lookAheadTurnsRemaining > 0)
            {
                lookAheadTurnsRemaining--;
                if (lookAheadTurnsRemaining == 0)
                {
                    queue.visibleCount = 2;
                }
            }

            // The tower now occupying the last visible slot was hidden before the advance; it grows upward into place
            // while the others slide forward. TowerController only uses the flag on a hidden -> visible transition.
            int lastVisibleIndex = Mathf.Min(queue.visibleCount, queue.towerQueue.Count) - 1;
            if (lastVisibleIndex >= 0 && queue.towerQueue[lastVisibleIndex] != null)
                queue.towerQueue[lastVisibleIndex].GetComponent<TowerController>().growOnQueueEntry = true;

            onTowerPlaced?.Invoke(column);

            StartCoroutine(SaveLevelAfterMove());
        }

        /// <summary>
        /// Waits for grid pieces to stop falling before finalizing the move and evaluating game rules.
        /// </summary>
        private IEnumerator SaveLevelAfterMove()
        {
            yield return null;
            yield return new WaitUntil(grid.IsSettled);
            CompleteComboTurnAfterSettlement();
            moveResolving = false;
            onMoveResolved?.Invoke();

            if (LevelFlow.ActiveSession != null)
            {
                LevelFlow.ActiveSession.ResolveMove(CheckLoseCondition_FullGrid());
            }
            else
            {
                if (CheckLoseCondition_FullGrid())
                {
                    HandleLossCheck();
                }
                else
                {
                    HandleLevelUpCheck();
                }

                // Endless Mode: save after every move once grid is settled
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.SaveGame();
                }
            }
        }

        /// <summary>
        /// Evaluates whether the grid has no playable moves remaining for the next tower in queue.
        /// </summary>
        private bool CheckLoseCondition_FullGrid()
        {
            if (queue.towerQueue.Count == 0) return false;

            int nextLevel = GetTowerFromQueue().GetComponent<TowerController>().level;
            int playableColumns = 0;

            for (int column = 0; column < grid.totalColumns; column++)
            {
                int topRow = grid.TopPlayableRow(column);
                if (topRow < 0) continue;

                playableColumns++;
                if (grid.HasObstacle(column, topRow)) continue;

                GameObject topTower = grid.grid[column, topRow];
                // A move is valid if the top space is empty or matching in level
                if (topTower == null || topTower.GetComponent<TowerController>().level == nextLevel)
                {
                    return false;
                }
            }

            return playableColumns > 0;
        }

        /// <summary>
        /// Checks if a tower can be placed in a column:
        /// 0 = slot is empty, 1 = levels match (valid merge), 2 = levels differ or invalid.
        /// </summary>
        private int CheckForValidColumn(GameObject topOfColumn, GameObject towerToCheck)
        {
            if (topOfColumn == null) return 0;
            if (topOfColumn.GetComponent<TowerController>().level == towerToCheck.GetComponent<TowerController>().level) return 1;
            return 2;
        }

        /// <summary>
        /// Returns the front tower in the queue without dequeuing it.
        /// </summary>
        private GameObject GetTowerFromQueue()
        {
            return queue.towerQueue[0];
        }

        /// <summary>
        /// Instantiates a new tower with the given level and adds it to the queue.
        /// </summary>
        private void AddTowerToQueue(int towerLevel)
        {
            // Reroll invalid levels before creating the queued tower.
            while (towerLevel < 1)
            {
                towerLevel = GetNextQueueLevel();
            }

            Vector3 towerSpawnPosition = new Vector3(queue.transform.position.x - 5, queue.transform.position.y, queue.transform.position.z);
            GameObject newTower = Instantiate(tower, towerSpawnPosition, Quaternion.identity);

            TowerController towerCtrl = newTower.GetComponent<TowerController>();
            towerCtrl.state = TowerController.TowerState.queue;
            towerCtrl.queue = queue;
            towerCtrl.grid = grid;
            towerCtrl.onTowerLevelUpEvent += IncrementLevel;
            towerCtrl.level = towerLevel;

            queue.towerQueue.Add(newTower);
        }

        /// <summary>
        /// Determines the level for the next spawned tower from the level definition or random endless range.
        /// </summary>
        private int GetNextQueueLevel()
        {
            if (levelDefinition == null) return GetTowerSpawnLevelRange();

            QueueEntry entry = queueIndex < levelDefinition.queue.Count ? levelDefinition.queue[queueIndex] : null;
            queueIndex++;

            if (entry == null) return Random.Range(1, Mathf.Max(2, levelDefinition.randomMaximumLevel + 1));

            switch (entry.type)
            {
                case QueueEntryType.Exact:
                    return entry.exactLevel;
                case QueueEntryType.RandomRange:
                    return Random.Range(entry.minimumLevel, entry.maximumLevel + 1);
                default:
                    return Random.Range(1, Mathf.Max(2, levelDefinition.randomMaximumLevel + 1));
            }
        }

        /// <summary>
        /// Generates a dynamic level range for spawns in Endless mode based on current progress.
        /// </summary>
        internal static void GetEndlessSpawnBounds(int highestLevel, out int minimumLevel, out int maximumLevelExclusive)
        {
            maximumLevelExclusive = highestLevel > 10 ? highestLevel - 5 : 6;
            minimumLevel = highestLevel > 10
                ? maximumLevelExclusive - Mathf.RoundToInt(highestLevel / 2f)
                : 1;
        }

        public int MinimumEndlessSpawnLevel
        {
            get
            {
                GetEndlessSpawnBounds(level, out int minimumLevel, out _);
                return Mathf.Max(1, minimumLevel);
            }
        }

        private int GetTowerSpawnLevelRange()
        {
            GetEndlessSpawnBounds(level, out int minimumLevel, out int maximumLevelExclusive);
            return Random.Range(minimumLevel, maximumLevelExclusive);
        }

        /// <summary>
        /// Increases score based on merged tower level and updates records and UI.
        /// </summary>
        public event System.Action<int, int, int, Vector3, int> onMergeScoreAwarded;

        public void AwardMergeScore(int towerLevel, Vector3 worldPosition)
        {
            if (_scoreFeedback != null) _scoreFeedback.CaptureRecordBaseline();
            int multiplier = _comboTurnActive ? ++_turnMergeCount : 1;
            int basePoints = (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, (long)towerLevel * 10));
            long desired = (long)basePoints * multiplier;
            int points = (int)System.Math.Min(desired, System.Math.Max(0L, (long)int.MaxValue - score));
            score += points;
            if (score > recordScore) recordScore = score;
            onMergeScoreAwarded?.Invoke(basePoints, multiplier, points, worldPosition, _comboTurnId);
            UpdateScoreUI();
        }

        public void IncrementScore(int towerLevel)
        {
            score += towerLevel * 10;
            if (score > recordScore) recordScore = score;
            UpdateScoreUI();
        }

        /// <summary>
        /// Updates the current and record level if a higher tower was reached.
        /// </summary>
        public void IncrementLevel(int towerLevel)
        {
            if (towerLevel > level)
            {
                level = towerLevel;

                if (level > 6 && levelDefinition == null)
                {
                    prepLevelUpPanel = true;
                }
            }

            if (level > recordLevel)
            {
                recordLevel = level;
            }
        }

        /// <summary>
        /// Clears all game values back to zero on loss or run restart.
        /// </summary>
        public void ResetValuesOnLoss()
        {
            EndComboTurn();
            hasActiveRun = false;
            score = 0;
            level = 0;
            firstInQueue = 0;
            nextInQueue = 0;
            savedThirdInQueue = 0;
            lookAheadTurnsRemaining = 0;
            lookAheadUsed = false;
            state = GameState.start;
            if (grid != null)
            {
                grid.ClearBoard();
            }
            if (queue != null) queue.visibleCount = 2;
        }

        public void RebuildQueueFromSaved()
        {
            if (queue == null) return;

            for (int i = queue.towerQueue.Count - 1; i >= 0; i--)
            {
                if (queue.towerQueue[i] != null)
                {
                    Destroy(queue.towerQueue[i]);
                }
            }
            queue.towerQueue.Clear();

            if (firstInQueue > 0 && nextInQueue > 0)
            {
                AddTowerToQueue(firstInQueue);
                AddTowerToQueue(nextInQueue);
            }
            else
            {
                AddTowerToQueue(GetNextQueueLevel());
                AddTowerToQueue(GetNextQueueLevel());
            }

            AddTowerToQueue(savedThirdInQueue > 0 ? savedThirdInQueue : GetNextQueueLevel());
            queue.visibleCount = lookAheadTurnsRemaining > 0 ? 3 : 2;
        }

        private void InitializeTowerQueue()
        {
            if (queue == null || queue.towerQueue.Count >= 2) return;

            if (firstInQueue > 0 && nextInQueue > 0)
            {
                AddTowerToQueue(firstInQueue);
                AddTowerToQueue(nextInQueue);
            }
            else
            {
                AddTowerToQueue(GetNextQueueLevel());
                AddTowerToQueue(GetNextQueueLevel());
            }

            AddTowerToQueue(savedThirdInQueue > 0 ? savedThirdInQueue : GetNextQueueLevel());
        }

        private void HandleLevelUpCheck()
        {
            if (HasPendingScoreFeedback) return;
            if (prepLevelUpPanel)
            {
                prepLevelUpPanel = false;
                if (levelUpPanel != null) UI.UIPanelAnimator.SetAnimatedActive(levelUpPanel, true);
                if (playPanel != null) UI.UIPanelAnimator.SetAnimatedActive(playPanel, false);
                state = GameState.levelUp;
            }
        }

        private void HandleLossCheck()
        {
            bool hasActiveSession = LevelFlow.ActiveSession != null;
            bool outOfMoves = hasActiveSession &&
                              LevelFlow.ActiveSession.Definition.moveLimit > 0 &&
                              LevelFlow.ActiveSession.MovesRemaining <= 0;

            if (outOfMoves)
            {
                LevelFlow.ActiveSession.ResolveMove(CheckLoseCondition_FullGrid());
            }
            else if (CheckLoseCondition_FullGrid())
            {
                if (hasActiveSession)
                {
                    LevelFlow.ActiveSession.ResolveMove(true);
                }
                else
                {
                    state = GameState.lose;

                    LevelRulesUI rulesUI = LevelRulesUI.Instance ?? FindAnyObjectByType<LevelRulesUI>();
                    if (rulesUI != null)
                    {
                        rulesUI.ShowLoss();
                    }
                    else
                    {
                        if (losePanel != null) UI.UIPanelAnimator.SetAnimatedActive(losePanel, true);
                        if (playPanel != null) UI.UIPanelAnimator.SetAnimatedActive(playPanel, false);
                    }

                    if (levelDefinition == null && !endlessFinished)
                    {
                        endlessFinished = true;
                        LevelAnalytics.EndlessFailed(level, EndlessAttemptNumber, TotalMoves);
                        ReportEndlessRecordIfBeaten();
                    }

                    if (DataPersistenceController.instance != null)
                    {
                        DataPersistenceController.instance.SaveGame();
                    }
                }
            }
        }

        private void UpdateScoreUI()
        {
            if (scoreText != null) scoreText.text = DisplayedScore.ToString();
            if (recordScoreText != null) recordScoreText.text = DisplayedRecordScore.ToString();
        }

        private void RecordBoosterUse()
        {
            string id = LevelAnalytics.BoosterId(powerUp);
            bool endless = LevelFlow.ActiveSession == null;
            int currentLevel = endless ? level : LevelFlow.ActiveSession.Definition.levelNumber;
            int attempt = endless ? EndlessAttemptNumber : LevelFlow.ActiveSession.AttemptNumber;
            string attemptId = endless ? "" : LevelFlow.ActiveSession.AttemptId;
            // Called right after PowerUpStore.TryUse, so this is the balance after the use.
            LevelAnalytics.BoosterUsed(id, currentLevel, attempt, endless, attemptId, PowerUpStore.Uses(powerUp));
        }
    }
}

