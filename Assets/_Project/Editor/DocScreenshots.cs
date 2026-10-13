using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders the screenshots in Docs/Hikone from fixed viewpoints, so the pictures can be
/// refreshed after the environment or the title menu changes. The replay and feedback pictures
/// come from the PlayMode test AccidentExperienceTests (run with graphics); see Docs/Hikone/README.md.
/// </summary>
public static class DocScreenshots
{
    const string OutputFolder = "Docs/Hikone";
    const string GameplayScene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
    const string TitleScene = "Assets/_Project/Scenes/Title.unity";
    const int Width = 1600, Height = 900;

    struct View
    {
        public string file;
        public Vector3 position, target;
        public float fov;
        public int scenario;   // only this built-in scenario's objects are shown (0-based)
        public View(string file, Vector3 position, Vector3 target, float fov = 60f, int scenario = 0)
        {
            this.file = file; this.position = position; this.target = target; this.fov = fov; this.scenario = scenario;
        }
    }

    // world coordinates: the Kyobashi junction spans x 22..42, z 16..36; the bridge is north of it,
    // Castle Road runs south, Route 25 runs west past the Honmachi T (x 0..20)
    static readonly View[] Views =
    {
        new View("01_kyobashi_intersection.jpg", new Vector3(24.5f, 1.75f, 12.5f), new Vector3(34f, 3f, 60f)),
        new View("02_castle_road.jpg", new Vector3(32f, 1.75f, 15f), new Vector3(32f, 1.5f, -60f)),
        new View("03_aerial.jpg", new Vector3(75f, 55f, -45f), new Vector3(30f, 0f, 60f)),
        new View("04_bridge.jpg", new Vector3(32f, 1.75f, 37f), new Vector3(32f, 3.5f, 90f)),
        new View("05_route25_west_gate.jpg", new Vector3(20f, 1.75f, 21f), new Vector3(-60f, 3f, 26f)),
        new View("06_junctions_from_above.jpg", new Vector3(21f, 38f, 3f), new Vector3(21f, 0f, 26f), 55f),
        new View("10_parked_trucks.jpg", new Vector3(44f, 1.55f, 21.5f), new Vector3(56f, 1.6f, 24f), 60f, 2),
    };

    [MenuItem("Tools/VRLearn/Docs/Capture Screenshots")]
    public static void Capture()
    {
        Directory.CreateDirectory(OutputFolder);
        EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
        var runtime = Object.FindFirstObjectByType<ScenarioRuntime>(FindObjectsInactive.Include);
        var entries = new SerializedObject(runtime).FindProperty("entries");
        var camera = NewCamera();
        try
        {
            camera.orthographic = false;
            foreach (var view in Views)
            {
                // as the participant sees it: one scenario at a time (the scene is not saved)
                for (var i = 0; i < entries.arraySize; i++)
                    (entries.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue as GameObject)?.SetActive(i == view.scenario);
                camera.fieldOfView = view.fov;
                camera.transform.position = view.position;
                camera.transform.LookAt(view.target);
                Save(camera, view.file);
            }
        }
        finally
        {
            Object.DestroyImmediate(camera.gameObject);
        }

        // the title menu, straight on and only the canvas (the rig behind it is left out)
        EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Single);
        var canvas = (RectTransform)GameObject.Find("MenuWorldRoot/Canvas_TitleMenu").transform;
        var size = canvas.rect.size * canvas.lossyScale.x;
        camera = NewCamera();
        try
        {
            camera.orthographic = true;
            camera.orthographicSize = size.y * 0.5f * 1.03f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.SetPositionAndRotation(canvas.position - canvas.forward * 2f, canvas.rotation);
            camera.nearClipPlane = 1.95f;
            camera.farClipPlane = 2.05f;
            Canvas.ForceUpdateCanvases();
            Save(camera, "07_title_menu.jpg", Mathf.RoundToInt(Height * size.x * 1.02f / (size.y * 1.03f)));
        }
        finally
        {
            Object.DestroyImmediate(camera.gameObject);
        }
        Debug.Log("[DocScreenshots] saved to " + OutputFolder);
        CapturePropModels();
        CaptureTrafficSignals();
    }

    [MenuItem("Tools/VRLearn/Docs/Capture Traffic Signals")]
    public static void CaptureTrafficSignals()
    {
        Directory.CreateDirectory(OutputFolder);
        EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
        var heads = Object.FindObjectsByType<TrafficSignalPreset>(FindObjectsSortMode.None);
        var head = heads.First(s => s.IsVertical && !s.IsPedestrian);
        var camera = NewCamera();
        try
        {
            camera.fieldOfView = 42f;
            camera.transform.position = head.transform.position - head.transform.forward * 10f
                + head.transform.right * 1.2f + Vector3.up * 0.8f;
            camera.transform.LookAt(head.transform.position + Vector3.up * 0.5f);
            var times = new[] { 0f, 20.5f, 23.5f };
            var names = new[] { "12_signal_green.jpg", "13_signal_yellow.jpg", "14_signal_red.jpg" };
            for (var i = 0; i < times.Length; i++)
            {
                foreach (var signal in heads)
                {
                    var aspect = TrafficSignalCycle.Evaluate(times[i], true, signal.IsVertical,
                        signal.IsPedestrian, out var visible);
                    signal.Apply(aspect, visible);
                }
                Save(camera, names[i]);
            }
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
        EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
    }

    /// <summary>The ported props at their authored scale, independent of scenario activation.</summary>
    public static void CapturePropModels()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(HikonePaintAssets.SkyMaterialPath);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = HikonePaintAssets.AmbientSky;
        RenderSettings.ambientEquatorColor = new Color(0.82f, 0.82f, 0.82f);
        RenderSettings.ambientGroundColor = HikonePaintAssets.AmbientGround;
        RenderSettings.fog = false;
        var sun = new GameObject("PreviewSun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = HikonePaintAssets.SunColor;
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.sun = sun;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.transform.position = new Vector3(0f, -0.015f, -4f);
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Hikone/Materials/HK_Sidewalk.mat");
        void Place(string name, float x, float z, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Hikone/Prefabs/{name}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
        }
        Place("HK_StreetLamp", -7f, 0f);
        Place("HK_Sedan", -3.5f, 0f);
        Place("HK_Kei_Tall", 0f, 0f);
        Place("HK_Kei_Hatch", 3.5f, 0f);
        Place("HK_Truck_Large", -3.5f, -10f, 90f);
        Place("HK_Truck_Medium", 3.5f, -9f, 90f);
        var camera = NewCamera();
        camera.fieldOfView = 43f;
        camera.transform.position = new Vector3(16f, 10f, 22f);
        camera.transform.LookAt(new Vector3(-0.5f, 1.7f, -3f));
        Save(camera, "11_vehicle_models.jpg");
        Object.DestroyImmediate(camera.gameObject);
        // Discard the preview rather than making it another project scene.
        EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
    }

    static Camera NewCamera()
    {
        var camera = new GameObject("DocScreenshotCamera").AddComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1500f;
        return camera;
    }

    static void Save(Camera camera, string file, int width = Width)
    {
        var target = new RenderTexture(width, Height, 24) { antiAliasing = 4 };
        // compile shaders before drawing: an asynchronously compiling shader renders a placeholder
        var asyncCompile = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var image = new Texture2D(width, Height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            camera.aspect = (float)width / Height;
            camera.targetTexture = target;
            camera.Render(); // warm the newly loaded materials before the captured frame
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputFolder, file), image.EncodeToJPG(88));
        }
        finally
        {
            camera.targetTexture = null;
            ShaderUtil.allowAsyncCompilation = asyncCompile;
            RenderTexture.active = active;
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
