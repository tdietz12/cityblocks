using System.Collections;
using Data_Persistence;
using TMPro;
using UnityEngine;

namespace Gameplay
{
    public class GameController : MonoBehaviour, DataPersistenceInterface
    {
        //Creates a singleton (only one per scene), get the instace publicly, can only be set privately
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
            deleteRow,
            levelUp,
            extraTurns
        }
        public PowerUpType powerUp;

        public GameObject tower;
        public QueueController queue;
        public GridController grid;

        public GameObject playPanel;
        public GameObject levelUpPanel;
        public GameObject powerUpPanel_Delete;
        public GameObject powerUpPanel_DeleteRow;
        public GameObject powerUpPanel_LevelUp;
        public GameObject losePanel;

        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI recordScoreText;

        public int score;
        public int level;
        private int firstInQueue;
        private int nextInQueue;
        private LevelDefinition levelDefinition;
        private int queueIndex;
        public int QueueIndex => queueIndex;
        public bool QueueReady => queue != null && queue.towerQueue.Count >= 2;
        public bool IsResolvingMove => moveResolving;
        private int savedThirdInQueue;
        private int previewTurnsRemaining;
        private bool previewUsed;
        public int PreviewTurnsRemaining => previewTurnsRemaining;
        public bool PreviewUsed => previewUsed;
        public int TotalMoves { get; private set; }
        public int EndlessAttemptNumber { get; private set; }
        private bool endlessFinished;

        public bool TryBeginPowerUp(PowerUpType type)
        {
            if (state != GameState.play || moveResolving || PowerUpStore.Uses(type) <= 0) return false;
            powerUp = type;
            state = GameState.powerUp;
            return true;
        }

        public int recordScore;
        public int recordLevel;

        private bool prepLevelUpPanel = false;
        private bool moveResolving;

        void Awake()
        {
            if (instance != null)
            {
                Debug.LogError("Found more than one GameObject with this class in the scene.");
            }
            instance = this;
        }

        public void Load(GameSaveData gameSaveData, ProgressionData progressionData)
        {
            score = gameSaveData.score;
            level = gameSaveData.level;
            firstInQueue = gameSaveData.firstInQueue;
            nextInQueue = gameSaveData.nextInQueue;

            recordScore = progressionData.recordScore;
            recordLevel = progressionData.recordLevel;
        }

        public void ConfigureLevel(LevelDefinition definition, int nextQueueIndex, int thirdQueueLevel,
            int savedPreviewTurns, bool savedPreviewUsed)
        {
            levelDefinition = definition;
            queueIndex = Mathf.Max(0, nextQueueIndex);
            savedThirdInQueue = thirdQueueLevel;
            previewTurnsRemaining = Mathf.Max(0, savedPreviewTurns);
            previewUsed = savedPreviewUsed;
            queue.visibleCount = previewTurnsRemaining > 0 ? 3 : 2;
        }

        public void GetQueueLevels(out int first, out int next, out int third)
        {
            first = queue.towerQueue.Count > 0 ? queue.towerQueue[0].GetComponent<TowerController>().level : 0;
            next = queue.towerQueue.Count > 1 ? queue.towerQueue[1].GetComponent<TowerController>().level : 0;
            third = queue.towerQueue.Count > 2 ? queue.towerQueue[2].GetComponent<TowerController>().level : 0;
        }

        public bool ActivatePreview()
        {
            if (levelDefinition == null || previewUsed || state != GameState.play || moveResolving) return false;
            if (levelDefinition.moveLimit > 0 && LevelFlow.ActiveSession != null &&
                LevelFlow.ActiveSession.MovesRemaining <= 0) return false;
            previewUsed = true;
            previewTurnsRemaining = 3;
            queue.visibleCount = 3;
            LevelFlow.ActiveSession?.SaveRun();
            return true;
        }

        public void Save(ref GameSaveData gameSaveData, ref ProgressionData progressionData)
        {
            gameSaveData.score = score;
            gameSaveData.level = level;
            gameSaveData.firstInQueue = firstInQueue;
            gameSaveData.nextInQueue = nextInQueue;

            progressionData.recordScore = recordScore;
            progressionData.recordLevel = recordLevel;
        }

        void Start()
        {
            scoreText.text = score.ToString();
            recordScoreText.text = recordScore.ToString();

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
            if (levelDefinition != null)
                AddTowerToQueue(savedThirdInQueue > 0 ? savedThirdInQueue : GetNextQueueLevel());

            state = GameState.start;
            if (levelDefinition == null && LevelFlow.ActiveSession == null)
            {
                EndlessAttemptNumber = PlayerPrefs.GetInt("cityblocks_endless_attempt", 0) + 1;
                PlayerPrefs.SetInt("cityblocks_endless_attempt", EndlessAttemptNumber);
                PlayerPrefs.Save();
                LevelAnalytics.EndlessStarted(EndlessAttemptNumber);
            }
        }

        // Update is called once per frame, covers all gameplay interaction
        void Update()
        {
            if (state == GameState.play)
            {
                //Actions that can only occur while towers are not in play
                if (!moveResolving && grid.IsSettled())
                {
                    //Check for level up call
                    if (prepLevelUpPanel == true)
                    {
                        prepLevelUpPanel = false;
                        levelUpPanel.SetActive(true);
                        playPanel.SetActive(false);
                        state = GameState.levelUp;
                    }

                    //Check for lose conditions when not in play
                    if (LevelFlow.ActiveSession != null && LevelFlow.ActiveSession.Definition.moveLimit > 0 &&
                        LevelFlow.ActiveSession.MovesRemaining <= 0)
                    {
                        LevelFlow.ActiveSession.ResolveMove(CheckLoseCondition_FullGrid());
                    }
                    else if (CheckLoseCondition_FullGrid())
                    {
                        if (LevelFlow.ActiveSession != null) LevelFlow.ActiveSession.ResolveMove(true);
                        else
                        {
                            losePanel.SetActive(true);
                            playPanel.SetActive(false);
                            state = GameState.lose;
                            if (levelDefinition == null && !endlessFinished)
                            {
                                endlessFinished = true;
                                LevelAnalytics.EndlessFailed(level, EndlessAttemptNumber, TotalMoves);
                            }
                        }
                    }
                }
            }

            if (state != GameState.lose && queue.towerQueue.Count >= 2)
            {
                firstInQueue = queue.towerQueue[0].GetComponent<TowerController>().level;
                nextInQueue = queue.towerQueue[1].GetComponent<TowerController>().level;
            }
        }

        public void GetCellHit(int col, int row)
        {
            if (!grid.IsAvailable(col, row)) return;

            if (state == GameState.play)
            {
                if (levelDefinition != null && LevelFlow.ActiveSession != null && levelDefinition.moveLimit > 0 &&
                    LevelFlow.ActiveSession.MovesRemaining <= 0) return;
                if (!moveResolving && grid.IsSettled() && queue.towerQueue.Count > 0)
                {
                    int topRow = grid.TopPlayableRow(col);
                    if (topRow < 0) return;
                    switch (grid.HasObstacle(col, topRow) ? 2 :
                                CheckForValidColumn(grid.grid[col, topRow], GetTowerFromQueue()))
                    {
                        case 0:
                            PlayTower(col, GetTowerFromQueue(), false);
                            break;
                        case 1:
                            PlayTower(col, GetTowerFromQueue(), true);
                            break;
                        case 2:
                            break;
                    }
                }
            }
            if (state == GameState.powerUp)
            {
                if (grid.towersInPlay.Count == 0)
                {
                    if (powerUp == PowerUpType.delete)
                    {
                        if (grid.grid[col, row] == null || !PowerUpStore.TryUse(powerUp)) return;
                        RecordBoosterUse();
                        GameObject removed = grid.grid[col, row];
                        grid.grid[col, row] = null;
                        Destroy(removed);
                        grid.ResolveGravity(col);
                        playPanel.SetActive(true);
                        powerUpPanel_Delete.SetActive(false);
                    }

                    if (powerUp == PowerUpType.deleteRow)
                    {
                        bool hasTower = false;
                        for (int column = 0; column < grid.totalColumns; column++)
                            if (grid.grid[column, row] != null) { hasTower = true; break; }
                        if (!hasTower || !PowerUpStore.TryUse(powerUp)) return;
                        RecordBoosterUse();
                        for (int columnInThisRow = 0; columnInThisRow < grid.totalColumns; columnInThisRow++)
                        {
                            if (grid.grid[columnInThisRow, row] != null)
                            {
                                GameObject removed = grid.grid[columnInThisRow, row];
                                grid.grid[columnInThisRow, row] = null;
                                Destroy(removed);
                            }

                            grid.ResolveGravity(columnInThisRow);
                        }
                        playPanel.SetActive(true);
                        powerUpPanel_DeleteRow.SetActive(false);
                    }

                    if (powerUp == PowerUpType.levelUp)
                    {
                        if (grid.grid[col, row] == null || !PowerUpStore.TryUse(powerUp)) return;
                        RecordBoosterUse();
                        grid.grid[col, row].GetComponent<TowerController>().level++;
                        grid.grid[col, row].GetComponent<TowerController>().merge = true;
                        grid.DamageAdjacentBreakables(col, row);
                        playPanel.SetActive(true);
                        powerUpPanel_LevelUp.SetActive(false);
                    }

                    if (powerUp == PowerUpType.extraTurns)
                    {
                        if (LevelFlow.ActiveSession == null || !PowerUpStore.TryUse(powerUp)) return;
                        RecordBoosterUse();
                        LevelFlow.ActiveSession.GrantExtraTurns(3);
                        playPanel.SetActive(true);
                    }
                }

                state = GameState.play;
            }
        }

        bool CheckLoseCondition_FullGrid()
        {
            if (queue.towerQueue.Count == 0) return false;
            int nextLevel = GetTowerFromQueue().GetComponent<TowerController>().level;
            int playableColumns = 0;
            for (int column = 0; column < grid.totalColumns; column++)
            {
                int topRow = grid.TopPlayableRow(column);
                if (topRow < 0) continue;
                playableColumns++;
                GameObject top = grid.grid[column, topRow];
                if (grid.HasObstacle(column, topRow)) continue;
                if (top == null || top.GetComponent<TowerController>().level == nextLevel)
                    return false;
            }
            return playableColumns > 0;
        }

        int CheckForValidColumn(GameObject topOfColumn, GameObject tower)
        {
            if (topOfColumn == null)
            {
                return 0;
            }
            else if (topOfColumn.GetComponent<TowerController>().level == tower.GetComponent<TowerController>().level)
            {
                return 1;
            }
            return 2;
        }

        GameObject GetTowerFromQueue()
        {
            GameObject obj = queue.towerQueue[0];
            return obj;
        }

        private void RecordBoosterUse()
        {
            string id = powerUp == PowerUpType.deleteRow ? "delete_row" : powerUp.ToString();
            bool endless = LevelFlow.ActiveSession == null;
            int currentLevel = endless ? level : LevelFlow.ActiveSession.Definition.levelNumber;
            int attempt = endless ? EndlessAttemptNumber : LevelFlow.ActiveSession.AttemptNumber;
            LevelAnalytics.BoosterUsed(id, currentLevel, attempt, endless);
        }

        void PlayTower(int column, GameObject tower, bool isTopOfColumn)
        {
            TotalMoves++;
            moveResolving = true;
            //Remove tower from front of queue
            queue.towerQueue.Remove(tower);

            //Is the top of the column full?
            if (isTopOfColumn)
            {
                //Empty the target slot at the top of the column
                int topRow = grid.TopPlayableRow(column);
                GameObject topOfColumn = grid.grid[column, topRow];
                grid.grid[column, topRow] = null;
                Destroy(topOfColumn);
                tower.GetComponent<TowerController>().level++;
                IncrementScore(tower.GetComponent<TowerController>().level);
                // This placement levels up against the tower at topRow, so adjacent
                // breakable obstacles at that same cell must take the damage.
                grid.DamageAdjacentBreakables(column, topRow);
                grid.ResolveGravity(column);
            }

            if (levelDefinition != null)
                IncrementLevel(tower.GetComponent<TowerController>().level);
            else if (isTopOfColumn)
                IncrementLevel(tower.GetComponent<TowerController>().level);

            //Put this tower into its grid slot
            grid.grid[column, grid.totalRows - 1] = tower;

            //Set the tower's starting column and row values
            tower.GetComponent<TowerController>().col = column;
            tower.GetComponent<TowerController>().row = grid.totalRows - 1;
            //Set the tower in "grid" state
            tower.GetComponent<TowerController>().state = TowerController.TowerState.grid;

            //Set the start positionof the tower at the top of the column
            tower.transform.position = grid.WorldPosition(column, grid.totalRows - 1);

            //Run the "Drop" function on this new tower
            tower.GetComponent<TowerController>().drop = true;

            //Enqueue a new object
            AddTowerToQueue(GetNextQueueLevel());
            if (LevelFlow.ActiveSession != null)
            {
                LevelFlow.ActiveSession.RecordMove();
                if (previewTurnsRemaining > 0)
                {
                    previewTurnsRemaining--;
                    if (previewTurnsRemaining == 0) queue.visibleCount = 2;
                }
            }
            StartCoroutine(SaveLevelAfterMove());
        }

        private IEnumerator SaveLevelAfterMove()
        {
            yield return null;
            yield return new WaitUntil(grid.IsSettled);
            moveResolving = false;
            if (LevelFlow.ActiveSession != null)
                LevelFlow.ActiveSession.ResolveMove(CheckLoseCondition_FullGrid());
        }

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

        void AddTowerToQueue(int level)
        {
            Vector3 towerSpawnPosition = new Vector3(queue.transform.position.x - 5, queue.transform.position.y, queue.transform.position.z);
            GameObject newTower = Instantiate(tower, towerSpawnPosition, Quaternion.identity);
            newTower.GetComponent<TowerController>().state = TowerController.TowerState.queue;
            newTower.GetComponent<TowerController>().queue = queue;
            newTower.GetComponent<TowerController>().grid = grid;
            newTower.GetComponent<TowerController>().onTowerLevelUpEvent += IncrementScore;
            newTower.GetComponent<TowerController>().onTowerLevelUpEvent += IncrementLevel;
            newTower.GetComponent<TowerController>().level = level;
            queue.towerQueue.Add(newTower);
        }

        public void IncrementScore(int towerLevel)
        {
            score += towerLevel * 10;

            if (score > recordScore)
            {
                recordScore = score;
            }

            scoreText.text = score.ToString();
            recordScoreText.text = recordScore.ToString();
        }

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

        int GetTowerSpawnLevelRange()
        {
            int towerLevelMin;
            int towerLevelMax;

            if (level > 10)
            {
                towerLevelMax = level - 5;
                towerLevelMin = towerLevelMax - (Mathf.RoundToInt(level / 2));
            }
            else
            {
                towerLevelMax = 6;
                towerLevelMin = 1;
            }

            return Random.Range(towerLevelMin, towerLevelMax);
        }

        //TO FIX BUG WITH LOSE STATE, MAYBE MOVE RECORD SCORE AND LEVEL TO PROGRESSION DATA, AND DELETE GAME SAVE DATA ENTIRELY INSTEAD?

        public void ResetValuesOnLoss()
        {
            score = 0;
            level = 0;
            firstInQueue = 0;
            nextInQueue = 0;
        }
    }
}

