using System.IO;
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
        new View("01_kyobashi_intersection.jpg", new Vector3(24.5f, 2.0f, 12.5f), new Vector3(34f, 3f, 60f)),
        new View("02_castle_road.jpg", new Vector3(32f, 2.0f, 15f), new Vector3(32f, 1.5f, -60f)),
        new View("03_aerial.jpg", new Vector3(75f, 55f, -45f), new Vector3(30f, 0f, 60f)),
        new View("04_bridge.jpg", new Vector3(32f, 2.0f, 37f), new Vector3(32f, 3.5f, 90f)),
        new View("05_route25_west_gate.jpg", new Vector3(20f, 2.0f, 21f), new Vector3(-60f, 3f, 26f)),
        new View("06_junctions_from_above.jpg", new Vector3(21f, 38f, 3f), new Vector3(21f, 0f, 26f), 55f),
        new View("10_parked_trucks.jpg", new Vector3(44f, 1.8f, 21.5f), new Vector3(56f, 1.6f, 24f), 60f, 2),
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
        var image = new Texture2D(width, Height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            camera.aspect = (float)width / Height;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputFolder, file), image.EncodeToJPG(88));
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = active;
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
