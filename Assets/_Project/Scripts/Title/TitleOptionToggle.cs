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
    Hz,
    // 8 was the standard / Hikone environment choice (removed with the standard scene);
    // the values below are serialized in the title scene and must not shift.
    EventNumber = 9,
    CustomScenario = 10  // runtime-filled tile for a JSON scenario (stringValue = its id)
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Toggle))]
public sealed class TitleOptionToggle : MonoBehaviour
{
    static readonly Color SelectedOutlineColor = new Color32(255, 255, 255, 255);
    public static readonly Color SelectedColor = new Color32(14, 165, 233, 255);
    public static readonly Color SelectedHoverColor = new Color32(56, 189, 248, 255);

    [SerializeField] GameDirector_Title director;
    [SerializeField] TitleOptionKind kind;
    [SerializeField] int intValue;
    [SerializeField] float floatValue;
    [SerializeField] string stringValue;

    public TitleOptionKind Kind => kind;
    public string StringValue => stringValue;

    /// <summary>Binds a runtime custom-scenario tile (TitleCustomScenarios fills a fixed set of slots).</summary>
    public void ConfigureCustom(GameDirector_Title configuredDirector, string scenarioId)
    {
        director = configuredDirector;
        kind = TitleOptionKind.CustomScenario;
        stringValue = scenarioId;
        RefreshFromModel();
    }

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
        // Selected choices are filled with the accent colour so the current value of every
        // tiled option is readable at a glance from the headset.
        var colors = toggle.colors;
        colors.normalColor = selected ? SelectedColor : TitleButtonFeedback.NormalColor;
        colors.highlightedColor = selected ? SelectedHoverColor : TitleButtonFeedback.HoverColor;
        colors.selectedColor = colors.normalColor;
        toggle.colors = colors;
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
                case TitleOptionKind.EventNumber: return director.EventNumber == intValue;
                case TitleOptionKind.CustomScenario:
                    return CustomScenarioSession.IsCustom(director.EventNumber)
                        && CustomScenarioSession.Current != null
                        && CustomScenarioSession.Current.id == stringValue;
                default: return false;
            }
        }
    }

    void OnValueChanged(bool isOn)
    {
        if (!isOn || director == null)
            return;
        if (kind == TitleOptionKind.CustomScenario)
            director.SelectCustomScenario(stringValue);
        else
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
