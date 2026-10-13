using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-time migration of the gameplay scene from 40 cm to 15 cm kerbs (RATIONALE.md D37).
/// Heights map as in the browser port: the carriageway (y 0.01) and everything below it stay,
/// everything at the old pavement height (0.41) and above moves down 0.25 m, the kerb faces and
/// corner ramps in between are compressed. Horizontal positions never change.
/// <list type="bullet">
/// <item>Road tiles under <c>Environment/RoadContainerNeo</c>: their render and collider meshes are
/// replaced by height-mapped copies in <c>Assets/_Project/Hikone/Kerb15</c> (the third-party
/// originals stay untouched).</item>
/// <item>Every other placed object (experiment objects, lamp poles, walls, …) moves by the change
/// of the road height under its pivot; walk-on helpers (<c>InvisibleBlock</c>) by the change of
/// their top, so a ramp never stands proud of the new pavement.</item>
/// <item><c>HikoneEnvironment</c> is generated and is rebuilt afterwards (steps 0–2).</item>
/// </list>
/// Refuses to run twice. Writes what moved to <c>Logs/kerb-migration.txt</c>.
/// </summary>
public static class HikoneKerbMigration
{
    public const float Carriageway = 0.01f, OldPavement = 0.41f, Pavement = 0.16f, Drop = OldPavement - Pavement;
    const string Scene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const string MeshDir = "Assets/_Project/Hikone/Kerb15";
    const string Report = "Logs/kerb-migration.txt";
    // the carriageway meshes sit at 0.010–0.013: moves of a few millimetres are noise, not kerb
    const float MinMove = 0.005f;
    static readonly string[] Roots =
    {
        "ScenarioHost", "OnOffContainer", "WayPointContainer", "CarFactoryContainer", "RuntimeActors",
        "Environment/InvisibleWallContainer", "Environment/RoadLineContainer", "Environment/RoadContainerNeo",
    };

    /// <summary>An old (40 cm kerb) height → the new one.</summary>
    public static float Map(float y) =>
        y <= Carriageway ? y
        : y >= OldPavement ? y - Drop
        : Carriageway + (y - Carriageway) * (Pavement - Carriageway) / (OldPavement - Carriageway);

    [MenuItem("Tools/VRLearn/Hikone/Migrate Scene To 15 cm Kerbs")]
    public static void Run() => Migrate(apply: true);

    /// <summary>Writes the report of what would move, changing nothing.</summary>
    public static void DryRun() => Migrate(apply: false);

    static void Migrate(bool apply)
    {
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var roads = GameObject.Find("Environment/RoadContainerNeo");
        var tiles = RoadTiles(roads.transform);
        var pavementTop = tiles.SelectMany(t => t.GetComponents<MeshCollider>()).Where(c => c.sharedMesh != null)
            .Max(c => c.bounds.max.y);
        if (pavementTop < OldPavement - 0.05f)
        {
            Debug.LogWarning($"[Kerb] the road colliders top out at {pavementTop:0.00}: already migrated, nothing done.");
            return;
        }

        // ground under every pivot, sampled on the original colliders before anything changes
        var colliders = tiles.SelectMany(t => t.GetComponents<MeshCollider>()).ToArray();
        float? Ground(Vector3 p)
        {
            var ray = new Ray(new Vector3(p.x, 60f, p.z), Vector3.down);
            float? best = null;
            foreach (var c in colliders)
                if (c.Raycast(ray, out var hit, 200f) && (best == null || hit.point.y > best)) best = hit.point.y;
            return best;
        }

        var moves = new List<(Transform t, float dy, string why)>();
        var tileSet = new HashSet<Transform>(tiles);
        foreach (var path in Roots)
        {
            var root = GameObject.Find(path);
            if (root == null) { Debug.LogWarning("[Kerb] missing " + path); continue; }
            foreach (Transform child in root.transform) Collect(child, tileSet, Ground, moves);
        }

        var report = new StringBuilder($"Kerb migration of {Scene}{(apply ? "" : " (dry run)")}: pavement {OldPavement} -> {Pavement}\n");
        var meshes = tiles.SelectMany(t => t.GetComponents<MeshFilter>().Select(f => f.sharedMesh)
            .Concat(t.GetComponents<MeshCollider>().Select(c => c.sharedMesh))).Where(m => m != null).Distinct().ToList();
        report.Append($"road tiles: {tiles.Count}, meshes to map: {string.Join(", ", meshes.Select(m => m.name))}\n");
        foreach (var tile in tiles.Where(t => !PlainPlacement(t)))
            throw new System.InvalidOperationException($"{tile.name}: road tile is tilted; the height mapping assumes a rotation about y only");
        foreach (var (t, dy, why) in moves.Where(m => Mathf.Abs(m.dy) >= MinMove))
            report.Append($"{Path(t)}\t{dy:+0.000;-0.000}\t{why}\n");
        report.Append($"objects moved: {moves.Count(m => Mathf.Abs(m.dy) >= MinMove)} of {moves.Count} units\n");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, report.ToString());
        if (!apply) return;

        Directory.CreateDirectory(MeshDir);
        var mapped = new Dictionary<(Mesh, float, float), Mesh>();
        foreach (var tile in tiles)
        {
            foreach (var mf in tile.GetComponents<MeshFilter>()) mf.sharedMesh = MapMesh(mf.sharedMesh, tile, mapped);
            foreach (var mc in tile.GetComponents<MeshCollider>()) mc.sharedMesh = MapMesh(mc.sharedMesh, tile, mapped);
            EditorUtility.SetDirty(tile.gameObject);
        }
        report.Append($"mapped mesh assets: {mapped.Count}\n");
        File.WriteAllText(Report, report.ToString());
        foreach (var (t, dy, _) in moves.Where(m => Mathf.Abs(m.dy) >= MinMove))
        {
            t.position += Vector3.up * dy;
            EditorUtility.SetDirty(t);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Kerb] migrated: {tiles.Count} road tiles, {moves.Count(m => Mathf.Abs(m.dy) >= MinMove)} objects moved (see {Report})");
    }

    /// <summary>Road tiles: objects carrying the road meshes (not the lamp poles).</summary>
    static List<Transform> RoadTiles(Transform roads) =>
        roads.GetComponentsInChildren<MeshCollider>(true)
            .Where(c => c.sharedMesh != null && !c.sharedMesh.name.StartsWith("Pole"))
            .Select(c => c.transform).Distinct().ToList();

    /// <summary>Rotated about y only: a vertex's world height is position.y + scale.y · local y.</summary>
    static bool PlainPlacement(Transform t) => Mathf.Abs(Vector3.Dot(t.up, Vector3.up) - 1f) < 1e-4f;

    /// <summary>
    /// A unit moves as a whole: the first object down the hierarchy that has a collider, renderer,
    /// light, animator or rigidbody, or is a leaf point (spawns, waypoints). Containers, also those that only carry scripts,
    /// are walked into; road tiles are mapped, not moved.
    /// </summary>
    static void Collect(Transform t, HashSet<Transform> tiles, System.Func<Vector3, float?> ground, List<(Transform, float, string)> moves)
    {
        if (tiles.Contains(t))
        {
            foreach (Transform child in t) Collect(child, tiles, ground, moves);
            return;
        }
        // an Animator or Rigidbody holds its children together (the avatar's mesh and skeleton)
        var physical = t.GetComponent<Collider>() != null || t.GetComponent<Renderer>() != null || t.GetComponent<Light>() != null
            || t.GetComponent<Animator>() != null || t.GetComponent<Rigidbody>() != null
            || t.name.StartsWith("Pole1");                 // lamp-pole groups: step 0 exports their position
        if (!physical && t.childCount > 0)
        {
            foreach (Transform child in t) Collect(child, tiles, ground, moves);
            return;
        }
        if (t.name.StartsWith("InvisibleBlock"))
        {
            var c = t.GetComponent<Collider>();
            var top = c != null && c.bounds.size.y > 0f ? c.bounds.max.y : t.position.y;
            moves.Add((t, Map(top) - top, $"walk-on helper, top {top:0.00}"));
            return;
        }
        var g = ground(t.position);
        if (g.HasValue) moves.Add((t, Map(g.Value) - g.Value, $"ground {g.Value:0.00}"));
        else moves.Add((t, t.position.y >= 0.2f ? -Drop : 0f, "off the roads"));
    }

    /// <summary>A copy of the mesh whose world heights are mapped, for one placement height and y scale.</summary>
    static Mesh MapMesh(Mesh source, Transform placement, Dictionary<(Mesh, float, float), Mesh> done)
    {
        if (source == null) return null;
        float baseY = Mathf.Round(placement.position.y * 10000f) / 10000f, scaleY = placement.lossyScale.y;
        if (done.TryGetValue((source, baseY, scaleY), out var copy)) return copy;
        copy = Object.Instantiate(source);
        copy.name = $"{source.name}_Kerb15_y{Mathf.RoundToInt(baseY * 1000f)}";
        copy.vertices = source.vertices.Select(v => new Vector3(v.x, (Map(baseY + scaleY * v.y) - baseY) / scaleY, v.z)).ToArray();
        copy.RecalculateNormals();
        copy.RecalculateBounds();
        var path = $"{MeshDir}/{copy.name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(copy, path);
        done[(source, baseY, scaleY)] = copy;
        return copy;
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
