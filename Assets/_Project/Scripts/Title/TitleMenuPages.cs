using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title menu in three steps (① 参加者 ② 条件 ③ 場面) so every screen has few, large controls
/// that stay legible on Quest 2. The footer (summary, Reset, Start) is always visible.
/// The last step shown is remembered across returns to the title.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleMenuPages : MonoBehaviour
{
    [SerializeField] GameDirector_Title director;
    [SerializeField] GameObject[] pages;
    [SerializeField] Button[] tabs;
    [SerializeField] TMP_Text summary;

    static int rememberedPage;

    public int Current { get; private set; } = -1;
    public int Count => pages != null ? pages.Length : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => rememberedPage = 0;

    void Awake()
    {
        for (var i = 0; i < tabs.Length; i++)
        {
            var page = i;
            tabs[i].onClick.AddListener(TitleButtonFeedback.PlayClickFeedback);
            tabs[i].onClick.AddListener(() => ShowPage(page));
        }
    }

    void OnEnable()
    {
        if (director != null)
            director.UIStateChanged += UpdateSummary;
    }

    void OnDisable()
    {
        if (director != null)
            director.UIStateChanged -= UpdateSummary;
    }

    void Start()
    {
        ShowPage(rememberedPage);
        UpdateSummary();
    }

    public void ShowPage(int index)
    {
        index = Mathf.Clamp(index, 0, pages.Length - 1);
        Current = index;
        rememberedPage = index;
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            var colors = tabs[i].colors;
            colors.normalColor = i == index ? TitleOptionToggle.SelectedColor : TitleButtonFeedback.NormalColor;
            colors.highlightedColor = i == index ? TitleOptionToggle.SelectedHoverColor : TitleButtonFeedback.HoverColor;
            colors.selectedColor = colors.normalColor;
            tabs[i].colors = colors;
        }
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
        summary.text = $"場面 <b>{scenario}</b>　不快音 <b>{tone}</b>　{director.Height} cm・{director.Weight} kg・{director.Age} 歳";
    }

#if UNITY_EDITOR
    public void EditorConfigure(GameDirector_Title configuredDirector, GameObject[] configuredPages, Button[] configuredTabs, TMP_Text configuredSummary)
    {
        director = configuredDirector;
        pages = configuredPages;
        tabs = configuredTabs;
        summary = configuredSummary;
    }
#endif
}
