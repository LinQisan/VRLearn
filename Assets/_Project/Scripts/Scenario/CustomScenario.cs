using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// A scenario authored outside Unity (scenario editor / hand-written JSON) and built at runtime.
/// Coordinates are Unity world metres on the ground plane (x, z); heights come from the road and
/// ground colliders. Serialized with JsonUtility, so field names are the JSON keys.
/// Format reference: Scenarios/scenario.schema.json.
/// </summary>
[Serializable]
public sealed class CustomScenario
{
    public const string FormatName = "vrlearn-scenario";
    public const int CurrentVersion = 1;
    public const string HikoneMap = "hikone-kyobashi";

    public string format = FormatName;
    public int version = CurrentVersion;
    /// <summary>File-safe identifier, also written to the CSV.</summary>
    public string id;
    public string name;
    public string nameEn;
    /// <summary>"crossing", "midblock" or "bicycle": the title-menu group.</summary>
    public string setting = "crossing";
    public string learningGoal;
    /// <summary>What happens in the accident (feedback page, "事故の状況").</summary>
    public string situation;
    /// <summary>What to do instead (feedback page, "安全確認のポイント").</summary>
    public string point;
    public string map = HikoneMap;
    /// <summary>"walking" or "bicycle".</summary>
    public string playerMode = "walking";
    /// <summary>Where the participant starts; they face the goal.</summary>
    public GroundPoint spawn = new GroundPoint();
    public GroundArea goal = new GroundArea();
    /// <summary>Entering this area fires the "trigger" start of vehicles. Width 0 = no trigger.</summary>
    public GroundArea trigger = new GroundArea { width = 0f, depth = 0f };
    public ScenarioVehicle[] vehicles = new ScenarioVehicle[0];

    public bool IsBicycle => playerMode == "bicycle";
    public bool HasTrigger => trigger != null && trigger.width > 0f && trigger.depth > 0f;

    public ScenarioPlayerMode PlayerMode => IsBicycle ? ScenarioPlayerMode.Bicycle : ScenarioPlayerMode.Walking;

    public ScenarioSetting Setting => setting switch
    {
        "midblock" => ScenarioSetting.MidBlock,
        "bicycle" => ScenarioSetting.Bicycle,
        _ => ScenarioSetting.Crossing
    };

    /// <summary>Same layout as ScenarioDefinitionAsset.eventSummary.</summary>
    public string EventSummary => $"事故の状況：{situation}\n安全確認のポイント：{point}";

    static readonly Regex LongDecimal = new Regex(@"-?\d+\.\d{4,}");

    /// <summary>Pretty JSON with coordinates rounded to centimetres (hand-editable, diff-friendly).</summary>
    public string ToJson() => LongDecimal.Replace(JsonUtility.ToJson(this, true), m =>
    {
        var value = Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture), 2);
        return value.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
    });

    /// <summary>Parses and validates. Returns null (with errors) when the file cannot be used.</summary>
    public static CustomScenario FromJson(string json, List<string> errors)
    {
        CustomScenario scenario;
        try
        {
            scenario = JsonUtility.FromJson<CustomScenario>(json);
        }
        catch (Exception exception)
        {
            errors.Add("JSON を読めません: " + exception.Message);
            return null;
        }
        if (scenario == null)
        {
            errors.Add("JSON が空です。");
            return null;
        }
        scenario.Validate(errors);
        return errors.Count == 0 ? scenario : null;
    }

    static readonly Regex IdPattern = new Regex("^[a-z0-9][a-z0-9_-]{0,39}$");
    static readonly string[] PlayerModes = { "walking", "bicycle" };
    static readonly string[] Settings = { "crossing", "midblock", "bicycle" };

    // generous bounds of the Hikone Kyobashi map (roads span about x -115..100, z -15..70)
    public static readonly Rect HikoneBounds = Rect.MinMaxRect(-160f, -80f, 160f, 140f);

    /// <summary>Structural checks that need no scene. Scene checks (ground, models) happen at runtime.</summary>
    public void Validate(List<string> errors)
    {
        if (format != FormatName)
            errors.Add($"format は \"{FormatName}\" である必要があります。");
        if (version < 1 || version > CurrentVersion)
            errors.Add($"version {version} には対応していません（最大 {CurrentVersion}）。");
        if (string.IsNullOrEmpty(id) || !IdPattern.IsMatch(id))
            errors.Add("id は英小文字・数字・-・_ の 1〜40 文字にしてください。");
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("name（場面の名前）がありません。");
        if (map != HikoneMap)
            errors.Add($"map は \"{HikoneMap}\" のみ対応しています。");
        if (!PlayerModes.Contains(playerMode))
            errors.Add("playerMode は walking か bicycle です。");
        if (!Settings.Contains(setting))
            errors.Add("setting は crossing / midblock / bicycle のいずれかです。");
        if (spawn == null || goal == null || trigger == null)
        {
            errors.Add("spawn / goal / trigger がありません。");
            return;
        }
        CheckInside("spawn", spawn.x, spawn.z, errors);
        CheckInside("goal", goal.x, goal.z, errors);
        if (goal.width < 0.5f || goal.depth < 0.5f)
            errors.Add("goal の幅・奥行きは 0.5 m 以上にしてください。");
        if (new Vector2(goal.x - spawn.x, goal.z - spawn.z).magnitude < 3f)
            errors.Add("spawn と goal は 3 m 以上離してください。");
        if (HasTrigger)
            CheckInside("trigger", trigger.x, trigger.z, errors);

        if (vehicles == null)
            vehicles = new ScenarioVehicle[0];
        for (var i = 0; i < vehicles.Length; i++)
        {
            var v = vehicles[i];
            var label = $"vehicles[{i}]" + (string.IsNullOrEmpty(v?.name) ? "" : $"（{v.name}）");
            if (v == null)
            {
                errors.Add(label + " が空です。");
                continue;
            }
            if (v.route == null || v.route.Length < 2)
                errors.Add(label + ": route には 2 点以上が必要です。");
            else
                foreach (var p in v.route)
                    CheckInside(label + ".route", p.x, p.z, errors);
            if (v.speedKmh < 5f || v.speedKmh > 80f)
                errors.Add(label + ": speedKmh は 5〜80 です。");
            if (Array.IndexOf(ScenarioVehicle.Bodies, v.body ?? "") < 0)
                errors.Add(label + ": body は 空（セダン）・kei-tall・kei-hatch のいずれかです。");
            if (v.start != ScenarioVehicle.StartBegin && v.start != ScenarioVehicle.StartTrigger)
                errors.Add(label + ": start は begin か trigger です。");
            if (v.delaySeconds < 0f || v.delaySeconds > 120f)
                errors.Add(label + ": delaySeconds は 0〜120 秒です。");
            if (v.repeatMinSeconds < 0f || v.repeatMaxSeconds < v.repeatMinSeconds)
                errors.Add(label + ": repeatMinSeconds ≤ repeatMaxSeconds にしてください。");
            if (v.IsRepeating && v.repeatMinSeconds < 1f)
                errors.Add(label + ": 繰り返し間隔は 1 秒以上にしてください。");
            if ((v.start == ScenarioVehicle.StartTrigger || v.stopOnTrigger) && !HasTrigger)
                errors.Add(label + ": trigger を使うには trigger エリアが必要です。");
        }
    }

    static void CheckInside(string label, float x, float z, List<string> errors)
    {
        if (!HikoneBounds.Contains(new Vector2(x, z)))
            errors.Add($"{label} ({x:0.#}, {z:0.#}) が地図の外です。");
    }
}

[Serializable]
public sealed class GroundPoint
{
    public float x;
    public float z;

    public GroundPoint() { }
    public GroundPoint(float x, float z) { this.x = x; this.z = z; }
    public Vector3 ToVector3(float y = 0f) => new Vector3(x, y, z);
}

/// <summary>Rectangle on the ground: centre, size along its own x (width) and z (depth), yaw in degrees.</summary>
[Serializable]
public sealed class GroundArea
{
    public float x;
    public float z;
    public float width = 4f;
    public float depth = 4f;
    public float yaw;
}

[Serializable]
public sealed class ScenarioVehicle
{
    public const string StartBegin = "begin";
    public const string StartTrigger = "trigger";

    public string name;
    /// <summary>Vehicle prefab name (Car_Left, Car_Right, Car_SideHit, Car_EndlessGo).</summary>
    public string model = "Car_Left";
    /// <summary>Car body: "" = the prefab's sedan, or a kei car ("kei-tall", "kei-hatch"); see VehicleBody.</summary>
    public string body = "";
    /// <summary>Allowed bodies; the kei ids must match Resources/VehicleBodies (a test checks it).</summary>
    public static readonly string[] Bodies = { "", "kei-tall", "kei-hatch" };
    /// <summary>The car appears at route[0] and drives through the rest.</summary>
    public GroundPoint[] route = new GroundPoint[0];
    public float speedKmh = 36f;
    /// <summary>"begin": counted from the scenario start; "trigger": from entering the trigger area.</summary>
    public string start = StartBegin;
    public float delaySeconds;
    /// <summary>0 = one car. Otherwise another car every repeatMin..repeatMax seconds.</summary>
    public float repeatMinSeconds;
    public float repeatMaxSeconds;
    /// <summary>Stop spawning this vehicle (background traffic) when the trigger fires.</summary>
    public bool stopOnTrigger;
    /// <summary>Marks the car as the accident car (CSV AcidentCar = 1).</summary>
    public bool accident;
    /// <summary>Initial heading; otherwise the car faces its first route segment.</summary>
    public bool overrideYaw;
    public float yaw;

    public bool IsRepeating => repeatMaxSeconds > 0f;
    public float SpeedMetersPerSecond => speedKmh / 3.6f;
}

/// <summary>The custom scenario chosen for the current run (survives scene reloads for "Try again").</summary>
public static class CustomScenarioSession
{
    /// <summary>GameDirector.EventNumber of every custom scenario (the CSV adds the scenario id).</summary>
    public const int EventNumber = 100;

    public static CustomScenario Current { get; private set; }

    public static bool IsCustom(int eventNumber) => eventNumber == EventNumber;

    public static void Select(CustomScenario scenario) => Current = scenario;

    public static void Clear() => Current = null;

#if UNITY_EDITOR
    const string EditorPathKey = "VRLearn.CustomScenarioPath";

    /// <summary>Editor play-mode entry reloads the domain; remember the file across it.</summary>
    public static void RememberForPlayMode(string path) => UnityEditor.SessionState.SetString(EditorPathKey, path ?? "");

    public static string RememberedPath => UnityEditor.SessionState.GetString(EditorPathKey, "");

    const string EditorPlayKey = "VRLearn.PlayCustomScenarioOnce";

    /// <summary>The next gameplay start in this Editor session runs the remembered file.</summary>
    public static void RequestEditorPlay()
    {
        UnityEditor.SessionState.SetBool(EditorPlayKey, true);
        Current = null;
    }

    public static bool ConsumeEditorPlayRequest()
    {
        if (!UnityEditor.SessionState.GetBool(EditorPlayKey, false))
            return false;
        UnityEditor.SessionState.EraseBool(EditorPlayKey);
        return true;
    }
#endif

    /// <summary>Current, or (in the Editor) the file chosen with Tools/VRLearn/Custom Scenarios/Play.</summary>
    public static CustomScenario Resolve(List<string> errors)
    {
        if (Current != null)
            return Current;
#if UNITY_EDITOR
        var path = RememberedPath;
        if (!string.IsNullOrEmpty(path))
        {
            var scenario = CustomScenarioLibrary.Load(path, errors);
            if (scenario != null)
                Current = scenario;
            return scenario;
        }
#endif
        errors.Add("カスタム場面が選ばれていません。");
        return null;
    }
}

/// <summary>Finds and loads scenario files.</summary>
public static class CustomScenarioLibrary
{
    /// <summary>On the headset: files synced from the PC. Also used in the Editor.</summary>
    public static string DeviceFolder => Path.Combine(Application.persistentDataPath, "Scenarios");

    /// <summary>Editor only: the repository's Scenarios folder, shared with the scenario editor.</summary>
    public static string ProjectFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Scenarios"));

    public static IEnumerable<string> Folders
    {
        get
        {
            yield return DeviceFolder;
            if (Application.isEditor)
                yield return ProjectFolder;
        }
    }

    /// <summary>Scenario files only: not the schema, the exported maps, or hidden folders/files (.trash, .play-request).</summary>
    public static bool IsScenarioFile(string folder, string path)
    {
        var relative = path.Substring(folder.Length).Replace('\\', '/').TrimStart('/');
        var parts = relative.Split('/');
        if (parts.Any(p => p.StartsWith(".")) || parts[0] == "maps")
            return false;
        return !Path.GetFileName(path).EndsWith(".schema.json");
    }

    public static CustomScenario Load(string path, List<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add("ファイルがありません: " + path);
            return null;
        }
        return CustomScenario.FromJson(File.ReadAllText(path), errors);
    }

    /// <summary>
    /// Scenarios offered on the title screen: valid files only, the headset folder first, ids unique,
    /// templates (Scenarios/templates, the exported built-ins) left out. Also returns how many files
    /// could not be used.
    /// </summary>
    public static (List<CustomScenario> scenarios, int rejected) ScanForTitle()
    {
        var result = new List<CustomScenario>();
        var ids = new HashSet<string>();
        var rejected = 0;
        foreach (var (path, scenario, _) in Scan())
        {
            if (path.Replace('\\', '/').Contains("/templates/"))
                continue;
            if (scenario == null || !ids.Add(scenario.id))
            {
                rejected++;
                continue;
            }
            result.Add(scenario);
        }
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return (result, rejected);
    }

    /// <summary>Every readable scenario file (invalid files are reported, not thrown).</summary>
    public static List<(string path, CustomScenario scenario, List<string> errors)> Scan()
    {
        var result = new List<(string, CustomScenario, List<string>)>();
        foreach (var folder in Folders)
        {
            if (!Directory.Exists(folder))
                continue;
            foreach (var path in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).OrderBy(p => p))
            {
                if (!IsScenarioFile(folder, path))
                    continue;
                var errors = new List<string>();
                result.Add((path, Load(path, errors), errors));
            }
        }
        return result;
    }
}
