using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The browser port's painted look, made for the Quest (RATIONALE.md D38): pigments per material,
/// the brush texture the VRLearn/HikonePaint shader samples, the painted sky (baked once into a
/// panorama instead of computed per pixel) and the light. Ported from hikone-traffic-sim
/// (tools/build_impressionist.py COLORS, src/style.js PIGMENTS / LIGHT / paintedSky,
/// src/paint-texture.js); keep the numbers in step with it. Used by step 1 (materials, textures)
/// and step 2 (sky, light) of HikoneEnvironmentBuilder.
/// </summary>
public static class HikonePaintAssets
{
    public const string ShaderName = "VRLearn/HikonePaint";
    public const string BrushPath = "Assets/_Project/Hikone/Textures/HK_T_Brush.png";
    public const string SkyTexturePath = "Assets/_Project/Hikone/Textures/HK_T_SkyPainted.png";
    public const string SkyMaterialPath = "Assets/_Project/Hikone/Materials/HK_SkyPainted.mat";

    /// <summary>Base pigment of each material (sRGB), build_impressionist.py COLORS.</summary>
    static readonly Dictionary<string, Color> Base = new Dictionary<string, Color>
    {
        ["HK_Plaster"] = C(0.89f, 0.86f, 0.76f), ["HK_PlasterWarm"] = C(0.93f, 0.83f, 0.66f),
        ["HK_WoodDark"] = C(0.34f, 0.29f, 0.33f), ["HK_WoodMid"] = C(0.58f, 0.42f, 0.29f),
        ["HK_WoodLight"] = C(0.76f, 0.59f, 0.39f), ["HK_RoofTile"] = C(0.41f, 0.46f, 0.57f),
        ["HK_RoofRidge"] = C(0.34f, 0.37f, 0.48f), ["HK_Stone"] = C(0.61f, 0.64f, 0.66f),
        ["HK_StoneDark"] = C(0.48f, 0.50f, 0.60f), ["HK_Namako"] = C(0.47f, 0.53f, 0.65f),
        ["HK_Asphalt"] = C(0.56f, 0.59f, 0.69f), ["HK_Sidewalk"] = C(0.85f, 0.77f, 0.61f),
        ["HK_Curb"] = C(0.77f, 0.75f, 0.68f), ["HK_WhitePaint"] = C(0.98f, 0.93f, 0.77f),
        ["HK_Water"] = C(0.42f, 0.63f, 0.73f), ["HK_Grass"] = C(0.58f, 0.64f, 0.40f),
        ["HK_Soil"] = C(0.61f, 0.48f, 0.36f), ["HK_Leaf"] = C(0.45f, 0.61f, 0.36f),
        ["HK_LeafDark"] = C(0.28f, 0.46f, 0.40f), ["HK_Sakura"] = C(0.94f, 0.68f, 0.73f),
        ["HK_Bark"] = C(0.47f, 0.35f, 0.29f), ["HK_Metal"] = C(0.29f, 0.39f, 0.46f),
        ["HK_MetalLight"] = C(0.66f, 0.70f, 0.77f), ["HK_Glass"] = C(0.32f, 0.47f, 0.59f),
        ["HK_Shoji"] = C(0.94f, 0.89f, 0.71f), ["HK_TruckCab"] = C(0.84f, 0.87f, 0.85f),
        ["HK_Rubber"] = C(0.26f, 0.27f, 0.35f), ["HK_KeiPaint"] = C(0.90f, 0.91f, 0.87f),
        ["HK_VanBody"] = C(0.77f, 0.79f, 0.84f), ["HK_VanRib"] = C(0.62f, 0.65f, 0.73f),
        ["HK_Noren"] = C(0.32f, 0.44f, 0.64f), ["HK_NorenRed"] = C(0.77f, 0.39f, 0.29f),
        ["HK_Lamp"] = C(1.0f, 0.88f, 0.58f), ["HK_Gold"] = C(0.85f, 0.67f, 0.35f),
        ["HK_SignRed"] = C(0.83f, 0.23f, 0.24f), ["HK_SignBlue"] = C(0.22f, 0.42f, 0.74f),
        ["HK_SignYellow"] = C(0.98f, 0.81f, 0.29f), ["HK_PlateGreen"] = C(0.23f, 0.47f, 0.36f),
        ["HK_Guard"] = C(0.45f, 0.36f, 0.33f),
        ["ART_Frame"] = C(0.27f, 0.34f, 0.45f), ["ART_FrameLight"] = C(0.52f, 0.59f, 0.65f),
        ["ART_LampGlow"] = C(1f, 0.87f, 0.59f), ["ART_LampBase"] = C(0.48f, 0.48f, 0.57f),
        ["ART_SignalRed"] = C(0.94f, 0.19f, 0.16f), ["ART_SignalYellow"] = C(1f, 0.77f, 0.13f),
        ["ART_Glass"] = C(0.35f, 0.52f, 0.66f), ["ART_Cargo"] = C(0.80f, 0.82f, 0.82f),
        ["ART_Chassis"] = C(0.17f, 0.18f, 0.22f), ["ART_VehiclePaint"] = C(0.90f, 0.91f, 0.87f),
        ["ART_Rubber"] = C(0.26f, 0.27f, 0.35f), ["ART_WhitePaint"] = C(0.98f, 0.93f, 0.77f),
        ["ART_PlateGreen"] = C(0.23f, 0.47f, 0.36f),
        ["ART_SignalHousing"] = C(0.55f, 0.57f, 0.57f), ["ART_SignalHood"] = C(0.50f, 0.52f, 0.53f),
        ["ART_SignalHousingDark"] = C(0.25f, 0.25f, 0.27f), ["ART_SignalHoodDark"] = C(0.22f, 0.23f, 0.26f),
        ["ART_SignalGreen"] = C(0.17f, 0.76f, 0.46f), ["ART_SignalGlyph"] = C(1f, 0.96f, 0.80f),
        ["ART_SignalPole"] = C(0.36f, 0.29f, 0.25f),
    };

    /// <summary>Every Hikone material (one per name used by the Art/Hikone models).</summary>
    public static IEnumerable<string> MaterialNames => Base.Keys;

    /// <summary>Warm and cool dab pigments (sRGB), style.js PIGMENTS.</summary>
    static readonly Dictionary<string, (Color warm, Color cool)> Dabs = new Dictionary<string, (Color, Color)>
    {
        ["HK_Asphalt"] = (C(0.88f, 0.74f, 0.55f), C(0.56f, 0.64f, 0.83f)),
        ["HK_Sidewalk"] = (C(0.98f, 0.86f, 0.61f), C(0.61f, 0.67f, 0.83f)),
        ["HK_Curb"] = (C(0.93f, 0.85f, 0.65f), C(0.59f, 0.68f, 0.83f)),
        ["HK_Plaster"] = (C(1.0f, 0.91f, 0.71f), C(0.62f, 0.73f, 0.90f)),
        ["HK_PlasterWarm"] = (C(0.98f, 0.83f, 0.62f), C(0.69f, 0.75f, 0.89f)),
        ["HK_RoofTile"] = (C(0.76f, 0.64f, 0.53f), C(0.47f, 0.59f, 0.78f)),
        ["HK_WoodDark"] = (C(0.62f, 0.46f, 0.32f), C(0.43f, 0.43f, 0.55f)),
        ["HK_WoodMid"] = (C(0.80f, 0.60f, 0.38f), C(0.49f, 0.49f, 0.62f)),
        ["HK_Bark"] = (C(0.65f, 0.48f, 0.32f), C(0.44f, 0.44f, 0.54f)),
        ["HK_Stone"] = (C(0.89f, 0.81f, 0.63f), C(0.55f, 0.66f, 0.83f)),
        ["HK_StoneDark"] = (C(0.80f, 0.72f, 0.59f), C(0.50f, 0.60f, 0.78f)),
        ["HK_Leaf"] = (C(0.75f, 0.79f, 0.35f), C(0.34f, 0.59f, 0.58f)),
        ["HK_LeafDark"] = (C(0.57f, 0.69f, 0.37f), C(0.32f, 0.51f, 0.58f)),
        ["HK_Sakura"] = (C(1.0f, 0.85f, 0.76f), C(0.83f, 0.55f, 0.72f)),
        ["HK_Grass"] = (C(0.79f, 0.78f, 0.43f), C(0.39f, 0.57f, 0.57f)),
        ["HK_Water"] = (C(0.86f, 0.84f, 0.63f), C(0.42f, 0.61f, 0.83f)),
    };

    /// <summary>Light, ambient and fog (style.js LIGHT; sun and sky colours sRGB).</summary>
    public static readonly Color SunColor = C(1f, 0.94f, 0.81f);
    public static readonly Color AmbientSky = C(0.76f, 0.84f, 1f), AmbientGround = C(0.80f, 0.75f, 0.68f);
    public static readonly Color FogColor = C(0.78f, 0.84f, 0.87f);
    public const float FogStart = 85f, FogEnd = 430f;
    static readonly Color Horizon = C(0.89f, 0.90f, 0.84f), Zenith = C(0.39f, 0.64f, 0.88f);

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    /// <summary>Paints a Hikone material: pigment, dab pigments and strength, brush; keeps its emission.</summary>
    public static void Paint(Material m, string name, Texture2D brush)
    {
        m.shader = Shader.Find(ShaderName);
        // drop what the earlier URP/Lit material kept (photo textures, normal map, keywords): stale
        // texture slots still count as references and would keep the deleted textures alive
        var so = new SerializedObject(m);
        foreach (var list in new[] { "m_SavedProperties.m_TexEnvs", "m_SavedProperties.m_Floats", "m_SavedProperties.m_Colors" })
        {
            var props = so.FindProperty(list);
            for (var i = props.arraySize - 1; i >= 0; i--)
                if (m.shader.FindPropertyIndex(props.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue) < 0)
                    props.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        m.shaderKeywords = new string[0];
        var meshPigment = name.StartsWith("ART_");
        m.SetColor("_BaseColor", meshPigment ? Color.white : Base.TryGetValue(name, out var c) ? c : C(0.73f, 0.70f, 0.67f));
        m.SetFloat("_VertexPigment", meshPigment ? 1f : 0f);
        var known = Dabs.TryGetValue(name, out var d);
        m.SetColor("_Warm", known ? d.warm : C(0.76f, 0.64f, 0.49f));
        m.SetColor("_Cool", known ? d.cool : C(0.45f, 0.55f, 0.72f));
        // vehicle paint stays one colour so the per-car tint reads; unknown materials barely dab
        m.SetFloat("_DabStrength", meshPigment || name == "HK_KeiPaint" ? 0f : known ? 0.65f : 0.12f);
        m.SetTexture("_BrushTex", brush);
        m.SetFloat("_Paint", 1f);
    }

    // ------------------------------------------------------------------ brush texture
    const int Period = 32, Size = 512;

    /// <summary>paint-texture.js hash: 32-bit integer hash of a lattice cell, in [0, 1).</summary>
    static double Hash(int x, int y, int salt = 0)
    {
        x = ((x % Period) + Period) % Period;
        y = ((y % Period) + Period) % Period;
        unchecked
        {
            int n = (x + 19) * 374761393 ^ (y + 47) * 668265263 ^ salt;
            n = (int)((uint)n ^ ((uint)n >> 13)) * 1274126177;
            return ((uint)n ^ ((uint)n >> 16)) / 4294967296.0;
        }
    }

    /// <summary>The seamless brush texture (R dab mask, G dab pigment, B grain), as paint-texture.js.</summary>
    public static Texture2D WriteBrushTexture()
    {
        var cells = new (double x, double y, double seed, double c, double s)[Period * Period];
        for (var i = 0; i < cells.Length; i++)
        {
            int x = i % Period, y = i / Period;
            var seed = Hash(x, y);
            var angle = (seed - 0.5) * 0.8;
            cells[i] = (Hash(x, y, 7351), Hash(x, y, 15329), seed, System.Math.Cos(angle), System.Math.Sin(angle));
        }
        var pixels = new Color32[Size * Size];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            double px = (x + 0.5) / Size * Period, py = (y + 0.5) / Size * Period;
            int cx = (int)System.Math.Floor(px - 0.5), cy = (int)System.Math.Floor(py - 0.5);
            double mask = 0, pigment = 0, grain = Hash(x, y, 90787);
            for (var j = 0; j < 2; j++)
            for (var i = 0; i < 2; i++)
            {
                int ix = cx + i, iy = cy + j;
                var cell = cells[(((iy + Period) % Period) * Period) + ((ix + Period) % Period)];
                double dx = px - ix - cell.x, dy = py - iy - cell.y;
                double qx = dx * cell.c - dy * cell.s, qy = dx * cell.s + dy * cell.c;
                var edge = System.Math.Max(System.Math.Abs(qx) / (0.63 + cell.seed * 0.19), System.Math.Abs(qy) / (0.27 + cell.seed * 0.12))
                    + (grain - 0.5) * 0.20 + System.Math.Abs(qx + qy) * 0.13;
                var t = System.Math.Max(0, System.Math.Min(1, (edge - 0.71) / 0.33));
                var dab = 1 - t * t * (3 - 2 * t);
                if (dab > mask) { mask = dab; pigment = cell.seed; }
            }
            pixels[y * Size + x] = new Color32((byte)System.Math.Round(mask * 255), (byte)System.Math.Round(pigment * 255), (byte)System.Math.Round(grain * 255), 255);
        }
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(BrushPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(BrushPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(BrushPath);
        ti.sRGBTexture = false;                       // data, not colour
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;
        ti.maxTextureSize = Size;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(BrushPath);
    }

    // ------------------------------------------------------------------ painted sky
    static float Fract(float v) => v - Mathf.Floor(v);

    static float SkyHash(float x, float y)
    {
        float qx = Fract(x * 0.1031f), qy = Fract(y * 0.1031f), qz = Fract(x * 0.1031f);
        var d = qx * (qy + 33.33f) + qy * (qz + 33.33f) + qz * (qx + 33.33f);
        qx += d; qy += d; qz += d;
        return Fract((qx + qy) * qz);
    }

    static float Noise(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
        float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(SkyHash(ix, iy), SkyHash(ix + 1, iy), ux), Mathf.Lerp(SkyHash(ix, iy + 1), SkyHash(ix + 1, iy + 1), ux), uy);
    }

    static float Fbm(float x, float y)
    {
        float v = 0, a = 0.55f;
        for (var i = 0; i < 4; i++) { v += a * Noise(x, y); x = x * 2.03f + 1.7f; y = y * 2.03f + 1.7f; a *= 0.5f; }
        return v;
    }

    /// <summary>style.js hkDabs: jittered, chipped short dabs (mask, pigment).</summary>
    static Vector2 SkyDabs(float px, float py)
    {
        float cx = Mathf.Floor(px - 0.5f), cy = Mathf.Floor(py - 0.5f), mask = 0, pigment = 0;
        var bristle = (Noise(px * 8f, py * 31f) - 0.5f) * 0.20f;
        for (var j = 0; j <= 1; j++)
        for (var i = 0; i <= 1; i++)
        {
            float idx = cx + i, idy = cy + j;
            var seed = SkyHash(idx + 8.7f, idy + 8.7f);
            float qx = px - (idx + SkyHash(idx + 1.2f, idy + 1.2f)), qy = py - (idy + SkyHash(idx + 5.8f, idy + 5.8f));
            var a = (seed - 0.5f) * 0.8f;
            float rx = Mathf.Cos(a) * qx + Mathf.Sin(a) * qy, ry = -Mathf.Sin(a) * qx + Mathf.Cos(a) * qy;
            var edge = Mathf.Max(Mathf.Abs(rx) / (0.63f + seed * 0.19f), Mathf.Abs(ry) / (0.27f + seed * 0.12f)) + bristle + Mathf.Abs(rx + ry) * 0.13f;
            var dab = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.71f, 1.04f, edge));
            if (dab > mask) { mask = dab; pigment = seed; }
        }
        return new Vector2(mask, pigment);
    }

    static Vector3 Lin(Color c) => new Vector3(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));

    /// <summary>The painted sky colour (linear) in direction d, style.js paintedSky.</summary>
    static Vector3 Sky(Vector3 d)
    {
        var h = Mathf.Clamp01(d.y);
        var blue = Vector3.Lerp(Lin(Horizon), Lin(Zenith), Mathf.Pow(h, 0.38f));
        float ux = d.x / (h + 0.48f) * 2f, uy = d.z / (h + 0.48f) * 2f;
        float qx = 0.88f * ux + 0.47f * uy, qy = -0.47f * ux + 0.88f * uy;
        var mass = Fbm(ux * 1.55f + 4f, uy * 1.55f + 4f);
        var brush = SkyDabs(qx * 22f, qy * 36f);
        float pigment = brush.y, dab = brush.x;
        var cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.46f, 0.69f, mass + (pigment - 0.5f) * 0.13f));
        cloud *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.015f, 0.14f, h));
        var cream = Vector3.Lerp(new Vector3(0.91f, 0.91f, 0.87f), new Vector3(1f, 0.90f, 0.71f), pigment * 0.5f);
        var c = Vector3.Lerp(blue * (0.90f + pigment * 0.18f), cream, cloud * (0.43f + dab * 0.47f));
        return Vector3.Lerp(c, new Vector3(0.73f, 0.81f, 0.94f), (1f - dab) * cloud * 0.15f);
    }

    /// <summary>Bakes the painted sky into a latitude–longitude panorama and its skybox material.</summary>
    public static Material WriteSky(int width = 2048)
    {
        var height = width / 2;
        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            var lat = ((y + 0.5f) / height - 0.5f) * Mathf.PI;
            for (var x = 0; x < width; x++)
            {
                var lon = ((x + 0.5f) / width - 0.5f) * 2f * Mathf.PI;
                var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                var c = Sky(d);
                pixels[y * width + x] = new Color(Mathf.LinearToGammaSpace(c.x), Mathf.LinearToGammaSpace(c.y), Mathf.LinearToGammaSpace(c.z), 1f);
            }
        }
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SkyTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SkyTexturePath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(SkyTexturePath);
        ti.wrapModeU = TextureWrapMode.Repeat;
        ti.wrapModeV = TextureWrapMode.Clamp;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = width;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();

        var m = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(m, SkyMaterialPath);
        }
        m.shader = Shader.Find("Skybox/Panoramic");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath));
        m.SetFloat("_Mapping", 1f);       // latitude-longitude layout
        m.SetFloat("_ImageType", 0f);     // 360°
        m.SetFloat("_Exposure", 1f);
        m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }
}
