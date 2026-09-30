using UnityEngine;

/// <summary>Per-button colour override for title controls (e.g. the primary Start button).</summary>
[DisallowMultipleComponent]
public sealed class TitleButtonStyle : MonoBehaviour
{
    public Color normal = new Color32(245, 158, 11, 255);
    public Color hover = new Color32(251, 191, 36, 255);
    public Color pressed = new Color32(217, 119, 6, 255);
}
