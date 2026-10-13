using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the Hikone castle-town variant of the gameplay scene.
/// Only the environment is replaced: every gameplay object (player, routes, scenarios,
/// traffic lights, invisible walls, road colliders used for ground raycasts) is kept as-is.
/// Source art: Art/Hikone/*.py (Blender) -> Assets/_Project/Hikone/Models/*.fbx.
/// </summary>
public static class HikoneEnvironmentBuilder
{
    const string Root = "Assets/_Project/Hikone";
    const string ModelDir = Root + "/Models";
    const string MaterialDir = Root + "/Materials";
    const string TextureDir = Root + "/Textures";
    const string PrefabDir = Root + "/Prefabs";
    const string LayoutPath = Root + "/Layout/hikone_layout.json";
    public const string TargetScene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const string EnvironmentRootName = "HikoneEnvironment";

    static readonly HashSet<string> BuildingAssets = new HashSet<string>
    {
        "HK_Machiya_A", "HK_Machiya_A_Narrow", "HK_Machiya_B", "HK_Machiya_C", "HK_Kura", "HK_Dobei_5m"
    };


    // ------------------------------------------------------------------ constraints
    const string ArtDir = "Art/Hikone";

    /// <summary>
    /// Exports the gameplay facts the layout must respect from the Hikone scene (its experiment
    /// objects and road colliders are the originals; only the visuals were replaced):
    /// road tiles (bounds + kind), waypoints/spawns/goals/triggers/signals, invisible walls and
    /// lamp poles (scene_constraints.json), plus the original road mesh triangles in world space
    /// (road_tiles_geometry.json) from which the raised sidewalks/curbs/ramps are rebuilt.
    /// </summary>
    [MenuItem("Tools/VRLearn/Hikone/0. Export Constraints From Scene")]
    public static void ExportConstraints()
    {
        EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string F(float v) => v.ToString("F2", inv);
        var roads = GameObject.Find("Environment/RoadContainerNeo").transform;

        var sb = new System.Text.StringBuilder("{\"tiles\":[");
        var first = true;
        foreach (Transform tile in roads)
        {
            if (tile.name == "GroundContainer") continue;
            var b = new Bounds(); var has = false;
            foreach (var r in tile.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.bounds.size.y > 1f) continue;          // skip lamp poles
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) continue;
            var kind = tile.name.StartsWith("Crossroads_1_lines_2") ? "tee"
                : tile.name.StartsWith("Crossroads") ? "cross"
                : tile.name.Contains("turn") ? "turn" : "straight";
            sb.Append(first ? "" : ",").Append(
                $"{{\"name\":\"{tile.name}\",\"kind\":\"{kind}\",\"min\":[{F(b.min.x)},{F(b.min.z)}],\"max\":[{F(b.max.x)},{F(b.max.z)}],\"y\":{tile.position.y.ToString("F3", inv)},\"active\":{(tile.gameObject.activeInHierarchy ? "true" : "false")}}}");
            first = false;
        }
        sb.Append("],\"points\":[");
        first = true;
        foreach (var rootName in new[] { "WayPointContainer", "ScenarioHost", "CarFactoryContainer", "OnOffContainer" })
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                string kind = n.StartsWith("WayPoint") && t.childCount == 0 ? "waypoint"
                    : n.StartsWith("Spawn") ? "spawn"
                    : n.StartsWith("GoalArea") ? "goal"
                    : n.StartsWith("AcidentArea") && t.GetComponent<Collider>() != null ? "accident"
                    : n.StartsWith("CarFactory") ? "factory"
                    : n.StartsWith("DestroyArea_") ? "destroy"
                    : n.StartsWith("TrafficLight_") ? "signal"
                    : n.StartsWith("BlackWall") ? "blackwall"
                    : n.StartsWith("Car 5") || n.StartsWith("Truck") ? "parked" : null;
                if (kind == null) continue;
                var p = t.position;
                sb.Append(first ? "" : ",").Append(
                    $"{{\"kind\":\"{kind}\",\"name\":\"{n}\",\"path\":\"{(t.parent != null ? t.parent.name : "")}\",\"p\":[{F(p.x)},{F(p.y)},{F(p.z)}]}}");
                first = false;
            }
        }
        sb.Append("],\"walls\":[");
        first = true;
        foreach (var c in GameObject.Find("Environment/InvisibleWallContainer").GetComponentsInChildren<BoxCollider>(true))
        {
            var b = c.bounds;
            sb.Append(first ? "" : ",").Append($"{{\"name\":\"{c.name}\",\"min\":[{F(b.min.x)},{F(b.min.z)}],\"max\":[{F(b.max.x)},{F(b.max.z)}]}}");
            first = false;
        }
        sb.Append("],\"poles\":[");
        first = true;
        foreach (var t in roads.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Pole1")) continue;
            sb.Append(first ? "" : ",").Append($"[{F(t.position.x)},{t.position.y.ToString("F3", inv)},{F(t.position.z)}]");
            first = false;
        }
        sb.Append("]}");
        File.WriteAllText($"{ArtDir}/scene_constraints.json", sb.ToString());

        // original road meshes, world space, winding preserved (normal = cross(b-a, c-a))
        var geo = new System.Text.StringBuilder("{");
        var firstTile = true;
        foreach (Transform tile in roads)
        {
            if (tile.name == "GroundContainer") continue;
            var tris = new System.Text.StringBuilder();
            var firstTri = true;
            foreach (var mf in tile.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.StartsWith("Pole")) continue;
                var m = mf.sharedMesh; var v = m.vertices; var idx = m.triangles; var tr = mf.transform;
                var mirrored = tr.lossyScale.x * tr.lossyScale.y * tr.lossyScale.z < 0;
                for (var i = 0; i < idx.Length; i += 3)
                {
                    var a = tr.TransformPoint(v[idx[i]]);
                    var bb = tr.TransformPoint(v[idx[i + (mirrored ? 2 : 1)]]);
                    var cc = tr.TransformPoint(v[idx[i + (mirrored ? 1 : 2)]]);
                    tris.Append(firstTri ? "" : ",").Append("[").Append(string.Join(",",
                        new[] { a.x, a.y, a.z, bb.x, bb.y, bb.z, cc.x, cc.y, cc.z }.Select(f => f.ToString("F4", inv)))).Append("]");
                    firstTri = false;
                }
            }
            geo.Append(firstTile ? "" : ",").Append($"\"{tile.name}\":[{tris}]");
            firstTile = false;
        }
        geo.Append("}");
        File.WriteAllText($"{ArtDir}/road_tiles_geometry.json", geo.ToString());
        Debug.Log($"[Hikone] exported constraints and road geometry to {ArtDir}");
    }

    [MenuItem("Tools/VRLearn/Hikone/1. Import Models, Materials && Prefabs")]
    public static void SetupAssets()
    {
        EnsureFolder(MaterialDir);
        EnsureFolder(PrefabDir);
        var mats = BuildMaterials();
        ConfigureModels(mats);
        BuildPrefabs();
        HikoneVehicleVisuals.RebuildCarPrefabs();
        WriteVehicleBodyCatalog();
        AssetDatabase.SaveAssets();
        NormalizePortedPropAssets();
        Debug.Log($"[Hikone] assets ready: {mats.Count} materials");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    /// <summary>Import only signal models when other generated geometry has not changed.</summary>
    public static void SetupSignals()
    {
        var mats = BuildMaterials();
        ConfigureModels(mats, true);
        BuildPrefabs(true);
        AssetDatabase.SaveAssets();
        NormalizePortedPropAssets();
    }

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    /// <summary>
    /// One painted material per Hikone material name (HikonePaintAssets: the browser port's pigments
    /// and brush, VRLearn/HikonePaint shader); the lamp glass glows.
    /// </summary>
    static Dictionary<string, Material> BuildMaterials()
    {
        var brush = HikonePaintAssets.WriteBrushTexture();
        var shader = Shader.Find(HikonePaintAssets.ShaderName);
        var result = new Dictionary<string, Material>();
        foreach (var name in HikonePaintAssets.MaterialNames)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            HikonePaintAssets.Paint(m, name, brush);
            // the browser port lights emissive pigments at 0.4 of their colour: warm glass, not white
            m.SetColor("_EmissionColor", name == "HK_Lamp" ? m.GetColor("_BaseColor") * 0.4f
                : name == "ART_LampGlow" ? new Color(1f, 0.70f, 0.32f).linear * 0.20f
                : name == "ART_SignalRed" ? new Color(1f, 0.12f, 0.06f).linear * 0.38f
                : name == "ART_SignalYellow" ? new Color(1f, 0.65f, 0.04f).linear * 0.38f : Color.black);
            if (name == "ART_SignalGreen") m.SetColor("_EmissionColor", new Color(0.04f, 0.85f, 0.24f).linear * 0.38f);
            if (name == "ART_SignalGlyph") m.SetColor("_EmissionColor", new Color(1f, 0.91f, 0.68f).linear * 0.18f);
            if (name.StartsWith("ART_Signal") && name != "ART_SignalRed" && name != "ART_SignalYellow")
                m.SetFloat("_Paint", 0f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            result[name] = m;
        }
        return result;
    }

    static void ConfigureModels(Dictionary<string, Material> mats, bool signalsOnly = false)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (signalsOnly && !Path.GetFileNameWithoutExtension(path).StartsWith("HK_Signal_")) continue;
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            // car bodies are dented on impact (AccidentVehicleDamage needs readable meshes)
            var name = Path.GetFileNameWithoutExtension(path);
            mi.isReadable = name.StartsWith("HK_Kei_") || name == "HK_Sedan";
            if (IsPortedProp(name)) mi.preserveHierarchy = true;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importBlendShapes = false;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // Ported pigment materials belong only to the ported prop models. Avoid adding
            // unused material references to every building, road and tree importer.
            if (!IsPortedProp(name))
                foreach (var key in mi.GetExternalObjectMap().Keys.Where(k => k.name.StartsWith("ART_")).ToArray())
                    mi.RemoveRemap(key);
            foreach (var kv in mats)
                if (!kv.Key.StartsWith("ART_") || IsPortedProp(name))
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
        }
    }

    const string VehicleBodyCatalogPath = "Assets/_Project/Resources/VehicleBodies.asset";

    /// <summary>
    /// The kei cars mixed into the background traffic (VehicleBody). Sizes are the kei limits
    /// (3.395 x 1.475 m); shares follow the roughly 40 % kei share of Japanese passenger cars.
    /// </summary>
    static void WriteVehicleBodyCatalog()
    {
        EnsureFolder(Path.GetDirectoryName(VehicleBodyCatalogPath).Replace('\\', '/'));
        var catalog = AssetDatabase.LoadAssetAtPath<VehicleBodyCatalog>(VehicleBodyCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<VehicleBodyCatalog>();
            AssetDatabase.CreateAsset(catalog, VehicleBodyCatalogPath);
        }
        // pearl white, silver, black, ivory, pale pink, sky blue, mint, red
        var colors = new[]
        {
            C(0.93f, 0.93f, 0.91f), C(0.72f, 0.73f, 0.75f), C(0.08f, 0.08f, 0.09f), C(0.86f, 0.81f, 0.7f),
            C(0.9f, 0.74f, 0.76f), C(0.58f, 0.72f, 0.82f), C(0.65f, 0.8f, 0.72f), C(0.62f, 0.1f, 0.12f)
        };
        VehicleBodyCatalog.Entry Body(string id, string label, string prefab, float h, float mass, float share) => new VehicleBodyCatalog.Entry
        {
            id = id, label = label, prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefab}.prefab"),
            size = new Vector3(1.475f, h, 3.395f), massKg = mass, trafficShare = share, paintMaterial = "ART_VehiclePaint", colors = colors
        };
        catalog.bodies = new[]
        {
            Body("kei-tall", "軽ハイトワゴン / Kei tall wagon", "HK_Kei_Tall", 1.79f, 900f, 0.25f),
            Body("kei-hatch", "軽ハッチバック / Kei hatchback", "HK_Kei_Hatch", 1.525f, 680f, 0.15f),
        };
        EditorUtility.SetDirty(catalog);
    }

    static void BuildPrefabs(bool signalsOnly = false)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            if (signalsOnly && !name.StartsWith("HK_Signal_")) continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            // Wheel pivots change the hierarchy, which is not allowed inside a model
            // prefab instance. Keep imported mesh references in a generated plain prefab.
            if (IsPortedProp(name))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (BuildingAssets.Contains(name) || name.StartsWith("HK_Terrain") || name.StartsWith("HK_Hikone") || name.StartsWith("HK_Ishigaki"))
                flags |= StaticEditorFlags.OccluderStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (IsPortedProp(name) && t != go.transform && t.name.StartsWith(name + "_"))
                {
                    t.name = t.name.Substring(name.Length + 1);
                    if (t.name.StartsWith("Tire_"))
                    {
                        var pivot = new GameObject("Wheel_" + t.name.Substring("Tire_".Length)).transform;
                        pivot.SetParent(go.transform, false);
                        pivot.position = t.GetComponent<Renderer>().bounds.center;
                        t.SetParent(pivot, true);
                        t.name = "Tire";
                    }
                }
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    name == "HK_Sedan" || name.StartsWith("HK_Kei_") || name.StartsWith("HK_Signal_") ? 0 : flags);
            }
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                // small props and distant forest do not need to cast shadows on Quest
                var cast = !(name.StartsWith("HK_Road") || name.StartsWith("HK_Ground") || name == "HK_Water" ||
                             name.StartsWith("HK_Sign") || name == "HK_Bollard" || name == "HK_Guardrail");
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            if (BuildingAssets.Contains(name))
            {
                var b = LocalBounds(go);
                var box = go.AddComponent<BoxCollider>();
                // footprint only up to the eaves: keeps roof overhangs above sidewalks non-blocking
                box.center = new Vector3(b.center.x, Mathf.Min(b.size.y, 5f) * 0.5f, name == "HK_Dobei_5m" ? 0f : b.center.z - 0.3f);
                box.size = new Vector3(b.size.x - 0.2f, Mathf.Min(b.size.y, 5f), name == "HK_Dobei_5m" ? 0.5f : b.size.z - 2.2f);
            }
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            NormalizeSerializedText($"{PrefabDir}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static bool IsPortedProp(string name) => name == "HK_StreetLamp" || name == "HK_Sedan"
        || name.StartsWith("HK_Kei_") || name.StartsWith("HK_Truck_") || name.StartsWith("HK_Signal_");

    // Unity writes a space after empty YAML values. Normalize generated text so a
    // scene rebuild also passes the repository's required git diff --check.
    public static void NormalizeSerializedText(string path) =>
        File.WriteAllLines(path, File.ReadAllLines(path).Select(line => line.TrimEnd()));

    /// <summary>Keep newly generated pigment assets and model metadata free of trailing spaces.</summary>
    public static void NormalizePortedPropAssets()
    {
        foreach (var name in HikonePaintAssets.MaterialNames.Where(n => n.StartsWith("ART_")))
            foreach (var suffix in new[] { ".mat", ".mat.meta" })
                NormalizeSerializedText($"{MaterialDir}/{name}{suffix}");
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsPortedProp(Path.GetFileNameWithoutExtension(path)))
                NormalizeSerializedText(path + ".meta");
        }
        Debug.Log("[Hikone] ported prop asset text normalized");
    }

    static Bounds LocalBounds(GameObject go)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero);
        var first = true;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var mb = mf.sharedMesh.bounds;
            var m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3(sx, sy, sz)));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ------------------------------------------------------------------ scene
    [Serializable] class LayoutItem { public string a; public float[] p; public float r; public float[] s; public string g; public string c; }
    [Serializable] class Layout { public int version; public LayoutItem[] items; }

    [MenuItem("Tools/VRLearn/Hikone/2. Build Hikone Scene")]
    public static void BuildScene()
    {
        if (!File.Exists(TargetScene))
            throw new InvalidOperationException("Missing " + TargetScene + " (the scene holds the experiment objects; restore it from git)");
        var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        var env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment");
        if (env == null) throw new InvalidOperationException("Environment root missing");

        HideLegacyVisuals(env.transform);
        ConfigureSpawnOccluders(scene);
        ReplaceTruckVisuals(scene);
        HikoneVehicleVisuals.ReplaceParkedCars(scene);

        var existing = env.transform.Find(EnvironmentRootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        var root = new GameObject(EnvironmentRootName).transform;
        root.SetParent(env.transform, false);

        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        var groups = new Dictionary<string, Transform>();
        var prefabs = new Dictionary<string, GameObject>();
        var missing = new HashSet<string>();
        foreach (var it in layout.items)
        {
            if (!prefabs.TryGetValue(it.a, out var prefab))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{it.a}.prefab");
                prefabs[it.a] = prefab;
            }
            if (prefab == null) { missing.Add(it.a); continue; }
            if (!groups.TryGetValue(it.g, out var parent))
            {
                parent = new GameObject(it.g).transform;
                parent.SetParent(root, false);
                groups[it.g] = parent;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(it.p[0], it.p[1], it.p[2]), Quaternion.Euler(0f, it.r, 0f));
            go.transform.localScale = new Vector3(it.s[0], it.s[1], it.s[2]);
        }
        if (missing.Count > 0) Debug.LogWarning("[Hikone] missing prefabs: " + string.Join(", ", missing));

        ApplyAtmosphere();
        HikoneSignalVisuals.Replace(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        NormalizeSerializedText(TargetScene);
        AddToBuildSettings();
        Debug.Log($"[Hikone] built {layout.items.Length} placements into {TargetScene}");
    }

    /// <summary>Old visuals off; colliders, lights and gameplay components untouched.</summary>
    static void HideLegacyVisuals(Transform env)
    {
        foreach (var name in new[] { "RoadContainerNeo", "RoadLineContainer" })
        {
            var t = env.Find(name);
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
    }

    /// <summary>
    /// The parked trucks of the truck scenarios (an imported European tractor-trailer) are shown as
    /// Japanese box trucks: the old tractor and trailer renderers are switched off (their colliders,
    /// the TruckCollider and every transform stay) and HK_Truck_Large (10 t) or HK_Truck_Medium (4 t)
    /// is placed on the old visual's footprint, so the occlusion stays the same. Idempotent.
    /// </summary>
    static void ReplaceTruckVisuals(UnityEngine.SceneManagement.Scene scene)
    {
        var large = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/HK_Truck_Large.prefab");
        var medium = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/HK_Truck_Medium.prefab");
        if (large == null || medium == null)
        {
            Debug.LogWarning("[Hikone] truck prefabs missing; run step 1 first. Trucks left unchanged.");
            return;
        }
        var replaced = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var truck in root.GetComponentsInChildren<Transform>(true)
                     .Where(t => t.name.StartsWith("Trucks") && t.parent != null && t.parent.name == "TrucksContainer").ToArray())
        {
            var parts = new[] { truck.Find("Truck"), truck.Find("Trailer") }.Where(p => p != null).ToArray();
            if (parts.Length == 0) continue;
            // world bounds from the meshes (valid whether or not the renderers are already off)
            var bounds = new Bounds();
            var first = true;
            foreach (var mf in parts.SelectMany(p => p.GetComponentsInChildren<MeshFilter>(true)))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                foreach (var sx in new[] { -1f, 1f })
                foreach (var sy in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var w = mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3(sx, sy, sz)));
                    if (first) { bounds = new Bounds(w, Vector3.zero); first = false; }
                    else bounds.Encapsulate(w);
                }
            }
            foreach (var r in parts.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)))
                r.enabled = false;
            var old = truck.Find("HK_TruckVisual");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            // the cab points the same way as the old tractor (towards the truck's root)
            var along = truck.right;
            var cabAtRoot = Vector3.Dot(bounds.center - truck.position, along) > 0f;
            var prefab = Mathf.Max(bounds.size.x, bounds.size.z) > 10f ? large : medium;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, truck);
            go.name = "HK_TruckVisual";
            go.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                truck.rotation * Quaternion.Euler(0f, cabAtRoot ? 0f : 180f, 0f));
            go.transform.localScale = Vector3.one;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = parts[0].gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            }
            replaced++;
        }
        Debug.Log($"[Hikone] truck visuals replaced: {replaced}");
    }

    /// <summary>
    /// The black walls hide where vehicles spawn and despawn (no code references them).
    /// In this scene they sit inside gatehouses so they read as a dark gate passage:
    /// BlackWall stays as-is inside the Route 25 yagura gate; BlackWall (1) is fitted to the
    /// passage of the nagaya gate over the side street; BlackWall (2) is replaced by the
    /// white wall along Honmachi and switched off.
    /// </summary>
    static void ConfigureSpawnOccluders(UnityEngine.SceneManagement.Scene scene)
    {
        var onOff = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "OnOffContainer");
        var container = onOff != null ? onOff.transform.Find("BlackWallContainer") : null;
        if (container == null)
        {
            Debug.LogWarning("[Hikone] BlackWallContainer not found; occluders left unchanged.");
            return;
        }
        var passage = container.Find("BlackWall (1)");
        if (passage != null)
        {
            passage.position = new Vector3(10f, 2.4f, 6.63f);
            passage.localScale = new Vector3(9.4f, 5f, passage.localScale.z);
        }
        var honmachi = container.Find("BlackWall (2)");
        if (honmachi != null)
            honmachi.gameObject.SetActive(false);
    }

    /// <summary>
    /// The browser port's light (HikonePaintAssets): its painted sky, linear fog, a sky-to-ground
    /// ambient and a warm sun. three.js divides Lambert light by π and the port shows it at exposure
    /// 1.08, so its sun (2.5) and hemisphere (2.1) become 0.86 and 0.72 here (Lambert without π).
    /// </summary>
    static void ApplyAtmosphere()
    {
        const float exposure = 1.08f, sun = 2.5f * exposure / Mathf.PI, hemisphere = 2.1f * exposure / Mathf.PI;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = HikonePaintAssets.FogColor;
        RenderSettings.fogStartDistance = HikonePaintAssets.FogStart;
        RenderSettings.fogEndDistance = HikonePaintAssets.FogEnd;
        Color Scaled(Color c) => new Color(Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(c.r) * hemisphere),
            Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(c.g) * hemisphere), Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(c.b) * hemisphere));
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Scaled(HikonePaintAssets.AmbientSky);
        RenderSettings.ambientEquatorColor = Scaled(Color.Lerp(HikonePaintAssets.AmbientSky, HikonePaintAssets.AmbientGround, 0.5f));
        RenderSettings.ambientGroundColor = Scaled(HikonePaintAssets.AmbientGround);
        var sky = HikonePaintAssets.WriteSky();
        RenderSettings.skybox = sky;
        // GameDirector keeps this light for the clear-day condition and shows DaySky
        var director = UnityEngine.Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
        if (director != null)
        {
            var so = new SerializedObject(director);
            so.FindProperty("DaySky").objectReferenceValue = sky;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var light = RenderSettings.sun != null ? RenderSettings.sun
            : UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (light != null)
        {
            light.color = HikonePaintAssets.SunColor;
            light.intensity = sun;
            EditorUtility.SetDirty(light);
        }
        else Debug.LogWarning("[Hikone] no directional light: sun left unchanged");
    }

    static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == TargetScene)) return;
        scenes.Add(new EditorBuildSettingsScene(TargetScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
