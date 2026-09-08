using UnityEngine;
using UnityEngine.UI;

public enum TitleOptionKind
{
    Gender,
    License,
    Incident,
    DieFlash,
    SmartPhone,
    Weather,
    SkyTime,
    Hz
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Toggle))]
public sealed class TitleOptionToggle : MonoBehaviour
{
    static readonly Color SelectedOutlineColor = new Color32(24, 168, 102, 255);

    [SerializeField] GameDirector_Title director;
    [SerializeField] TitleOptionKind kind;
    [SerializeField] int intValue;
    [SerializeField] float floatValue;

    Toggle toggle;
    Outline selectedOutline;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        TitleButtonFeedback.ConfigureSelectable(toggle);
        selectedOutline = GetComponent<Outline>();
        toggle.onValueChanged.AddListener(OnValueChanged);
    }

    public void RefreshFromModel()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();
        var selected = IsSelected;
        toggle.SetIsOnWithoutNotify(selected);
        if (selectedOutline != null)
            selectedOutline.enabled = selected;
    }

    public bool IsSelected
    {
        get
        {
            if (director == null)
                return false;

            switch (kind)
            {
                case TitleOptionKind.Gender: return director.Gender == intValue;
                case TitleOptionKind.License: return director.License == intValue;
                case TitleOptionKind.Incident: return director.Incident == intValue;
                case TitleOptionKind.DieFlash: return director.DieFlashNumber == intValue;
                case TitleOptionKind.SmartPhone: return director.SmartPhone == intValue;
                case TitleOptionKind.Weather: return director.Weather == intValue;
                case TitleOptionKind.SkyTime: return director.SkyTime == intValue;
                case TitleOptionKind.Hz: return Mathf.Approximately(director.Hz, floatValue);
                default: return false;
            }
        }
    }

    void OnValueChanged(bool isOn)
    {
        if (isOn && director != null)
            director.ApplyOption(kind, intValue, floatValue);
    }

#if UNITY_EDITOR
    public void EditorBindOutline(Outline outline)
    {
        selectedOutline = outline;
        if (selectedOutline == null)
            return;
        selectedOutline.effectColor = SelectedOutlineColor;
        // Title-menu controls are authored in small world-space canvas units.
        // A 2.5-unit outline is wider than several controls and visibly spills
        // into adjacent cards; keep selection clear without leaving the button.
        selectedOutline.effectDistance = new Vector2(0.35f, -0.35f);
        selectedOutline.useGraphicAlpha = false;
    }
#endif

#if UNITY_EDITOR
    public void EditorConfigure(
        GameDirector_Title configuredDirector,
        TitleOptionKind configuredKind,
        int configuredIntValue,
        float configuredFloatValue)
    {
        director = configuredDirector;
        kind = configuredKind;
        intValue = configuredIntValue;
        floatValue = configuredFloatValue;
    }
#endif
}
