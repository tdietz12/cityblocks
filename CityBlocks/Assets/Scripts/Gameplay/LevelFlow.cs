using Data_Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    public static class LevelFlow
    {
        private static int requestedLevel;
        public static bool IsLevelMode => requestedLevel > 0;
        public static LevelSession ActiveSession { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            requestedLevel = 0;
            ActiveSession = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static bool StartLevel(int levelNumber)
        {
            if (LevelCatalog.Get(levelNumber) == null || !LevelProgressStore.Load().IsUnlocked(levelNumber))
                return false;
            requestedLevel = levelNumber;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene("EndlessMode");
            return true;
        }

        public static bool Continue()
        {
            LevelProgressData progress = LevelProgressStore.Load();
            int target = progress.activeRun != null ? progress.activeRun.levelNumber : progress.highestUnlockedLevel;
            if (LevelCatalog.Get(target) == null && LevelCatalog.Levels != null && LevelCatalog.Levels.Count > 0)
            {
                target = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].levelNumber;
            }
            return StartLevel(target);
        }

        public static void GoToMenu()
        {
            if (ActiveSession != null)
            {
                // Discard level progress as requested when exiting back to main menu from a level
                ActiveSession.ExitToMenu();
            }
            else if (SceneManager.GetActiveScene().name == "EndlessMode")
            {
                GameController game = Object.FindAnyObjectByType<GameController>();
                if (game != null && game.state != GameController.GameState.lose && game.state != GameController.GameState.win)
                {
                    LevelAnalytics.EndlessQuit(game.level, game.EndlessAttemptNumber, game.TotalMoves);
                    if (DataPersistenceController.instance != null)
                    {
                        DataPersistenceController.instance.SaveGame();
                    }
                }
            }
            ActiveSession = null;
            requestedLevel = 0;
            SceneManager.LoadScene("MainMenu");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "EndlessMode" || requestedLevel == 0) return;
            GameController game = Object.FindAnyObjectByType<GameController>();
            if (game == null)
            {
                Debug.LogError("Level scene has no GameController.");
                return;
            }
            ActiveSession = game.gameObject.AddComponent<LevelSession>();
            ActiveSession.Initialize(LevelCatalog.Get(requestedLevel), game);
        }

        internal static void ClearSession(LevelSession session)
        {
            if (ActiveSession == session) ActiveSession = null;
        }
    }
}
