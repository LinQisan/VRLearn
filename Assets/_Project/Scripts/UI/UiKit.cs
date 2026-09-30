using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared look for the runtime-built panels (replay, feedback) and the title menu:
/// one palette and procedurally generated rounded / circle sprites, so no texture assets are needed.
/// </summary>
public static class UiKit
{
    // palette: deep navy glass, one accent per page
    public static readonly Color Backdrop = new Color32(7, 11, 22, 255);
    public static readonly Color Panel = new Color32(13, 20, 36, 250);
    public static readonly Color Card = new Color32(22, 32, 54, 255);
    public static readonly Color CardRaised = new Color32(31, 44, 72, 255);
    public static readonly Color Hairline = new Color32(51, 68, 104, 255);
    public static readonly Color Text = new Color32(241, 245, 249, 255);
    public static readonly Color Muted = new Color32(148, 163, 184, 255);
    public static readonly Color Sky = new Color32(125, 211, 252, 255);
    public static readonly Color Amber = new Color32(251, 191, 36, 255);
    public static readonly Color Coral = new Color32(248, 113, 113, 255);
    public static readonly Color Mint = new Color32(74, 222, 128, 255);
    public static readonly Color Ink = new Color32(15, 23, 42, 255);

    public const int SpriteSize = 64;
    public const int SpriteRadius = 28;

    static Sprite rounded;
    static Sprite circle;

    /// <summary>9-sliced rounded rectangle (corner radius 28 px at multiplier 1).</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded == null)
            {
                var texture = RoundedTexture(SpriteSize, SpriteRadius);
                rounded = Sprite.Create(texture, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, Vector4.one * (SpriteRadius + 2));
                rounded.name = "UiKit_Rounded";
            }
            return rounded;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                var texture = RoundedTexture(SpriteSize, SpriteSize / 2);
                circle = Sprite.Create(texture, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), 100f);
                circle.name = "UiKit_Circle";
            }
            return circle;
        }
    }

    /// <summary>White, anti-aliased rounded square; alpha carries the shape.</summary>
    public static Texture2D RoundedTexture(int size, int radius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "UiKit_Rounded_" + radius,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                var a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    public static RectTransform Box(Transform parent, string name, Color color, Vector2 position, Vector2 size,
        float cornerScale = 1f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (cornerScale > 0f)
        {
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / cornerScale;
        }
        return rect;
    }

    public static RectTransform Dot(Transform parent, string name, Color color, Vector2 position, float diameter)
    {
        var rect = Box(parent, name, color, position, new Vector2(diameter, diameter), 0f);
        rect.GetComponent<Image>().sprite = Circle;
        return rect;
    }

    public static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, string text, float size,
        Vector2 position, Vector2 box, Color color, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = align;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.extraPadding = true;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>Pill button with a label; colours follow the Selectable states.</summary>
    public static Button PillButton(Transform parent, string name, TMP_FontAsset font, string text, Color fill,
        Color labelColor, Vector2 position, Vector2 size, float fontSize = 32f)
    {
        var rect = Box(parent, name, Color.white, position, size, 2f);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = fill;
        colors.selectedColor = Color.Lerp(fill, Color.white, 0.12f);
        colors.highlightedColor = Color.Lerp(fill, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.2f);
        colors.fadeDuration = 0.07f;
        button.colors = colors;
        Label(rect, "Label", font, text, fontSize, Vector2.zero, size - new Vector2(24f, 8f), labelColor,
            FontStyles.Bold, TextAlignmentOptions.Center);
        return button;
    }

    public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
}
