using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Custom scenarios bring text written on the PC (names, explanations) that the static Japanese
/// atlases were never baked with. A dynamic TMP font asset built from the same Noto Sans JP file is
/// added as fallback to every static Japanese font asset, so any character renders at runtime.
/// Its atlas is cleared on build (only the glyphs a session needs are rasterised on the device).
/// </summary>
public static class DynamicFontFallback
{
    const string Folder = "Assets/_Project/UI/Fonts";
    const string SourceFont = Folder + "/NotoSansJP-SemiBold.ttf";
    public const string FallbackPath = Folder + "/NotoSansJP Dynamic Fallback SDF.asset";

    [MenuItem("Tools/VRLearn/Setup Dynamic Font Fallback (also clears its atlas)")]
    public static void Setup()
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            fallback = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            fallback.name = "NotoSansJP Dynamic Fallback SDF";
            AssetDatabase.CreateAsset(fallback, FallbackPath);
            // atlas textures and material live inside the font asset
            foreach (var texture in fallback.atlasTextures)
            {
                texture.name = fallback.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, fallback);
            }
            fallback.material.name = fallback.name + " Material";
            AssetDatabase.AddObjectToAsset(fallback.material, fallback);
        }
        // keep the asset empty in the repository: glyphs are added on demand at runtime
        fallback.ClearFontAssetData(true);
        var serialized = new SerializedObject(fallback);
        var clear = serialized.FindProperty("m_ClearDynamicDataOnBuild");
        if (clear != null)
        {
            clear.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(fallback);

        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { Folder }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || asset == fallback || asset.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                continue;
            asset.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!asset.fallbackFontAssetTable.Contains(fallback))
            {
                asset.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(asset);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Font] dynamic fallback ready: " + FallbackPath);
    }

    /// <summary>True when every static Japanese font asset falls back to the dynamic one.</summary>
    public static bool IsConfigured()
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        return fallback != null && AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null && a != fallback && a.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            .All(a => a.fallbackFontAssetTable != null && a.fallbackFontAssetTable.Contains(fallback));
    }
}
