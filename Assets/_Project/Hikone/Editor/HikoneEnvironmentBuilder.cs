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

    // ------------------------------------------------------------------ materials
    struct MatSpec
    {
        public string Tex; public float TileMeters; public Color Color; public float Smooth; public float Metal;
        public bool Emissive; public string Normal; public float NormalTile;
        public MatSpec(string tex, float tile, Color c, float smooth = 0.12f, float metal = 0f)
        { Tex = tex; TileMeters = tile; Color = c; Smooth = smooth; Metal = metal; Emissive = false; Normal = null; NormalTile = 1f; }
    }

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    static Dictionary<string, MatSpec> MaterialSpecs()
    {
        var white = Color.white;
        var d = new Dictionary<string, MatSpec>
        {
            ["HK_Plaster"] = new MatSpec("HK_T_Plaster", 2f, white, 0.08f),
            ["HK_PlasterWarm"] = new MatSpec("HK_T_Plaster", 2f, C(0.95f, 0.91f, 0.82f), 0.08f),
            ["HK_WoodDark"] = new MatSpec("HK_T_Wood", 2f, C(0.42f, 0.36f, 0.33f), 0.18f),
            ["HK_WoodMid"] = new MatSpec("HK_T_Wood", 2f, C(0.82f, 0.74f, 0.66f), 0.2f),
            ["HK_WoodLight"] = new MatSpec("HK_T_Wood", 2f, C(1.3f, 1.2f, 1.05f), 0.2f),
            ["HK_RoofTile"] = new MatSpec("HK_T_RoofTile", 2f, C(0.9f, 0.92f, 0.96f), 0.35f),
            ["HK_RoofRidge"] = new MatSpec(null, 1f, C(0.18f, 0.19f, 0.21f), 0.35f),
            ["HK_Stone"] = new MatSpec("HK_T_StoneWall", 3.2f, white, 0.1f),
            ["HK_StoneDark"] = new MatSpec("HK_T_StoneWall", 2f, C(0.7f, 0.69f, 0.67f), 0.1f),
            ["HK_Asphalt"] = new MatSpec("HK_T_Asphalt", 4f, white, 0.18f),
            ["HK_Sidewalk"] = new MatSpec("HK_T_Pavers", 2.4f, white, 0.1f),
            ["HK_Curb"] = new MatSpec("HK_T_StoneWall", 1.5f, C(1.08f, 1.06f, 1.02f), 0.1f),
            ["HK_WhitePaint"] = new MatSpec(null, 1f, C(0.92f, 0.92f, 0.9f), 0.2f),
            ["HK_Grass"] = new MatSpec("HK_T_Grass", 6f, white, 0.05f),
            ["HK_Soil"] = new MatSpec(null, 1f, C(0.3f, 0.26f, 0.2f), 0.05f),
            ["HK_Leaf"] = new MatSpec(null, 1f, C(0.27f, 0.42f, 0.19f), 0.1f),
            ["HK_LeafDark"] = new MatSpec(null, 1f, C(0.14f, 0.27f, 0.15f), 0.1f),
            ["HK_Sakura"] = new MatSpec(null, 1f, C(0.95f, 0.76f, 0.82f), 0.1f),
            ["HK_Bark"] = new MatSpec("HK_T_Wood", 1f, C(0.62f, 0.55f, 0.5f), 0.05f),
            ["HK_Metal"] = new MatSpec(null, 1f, C(0.19f, 0.19f, 0.2f), 0.45f, 0.6f),
            ["HK_MetalLight"] = new MatSpec(null, 1f, C(0.62f, 0.63f, 0.64f), 0.5f, 0.7f),
            ["HK_Glass"] = new MatSpec(null, 1f, C(0.1f, 0.12f, 0.13f), 0.88f),
            ["HK_Shoji"] = new MatSpec(null, 1f, C(0.95f, 0.92f, 0.82f), 0.05f),
            ["HK_Noren"] = new MatSpec(null, 1f, C(0.14f, 0.19f, 0.34f), 0.05f),
            ["HK_NorenRed"] = new MatSpec(null, 1f, C(0.58f, 0.14f, 0.12f), 0.05f),
            ["HK_Gold"] = new MatSpec(null, 1f, C(0.8f, 0.63f, 0.27f), 0.6f, 0.85f),
            ["HK_Namako"] = new MatSpec("HK_T_Namako", 1.2f, white, 0.15f),
            ["HK_SignRed"] = new MatSpec(null, 1f, C(0.78f, 0.1f, 0.1f), 0.3f),
            ["HK_SignBlue"] = new MatSpec(null, 1f, C(0.08f, 0.3f, 0.66f), 0.3f),
            ["HK_SignYellow"] = new MatSpec(null, 1f, C(0.95f, 0.78f, 0.1f), 0.3f),
            ["HK_Guard"] = new MatSpec(null, 1f, C(0.3f, 0.22f, 0.16f), 0.3f),
            ["HK_TruckCab"] = new MatSpec(null, 1f, C(0.9f, 0.9f, 0.88f), 0.45f),
            ["HK_Rubber"] = new MatSpec(null, 1f, C(0.05f, 0.05f, 0.05f), 0.15f),
            ["HK_PlateGreen"] = new MatSpec(null, 1f, C(0.1f, 0.34f, 0.19f), 0.3f),
            ["HK_VanBody"] = new MatSpec(null, 1f, C(0.8f, 0.81f, 0.82f), 0.35f),
            ["HK_VanRib"] = new MatSpec(null, 1f, C(0.6f, 0.61f, 0.62f), 0.3f),
            // kei car paint: white, tinted per car (VehicleBody) through a property block
            ["HK_KeiPaint"] = new MatSpec(null, 1f, C(1f, 1f, 1f), 0.6f),
        };
        var lamp = new MatSpec(null, 1f, C(1f, 0.86f, 0.62f), 0.2f) { Emissive = true };
        d["HK_Lamp"] = lamp;
        var water = new MatSpec(null, 1f, C(0.13f, 0.24f, 0.24f), 0.93f) { Normal = "HK_T_WaterNormal", NormalTile = 12f };
        d["HK_Water"] = water;
        return d;
    }

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
        ConfigureTextures();
        var mats = BuildMaterials();
        ConfigureModels(mats);
        BuildPrefabs();
        WriteVehicleBodyCatalog();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Hikone] assets ready: {mats.Count} materials");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void ConfigureTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            var normal = path.Contains("Normal");
            var changed = ti.wrapMode != TextureWrapMode.Repeat || ti.maxTextureSize != 512 ||
                          (normal && ti.textureType != TextureImporterType.NormalMap) || ti.anisoLevel != 4;
            if (!changed) continue;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = 512;
            ti.anisoLevel = 4;
            ti.mipmapEnabled = true;
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
    }

    static Dictionary<string, Material> BuildMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var result = new Dictionary<string, Material>();
        foreach (var kv in MaterialSpecs())
        {
            var path = $"{MaterialDir}/{kv.Key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            var s = kv.Value;
            m.SetColor("_BaseColor", s.Color);
            m.SetFloat("_Smoothness", s.Smooth);
            m.SetFloat("_Metallic", s.Metal);
            if (s.Tex != null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{s.Tex}.png");
                m.SetTexture("_BaseMap", tex);
                var tile = 1f / s.TileMeters;
                m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            }
            else
            {
                m.SetTexture("_BaseMap", null);
            }
            if (s.Normal != null)
            {
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{s.Normal}.png"));
                m.SetTextureScale("_BumpMap", new Vector2(1f / s.NormalTile, 1f / s.NormalTile));
                m.SetFloat("_BumpScale", 0.6f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (s.Emissive)
            {
                m.SetColor("_EmissionColor", s.Color * 1.4f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            result[kv.Key] = m;
        }
        return result;
    }

    static void ConfigureModels(Dictionary<string, Material> mats)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            // car bodies are dented on impact (AccidentVehicleDamage needs readable meshes)
            mi.isReadable = Path.GetFileNameWithoutExtension(path).StartsWith("HK_Kei_");
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importBlendShapes = false;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
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
            size = new Vector3(1.475f, h, 3.395f), massKg = mass, trafficShare = share, paintMaterial = "HK_KeiPaint", colors = colors
        };
        catalog.bodies = new[]
        {
            Body("kei-tall", "軽ハイトワゴン / Kei tall wagon", "HK_Kei_Tall", 1.79f, 900f, 0.25f),
            Body("kei-hatch", "軽ハッチバック / Kei hatchback", "HK_Kei_Hatch", 1.525f, 680f, 0.15f),
        };
        EditorUtility.SetDirty(catalog);
    }

    static void BuildPrefabs()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (BuildingAssets.Contains(name) || name.StartsWith("HK_Terrain") || name.StartsWith("HK_Hikone") || name.StartsWith("HK_Ishigaki"))
                flags |= StaticEditorFlags.OccluderStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
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
            UnityEngine.Object.DestroyImmediate(go);
        }
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
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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

    static void ApplyAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.74f, 0.8f, 0.86f);
        RenderSettings.fogStartDistance = 110f;
        RenderSettings.fogEndDistance = 620f;
    }

    static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == TargetScene)) return;
        scenes.Add(new EditorBuildSettingsScene(TargetScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
