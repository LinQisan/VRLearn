using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor side of custom scenarios: export the built-in scenarios as JSON templates, validate
/// the files in Scenarios/, and play one file in the Hikone scene.
/// </summary>
public static class CustomScenarioTools
{
    const string HikoneScene = "Assets/_Project/Scenes/TraficAcident_Hikone_Meta.unity";
    public static string TemplateFolder => Path.Combine(CustomScenarioLibrary.ProjectFolder, "templates");

    [MenuItem("Tools/VRLearn/Custom Scenarios/Play Scenario File…")]
    public static void PlayFile()
    {
        var path = EditorUtility.OpenFilePanel("Play scenario", CustomScenarioLibrary.ProjectFolder, "json");
        if (!string.IsNullOrEmpty(path))
            Play(path);
    }

    public static void Play(string path)
    {
        var errors = new List<string>();
        var scenario = CustomScenarioLibrary.Load(path, errors);
        if (scenario == null)
        {
            EditorUtility.DisplayDialog("Scenario has errors", string.Join("\n", errors), "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(HikoneScene);
        CustomScenarioSession.RememberForPlayMode(path);
        CustomScenarioSession.RequestEditorPlay();
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/VRLearn/Custom Scenarios/Validate Scenario Files")]
    public static void ValidateAll()
    {
        var files = CustomScenarioLibrary.Scan();
        var bad = 0;
        foreach (var (path, scenario, errors) in files)
        {
            if (scenario != null)
                Debug.Log($"[Scenarios] OK  {path}  ({scenario.name}, {scenario.vehicles.Length} vehicles)");
            else
            {
                bad++;
                Debug.LogError($"[Scenarios] NG  {path}\n  " + string.Join("\n  ", errors));
            }
        }
        Debug.Log($"[Scenarios] {files.Count} files, {bad} with errors");
    }

    [MenuItem("Tools/VRLearn/Custom Scenarios/Export Built-in Scenarios As Templates")]
    public static void ExportBuiltIns()
    {
        EditorSceneManager.OpenScene(HikoneScene);
        Directory.CreateDirectory(TemplateFolder);
        for (var id = 0; id < 10; id++)
        {
            var scenario = ExportBuiltIn(id);
            var errors = new List<string>();
            scenario.Validate(errors);
            if (errors.Count > 0)
                Debug.LogWarning($"[Scenarios] builtin {id + 1:00}: " + string.Join(" / ", errors));
            File.WriteAllText(Path.Combine(TemplateFolder, scenario.id + ".json"), scenario.ToJson() + "\n");
        }
        Debug.Log("[Scenarios] templates written to " + TemplateFolder);
    }

    /// <summary>
    /// The built-in scenario as data: same spawn, goal, trigger, routes, headings, timings and car
    /// models. Behaviour that only exists as special code for a built-in number (speeding and not
    /// stopping in 06, the side-impact crash of 07/08) is not part of the file.
    /// </summary>
    public static CustomScenario ExportBuiltIn(int id)
    {
        var runtime = Object.FindFirstObjectByType<ScenarioRuntime>(FindObjectsInactive.Include);
        var context = Object.FindFirstObjectByType<GameplaySceneContext>(FindObjectsInactive.Include);
        var entry = runtime.GetById(id);
        var asset = entry.asset;
        SplitSummary(asset.eventSummary, out var situation, out var point);
        // the trigger is the AccidentCarFactory volume; its parent holds the scenario's factories and routes
        var trigger = entry.accidentArea != null
            ? entry.accidentArea.GetComponentInChildren<AccidentCarFactory>(true)
            : null;
        var scenario = new CustomScenario
        {
            id = $"builtin-{id + 1:00}",
            name = asset.displayName,
            nameEn = asset.shortTitleEn,
            setting = asset.setting switch
            {
                ScenarioSetting.MidBlock => "midblock",
                ScenarioSetting.Bicycle => "bicycle",
                _ => "crossing"
            },
            learningGoal = asset.learningGoal,
            situation = situation,
            point = point,
            playerMode = asset.playerMode == ScenarioPlayerMode.Bicycle ? "bicycle" : "walking",
            spawn = new GroundPoint(entry.playerSpawn.position.x, entry.playerSpawn.position.z),
            goal = AreaOf(entry.goal.transform),
            trigger = trigger != null && (asset.accidentLaunches.Length > 0 || asset.stopTraffic.Length > 0)
                ? AreaOf(trigger.transform)
                : new GroundArea { width = 0f, depth = 0f }
        };

        var area = trigger != null ? trigger.transform.parent : entry.accidentArea != null ? entry.accidentArea.transform : null;
        CarFactory Resolve(ScenarioFactory key) => key switch
        {
            ScenarioFactory.Left => context.LeftFactory,
            ScenarioFactory.Right => context.RightFactory,
            ScenarioFactory.Left2 => context.LeftFactory2,
            ScenarioFactory.Right2 => context.RightFactory2,
            ScenarioFactory.AreaAccidentLeft => area?.Find("CarFactory_Acident_Left")?.GetComponent<CarFactory>(),
            ScenarioFactory.AreaAccidentRight => area?.Find("CarFactory_Acident_Right")?.GetComponent<CarFactory>(),
            _ => area?.Find("CarFactory_Acident")?.GetComponent<CarFactory>()
        };
        var stopped = new HashSet<CarFactory>(asset.stopTraffic.Select(Resolve).Where(f => f != null));

        // regular traffic: the shared flow of this scenario, or the cycling scenarios' own side factories
        var background = new List<CarFactory>();
        if (asset.trafficFlow == ScenarioTrafficFlow.Beside)
            background.AddRange(new[] { context.LeftFactory, context.RightFactory });
        else if (asset.trafficFlow == ScenarioTrafficFlow.Vertical)
            background.AddRange(new[] { context.LeftFactory2, context.RightFactory2 });
        if (area != null)
            background.AddRange(area.GetComponentsInChildren<CarFactory>(true)
                .Where(f => f.CompareTag("LeftFactory") || f.CompareTag("RightFactory")));

        var vehicles = new List<ScenarioVehicle>();
        foreach (var factory in background)
        {
            var yaw = CarFactory.RegularTrafficYaw(factory.transform, id);
            vehicles.Add(new ScenarioVehicle
            {
                name = "車の流れ " + factory.name.Replace("CarFactory_", ""),
                model = factory.CarPrefab.name,
                route = RouteOf(factory, id, Vector3.zero),
                start = ScenarioVehicle.StartBegin,
                // cycling scenarios start with a car almost due (TimeProgress = 4 of 4-6 s)
                delaySeconds = id == 6 || id == 7 || id == 9 ? 1f : 5f,
                repeatMinSeconds = 4f,
                repeatMaxSeconds = 6f,
                stopOnTrigger = stopped.Contains(factory),
                overrideYaw = true,
                yaw = yaw ?? 0f
            });
        }
        var n = 0;
        foreach (var launch in asset.accidentLaunches)
        {
            var factory = Resolve(launch.factory);
            vehicles.Add(new ScenarioVehicle
            {
                name = "事故車 " + (++n),
                model = factory.CarPrefab.name,
                route = RouteOf(factory, id, launch.offset),
                start = ScenarioVehicle.StartTrigger,
                delaySeconds = launch.delaySeconds,
                accident = true,
                overrideYaw = true,       // factories spawn with identity rotation unless the launch turns them
                yaw = launch.overrideYaw ? launch.yaw : 0f
            });
        }
        scenario.vehicles = vehicles.ToArray();
        return scenario;
    }

    static GroundPoint[] RouteOf(CarFactory factory, int id, Vector3 offset)
    {
        var start = factory.transform.position + offset;
        var points = new List<GroundPoint> { new GroundPoint(Round(start.x), Round(start.z)) };
        var route = CarFactory.ResolveRoute(factory.transform, id);
        foreach (Transform p in route)
            points.Add(new GroundPoint(Round(p.position.x), Round(p.position.z)));
        return points.ToArray();
    }

    static GroundArea AreaOf(Transform t)
    {
        var box = t.GetComponent<BoxCollider>();
        var size = box != null ? Vector3.Scale(box.size, t.lossyScale) : t.lossyScale;
        var center = box != null ? t.TransformPoint(box.center) : t.position;
        return new GroundArea
        {
            x = Round(center.x), z = Round(center.z),
            width = Round(Mathf.Abs(size.x)), depth = Round(Mathf.Abs(size.z)),
            yaw = Round(t.eulerAngles.y)
        };
    }

    static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    static void SplitSummary(string summary, out string situation, out string point)
    {
        situation = summary ?? "";
        point = "";
        foreach (var raw in (summary ?? "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("事故の状況：")) situation = line.Substring("事故の状況：".Length);
            else if (line.StartsWith("安全確認のポイント：")) point = line.Substring("安全確認のポイント：".Length);
        }
    }
}
