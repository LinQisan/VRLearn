using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene data for the browser port Hikone Traffic Simulator (a separate repository, ../hikone-traffic-sim by default; override with the
/// VRLEARN_WEB environment variable): the Hikone materials as authored in Unity, light and fog,
/// traffic signals, black walls and, per built-in scenario, the spawn heading and the parked
/// vehicles (with their body). The port rebuilds the environment from the same layout and models.
/// Coordinates are Unity world metres; yaw in degrees (Unity's clockwise-from-above, 0 = +z).
/// </summary>
public static class WebSceneExporter
{
    const string Scene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const string MaterialDir = "Assets/_Project/Hikone/Materials";

    [Serializable] class Mat { public string name; public float[] color; public string texture; public float tileMeters; public float smoothness; public float metallic; public bool emissive; public string normalMap; public float normalTileMeters; }
    [Serializable] class Placed { public string kind; public string model; public float x, y, z, yaw; public float[] size; }
    /// <summary>A mesh in its own space (Unity coordinates).</summary>
    [Serializable] class MeshData { public string name; public float[] vertices; public int[] indices; }
    /// <summary>One renderer: a mesh of <see cref="SignalData.meshes"/> placed by a local-to-world matrix (column-major, Unity coordinates).</summary>
    [Serializable] class MeshPart { public string name; public string group; public int mesh; public float[] matrix; public float[] color; public float[] emission; }
    /// <summary>signals.json: the traffic signals exactly as built in the scene (head, lamps, arm, pole).</summary>
    [Serializable] class SignalData { public string format = "vrlearn-web-signals"; public int version = 1; public List<MeshData> meshes = new List<MeshData>(); public MeshPart[] parts; }
    [Serializable] class ScenarioData { public int id; public float spawnYaw; public Placed[] parked = new Placed[0]; }
    [Serializable] class Light { public float[] direction; public float[] color; public float intensity; public float[] ambientSky, ambientEquator, ambientGround; public float[] fogColor; public float fogStart, fogEnd; public float[] background; }
    [Serializable]
    class Data
    {
        public string format = "vrlearn-web-scene";
        public int version = 1;
        public string exportedFrom = Scene;
        public Mat[] materials;
        public Light light;
        public Placed[] signals;
        public Placed[] blackWalls;
        public ScenarioData[] scenarios;
    }

    public static string OutputFolder =>
        Environment.GetEnvironmentVariable("VRLEARN_WEB") is string dir && dir.Length > 0
            ? Path.Combine(dir, "data")
            : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "hikone-traffic-sim", "data"));

    [MenuItem("Tools/VRLearn/Web/Export Scene For Web")]
    public static void Export()
    {
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var data = new Data
        {
            materials = ExportMaterials(),
            light = ExportLight(),
            signals = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("TrafficLight_"))
                .OrderBy(t => t.name, StringComparer.Ordinal).ThenBy(t => t.position.x).ThenBy(t => t.position.z)
                .Select(t => Place(t, t.name.Contains("Car") ? "car" : "pedestrian", t.name)).ToArray(),
            blackWalls = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("BlackWall") && t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().enabled)
                .Select(t =>
                {
                    var p = Place(t, "blackwall", t.name);
                    p.size = new[] { t.lossyScale.x, t.lossyScale.y, t.lossyScale.z };
                    return p;
                }).ToArray(),
            scenarios = ExportScenarios(),
        };
        Directory.CreateDirectory(OutputFolder);
        var path = Path.Combine(OutputFolder, "scene.json");
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        var signals = new SignalData();
        var meshIndex = new Dictionary<string, int>();
        signals.parts = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("TrafficLight_"))
                .OrderBy(t => t.position.x).ThenBy(t => t.position.z)
                .SelectMany(t => t.GetComponentsInChildren<MeshRenderer>(false).Where(r => r.enabled)
                    .Select(r => Part(r, $"{t.parent?.name}/{t.name}@{t.position.x:0.0},{t.position.z:0.0}", signals, meshIndex)))
                .Where(m => m != null).ToArray();
        File.WriteAllText(Path.Combine(OutputFolder, "signals.json"), JsonUtility.ToJson(signals, false));
        Debug.Log($"[Web] scene data → {path} ({data.materials.Length} materials, {data.signals.Length} signals, {data.scenarios.Length} scenarios), signals.json ({signals.parts.Length} parts, {signals.meshes.Count} meshes)");
    }

    static Placed Place(Transform t, string kind, string model) => new Placed
    {
        kind = kind, model = model, x = t.position.x, y = t.position.y, z = t.position.z, yaw = t.eulerAngles.y
    };

    static float Mm(float v) => Mathf.Round(v * 1000f) / 1000f;

    static float[] Rgb(Color c) => new[] { c.r, c.g, c.b };

    static MeshPart Part(MeshRenderer r, string group, SignalData data, Dictionary<string, int> meshIndex)
    {
        var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null) return null;
        // ProBuilder gives every lamp its own copy of the same mesh: share identical ones
        var vertices = mesh.vertices.SelectMany(v => new[] { Mm(v.x), Mm(v.y), Mm(v.z) }).ToArray();
        var indices = mesh.triangles;
        var key = string.Join(",", vertices) + "|" + string.Join(",", indices);
        if (!meshIndex.TryGetValue(key, out var index))
        {
            meshIndex[key] = index = data.meshes.Count;
            data.meshes.Add(new MeshData { name = mesh.name, vertices = vertices, indices = indices });
        }
        var m = r.localToWorldMatrix;
        var matrix = new float[16];
        for (var i = 0; i < 16; i++) matrix[i] = m[i % 4, i / 4];
        var mat = r.sharedMaterial;
        var color = mat == null ? Color.gray : mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
        var emission = mat != null && mat.HasProperty("_EmissionColor") && mat.IsKeywordEnabled("_EMISSION") ? mat.GetColor("_EmissionColor") : Color.black;
        return new MeshPart { name = r.name, group = group, color = Rgb(color), emission = Rgb(emission), mesh = index, matrix = matrix };
    }

    static Mat[] ExportMaterials()
    {
        var list = new List<Mat>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialDir }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            var bump = m.HasProperty("_BumpMap") && m.IsKeywordEnabled("_NORMALMAP") ? m.GetTexture("_BumpMap") : null;
            var scale = tex != null ? m.GetTextureScale("_BaseMap").x : 1f;
            var bumpScale = bump != null ? m.GetTextureScale("_BumpMap").x : 1f;
            list.Add(new Mat
            {
                name = m.name,
                color = Rgb(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white),
                texture = tex != null ? tex.name : null,
                tileMeters = scale > 0f ? 1f / scale : 1f,
                smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.1f,
                metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f,
                emissive = m.IsKeywordEnabled("_EMISSION"),
                normalMap = bump != null ? bump.name : null,
                normalTileMeters = bumpScale > 0f ? 1f / bumpScale : 1f,
            });
        }
        return list.OrderBy(m => m.name, StringComparer.Ordinal).ToArray();
    }

    static Light ExportLight()
    {
        var sun = RenderSettings.sun != null ? RenderSettings.sun
            : UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        var camera = Camera.main;
        return new Light
        {
            direction = sun != null ? new[] { sun.transform.forward.x, sun.transform.forward.y, sun.transform.forward.z } : new[] { 0.3f, -0.8f, 0.4f },
            color = sun != null ? Rgb(sun.color) : new[] { 1f, 0.96f, 0.9f },
            intensity = sun != null ? sun.intensity : 1f,
            ambientSky = Rgb(RenderSettings.ambientSkyColor),
            ambientEquator = Rgb(RenderSettings.ambientEquatorColor),
            ambientGround = Rgb(RenderSettings.ambientGroundColor),
            fogColor = Rgb(RenderSettings.fogColor),
            fogStart = RenderSettings.fogStartDistance,
            fogEnd = RenderSettings.fogEndDistance,
            background = camera != null ? Rgb(camera.backgroundColor) : new[] { 0.55f, 0.72f, 0.9f },
        };
    }

    static ScenarioData[] ExportScenarios()
    {
        var runtime = UnityEngine.Object.FindFirstObjectByType<ScenarioRuntime>(FindObjectsInactive.Include);
        var entries = new SerializedObject(runtime).FindProperty("entries");
        var result = new List<ScenarioData>();
        for (var i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var root = entry.FindPropertyRelative("root").objectReferenceValue as GameObject;
            var spawn = entry.FindPropertyRelative("playerSpawn").objectReferenceValue as Transform;
            var asset = entry.FindPropertyRelative("asset").objectReferenceValue as ScenarioDefinitionAsset;
            var parked = new List<Placed>();
            if (root != null)
            {
                // parked sedans (the imported car model) and the trucks (their Hikone box-truck visual)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "HK_TruckVisual")
                    {
                        var prefab = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                        parked.Add(Place(t, "truck", prefab != null ? prefab.name : "HK_Truck_Large"));
                    }
                    else if (t.name == "Car_NO.1201" && t.parent != null && t.parent.name.StartsWith("Car 5"))
                        parked.Add(Place(t.parent, "car", "sedan"));
                }
            }
            result.Add(new ScenarioData
            {
                id = asset != null ? asset.id : i,
                spawnYaw = spawn != null ? spawn.eulerAngles.y : 0f,
                parked = parked.ToArray(),
            });
        }
        return result.ToArray();
    }
}
