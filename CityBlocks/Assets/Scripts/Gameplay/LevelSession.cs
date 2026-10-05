using System.Collections.Generic;
using Data_Persistence;
using UI;
using UnityEngine;

namespace Gameplay
{
    public class LevelSession : MonoBehaviour
    {
        public LevelDefinition Definition { get; private set; }
        public LevelProgressData Progress { get; private set; }
        public int MovesRemaining { get; private set; }
        public int AttemptNumber { get; private set; }
        private GameController game;
        private LevelRulesUI rulesUI;
        private bool finished;
        private bool initialized;
        private bool awaitingFailureDecision;
        private bool discardRunOnExit;
        private string pendingFailedObjective;

        public bool AwaitingFailureDecision => awaitingFailureDecision;
        public string PendingFailedObjective => pendingFailedObjective;

        public void Initialize(LevelDefinition definition, GameController gameController)
        {
            if (definition == null) return;
            Definition = definition;
            game = gameController;
            Progress = LevelProgressStore.Load();

            LevelRunData run = Progress.activeRun;
            bool resume = run != null && run.levelNumber == definition.levelNumber;
            if (!resume)
            {
                run = new LevelRunData
                {
                    levelNumber = definition.levelNumber,
                    attemptNumber = Progress.BeginAttempt(definition.levelNumber),
                    movesRemaining = definition.moveLimit,
                    board = new List<Vector3>(),
                    obstacleStateInitialized = true,
                    obstacles = new List<ObstacleSaveData>()
                };
                foreach (StartingBlock block in definition.startingBlocks)
                {
                    run.board.Add(new Vector3(block.column, block.row, block.towerLevel));
                    run.highestTowerLevel = Mathf.Max(run.highestTowerLevel, block.towerLevel);
                }
                if (definition.obstacles != null) foreach (LevelObstacle obstacle in definition.obstacles)
                    run.obstacles.Add(new ObstacleSaveData
                    {
                        type = obstacle.type, column = obstacle.column, row = obstacle.row,
                        durability = obstacle.type == BoardObstacleType.Breakable ? obstacle.durability : 0
                    });
                Progress.activeRun = run;
                LevelProgressStore.Save(Progress);
            }
            if (run.attemptNumber <= 0)
            {
                run.attemptNumber = Progress.BeginAttempt(definition.levelNumber);
                LevelProgressStore.Save(Progress);
            }
            AttemptNumber = run.attemptNumber;

            MovesRemaining = run.movesRemaining;
            game.grid.Configure(definition);
            GameSaveData save = new GameSaveData
            {
                score = run.score,
                level = run.highestTowerLevel,
                boardState = run.board,
                firstInQueue = run.firstInQueue,
                nextInQueue = run.nextInQueue
            };
            game.Load(save, new ProgressionData());
            game.ConfigureLevel(definition, run.queueIndex, run.thirdInQueue,
                run.lookAheadTurnsRemaining, run.lookAheadUsed);
            game.grid.Load(save, new ProgressionData());
            if (!run.obstacleStateInitialized)
            {
                run.obstacles = new List<ObstacleSaveData>();
                if (definition.obstacles != null) foreach (LevelObstacle obstacle in definition.obstacles)
                    run.obstacles.Add(new ObstacleSaveData
                    {
                        type = obstacle.type, column = obstacle.column, row = obstacle.row,
                        durability = obstacle.type == BoardObstacleType.Breakable ? obstacle.durability : 0
                    });
                run.obstacleStateInitialized = true;
            }
            game.grid.LoadObstacles(run.obstacles);
            BoardCameraFitter.Fit(Camera.main, definition.columns, definition.rows, game.grid.WorldOffset);
            rulesUI = FindAnyObjectByType<LevelRulesUI>(FindObjectsInactive.Include);
            if (rulesUI != null) rulesUI.Bind(this, game);
            initialized = true;
            LevelAnalytics.LevelStarted(definition.levelNumber, AttemptNumber);
        }

        public void RecordMove()
        {
            if (Definition.moveLimit > 0) MovesRemaining = Mathf.Max(0, MovesRemaining - 1);
        }

        public void GrantExtraTurns(int amount)
        {
            if (finished || Definition == null || Definition.moveLimit <= 0) return;
            MovesRemaining += Mathf.Max(0, amount);
            SaveRun();
            if (rulesUI != null) rulesUI.Refresh();
        }

        public void SaveRun()
        {
            if (discardRunOnExit || !initialized || finished || game == null || game.grid == null ||
                !game.QueueReady || game.IsResolvingMove) return;
            LevelRunData run = Progress.activeRun;
            if (run == null) return;
            run.score = game.score;
            run.highestTowerLevel = game.level;
            run.movesRemaining = MovesRemaining;
            run.queueIndex = game.QueueIndex;
            game.GetQueueLevels(out run.firstInQueue, out run.nextInQueue, out run.thirdInQueue);
            run.lookAheadTurnsRemaining = game.LookAheadTurnsRemaining;
            run.lookAheadUsed = game.LookAheadUsed;
            run.board = game.grid.CaptureBoardState();
            run.obstacles = game.grid.CaptureObstacles();
            run.obstacleStateInitialized = true;
            LevelProgressStore.Save(Progress);
        }

        public void Retry()
        {
            if (awaitingFailureDecision) FinalizeFailure();
            Progress.activeRun = null;
            LevelProgressStore.Save(Progress);
            finished = true;
            LevelFlow.StartLevel(Definition.levelNumber);
        }

        public bool ContinueAfterExtraMoves(int extraMoves)
        {
            if (!awaitingFailureDecision || finished || extraMoves <= 0) return false;
            Progress = LevelProgressStore.Load();
            MovesRemaining += extraMoves;
            awaitingFailureDecision = false;
            pendingFailedObjective = null;
            game.state = GameController.GameState.play;
            if (rulesUI != null)
            {
                rulesUI.HideLoss();
                rulesUI.Refresh();
            }
            SaveRun();
            return true;
        }

        public void CompleteLevelTutorial()
        {
            if (Progress == null || Progress.levelTutorialComplete) return;
            Progress.levelTutorialComplete = true;
            LevelProgressStore.Save(Progress);
        }

        public void Complete()
        {
            LevelAnalytics.LevelCompleted(Definition.levelNumber, AttemptNumber, MovesRemaining);
            LevelDefinition next = LevelCatalog.Next(Definition.levelNumber);
            Progress.Complete(Definition.levelNumber, next != null ? next.levelNumber : 0);
            LevelProgressStore.Save(Progress);
            finished = true;
            game.state = GameController.GameState.win;
            if (rulesUI != null) rulesUI.ShowWin();
        }

        public void Fail(string failedObjective = null)
        {
            if (finished || awaitingFailureDecision) return;
            awaitingFailureDecision = true;
            pendingFailedObjective = string.IsNullOrEmpty(failedObjective) ? FindFailedObjective() : failedObjective;
            game.state = GameController.GameState.lose;
            if (rulesUI != null) rulesUI.ShowLoss();
        }

        private void FinalizeFailure()
        {
            if (!awaitingFailureDecision || finished) return;
            LevelAnalytics.LevelFailed(Definition.levelNumber, AttemptNumber,
                string.IsNullOrEmpty(pendingFailedObjective) ? "unknown" : pendingFailedObjective);
            awaitingFailureDecision = false;
            finished = true;
            Progress.activeRun = null;
            LevelProgressStore.Save(Progress);
            game.state = GameController.GameState.lose;
        }

        public void ReturnToMenu()
        {
            discardRunOnExit = true;
            if (awaitingFailureDecision) FinalizeFailure();
            if (Progress != null && Progress.activeRun != null)
            {
                Progress.activeRun = null;
                LevelProgressStore.Save(Progress);
            }

            if (game != null && game.state == GameController.GameState.win && Definition.showAdAfterWin)
                UnityAdsProvider.ShowBeforeReturningToMenu(Definition.levelNumber, LevelFlow.GoToMenu);
            else LevelFlow.GoToMenu();
        }

        public void ExitToMenu()
        {
            discardRunOnExit = true;
            if (awaitingFailureDecision)
            {
                FinalizeFailure();
                return;
            }
            if (!initialized || finished) return;
            LevelAnalytics.LevelQuit(Definition.levelNumber, AttemptNumber);

            // User requirement: if exiting from a level back to main menu, do not save progress in the level.
            if (Progress != null && Progress.activeRun != null)
            {
                Progress.activeRun = null;
                LevelProgressStore.Save(Progress);
            }
        }

        public bool ObjectivesMet()
        {
            if (Definition.objectives == null || Definition.objectives.Count == 0) return false;
            foreach (LevelObjective objective in Definition.objectives)
            {
                bool met = objective.type == LevelObjectiveType.ReachTowerLevel
                    ? game.level >= objective.target
                    : game.score >= objective.target;
                if (!met) return false;
            }
            return true;
        }

        public void ResolveMove(bool boardFull)
        {
            if (finished) return;
            if (ObjectivesMet()) Complete();
            else if ((Definition.moveLimit > 0 && MovesRemaining <= 0) || boardFull)
                Fail(boardFull ? "board_full" : "moves_limit");
            else SaveRun();
            if (rulesUI != null) rulesUI.Refresh();
        }

        private string FindFailedObjective()
        {
            if (Definition.objectives == null) return "unknown";
            foreach (LevelObjective objective in Definition.objectives)
            {
                bool met = objective.type == LevelObjectiveType.ReachTowerLevel
                    ? game.level >= objective.target
                    : game.score >= objective.target;
                if (!met) return objective.type.ToString().ToLowerInvariant() + "_" + objective.target;
            }
            return "unknown";
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveRun();
        }

        private void OnApplicationQuit() { SaveRun(); }
        private void OnDisable()
        {
            if (!discardRunOnExit && !finished)
            {
                SaveRun();
            }
            LevelFlow.ClearSession(this);
        }
    }
}
