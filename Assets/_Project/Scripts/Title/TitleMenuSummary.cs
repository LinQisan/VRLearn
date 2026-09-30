using TMPro;
using UnityEngine;

/// <summary>
/// The "selection" box next to Start on the one-page title menu: scenario, unpleasant tone and
/// body data in large text, so the operator can confirm the conditions before starting.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleMenuSummary : MonoBehaviour
{
    [SerializeField] GameDirector_Title director;
    [SerializeField] TMP_Text summary;

    void OnEnable()
    {
        if (director != null)
            director.UIStateChanged += UpdateSummary;
        UpdateSummary();
    }

    void OnDisable()
    {
        if (director != null)
            director.UIStateChanged -= UpdateSummary;
    }

    public void UpdateSummary()
    {
        if (summary == null || director == null)
            return;
        string scenario;
        if (CustomScenarioSession.IsCustom(director.EventNumber) && CustomScenarioSession.Current != null)
            scenario = "カスタム「" + CustomScenarioSession.Current.name + "」";
        else if (director.EventNumber == SceneRoute.RandomScenarioId)
            scenario = "ランダム";
        else
            scenario = (director.EventNumber + 1).ToString("00");
        var tone = director.Hz > 0f ? $"{director.Hz:0} Hz" : "なし";
        summary.text = $"場面　<b>{scenario}</b>\n不快音　<b>{tone}</b>\n{director.Height} cm・{director.Weight} kg・{director.Age} 歳";
    }

#if UNITY_EDITOR
    public void EditorConfigure(GameDirector_Title configuredDirector, TMP_Text configuredSummary)
    {
        director = configuredDirector;
        summary = configuredSummary;
    }
#endif
}
