using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Occlusion check of DESIGN.md §4 (hard rule 4), run after any change to buildings, trees, walls
/// or other visuals. For each built-in scenario (from Scenarios/templates, only that scenario's
/// objects active): five eye points (1.6 m) from spawn to goal look at
/// 1. where its vehicles appear (route starts) and every DestroyArea (1.2 m): exposed pairs, and
/// 2. its accident cars' routes, sampled every 2 m (1.2 m): share visible within 35 m.
/// Occluders are the rendered meshes (temporary MeshColliders on a spare layer; the scene is not
/// saved). Report: Logs/occlusion-report.txt.
/// </summary>
public static class HikoneOcclusionCheck
{
    const string Scene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const string Report = "Logs/occlusion-report.txt";
    const int ProbeLayer = 30;
    const float Eye = 1.6f, Target = 1.2f, Range = 35f, Step = 2f;

    [Serializable] class P { public float x, z; }
    [Serializable] class Veh { public P[] route; public bool accident; }
    [Serializable] class Tpl { public string id; public P spawn; public P goal; public Veh[] vehicles; }

    [MenuItem("Tools/VRLearn/Hikone/3. Check Occlusion")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var entries = ScenarioRoots();
        var destroyAreas = UnityEngine.Object.FindObjectsByType<DestroyArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(d => d.transform.position).ToArray();
        var text = new StringBuilder();
        text.AppendLine($"Occlusion check {DateTime.Now:yyyy-MM-dd HH:mm} (eye {Eye} m, target {Target} m, route step {Step} m, range {Range} m)");
        text.AppendLine("scenario | appear/despawn pairs visible | accident route visible within range | occluding meshes");
        try
        {
            for (var n = 1; n <= 10; n++)
            {
                var tpl = JsonUtility.FromJson<Tpl>(File.ReadAllText($"Scenarios/templates/builtin-{n:00}.json"));
                for (var i = 0; i < entries.Count; i++)
                    if (entries[i] != null) entries[i].SetActive(i == n - 1);
                var probes = BuildProbes();
                try
                {
                    var eyes = Enumerable.Range(0, 5).Select(k =>
                    {
                        var p = Vector3.Lerp(new Vector3(tpl.spawn.x, 0f, tpl.spawn.z), new Vector3(tpl.goal.x, 0f, tpl.goal.z), k / 4f);
                        return Ground(p) + Vector3.up * Eye;
                    }).ToArray();
                    var appear = tpl.vehicles.Where(v => v.route != null && v.route.Length > 0)
                        .Select(v => new Vector3(v.route[0].x, 0f, v.route[0].z)).Distinct().Concat(destroyAreas)
                        .Select(p => Ground(p) + Vector3.up * Target).ToArray();
                    int pairs = 0, seen = 0;
                    foreach (var e in eyes)
                    foreach (var t in appear)
                    {
                        pairs++;
                        if (!Physics.Linecast(e, t, 1 << ProbeLayer, QueryTriggerInteraction.Ignore)) seen++;
                    }
                    int samples = 0, visible = 0;
                    foreach (var v in tpl.vehicles.Where(v => v.accident && v.route != null))
                    foreach (var s in Sample(v.route))
                    {
                        var t = Ground(s) + Vector3.up * Target;
                        var near = eyes.Where(e => Vector3.Distance(e, t) <= Range).ToArray();
                        if (near.Length == 0) continue;
                        samples++;
                        if (near.Any(e => !Physics.Linecast(e, t, 1 << ProbeLayer, QueryTriggerInteraction.Ignore))) visible++;
                    }
                    text.AppendLine($"S{n:00} | {seen}/{pairs} | " + (samples > 0 ? $"{100f * visible / samples:0}% ({visible}/{samples})" : "-") + $" | {probes.Count}");
                }
                finally
                {
                    foreach (var p in probes) UnityEngine.Object.DestroyImmediate(p);
                }
            }
        }
        finally
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);   // discard the probes and activations
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, text.ToString());
        Debug.Log("[Occlusion]\n" + text);
    }

    static List<GameObject> ScenarioRoots()
    {
        var runtime = UnityEngine.Object.FindFirstObjectByType<ScenarioRuntime>(FindObjectsInactive.Include);
        var list = new List<GameObject>();
        var entries = new SerializedObject(runtime).FindProperty("entries");
        for (var i = 0; i < entries.arraySize; i++)
            list.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue as GameObject);
        return list;
    }

    /// <summary>A collider copy of every visible opaque mesh (moving traffic and the rig excluded).</summary>
    static List<GameObject> BuildProbes()
    {
        var probes = new List<GameObject>();
        foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || r.GetComponentInParent<CarController>() != null || r.GetComponentInParent<Canvas>() != null
                || r.transform.root.name.StartsWith("Meta_XR_Player")) continue;
            if (r.sharedMaterials.Any(m => m == null || m.renderQueue >= 2450)) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var go = new GameObject("OcclusionProbe") { layer = ProbeLayer, hideFlags = HideFlags.DontSave };
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            probes.Add(go);
        }
        Physics.SyncTransforms();
        return probes;
    }

    static Vector3 Ground(Vector3 p)
    {
        var from = new Vector3(p.x, 30f, p.z);
        foreach (var hit in Physics.RaycastAll(from, Vector3.down, 60f, ~(1 << ProbeLayer), QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (hit.point.y < 5f) return new Vector3(p.x, hit.point.y, p.z);
        return new Vector3(p.x, 0.01f, p.z);
    }

    static IEnumerable<Vector3> Sample(P[] route)
    {
        for (var i = 0; i + 1 < route.Length; i++)
        {
            var a = new Vector3(route[i].x, 0f, route[i].z);
            var b = new Vector3(route[i + 1].x, 0f, route[i + 1].z);
            var n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / Step));
            for (var k = 0; k < n; k++) yield return Vector3.Lerp(a, b, k / (float)n);
        }
    }
}
