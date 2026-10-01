using System;
using System.IO;
using Data_Persistence;
using Gameplay;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public static class SaveDataEditorMenu
    {
        private const string MenuItemPath = "Tools/City Blocks/Delete All Save Files (Reset Player)";

        [MenuItem(MenuItemPath, false, 10)]
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
            }

            Debug.Log($"[SaveDataEditorMenu] Save data reset successfully. ({deletedCount} files deleted, PlayerPrefs cleared). The game is back to new player state.");
            EditorUtility.DisplayDialog(
                "Save Data Reset",
                "All save data has been deleted.\nThe game is now reset back to a clean new player state.",
                "OK"
            );
        }
    }
}

