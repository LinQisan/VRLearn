using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ReferenceRecoveryAudit
{
    [Serializable] public class Report
    {
        public string unityVersion;
        public string graphicsDevice;
        public string[] resolvedTargets;
        public string[] unresolvedTargets;
        public List<string> missingScripts = new List<string>();
        public List<string> missingRendererMaterials = new List<string>();
        public List<string> missingMaterialShaders = new List<string>();
        public List<string> logMessages = new List<string>();
        public List<string> scenes = new List<string>();
        public List<string> prefabs = new List<string>();
        public List<string> scenarioAssets = new List<string>();
        public List<string> volumeComponents = new List<string>();
        public List<string> modelMaterials = new List<string>();
        public List<string> modelRemaps = new List<string>();
        public List<string> textureReferences = new List<string>();
        public List<string> physicsReferences = new List<string>();
        public string canvasImage;
        public string restoredShader;
        public List<string> restoredShaderMessages = new List<string>();
        public int materialCount;
    }
    [Serializable] public class Rows { public Row[] items; }
    [Serializable] public class Row { public string path; public int line; public string guid; public string field; }

    static Report report;
    public static void Run()
    {
        report = new Report { unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceType.ToString() };
        Application.logMessageReceived += OnLog;
        try
        {
            var rows = JsonUtility.FromJson<Rows>("{\"items\":" + File.ReadAllText("Docs/Structure-20260908/pre-existing-unresolved-references.json") + "}").items;
            var guids = rows.Select(r => r.guid).Distinct().OrderBy(g => g).ToArray();
            report.resolvedTargets = guids.Where(g => !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g)))
                .Select(g => g + " -> " + AssetDatabase.GUIDToAssetPath(g)).ToArray();
            report.unresolvedTargets = guids.Where(g => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    report.materialCount++;
                    if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                        report.missingMaterialShaders.Add(path + " :: " + material.name);
                }
            }
            foreach (var path in rows.Where(r => r.path.EndsWith(".mat")).Select(r => r.path).Distinct())
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var serialized = new SerializedObject(material);
                var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                for (int i = 0; i < textures.arraySize; i++)
                {
                    var entry = textures.GetArrayElementAtIndex(i);
                    string property = entry.FindPropertyRelative("first").stringValue;
                    var texture = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue;
                    report.textureReferences.Add(path + " :: " + property + " -> " + (texture == null ? "null" : AssetDatabase.GetAssetPath(texture))
                        + " ; shaderHasProperty=" + material.HasProperty(property)
                        + " ; keywords=" + string.Join(",", material.shaderKeywords));
                }
            }
            var volume = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/DefaultVolumeProfile.asset");
            foreach (var c in volume.components)
                report.volumeComponents.Add(c == null ? "MISSING" : c.GetType().FullName + " ; active=" + c.active);
            foreach (string model in new[] { "Roads", "Poles" })
            {
                string path = "Assets/ThirdParty/ModularLowpolyStreetsFree/FBX/" + model + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var entry in importer.GetExternalObjectMap())
                    report.modelRemaps.Add(model + " :: " + entry.Key.name + " -> " + (entry.Value == null ? "MISSING" : AssetDatabase.GetAssetPath(entry.Value)));
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string g, out long id);
                    report.modelMaterials.Add(model + " :: embedded " + m.name + " ; guid=" + g + " ; fileID=" + id);
                }
                InspectRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
                foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true))
                    foreach (var m in renderer.sharedMaterials)
                        report.modelMaterials.Add(model + " :: renderer " + Hierarchy(renderer.transform) + " -> " + (m == null ? "MISSING" : m.name + " @ " + AssetDatabase.GetAssetPath(m)));
            }
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!File.Exists(entry.path)) throw new InvalidOperationException("Missing build scene: " + entry.path);
                if (!entry.enabled) continue;
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                report.scenes.Add(entry.path);
                foreach (var root in scene.GetRootGameObjects())
                {
                    InspectRoot(root, entry.path);
                    foreach (var image in root.GetComponentsInChildren<Image>(true))
                    {
                        if (image.name != "Canvas_MainButton") continue;
                        var material = new SerializedObject(image).FindProperty("m_Material").objectReferenceValue;
                        report.canvasImage = entry.path + " :: " + Hierarchy(image.transform) + " ; enabled=" + image.enabled
                            + " ; serializedMaterial=" + (material == null ? "MISSING" : AssetDatabase.GetAssetPath(material));
                    }
                }
            }
            foreach (var path in Directory.GetFiles("Assets/_Project/Prefabs", "*.prefab"))
            {
                InspectRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
                report.prefabs.Add(path);
            }
            string officePath = "Assets/ThirdParty/OfficeBuilding/environment/Office building/Prefabs/office building Prefab.prefab";
            var office = AssetDatabase.LoadAssetAtPath<GameObject>(officePath);
            InspectRoot(office, officePath);
            foreach (var collider in office.GetComponentsInChildren<Collider>(true))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(collider, out string g, out long id);
                if (id == 64027084200808062 || id == 64534853695449302 || collider.sharedMaterial != null)
                    report.physicsReferences.Add(officePath + " :: " + Hierarchy(collider.transform) + " ; fileID=" + id + " ; material=" + (collider.sharedMaterial == null ? "null/default" : AssetDatabase.GetAssetPath(collider.sharedMaterial)));
            }
            for (int id = 0; id < 10; id++)
            {
                string path = "Assets/_Project/ScenarioDefinitions/Scenario_" + id.ToString("00") + ".asset";
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) throw new InvalidOperationException("Missing scenario: " + path);
                report.scenarioAssets.Add(path);
            }
            var restoredMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("2c59325cff8447d47899d7e518a87833"));
            if (restoredMaterial != null && restoredMaterial.shader != null)
            {
                var shader = restoredMaterial.shader;
                bool passReady = false;
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    bool asyncAllowed = ShaderUtil.allowAsyncCompilation;
                    ShaderUtil.allowAsyncCompilation = false;
                    try { passReady = restoredMaterial.SetPass(0); }
                    finally { ShaderUtil.allowAsyncCompilation = asyncAllowed; }
                }
                report.restoredShader = shader.name + " ; passReady=" + passReady + " ; supported=" + shader.isSupported + " ; hasError=" + ShaderUtil.ShaderHasError(shader);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    report.restoredShaderMessages.Add(message.severity + " :: " + message.message);
            }
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            var output = Environment.GetEnvironmentVariable("VRLEARN_AUDIT_OUTPUT") ?? "Logs/ReferenceRecovery/unity-audit.json";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
    }
    static void InspectRoot(GameObject root, string path)
    {
        if (root == null) throw new InvalidOperationException("Could not load " + path);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missing > 0) report.missingScripts.Add(path + " :: " + Hierarchy(t) + " ; count=" + missing);
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            for (int i = 0; i < r.sharedMaterials.Length; i++)
                if (r.sharedMaterials[i] == null) report.missingRendererMaterials.Add(path + " :: " + Hierarchy(r.transform) + " ; slot=" + i + " ; type=" + r.GetType().Name + " ; enabled=" + r.enabled + " ; active=" + r.gameObject.activeInHierarchy);
    }
    static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.logMessages.Add(type + " :: " + message);
    }
}
