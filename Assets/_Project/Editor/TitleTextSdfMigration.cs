using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class TitleTextSdfMigration
{
    const string ScenePath = "Assets/_Project/Scenes/TraficAcidentTitle.unity";
    const string FontSourcePath = "Assets/_Project/UI/Fonts/NotoSansJP-SemiBold.ttf";
    const string FontAssetPath = "Assets/_Project/UI/Fonts/NotoSansJP SemiBold SDF.asset";
    const string FontAssetName = "NotoSansJP SemiBold SDF";
    const string MetaTitleScenePath = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
    const string MetaGameplayScenePath = "Assets/_Project/Scenes/TraficAcident_Meta.unity";
    const string MetaFontAssetPath = "Assets/_Project/UI/Fonts/NotoSansJP Meta Complete SDF.asset";
    const string MetaFontAssetName = "NotoSansJP Meta Complete SDF";
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    const int ExpectedTitleTextCount = 40;

    [MenuItem("Tools/VRLearn/Migrate Title Text To SDF")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = FindTitleCanvas(scene);
        var legacyTexts = canvas.GetComponentsInChildren<Text>(true);
        var requiredCharacters = new HashSet<char>();
        foreach (var item in legacyTexts)
            AddCharacters(requiredCharacters, item.text);
        foreach (var item in canvas.GetComponentsInChildren<TMP_Text>(true))
            AddCharacters(requiredCharacters, item.text);
        requiredCharacters.Add('✓');

        var fontAsset = GetOrCreateFontAsset(requiredCharacters);
        var converted = 0;
        foreach (var legacy in legacyTexts)
        {
            ConvertText(legacy, fontAsset);
            converted++;
        }

        var overlay = canvas.GetComponent<OVROverlayCanvas>();
        if (overlay == null)
            throw new System.InvalidOperationException("OVROverlayCanvas was not found on Canvas_MainButton.");

        overlay.maxTextureSize = 4096;
        overlay.superSample = true;
        var serializedOverlay = new SerializedObject(overlay);
        serializedOverlay.FindProperty("_dynamicResolution").boolValue = false;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(overlay);

        var director = Object.FindFirstObjectByType<GameDirector_Title>(FindObjectsInactive.Include);
        var centerEye = Object.FindFirstObjectByType<CenterEyeCamera>(FindObjectsInactive.Include);
        director.EditorConfigure(
            canvas,
            FindFirstPath(canvas.transform,
                "90_PrimaryActions/Group_PrimaryActions", "ButtonGroupe_Main").gameObject,
            centerEye,
            FindText(canvas.transform, "10_EventParameters/Field_EventCount/EventNumber", "TextPanel_EventNumber/EventNumber"),
            FindText(canvas.transform, "10_EventParameters/Field_Height/Height", "TextPanel_Height/Height"),
            FindText(canvas.transform, "10_EventParameters/Field_Weight/Weight", "TextPanel_Weight/Weight"),
            FindText(canvas.transform, "10_EventParameters/Field_Age/Age", "TextPanel_Age/Age"),
            canvas.GetComponentsInChildren<TitleOptionToggle>(true),
            canvas.GetComponentsInChildren<TitleButtonFeedback>(true));
        EditorUtility.SetDirty(director);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Title SDF migration complete: {converted} legacy Text components converted.");
    }

    [MenuItem("Tools/VRLearn/Repair Title Text Clarity")]
    public static void RepairClarity()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = FindTitleCanvas(scene);
        var texts = canvas.GetComponentsInChildren<TMP_Text>(true);
        var requiredCharacters = new HashSet<char> { '✓' };
        foreach (var text in texts)
            AddCharacters(requiredCharacters, text.text);

        var fontAsset = GetOrCreateFontAsset(requiredCharacters);
        fontAsset.material.SetFloat("_Sharpness", 0.1f);
        EditorUtility.SetDirty(fontAsset.material);

        foreach (var text in texts)
        {
            text.font = fontAsset;
            text.fontSharedMaterial = fontAsset.material;
            text.extraPadding = true;
            text.SetAllDirty();
            EditorUtility.SetDirty(text);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Title text clarity repaired: {texts.Length} TMP labels use {FontAssetName}.");
    }

    public static void Validate()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = FindTitleCanvas(scene);
        var legacyCount = canvas.GetComponentsInChildren<Text>(true).Length;
        var tmpTexts = canvas.GetComponentsInChildren<TMP_Text>(true);
        var nonUnitScaleCount = tmpTexts.Count(item =>
            !Approximately(item.rectTransform.localScale, Vector3.one));
        var wrongFontCount = tmpTexts.Count(item => item.font == null || item.font.name != FontAssetName);
        var overlay = canvas.GetComponent<OVROverlayCanvas>();
        var serializedOverlay = new SerializedObject(overlay);
        var dynamicResolution = serializedOverlay.FindProperty("_dynamicResolution").boolValue;

        Debug.Log(
            $"Title SDF validation: legacy={legacyCount}, tmp={tmpTexts.Length}, " +
            $"nonUnitScale={nonUnitScaleCount}, wrongFont={wrongFontCount}, " +
            $"overlay={overlay.maxTextureSize}, superSample={overlay.superSample}, " +
            $"dynamicResolution={dynamicResolution}");

        if (legacyCount != 0 || tmpTexts.Length != ExpectedTitleTextCount || nonUnitScaleCount != 0 ||
            wrongFontCount != 0 || overlay.maxTextureSize != 4096 ||
            !overlay.superSample || dynamicResolution)
            throw new System.InvalidOperationException("Title SDF validation failed.");
    }

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

    static bool Approximately(Vector3 a, Vector3 b) =>
        Mathf.Approximately(a.x, b.x) &&
        Mathf.Approximately(a.y, b.y) &&
        Mathf.Approximately(a.z, b.z);

    static TMP_FontAsset GetOrCreateFontAsset(IEnumerable<char> requiredCharacters)
    {
        EnsureTmpSettings();

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            ConfigureDefaultFont(existing);
            return existing;
        }

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (sourceFont == null)
            throw new System.InvalidOperationException($"Font source was not imported: {FontSourcePath}");

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 10, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, false);
        fontAsset.name = FontAssetName;
        fontAsset.isMultiAtlasTexturesEnabled = false;

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (var texture in fontAsset.atlasTextures)
            AssetDatabase.AddObjectToAsset(texture, fontAsset);

        var characters = new string(requiredCharacters.OrderBy(c => c).ToArray());
        if (!fontAsset.TryAddCharacters(characters, out var missingCharacters))
            Debug.LogWarning($"{FontAssetName} missing characters: {missingCharacters}");

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fontAsset);
        ConfigureDefaultFont(fontAsset);
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

    static void ConfigureDefaultFont(TMP_FontAsset fontAsset)
    {
        TMP_Settings.defaultFontAsset = fontAsset;
        EditorUtility.SetDirty(TMP_Settings.instance);
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

    static void ConvertText(Text legacy, TMP_FontAsset fontAsset)
    {
        var go = legacy.gameObject;
        var oldText = legacy.text;
        var oldColor = legacy.color;
        var oldRaycastTarget = legacy.raycastTarget;
        var oldAlignment = legacy.alignment;
        var oldFontStyle = legacy.fontStyle;
        var targetSelectables = go.GetComponentsInParent<Selectable>(true)
            .Where(selectable => selectable.targetGraphic == legacy)
            .ToArray();

        Object.DestroyImmediate(legacy);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (tmp == null)
            throw new System.InvalidOperationException($"Could not add TextMeshProUGUI to {go.name}.");
        tmp.font = fontAsset;
        tmp.text = oldText;
        tmp.color = oldColor;
        tmp.raycastTarget = oldRaycastTarget;
        tmp.alignment = ConvertAlignment(oldAlignment);
        tmp.fontStyle = ConvertStyle(oldFontStyle);
        tmp.richText = legacy.supportRichText;
        tmp.overflowMode = legacy.verticalOverflow == VerticalWrapMode.Overflow
            ? TextOverflowModes.Overflow
            : TextOverflowModes.Truncate;
        tmp.textWrappingMode = legacy.horizontalOverflow == HorizontalWrapMode.Wrap
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;

        // Preserve the authored RectTransform. SDF remains sharp when scaled, while
        // stretching every label to its parent destroys this legacy menu layout.
        tmp.enableAutoSizing = false;
        tmp.fontSize = legacy.fontSize;
        tmp.margin = Vector4.zero;

        foreach (var selectable in targetSelectables)
            selectable.targetGraphic = tmp;

        EditorUtility.SetDirty(go);
    }

    static TextAlignmentOptions ConvertAlignment(TextAnchor alignment) => alignment switch
    {
        TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
        TextAnchor.UpperCenter => TextAlignmentOptions.Top,
        TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
        TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
        TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
        TextAnchor.MiddleRight => TextAlignmentOptions.Right,
        TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
        TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
        TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
        _ => TextAlignmentOptions.Center,
    };

    static FontStyles ConvertStyle(FontStyle style) => style switch
    {
        FontStyle.Bold => FontStyles.Bold,
        FontStyle.Italic => FontStyles.Italic,
        FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
        _ => FontStyles.Normal,
    };

    static TMP_Text FindText(Transform root, params string[] paths)
    {
        return FindFirstPath(root, paths).GetComponent<TMP_Text>();
    }

    static Transform FindFirstPath(Transform root, params string[] paths)
    {
        foreach (var path in paths)
        {
            var target = root.Find(path);
            if (target != null)
                return target;
        }
        throw new System.InvalidOperationException($"Title UI path was not found: {string.Join(" or ", paths)}");
    }

    static Canvas FindTitleCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "Canvas_TitleMenu" || canvas.name == "Canvas_MainButton")
                    return canvas;
        throw new System.InvalidOperationException("Title Canvas was not found.");
    }

    static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == objectName)
                    return item.gameObject;
        throw new System.InvalidOperationException($"Scene object was not found: {objectName}");
    }
}
