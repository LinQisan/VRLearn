using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>The browser's heads, brown mast arms and plates at the preserved source anchors.</summary>
public static class HikoneSignalVisuals
{
    const string ScenePath = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const string AssetsRoot = "Assets/_Project/Hikone/Signals";
    public const string VisualName = "HK_SignalVisual";
    const string CycleName = "HK_SignalCycle";

    public static void BuildAndValidate()
    {
        HikoneEnvironmentBuilder.SetupSignals();
        Rebuild();
        HikoneOcclusionCheck.Run();
        ExportAndCapture();
        Debug.Log("[Signals] generation, occlusion, map, web export and screenshots complete.");
    }

    public static void RebuildAndCapture()
    {
        Rebuild();
        ExportAndCapture();
    }

    static void ExportAndCapture()
    {
        ScenarioMapExporter.Export();
        WebSceneExporter.Export();
        DocScreenshots.Capture();
    }

    [MenuItem("Tools/VRLearn/Hikone/4. Rebuild Traffic Signals")]
    public static void Rebuild()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Replace(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        HikoneEnvironmentBuilder.NormalizeSerializedText(ScenePath);
        foreach (var path in Directory.GetFiles(AssetsRoot))
            if (path.EndsWith(".mat") || path.EndsWith(".asset") || path.EndsWith(".meta"))
                HikoneEnvironmentBuilder.NormalizeSerializedText(path);
        HikoneEnvironmentBuilder.NormalizeSerializedText(AssetsRoot + ".meta");
        foreach (var name in new[] { "HK_Signal_Car", "HK_Signal_Pedestrian" })
            HikoneEnvironmentBuilder.NormalizeSerializedText($"Assets/_Project/Hikone/Prefabs/{name}.prefab.meta");
        foreach (var name in new[] { "Name", "Phase", "Walk" })
            HikoneEnvironmentBuilder.NormalizeSerializedText($"Assets/_Project/Hikone/Textures/HK_T_Signal{name}.png.meta");
    }

    public static void Replace(Scene scene)
    {
        if (!AssetDatabase.IsValidFolder(AssetsRoot))
            AssetDatabase.CreateFolder("Assets/_Project/Hikone", "Signals");
        var heads = UnityEngine.Object.FindObjectsByType<TrafficSignalPreset>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(s => s.gameObject.scene == scene).OrderBy(s => s.transform.position.x)
            .ThenBy(s => s.transform.position.z).ToArray();
        if (heads.Length != 6) throw new InvalidOperationException("Expected the six preserved Hikone signals.");
        foreach (var head in heads) ReplaceHead(head);
        var environment = scene.GetRootGameObjects().First(g => g.name == "Environment").transform.Find("HikoneEnvironment");
        var previous = environment.Find(CycleName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var clock = new GameObject(CycleName);
        clock.transform.SetParent(environment, false);
        clock.AddComponent<TrafficSignalCycle>().Configure(heads);
        Debug.Log("[Signals] six heads rebuilt; original transforms, colliders and lamp bindings preserved.");
    }

    static void ReplaceHead(TrafficSignalPreset head)
    {
        var t = head.transform;
        var previous = t.Find(VisualName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var pole = t.Find("Pole2");
        if (pole == null) throw new InvalidOperationException(head.name + " has no original pole anchor.");
        foreach (var renderer in head.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        var pedestrian = head.IsPedestrian;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/_Project/Hikone/Prefabs/HK_Signal_{(pedestrian ? "Pedestrian" : "Car")}.prefab");
        if (prefab == null) throw new InvalidOperationException("Import the signal models first.");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t);
        visual.name = VisualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        var sourceSize = pedestrian ? new Vector3(0.4269878566f, 0.6871233582f, 0.2046535462f)
            : new Vector3(1.78f, 0.62f, 0.19f);
        visual.transform.localScale = new Vector3(1f / sourceSize.x, 1f / sourceSize.y, 1f / sourceSize.z);
        foreach (var name in new[] { "RedLight", "YellowLight", "GreenLight" })
        {
            var lens = t.Find(name)?.GetComponent<TrafficLightController>();
            if (lens == null) continue;
            var renderers = visual.GetComponentsInChildren<Renderer>(true).Where(r => r.name == name).ToArray();
            if (renderers.Length != 1) throw new InvalidOperationException(head.name + " missing " + name);
            lens.BindVisuals(renderers);
            EditorUtility.SetDirty(lens);
        }
        BuildRig(head, visual.transform, pole.position);
        foreach (var part in visual.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = t.gameObject.layer;
    }

    static void BuildRig(TrafficSignalPreset source, Transform visual, Vector3 anchor)
    {
        var head = source.transform.position;
        var facing = -source.transform.forward;
        facing.y = 0f;
        facing.Normalize();
        var pedestrian = source.IsPedestrian;
        var height = pedestrian ? 3.75f : 7.4f;
        var radius = pedestrian ? 0.10f : 0.15f;
        var top = pedestrian ? 0.085f : 0.11f;
        var metal = new Shape(new Color(0.36f, 0.29f, 0.25f));
        var backs = new Shape(new Color(0.52f, 0.53f, 0.53f));
        Vector3 P(float y, float offset = 0f) => new Vector3(anchor.x, y, anchor.z) + facing * offset;
        metal.Tube(P(0f), P(height), radius, top);
        metal.Tube(P(height), P(height + 0.06f), top + 0.015f, 0.02f);
        var key = source.transform.parent.name + "_" + source.name + "_" +
            Mathf.RoundToInt(head.x * 100f) + "_" + Mathf.RoundToInt(head.z * 100f);
        void Plate(string kind, Vector3 centre, float width, float tall)
        {
            var face = new Shape(Color.white);
            face.Plate(centre, facing, width, tall);
            backs.Box(centre - facing * 0.016f, facing, width + 0.03f, tall + 0.03f, 0.025f);
            Add(visual, key + "_" + kind, face, PlateMaterial(kind));
        }
        if (!pedestrian)
        {
            var delta = new Vector3(head.x - anchor.x, 0f, head.z - anchor.z);
            var length = delta.magnitude;
            var direction = length > 0f ? delta / length : Vector3.forward;
            var reach = length + 0.85f;
            var armY = head.y + 0.47f;
            Vector3 A(float distance, float y) => P(y) + direction * distance;
            float Upper(float distance) => armY + 1.05f + (0.09f - 1.05f) * Mathf.Min(1f, distance / (reach - 0.15f));
            metal.Tube(A(0f, armY), A(reach, armY), 0.06f, 0.045f);
            metal.Tube(A(0f, Upper(0f)), A(reach - 0.15f, Upper(reach - 0.15f)), 0.04f, 0.03f);
            foreach (var f in new[] { 0.35f, 0.68f }) metal.Tube(A(reach * f, armY), A(reach * f, Upper(reach * f)), 0.022f);
            metal.Tube(A(0f, armY - 0.18f), A(0f, armY + 1.2f), radius + 0.03f);
            Plate("Phase", head + facing * 0.06f + Vector3.down * 0.62f, 1.05f, 0.4f);
            Plate("Name", P(height - 0.6f, top + 0.03f), 0.36f, 0.9f);
        }
        else
        {
            var rear = head - facing * 0.1f;
            foreach (var dy in new[] { -0.22f, 0.22f })
                metal.Tube(P(head.y + dy), new Vector3(rear.x, head.y + dy, rear.z), 0.025f);
            Plate("Walk", P(head.y, radius + 0.03f), 0.13f, 0.6f);
        }
        Add(visual, key + "_Posts", metal, PaintMaterial("ART_SignalPole"));
        Add(visual, key + "_PlateBacks", backs, PaintMaterial("ART_SignalHousing"));
    }

    static Material PaintMaterial(string name) =>
        AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Hikone/Materials/{name}.mat");

    static Material PlateMaterial(string kind)
    {
        var texturePath = $"Assets/_Project/Hikone/Textures/HK_T_Signal{kind}.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer == null) throw new InvalidOperationException("Bake the signal plate textures first.");
        if (importer.wrapMode != TextureWrapMode.Clamp || !importer.mipmapEnabled)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        var path = $"{AssetsRoot}/SignalPlate{kind}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0f);
        material.SetFloat("_Metallic", 0f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Add(Transform parent, string name, Shape shape, Material material)
    {
        var mesh = shape.Mesh(parent.worldToLocalMatrix);
        var path = $"{AssetsRoot}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        mesh.name = name;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    sealed class Shape
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly Color colour;
        public Shape(Color pigment) { colour = pigment.linear; }
        void Face(params Vector3[] points)
        {
            var start = vertices.Count;
            vertices.AddRange(points);
            for (var i = 0; i < points.Length; i++) uv.Add(new Vector2(i == 1 || i == 2 ? 1f : 0f, i >= 2 ? 1f : 0f));
            for (var i = 1; i < points.Length - 1; i++) triangles.AddRange(new[] { start, start + i, start + i + 1 });
        }
        public void Plate(Vector3 centre, Vector3 forward, float width, float height)
        {
            var right = Vector3.Cross(Vector3.up, forward) * width * 0.5f;
            var up = Vector3.up * height * 0.5f;
            Face(centre - right - up, centre + right - up, centre + right + up, centre - right + up);
            // The observer looks against the face normal, so their screen-right
            // runs opposite this quad's winding-right. Keep the baked text readable.
            for (var i = uv.Count - 4; i < uv.Count; i++) uv[i] = new Vector2(1f - uv[i].x, uv[i].y);
        }
        public void Box(Vector3 centre, Vector3 forward, float width, float height, float depth)
        {
            var r = Vector3.Cross(Vector3.up, forward) * width * 0.5f;
            var u = Vector3.up * height * 0.5f;
            var f = forward * depth * 0.5f;
            Face(centre-r-u+f, centre+r-u+f, centre+r+u+f, centre-r+u+f);
            Face(centre+r-u-f, centre-r-u-f, centre-r+u-f, centre+r+u-f);
            Face(centre+r-u+f, centre+r-u-f, centre+r+u-f, centre+r+u+f);
            Face(centre-r-u-f, centre-r-u+f, centre-r+u+f, centre-r+u-f);
            Face(centre-r+u+f, centre+r+u+f, centre+r+u-f, centre-r+u-f);
            Face(centre-r-u-f, centre+r-u-f, centre+r-u+f, centre-r-u+f);
        }
        public void Tube(Vector3 start, Vector3 end, float radius, float endRadius = -1f)
        {
            const int count = 14;
            var rotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
            var a = new Vector3[count];
            var b = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var offset = rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                a[i] = start + offset * radius;
                b[i] = end + offset * (endRadius < 0f ? radius : endRadius);
            }
            Face(a);
            Face(b.Reverse().ToArray());
            for (var i = 0; i < count; i++) { var j = (i + 1) % count; Face(a[i], b[i], b[j], a[j]); }
        }
        public Mesh Mesh(Matrix4x4 toLocal)
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices.Select(toLocal.MultiplyPoint3x4).ToList());
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(Enumerable.Repeat(colour, vertices.Count).ToList());
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
