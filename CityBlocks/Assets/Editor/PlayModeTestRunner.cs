using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

[InitializeOnLoad]
internal static class PlayModeTestRunner
{
    private static int frames;
    private static int stage;
    private static double started;
    private static Component ui, game, session;
    private static ScriptableObject definition;
    private static Component hud, start;
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static PlayModeTestRunner()
    {
        string state = SessionState.GetString("PlayModeTest.State", "Idle");
        if (state == "WaitingForCompile") EditorApplication.delayCall += () =>
        {
            SessionState.SetString("PlayModeTest.State", "EnteringPlayMode");
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.isPlaying = true;
        };
        else if (state == "EnteringPlayMode") EditorApplication.playModeStateChanged += Changed;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= Changed;
        started = EditorApplication.timeSinceStartup;
        SessionState.SetString("PlayModeTest.State", "InPlayMode");
        EditorApplication.update += Tick;
    }
    private static Type TypeNamed(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { var type = assembly.GetType(name); if (type != null) return type; }
        throw new Exception("Missing type " + name);
    }
    private static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static void Check(bool value, string name)
    {
        Debug.Log("[Test] " + name + ": " + value);
        if (!value) throw new Exception(name);
        checks.Add(name);
    }
    private static string Label(Component card, int index)
    {
        IList rows = (IList)Field(card, "rows");
        var label = (TMPro.TMP_Text)Field(rows[index], "label");
        return label.text;
    }
    private static void Tick()
    {
        if (++frames < 15) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 15) throw new Exception("Test timed out");
            if (stage == 0)
            {
                foreach (var c in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)) if (c.GetType().FullName == "UI.LevelRulesUI") ui = c;
                Check(ui != null, "Scene LevelRulesUI exists");
                hud = (Component)Field(ui, "scoreObjectivesCard"); start = (Component)Field(ui, "startObjectivesCard");
                game = (Component)ui.GetType().GetProperty("Game").GetValue(ui);
                Check(hud != null && start != null && game != null, "Serialized cards and game reference");
                Check(!hud.gameObject.activeSelf && !start.gameObject.activeSelf, "Endless mode hides both cards");
                definition = ScriptableObject.CreateInstance(TypeNamed("Gameplay.LevelDefinition"));
                IList objectives = (IList)Field(definition, "objectives");
                Type objectiveType = TypeNamed("Gameplay.LevelObjective");
                for (int i = 0; i < 5; i++) { object obj = Activator.CreateInstance(objectiveType); Set(obj, "type", Enum.ToObject(TypeNamed("Gameplay.LevelObjectiveType"), i % 2)); Set(obj, "target", i % 2 == 0 ? 6 : 1000); objectives.Add(obj); }
                var holder = new GameObject("TemporaryObjectiveTestSession");
                session = holder.AddComponent(TypeNamed("Gameplay.LevelSession"));
                Set(session, "<Definition>k__BackingField", definition);
                Set(game, "score", 450); Set(game, "level", 2);
                Call(ui, "Bind", session, game);
                Check(Label(start,0)=="6" && Label(start,1)=="1000", "Start card shows required targets");
                Check(Label(hud,0)=="2 / 6" && Label(hud,1)=="450 / 1000", "HUD shows actual progress");
                Check(((IList)Field(hud,"rows")).Count == 5, "Five objectives create five rows");
                var rowList = (IList)Field(hud,"rows");
                var icon0 = (UnityEngine.UI.Image)Field(rowList[0],"icon");
                var icon1 = (UnityEngine.UI.Image)Field(rowList[1],"icon");
                Check(icon0.sprite != null && icon1.sprite != null && icon0.sprite != icon1.sprite, "Distinct tower and score icons");
                Check(!icon0.raycastTarget && !icon1.raycastTarget, "Objective icons do not block input");
                Set(game,"level",8);Set(game,"score",1200);Call(ui,"Refresh");
                Check(Label(hud,0)=="8 / 6" && Label(hud,1)=="1200 / 1000", "Above-target progress remains accurate");
                while(objectives.Count>2)objectives.RemoveAt(objectives.Count-1);
                Call(ui,"Bind",session,game);Call(ui,"PauseGame");
                Check(Label(start,0)=="6" && !((GameObject)Field(((IList)Field(hud,"rows"))[2],"root")).activeSelf, "Rebinding hides unused rows and pause retains targets");
                Set(game,"level",2);Set(game,"score",450);Call(ui,"Refresh");
                stage=1;
            }
            else if(stage == 1)
            {
                Set(((IList)Field(definition,"objectives"))[1],"target",99999);
                Set(game,"score",99999);
                Call(ui,"Bind",session,game);
                Call(ui,"PauseGame");
                Canvas.ForceUpdateCanvases();
                foreach(var text in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                    Check(!text.isTextOverflowing, "HUD text fits: " + text.text);
                }
                if(System.IO.File.Exists("Assets/ObjectiveCardsTest.png")) System.IO.File.Delete("Assets/ObjectiveCardsTest.png");
                var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(viewType).Focus();
                var capture = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes("Assets/ObjectiveCardsTest.png",capture.EncodeToPNG());
                UnityEngine.Object.Destroy(capture);
                SessionState.SetString("PlayModeTest.ScreenshotPath","Assets/ObjectiveCardsTest.png");
                stage=2;
            }
            else if(stage == 2)
            {
                if(!System.IO.File.Exists("Assets/ObjectiveCardsTest.png"))return;
                Call(ui,"StartGame");
                Check(Label(hud,1)=="99999 / 99999", "Resume keeps HUD progress");
                ((IList)Field(definition,"objectives")).Clear();Call(ui,"Bind",session,game);
                Check(!hud.gameObject.activeSelf && !start.gameObject.activeSelf,"Empty objective lists hide cards");
                Call(ui,"InitializeEndlessMode");
                Check(!hud.gameObject.activeSelf && !start.gameObject.activeSelf,"Endless reinitialization clears campaign cards");
                Finish(null);
            }
        }
        catch(Exception e) { Finish(e.ToString()); }
    }
    [Serializable] private class TestResult { public bool success; public string error; public string[] checks; }
    private static void Finish(string error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetString("PlayModeTest.Result",JsonUtility.ToJson(new TestResult {success=error==null,error=error,checks=checks.ToArray()}));
        SessionState.SetString("PlayModeTest.State","Done");
        EditorApplication.isPlaying=false;
    }
}
