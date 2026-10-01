using System.Collections.Generic;
using Data_Persistence;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ObstacleFeatureSmokeCheck
{
    [MenuItem("Tools/City Blocks/Verify Obstacles")]
    public static void Run()
    {
        LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(
            "Assets/Resources/Levels/Level003.asset");
        string error = definition == null ? "asset missing" : null;
        if (definition == null || !definition.IsValid(out error))
            throw new System.Exception("Level003 invalid: " + error);

        Scene original = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        try
        {
            GridController grid = new GameObject("Obstacle smoke test grid").AddComponent<GridController>();
            grid.cell = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cell/Cell.prefab");
            grid.breakableObstaclePrefab = AssetDatabase.LoadAssetAtPath<BoardObstacle>(
                "Assets/Prefabs/Obstacles/BreakableCell.prefab");
            grid.fixedObstaclePrefab = AssetDatabase.LoadAssetAtPath<BoardObstacle>(
                "Assets/Prefabs/Obstacles/FixedUnbreakableCell.prefab");
            grid.fallingObstaclePrefab = AssetDatabase.LoadAssetAtPath<BoardObstacle>(
                "Assets/Prefabs/Obstacles/FallingUnbreakableCell.prefab");
            grid.Configure(definition);
            grid.Load(new GameSaveData { boardState = new List<Vector3>() }, new ProgressionData());
            var state = new List<ObstacleSaveData>();
            foreach (LevelObstacle obstacle in definition.obstacles)
                state.Add(new ObstacleSaveData
                {
                    type = obstacle.type, column = obstacle.column, row = obstacle.row,
                    durability = obstacle.durability
                });
            grid.LoadObstacles(state);
            Check(grid.CaptureObstacles().Count == 4, "Initial obstacles");
            Check(grid.FindLandingRow(1, definition.rows) == 2, "Falling obstacle blocks towers");
            grid.DamageAdjacentBreakables(0, 0);
            var afterBreak = grid.CaptureObstacles();
            Check(afterBreak.Count == 3, "One-hit obstacle removed");
            Check(afterBreak.Exists(o => o.type == BoardObstacleType.FallingUnbreakable &&
                                         o.column == 1 && o.row == 0), "Falling obstacle descended");
            Check(afterBreak.Exists(o => o.type == BoardObstacleType.FixedUnbreakable &&
                                         o.column == 2 && o.row == 0), "Fixed obstacle stayed");
            var saved = new LevelRunData
            {
                obstacleStateInitialized = true,
                obstacles = afterBreak
            };
            LevelRunData restored = JsonUtility.FromJson<LevelRunData>(JsonUtility.ToJson(saved));
            Check(restored.obstacleStateInitialized && restored.obstacles.Count == 3 &&
                  restored.obstacles.Exists(o => o.type == BoardObstacleType.FallingUnbreakable &&
                                                  o.column == 1 && o.row == 0),
                "Obstacle save round trip");
            for (int hit = 1; hit <= 3; hit++)
            {
                grid.DamageAdjacentBreakables(2, 1);
                Check(grid.CaptureObstacles().Exists(o => o.column == 3 && o.row == 1 &&
                    o.durability == 4 - hit), "Four-hit obstacle damage " + hit);
            }
            grid.DamageAdjacentBreakables(2, 1);
            Check(!grid.CaptureObstacles().Exists(o => o.column == 3 && o.row == 1),
                "Four-hit obstacle removed");
            Debug.Log("Obstacle feature smoke check passed.");
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(testScene, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new System.Exception("Obstacle smoke check failed: " + message);
    }
}
