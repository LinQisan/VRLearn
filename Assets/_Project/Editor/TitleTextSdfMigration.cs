using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

public static class TitleTextSdfMigration
{
    const string FontSourcePath = "Assets/_Project/UI/Fonts/NotoSansJP-SemiBold.ttf";
    const string MetaTitleScenePath = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
    const string MetaGameplayScenePath = "Assets/_Project/Scenes/TraficAcident_Meta.unity";
    const string MetaFontAssetPath = "Assets/_Project/UI/Fonts/NotoSansJP Meta Complete SDF.asset";
    const string MetaFontAssetName = "NotoSansJP Meta Complete SDF";
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    [MenuItem("Tools/VRLearn/Build Complete Meta Japanese UI")]
    public static void RepairMetaJapaneseUi()
    {
        EnsureTmpSettings();
        var requiredCharacters = new HashSet<char>();
        AddCharacters(requiredCharacters,
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 +-/:.()" +
            "事故の説明事故シナリオ車両との接触が発生しました。周囲確認と安全な横断判断を振り返ってください。" +
            "メニューに戻るA / X ボタンでもメニューに戻れます試聴再生");

        foreach (var guid in AssetDatabase.FindAssets("t:ScenarioDefinitionAsset", new[] { "Assets/_Project/ScenarioDefinitions" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition == null)
                continue;
            AddCharacters(requiredCharacters, definition.displayName);
            AddCharacters(requiredCharacters, definition.eventSummary);
        }

        var titleScene = EditorSceneManager.OpenScene(MetaTitleScenePath, OpenSceneMode.Single);
        var titleCanvas = FindTitleCanvas(titleScene);
        foreach (var text in titleCanvas.GetComponentsInChildren<TMP_Text>(true))
            AddCharacters(requiredCharacters, text.text);

        var fontAsset = RebuildMetaFontAsset(requiredCharacters);
        foreach (var text in titleCanvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = fontAsset;
            text.fontSharedMaterial = fontAsset.material;
            text.extraPadding = true;
            EditorUtility.SetDirty(text);
        }

        var titleSetup = Object.FindFirstObjectByType<MetaTitleSceneSetup>(FindObjectsInactive.Include);
        if (titleSetup == null)
            throw new System.InvalidOperationException("MetaTitleSceneSetup was not found.");
        var serializedTitleSetup = new SerializedObject(titleSetup);
        serializedTitleSetup.FindProperty("uiFont").objectReferenceValue = fontAsset;
        serializedTitleSetup.FindProperty("viewingDistance").floatValue = 3.3f;
        serializedTitleSetup.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(titleSetup);
        EditorSceneManager.MarkSceneDirty(titleScene);
        EditorSceneManager.SaveScene(titleScene);

        var gameplayScene = EditorSceneManager.OpenScene(MetaGameplayScenePath, OpenSceneMode.Single);
        var resultPresenter = Object.FindFirstObjectByType<AccidentResultPresenter>(FindObjectsInactive.Include);
        if (resultPresenter == null)
            throw new System.InvalidOperationException("AccidentResultPresenter was not found.");
        var serializedPresenter = new SerializedObject(resultPresenter);
        serializedPresenter.FindProperty("runtimeFont").objectReferenceValue = fontAsset;
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(resultPresenter);
        EditorSceneManager.MarkSceneDirty(gameplayScene);
        EditorSceneManager.SaveScene(gameplayScene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Meta Japanese UI repaired with {requiredCharacters.Count} required characters.");
    }

    static TMP_FontAsset RebuildMetaFontAsset(IEnumerable<char> requiredCharacters)
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MetaFontAssetPath) != null)
            AssetDatabase.DeleteAsset(MetaFontAssetPath);

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (sourceFont == null)
            throw new System.InvalidOperationException($"Font source was not imported: {FontSourcePath}");

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 8, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, false);
        fontAsset.name = MetaFontAssetName;
        fontAsset.isMultiAtlasTexturesEnabled = false;
        AssetDatabase.CreateAsset(fontAsset, MetaFontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (var texture in fontAsset.atlasTextures)
            AssetDatabase.AddObjectToAsset(texture, fontAsset);

        var characters = new string(requiredCharacters.OrderBy(character => character).ToArray());
        if (!fontAsset.TryAddCharacters(characters, out var missingCharacters))
            throw new System.InvalidOperationException($"Meta Japanese font is missing: {missingCharacters}");
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    static void AddCharacters(HashSet<char> destination, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        foreach (var character in text)
            destination.Add(character);
    }

    static void EnsureTmpSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
            return;

        var settings = ScriptableObject.CreateInstance<TMP_Settings>();
        var serializedSettings = new SerializedObject(settings);
        serializedSettings.FindProperty("assetVersion").stringValue = "2";
        serializedSettings.FindProperty("m_defaultFontSize").floatValue = 36f;
        serializedSettings.FindProperty("m_defaultAutoSizeMinRatio").floatValue = 0.5f;
        serializedSettings.FindProperty("m_defaultAutoSizeMaxRatio").floatValue = 2f;
        serializedSettings.FindProperty("m_defaultTextMeshProTextContainerSize").vector2Value = new Vector2(20f, 5f);
        serializedSettings.FindProperty("m_defaultTextMeshProUITextContainerSize").vector2Value = new Vector2(200f, 50f);
        serializedSettings.FindProperty("m_EnableRaycastTarget").boolValue = true;
        serializedSettings.FindProperty("m_GetFontFeaturesAtRuntime").boolValue = true;
        serializedSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(settings, TmpSettingsPath);
        AssetDatabase.SaveAssets();
    }

    static Canvas FindTitleCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "Canvas_TitleMenu" || canvas.name == "Canvas_MainButton")
                    return canvas;
        throw new System.InvalidOperationException("Title Canvas was not found.");
    }

}
