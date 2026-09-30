using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title menu: describes the currently selected scenario (name, where it happens, what it teaches)
/// next to the scenario tiles, so participants and staff can confirm the choice before starting.
/// Content comes from the ScenarioDefinitionAssets; the number readout is written by GameDirector_Title.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleScenarioDetail : MonoBehaviour
{
    [SerializeField] GameDirector_Title director;
    [SerializeField] ScenarioDefinitionAsset[] definitions;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text englishText;
    [SerializeField] TMP_Text goalText;
    [SerializeField] TMP_Text settingText;
    [SerializeField] Image settingChip;

    string shown;

    public static string SettingLabel(ScenarioSetting setting) => setting switch
    {
        ScenarioSetting.Crossing => "横断歩道・交差点",
        ScenarioSetting.MidBlock => "道路の途中",
        _ => "自転車"
    };

    public static string ModeLabel(ScenarioPlayerMode mode) => mode == ScenarioPlayerMode.Bicycle ? "自転車" : "歩行";

    public static Color SettingColor(ScenarioSetting setting) => setting switch
    {
        ScenarioSetting.Crossing => UiKit.Sky,
        ScenarioSetting.MidBlock => UiKit.Amber,
        _ => UiKit.Mint
    };

    void OnEnable()
    {
        if (director != null)
            director.UIStateChanged += Refresh;
        shown = null;
        Refresh();
    }

    void OnDisable()
    {
        if (director != null)
            director.UIStateChanged -= Refresh;
    }

    public void Refresh()
    {
        if (director == null)
            return;
        var custom = CustomScenarioSession.IsCustom(director.EventNumber) ? CustomScenarioSession.Current : null;
        var key = custom != null ? "custom:" + custom.id : director.EventNumber.ToString();
        if (key == shown)
            return;
        shown = key;
        if (custom != null)
        {
            titleText.text = custom.name;
            englishText.text = string.IsNullOrEmpty(custom.nameEn) ? "Custom scenario" : custom.nameEn;
            goalText.text = string.IsNullOrWhiteSpace(custom.learningGoal) ? custom.point : custom.learningGoal;
            settingText.text = "カスタム ・ " + SettingLabel(custom.Setting);
            settingChip.color = SettingColor(custom.Setting);
            return;
        }
        var definition = Find(director.EventNumber);
        if (definition == null)
        {
            titleText.text = "ランダム";
            englishText.text = "Random scenario";
            goalText.text = "10の場面から1つを選びます。どの場面かは始まるまでわかりません。";
            settingText.text = "10の場面から";
            settingChip.color = UiKit.Muted;
            return;
        }
        titleText.text = definition.shortTitle;
        englishText.text = definition.shortTitleEn;
        goalText.text = definition.learningGoal;
        settingText.text = SettingLabel(definition.setting);
        settingChip.color = SettingColor(definition.setting);
    }

    ScenarioDefinitionAsset Find(int id)
    {
        if (definitions == null)
            return null;
        foreach (var definition in definitions)
            if (definition != null && definition.id == id)
                return definition;
        return null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(GameDirector_Title configuredDirector, ScenarioDefinitionAsset[] configuredDefinitions,
        TMP_Text title, TMP_Text english, TMP_Text goal, TMP_Text setting, Image chip)
    {
        director = configuredDirector;
        definitions = configuredDefinitions;
        titleText = title;
        englishText = english;
        goalText = goal;
        settingText = setting;
        settingChip = chip;
    }
#endif
}
