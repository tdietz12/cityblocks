using Gameplay;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class LevelGridMigration
    {
        [MenuItem("Tools/City Blocks/Sync Grids for All Levels", false, 50)]
        public static void SyncAllLevels()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDefinition");
            int total = guids.Length;
            int synced = 0;
            int errors = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                    if (level == null) continue;

                    Undo.RecordObject(level, "Sync Grid Migration");

                    if (!level.HasPopulatedGrid())
                    {
                        level.SyncToGrid();
                    }
                    else
                    {
                        level.SyncFromGrid();
                    }

                    if (!level.IsValid(out string error))
                    {
                        Debug.LogError($"[LevelGridMigration] Level {level.name} at {path} failed validation: {error}");
                        errors++;
                    }

                    EditorUtility.SetDirty(level);
                    synced++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[LevelGridMigration] Successfully synced {synced} of {total} level definitions (Errors: {errors}).");

            if (errors == 0)
            {
                EditorUtility.DisplayDialog("Sync Complete", $"Successfully synced all {synced} level definitions with the new grid format!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Sync Completed with Errors", $"Synced {synced} levels, but {errors} had validation issues. Check console for details.", "OK");
            }
        }
    }
}
