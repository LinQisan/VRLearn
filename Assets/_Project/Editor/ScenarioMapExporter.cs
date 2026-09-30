using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Exports what the web scenario editor needs to draw and check scenarios on the Hikone map:
/// a top-down image and <c>map.json</c> (world ↔ pixel mapping, carriageway / sidewalk surfaces
/// from the road colliders, the invisible walls that bound the participant, the built-in vehicle
/// lanes, traffic signals). Output: Scenarios/maps/hikone-kyobashi/.
/// </summary>
public static class ScenarioMapExporter
{
    const string HikoneScene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const float Margin = 20f;
    const int MaxPixels = 4096;

    public static string OutputFolder =>
        Path.Combine(CustomScenarioLibrary.ProjectFolder, "maps", CustomScenario.HikoneMap);

    // ------------------------------------------------------------------ data written to map.json
    [Serializable] public sealed class MapFile
    {
        public string format = "vrlearn-map";
        public int version = 1;
        public string id = CustomScenario.HikoneMap;
        public string name = "彦根・京橋 / Hikone Kyobashi";
        public string image = "map.png";
        public int imageWidth;
        public int imageHeight;
        public float pixelsPerMeter;
        /// <summary>World rectangle covered by the image. Image right = +x, image up = +z.</summary>
        public WorldRect world = new WorldRect();
        public string coordinates = "Unity world metres on the ground plane. px = (x - xMin) * pixelsPerMeter; py (from top) = (zMax - z) * pixelsPerMeter. Yaw in degrees, clockwise seen from above, 0 = +z.";
        /// <summary>Triangles as flat [x0,z0,x1,z1,x2,z2, ...].</summary>
        public Surface[] surfaces = new Surface[0];
        public Wall[] walls = new Wall[0];
        public Lane[] lanes = new Lane[0];
        public Marker[] signals = new Marker[0];
        /// <summary>Values allowed for a vehicle's "model" in scenario files.</summary>
        public string[] vehicleModels = new string[0];
        public string exportedFrom = HikoneScene;
    }

    [Serializable] public sealed class WorldRect { public float xMin, zMin, xMax, zMax; }
    [Serializable] public sealed class Surface { public string kind; public float height; public float[] triangles; }
    /// <summary>Footprint of an invisible wall (closed polygon, x/z pairs).</summary>
    [Serializable] public sealed class Wall { public string name; public float[] polygon; }
    /// <summary>A built-in vehicle route (lane centre line, driving direction = point order).</summary>
    [Serializable] public sealed class Lane { public string id; public string name; public GroundPoint[] points; }
    [Serializable] public sealed class Marker { public string name; public float x, z, yaw; }

    [MenuItem("Tools/VRLearn/Custom Scenarios/Export Map For Scenario Editor")]
    public static void Export()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(HikoneScene);
        var map = BuildData();
        Directory.CreateDirectory(OutputFolder);
        RenderImage(map, Path.Combine(OutputFolder, map.image));
        File.WriteAllText(Path.Combine(OutputFolder, "map.json"), RoundJson(JsonUtility.ToJson(map, true)) + "\n");
        Debug.Log($"[Map] {map.imageWidth}x{map.imageHeight} px, {map.pixelsPerMeter} px/m, " +
                  $"{map.surfaces.Sum(s => s.triangles.Length / 6)} triangles, {map.walls.Length} walls, " +
                  $"{map.lanes.Length} lanes → {OutputFolder}");
    }

    public static MapFile BuildData()
    {
        var map = new MapFile();
        map.surfaces = CollectSurfaces();
        map.walls = CollectWalls();
        map.lanes = CollectLanes();
        map.signals = UnityEngine.Object.FindObjectsByType<TrafficSignalPreset>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(s => new Marker
            {
                name = s.name + (s.transform.parent != null ? " / " + s.transform.parent.name : ""),
                x = s.transform.position.x, z = s.transform.position.z, yaw = s.transform.eulerAngles.y
            }).ToArray();

        map.vehicleModels = UnityEngine.Object.FindObjectsByType<CarFactory>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(f => f.CarPrefab != null).Select(f => f.CarPrefab.name).Distinct().OrderBy(n => n).ToArray();

        // frame: everything a vehicle or participant can use, plus a margin
        var xs = new List<float>();
        var zs = new List<float>();
        foreach (var s in map.surfaces.Where(s => s.kind != "ground"))
            for (var i = 0; i < s.triangles.Length; i += 2) { xs.Add(s.triangles[i]); zs.Add(s.triangles[i + 1]); }
        foreach (var lane in map.lanes)
            foreach (var p in lane.points) { xs.Add(p.x); zs.Add(p.z); }
        map.world.xMin = Mathf.Floor(xs.Min() - Margin);
        map.world.xMax = Mathf.Ceil(xs.Max() + Margin);
        map.world.zMin = Mathf.Floor(zs.Min() - Margin);
        map.world.zMax = Mathf.Ceil(zs.Max() + Margin);
        var width = map.world.xMax - map.world.xMin;
        var height = map.world.zMax - map.world.zMin;
        map.pixelsPerMeter = Mathf.Min(10f, Mathf.Floor(MaxPixels / Mathf.Max(width, height)));
        map.imageWidth = Mathf.RoundToInt(width * map.pixelsPerMeter);
        map.imageHeight = Mathf.RoundToInt(height * map.pixelsPerMeter);
        return map;
    }

    /// <summary>Upward faces of the road colliders: carriageway (y≈0.01), sidewalk (y≈0.41), ramps, ground.</summary>
    static Surface[] CollectSurfaces()
    {
        var buckets = new Dictionary<string, (float height, List<float> tris)>();
        void Add(string kind, float y, Vector3 a, Vector3 b, Vector3 c)
        {
            if (!buckets.TryGetValue(kind, out var bucket))
                buckets[kind] = bucket = (y, new List<float>());
            bucket.tris.AddRange(new[] { a.x, a.z, b.x, b.z, c.x, c.z });
        }

        var ground = UnityEngine.Object.FindObjectsByType<GroundSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var surface in ground)
        foreach (var collider in surface.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger)
                continue;
            var isGround = collider.transform.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("GroundContainer"));
            foreach (var (a, b, c) in UpwardTriangles(collider))
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var y = (a.y + b.y + c.y) / 3f;
                // only the surface people and cars stand on: nothing of this collider above it
                var centroid = (a + b + c) / 3f;
                if (!collider.Raycast(new Ray(centroid + Vector3.up * 5f, Vector3.down), out var hit, 10f)
                    || Mathf.Abs(hit.point.y - y) > 0.03f)
                    continue;
                string kind;
                if (isGround) kind = "ground";
                else if (Mathf.Abs(normal.y) < 0.995f) kind = "ramp";
                else if (y < 0.2f) kind = "carriageway";
                else kind = "sidewalk";
                Add(kind, kind == "carriageway" ? 0.01f : kind == "sidewalk" ? 0.41f : y, a, b, c);
            }
        }
        var order = new[] { "ground", "carriageway", "ramp", "sidewalk" };
        return buckets.OrderBy(b => Array.IndexOf(order, b.Key))
            .Select(b => new Surface { kind = b.Key, height = b.Value.height, triangles = b.Value.tris.ToArray() })
            .ToArray();
    }

    static IEnumerable<(Vector3, Vector3, Vector3)> UpwardTriangles(Collider collider)
    {
        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            var mesh = meshCollider.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var t = collider.transform;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = t.TransformPoint(vertices[triangles[i]]);
                var b = t.TransformPoint(vertices[triangles[i + 1]]);
                var c = t.TransformPoint(vertices[triangles[i + 2]]);
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-8f)
                    continue;
                if (Mathf.Abs(n.normalized.y) > 0.7f)    // winding varies after the mirrored import
                    yield return (a, b, c);
            }
        }
        else if (collider is BoxCollider box)
        {
            var t = box.transform;
            var e = box.size * 0.5f;
            var top = new[]
            {
                t.TransformPoint(box.center + new Vector3(-e.x, e.y, -e.z)),
                t.TransformPoint(box.center + new Vector3(-e.x, e.y, e.z)),
                t.TransformPoint(box.center + new Vector3(e.x, e.y, e.z)),
                t.TransformPoint(box.center + new Vector3(e.x, e.y, -e.z))
            };
            if (Vector3.Cross(top[1] - top[0], top[2] - top[0]).normalized.y > 0.7f
                || Vector3.Cross(top[1] - top[0], top[2] - top[0]).normalized.y < -0.7f)
            {
                yield return (top[0], top[1], top[2]);
                yield return (top[0], top[2], top[3]);
            }
        }
    }

    static Wall[] CollectWalls()
    {
        var container = GameObject.Find("Environment/InvisibleWallContainer");
        if (container == null)
            return new Wall[0];
        return container.GetComponentsInChildren<BoxCollider>()
            .Where(b => b.enabled && !b.isTrigger)
            .Select(b =>
            {
                var t = b.transform;
                var e = b.size * 0.5f;
                var corners = new[] { new Vector3(-e.x, 0, -e.z), new Vector3(-e.x, 0, e.z), new Vector3(e.x, 0, e.z), new Vector3(e.x, 0, -e.z) }
                    .Select(c => t.TransformPoint(b.center + c));
                return new Wall { name = t.name, polygon = corners.SelectMany(c => new[] { c.x, c.z }).ToArray() };
            }).ToArray();
    }

    static Lane[] CollectLanes()
    {
        var registry = UnityEngine.Object.FindFirstObjectByType<WaypointRouteRegistry>(FindObjectsInactive.Include);
        var lanes = new List<Lane>();
        var seen = new HashSet<Transform>();
        foreach (WaypointRouteId id in Enum.GetValues(typeof(WaypointRouteId)))
        {
            var route = registry != null ? registry.Get(id) : null;
            if (route == null || !seen.Add(route))
                continue;
            lanes.Add(new Lane
            {
                id = id.ToString(),
                name = route.parent != null ? route.parent.name + "/" + route.name : route.name,
                points = route.Cast<Transform>().Select(p => new GroundPoint(p.position.x, p.position.z)).ToArray()
            });
        }
        return lanes.ToArray();
    }

    /// <summary>Orthographic top-down render of the environment only (no trees, markers, walls or rigs).</summary>
    static void RenderImage(MapFile map, string path)
    {
        var environment = GameObject.Find("Environment");
        var hiddenGroups = new[] { "StreetTrees", "Trees" };
        var changed = new List<Renderer>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var keep = environment != null && renderer.transform.IsChildOf(environment.transform)
                && !renderer.transform.GetComponentsInParent<Transform>().Any(t =>
                    hiddenGroups.Contains(t.name) || t.name == "InvisibleWallContainer");
            if (!keep && renderer.enabled)
            {
                renderer.enabled = false;
                changed.Add(renderer);
            }
        }

        // scene fog is tuned for eye level; from 250 m it washes the whole map out
        var fog = RenderSettings.fog;
        RenderSettings.fog = false;
        var go = new GameObject("__MapCamera") { hideFlags = HideFlags.HideAndDontSave };
        var texture = new RenderTexture(map.imageWidth, map.imageHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        try
        {
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = (map.world.zMax - map.world.zMin) * 0.5f;
            camera.aspect = (float)map.imageWidth / map.imageHeight;
            camera.transform.SetPositionAndRotation(
                new Vector3((map.world.xMin + map.world.xMax) * 0.5f, 250f, (map.world.zMin + map.world.zMax) * 0.5f),
                Quaternion.Euler(90f, 0f, 0f));    // looking down; camera up = +z, right = +x
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(58, 74, 52, 255);
            camera.targetTexture = texture;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            camera.Render();

            RenderTexture.active = texture;
            var image = new Texture2D(map.imageWidth, map.imageHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, map.imageWidth, map.imageHeight), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally
        {
            RenderSettings.fog = fog;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(go);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            foreach (var renderer in changed)
                if (renderer != null)
                    renderer.enabled = true;
        }
    }

    static readonly Regex LongDecimal = new Regex(@"-?\d+\.\d{3,}");

    static string RoundJson(string json) => LongDecimal.Replace(json, m =>
        Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture), 2)
            .ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
}
