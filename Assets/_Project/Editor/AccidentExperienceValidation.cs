using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AccidentExperienceValidation
{
    [MenuItem("Tools/VRLearn/Prepare Accident Experience Font")]
    public static void PrepareFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Fonts/NotoSansJP Meta Complete SDF.asset");
        if (font == null) throw new InvalidOperationException("Meta result font is missing.");
        // Bake new UI glyphs into the existing asset, preserving its GUID and all scene bindings.
        var text = ReadScript("AccidentResultPresenter") + ReadScript("AccidentReplayPresenter")
            + ReadScript("HybridAccidentPresentation") + ReadScript("TitleScenarioDetail");
        // scenario names and explanations are shown on the title, replay and feedback pages
        foreach (var guid in AssetDatabase.FindAssets("t:ScenarioDefinitionAsset", new[] { "Assets/_Project/ScenarioDefinitions" }))
        {
            var scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (scenario != null)
                text += scenario.displayName + scenario.shortTitle + scenario.shortTitleEn + scenario.learningGoal + scenario.eventSummary;
        }
        var characters = new string(text.Where(c => !char.IsControl(c) && !font.HasCharacter(c)).Distinct().ToArray());
        if (characters.Length > 0)
        {
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            var serializedFont = new SerializedObject(font);
            serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/UI/Fonts/NotoSansJP-SemiBold.ttf");
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                if (!font.TryAddCharacters(characters, out var missing))
                    throw new InvalidOperationException("Accident UI glyphs could not be baked: " + missing);
            }
            finally { font.atlasPopulationMode = AtlasPopulationMode.Static; }
        }
        EditorUtility.SetDirty(font);
        foreach (var atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        const string markerPath = "Assets/Resources/AccidentReplayMarkers.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(markerPath) == null)
        {
            var marker = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(marker, markerPath);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Accident experience glyphs are ready.");
    }

    /// <summary>Source of a runtime script by class name, wherever it sits under Assets/_Project/Scripts.</summary>
    public static string ReadScript(string className)
    {
        var path = Directory.GetFiles("Assets/_Project/Scripts", className + ".cs", SearchOption.AllDirectories).FirstOrDefault();
        if (path == null) throw new FileNotFoundException("Script not found: " + className);
        return File.ReadAllText(path);
    }

    public static void BuildAndroid()
    {
        PrepareFont();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes,
            // e.g. Builds/VRLearn-0.2.0-2.apk (bundleVersion - Android versionCode)
            locationPathName = $"Builds/VRLearn-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Android build failed: " + report.summary.result);
        Debug.Log("Accident experience APK built: " + report.summary.totalSize + " bytes");
    }
}
