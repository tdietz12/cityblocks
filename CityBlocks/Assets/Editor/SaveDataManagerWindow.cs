using System;
using System.Collections.Generic;
using System.IO;
using Data_Persistence;
using Gameplay;
using UI;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public static class SaveDataEditorMenu
    {
        private const string WindowMenuPath = "Tools/Player Reset Tool";
        private const string CityBlocksWindowMenuPath = "Tools/City Blocks/Player Reset Tool";
        private const string SetLevelMenuPath = "Tools/City Blocks/Set Player Level...";
        private const string DeleteAllMenuPath = "Tools/City Blocks/Delete All Save Files (Reset Player)";

        [MenuItem(WindowMenuPath, false, 10)]
        [MenuItem(CityBlocksWindowMenuPath, false, 10)]
        [MenuItem(SetLevelMenuPath, false, 11)]
        public static void OpenResetToolWindow()
        {
            SaveDataManagerWindow.ShowWindow();
        }

        [MenuItem(DeleteAllMenuPath, false, 20)]
        public static void DeleteAllSaveFilesAndReset()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset Save Data?",
                "Are you sure you want to delete all save files and reset the game back to the new player state?\n\n" +
                "This will:\n" +
                "• Delete level progress, active runs, and endless saves\n" +
                "• Delete login streak data\n" +
                "• Clear player preferences (e.g. endless attempts)\n" +
                "• Start from Level 1 tutorial on next play",
                "Reset to New Player",
                "Cancel"
            );

            if (!confirmed) return;

            string persistentPath = Application.persistentDataPath;
            string[] filesToDelete =
            {
                Path.Combine(persistentPath, "level-progress.json"),
                Path.Combine(persistentPath, "level-progress.json.tmp"),
                Path.Combine(persistentPath, "SaveGameData"),
                Path.Combine(persistentPath, "ProgressionData"),
                Path.Combine(persistentPath, "daily-login-streak.json")
            };

            int deletedCount = 0;
            foreach (var filePath in filesToDelete)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete file {filePath}: {ex.Message}");
                    }
                }
            }

            // Clear PlayerPrefs
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // Reset active in-memory controller if running in Play Mode
            if (Application.isPlaying)
            {
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.NewGame();
                }

                if (LevelFlow.ActiveSession != null)
                {
                    LevelFlow.ActiveSession.Progress.activeRun = null;
                    LevelFlow.GoToMenu();
                }

                RefreshActiveUI();
            }

            Debug.Log($"[SaveDataEditorMenu] Save data reset successfully. ({deletedCount} files deleted, PlayerPrefs cleared). The game is back to new player state.");
            EditorUtility.DisplayDialog(
                "Save Data Reset",
                "All save data has been deleted.\nThe game is now reset back to a clean new player state.",
                "OK"
            );
        }

        public static LevelProgressData ResetPlayerToLevel(
            int targetLevel,
            bool markPreviousCompleted = true,
            bool completeTutorial = true,
            bool resetPowerUps = false,
            bool clearEndlessSave = true,
            bool clearPlayerPrefsAndStreaks = false,
            bool showDialog = false)
        {
            int minLevel = 1;
            int maxLevel = 100;
            if (LevelCatalog.Levels != null && LevelCatalog.Levels.Count > 0)
            {
                minLevel = LevelCatalog.Levels[0].levelNumber;
                maxLevel = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].levelNumber;
            }

            targetLevel = Mathf.Clamp(targetLevel, minLevel, maxLevel);

            LevelProgressData existing = LevelProgressStore.Load();
            LevelProgressData data = new LevelProgressData();
            data.highestUnlockedLevel = targetLevel;
            data.completedLevels = new List<int>();

            if (markPreviousCompleted)
            {
                for (int lvl = minLevel; lvl < targetLevel; lvl++)
                {
                    data.completedLevels.Add(lvl);
                }
            }

            data.firstLevelTutorialComplete = completeTutorial;
            data.activeRun = null;

            if (!resetPowerUps && existing != null && existing.powerUps != null)
            {
                data.powerUps = existing.powerUps;
            }
            else
            {
                data.powerUps = new PowerUpInventory();
            }

            if (!clearPlayerPrefsAndStreaks && existing != null)
            {
                data.adsRemoved = existing.adsRemoved;
                data.hasMadePurchase = existing.hasMadePurchase;
                if (existing.processedPurchaseIds != null)
                {
                    data.processedPurchaseIds = new List<string>(existing.processedPurchaseIds);
                }
            }

            LevelProgressStore.Save(data);

            string persistentPath = Application.persistentDataPath;

            // Delete temporary save file if it exists
            string tmpFile = Path.Combine(persistentPath, "level-progress.json.tmp");
            if (File.Exists(tmpFile))
            {
                try { File.Delete(tmpFile); } catch (Exception ex) { Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete {tmpFile}: {ex.Message}"); }
            }

            // Clear endless run & session data
            if (clearEndlessSave)
            {
                string[] endlessFiles =
                {
                    Path.Combine(persistentPath, "SaveGameData"),
                    Path.Combine(persistentPath, "ProgressionData")
                };
                foreach (string f in endlessFiles)
                {
                    if (File.Exists(f))
                    {
                        try { File.Delete(f); } catch (Exception ex) { Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete {f}: {ex.Message}"); }
                    }
                }
            }

            // Clear daily streak & PlayerPrefs
            if (clearPlayerPrefsAndStreaks)
            {
                string streakFile = Path.Combine(persistentPath, "daily-login-streak.json");
                if (File.Exists(streakFile))
                {
                    try { File.Delete(streakFile); } catch (Exception ex) { Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete {streakFile}: {ex.Message}"); }
                }

                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }

            // Sync active runtime state if playing
            if (Application.isPlaying)
            {
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.NewGame();
                }

                if (LevelFlow.ActiveSession != null)
                {
                    if (LevelFlow.ActiveSession.Progress != null)
                    {
                        LevelFlow.ActiveSession.Progress.activeRun = null;
                    }
                    LevelFlow.GoToMenu();
                }

                RefreshActiveUI();
            }

            Debug.Log($"[SaveDataEditorMenu] Player set to Level {targetLevel}. (Highest unlocked: {data.highestUnlockedLevel}, Completed count: {data.completedLevels.Count}, Tutorial complete: {data.firstLevelTutorialComplete})");

            if (showDialog)
            {
                string completedText = data.completedLevels.Count > 0
                    ? $"1 through {targetLevel - 1} ({data.completedLevels.Count} levels)"
                    : "None";

                EditorUtility.DisplayDialog(
                    "Player Level Set",
                    $"Player successfully set to Level {targetLevel}!\n\n" +
                    $"• Highest Unlocked: Level {targetLevel}\n" +
                    $"• Completed Levels: {completedText}\n" +
                    $"• Tutorial Complete: {data.firstLevelTutorialComplete}\n" +
                    $"• Endless Mode: {(data.completedLevels.Contains(3) ? "Unlocked" : "Locked (requires Level 3)")}",
                    "OK"
                );
            }

            return data;
        }

        public static void RefreshActiveUI()
        {
            if (!Application.isPlaying) return;

            LevelButton_MainMenu[] levelButtons = UnityEngine.Object.FindObjectsByType<LevelButton_MainMenu>(FindObjectsInactive.Exclude);
            foreach (var btn in levelButtons)
            {
                if (btn != null) btn.RefreshButton();
            }

            PlayButton_MainMenu[] playButtons = UnityEngine.Object.FindObjectsByType<PlayButton_MainMenu>(FindObjectsInactive.Exclude);
            foreach (var btn in playButtons)
            {
                if (btn != null) btn.SendMessage("RefreshUnlockState", SendMessageOptions.DontRequireReceiver);
            }

            LevelRulesUI[] rulesUIs = UnityEngine.Object.FindObjectsByType<LevelRulesUI>(FindObjectsInactive.Exclude);
            foreach (var ui in rulesUIs)
            {
                if (ui != null) ui.Refresh();
            }
        }
    }

    public class SaveDataManagerWindow : EditorWindow
    {
        private int targetLevel = 1;
        private bool markPreviousCompleted = true;
        private bool completeTutorial = true;
        private bool resetPowerUps = false;
        private bool clearEndlessSave = true;
        private bool clearPlayerPrefsAndStreaks = false;

        private string statusMessage = "";
        private MessageType statusMessageType = MessageType.Info;
        private Vector2 scrollPos;

        public static void ShowWindow()
        {
            SaveDataManagerWindow window = GetWindow<SaveDataManagerWindow>("Player Reset Tool");
            window.minSize = new Vector2(360, 520);
            window.Show();
        }

        private void OnEnable()
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data != null && data.highestUnlockedLevel > 0)
            {
                targetLevel = data.highestUnlockedLevel;
            }
            completeTutorial = targetLevel > 1;
            markPreviousCompleted = targetLevel > 1;
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Player Reset & Level Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Set player progression to a specific level, or reset the player completely.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            // Level Catalog bounds
            int minLevel = 1;
            int maxLevel = 100;
            if (LevelCatalog.Levels != null && LevelCatalog.Levels.Count > 0)
            {
                minLevel = LevelCatalog.Levels[0].levelNumber;
                maxLevel = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].levelNumber;
            }

            // Current Player State Box
            DrawCurrentStateBox();

            EditorGUILayout.Space(10);

            // Target Level Configuration Box
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Set Specific Level", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            int newTarget = EditorGUILayout.IntSlider("Target Level", targetLevel, minLevel, maxLevel);
            if (newTarget != targetLevel)
            {
                targetLevel = newTarget;
                completeTutorial = targetLevel > 1;
                markPreviousCompleted = targetLevel > 1;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick Jump", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawQuickLevelButton(1, "1 (New)");
            DrawQuickLevelButton(2, "2");
            DrawQuickLevelButton(3, "3 (Endless)");
            DrawQuickLevelButton(5, "5");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawQuickLevelButton(10, "10");
            DrawQuickLevelButton(20, "20");
            DrawQuickLevelButton(50, "50");
            DrawQuickLevelButton(maxLevel, $"{maxLevel} (Max)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progression Options", EditorStyles.miniBoldLabel);

            markPreviousCompleted = EditorGUILayout.Toggle(
                new GUIContent("Mark Prior Levels Done", "Marks levels 1 through (Target - 1) as completed. Unlocks Endless mode (requires Level 3)."),
                markPreviousCompleted
            );

            completeTutorial = EditorGUILayout.Toggle(
                new GUIContent("Tutorial Completed", "If true, skips the Level 1 tutorial introduction."),
                completeTutorial
            );

            resetPowerUps = EditorGUILayout.Toggle(
                new GUIContent("Reset Power-Ups (0)", "If checked, clears power-up inventory. If unchecked, preserves existing power-ups."),
                resetPowerUps
            );

            clearEndlessSave = EditorGUILayout.Toggle(
                new GUIContent("Clear Endless Run Save", "Clears saved active endless board and session state."),
                clearEndlessSave
            );

            clearPlayerPrefsAndStreaks = EditorGUILayout.Toggle(
                new GUIContent("Clear Prefs & Login Streak", "Clears daily login streak file and all PlayerPrefs."),
                clearPlayerPrefsAndStreaks
            );

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Primary Action: Set to Level
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button($"Set Player to Level {targetLevel}", GUILayout.Height(36)))
            {
                SaveDataEditorMenu.ResetPlayerToLevel(
                    targetLevel: targetLevel,
                    markPreviousCompleted: markPreviousCompleted,
                    completeTutorial: completeTutorial,
                    resetPowerUps: resetPowerUps,
                    clearEndlessSave: clearEndlessSave,
                    clearPlayerPrefsAndStreaks: clearPlayerPrefsAndStreaks,
                    showDialog: false
                );
                statusMessage = $"Player successfully set to Level {targetLevel}!";
                statusMessageType = MessageType.Info;
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // Danger Zone: Full Reset
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Full Reset to New Player (Delete All Saves)", GUILayout.Height(28)))
            {
                SaveDataEditorMenu.DeleteAllSaveFilesAndReset();
                targetLevel = 1;
                completeTutorial = false;
                markPreviousCompleted = false;
                statusMessage = "All save data deleted. Player reset to clean new player state.";
                statusMessageType = MessageType.Warning;
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCurrentStateBox()
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data == null) data = new LevelProgressData();
            if (data.powerUps == null) data.powerUps = new PowerUpInventory();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Player State", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(65)))
            {
                statusMessage = "";
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField($"Highest Unlocked Level:", $"Level {data.highestUnlockedLevel}", EditorStyles.boldLabel);

            string completedSummary = data.completedLevels == null || data.completedLevels.Count == 0
                ? "None"
                : $"{data.completedLevels.Count} level(s)";
            EditorGUILayout.LabelField("Completed Levels:", completedSummary);

            EditorGUILayout.LabelField("Tutorial Status:", data.firstLevelTutorialComplete ? "Completed" : "Not Completed (will launch tutorial)");

            string activeRunSummary = data.activeRun != null && data.activeRun.levelNumber > 0
                ? $"Level {data.activeRun.levelNumber} (in progress)"
                : "None";
            EditorGUILayout.LabelField("Active Run:", activeRunSummary);

            bool endlessUnlocked = data.completedLevels != null && data.completedLevels.Contains(3);
            EditorGUILayout.LabelField("Endless Mode:", endlessUnlocked ? "Unlocked" : "Locked (requires Level 3 clear)");

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Power-Ups:  Del: {data.powerUps.deleteUses} | Look: {data.powerUps.deleteRowUses} | LvlUp: {data.powerUps.levelUpUses} | Extra: {data.powerUps.extraTurnsUses}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickLevelButton(int lvl, string label)
        {
            if (GUILayout.Button(label, GUILayout.Height(22)))
            {
                targetLevel = lvl;
                completeTutorial = targetLevel > 1;
                markPreviousCompleted = targetLevel > 1;
                GUI.FocusControl(null);
            }
        }
    }
}


