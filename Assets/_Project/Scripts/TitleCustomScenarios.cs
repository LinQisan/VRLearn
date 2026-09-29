using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title menu: the "カスタム" tab of the scenario card. Lists the JSON scenarios on this device
/// (copied from the PC with the scenario editor's "Questに送る") in a fixed grid of tiles built by
/// TitleMenuLayout, with paging and a rescan button. Selecting a tile selects that file.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleCustomScenarios : MonoBehaviour
{
    [SerializeField] GameDirector_Title director;
    [SerializeField] GameObject builtinView;
    [SerializeField] GameObject customView;
    [SerializeField] GameObject randomTile;
    [SerializeField] Button builtinTab;
    [SerializeField] Button customTab;
    [SerializeField] TMP_Text customTabLabel;
    [SerializeField] TitleOptionToggle[] slots;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text pageText;
    [SerializeField] Button previousPage;
    [SerializeField] Button nextPage;
    [SerializeField] Button refreshButton;

    public bool ShowingCustom { get; private set; }
    public int Page { get; private set; }
    public int PageCount => Mathf.Max(1, Mathf.CeilToInt(director.CustomScenarios.Count / (float)slots.Length));
    public int Rejected { get; private set; }

    void Start()
    {
        builtinTab.onClick.AddListener(() => Show(false));
        customTab.onClick.AddListener(() => Show(true));
        previousPage.onClick.AddListener(() => SetPage(Page - 1));
        nextPage.onClick.AddListener(() => SetPage(Page + 1));
        refreshButton.onClick.AddListener(Rescan);
        foreach (var button in new[] { builtinTab, customTab, previousPage, nextPage, refreshButton })
            button.onClick.AddListener(TitleButtonFeedback.PlayClickFeedback);
        Rescan();
        // coming back from a custom run: open on its tab
        Show(CustomScenarioSession.IsCustom(director.EventNumber));
    }

    public void Rescan()
    {
        var (scenarios, rejected) = CustomScenarioLibrary.ScanForTitle();
        Rejected = rejected;
        director.SetCustomScenarios(scenarios);
        // stay on the page of the selected scenario
        var selected = CustomScenarioSession.Current;
        var index = selected != null ? scenarios.FindIndex(s => s.id == selected.id) : -1;
        SetPage(index >= 0 ? index / slots.Length : Mathf.Min(Page, PageCount - 1));
    }

    public void Show(bool custom)
    {
        ShowingCustom = custom;
        builtinView.SetActive(!custom);
        customView.SetActive(custom);
        if (randomTile != null) randomTile.SetActive(!custom);
        refreshButton.gameObject.SetActive(custom);
        StyleTab(builtinTab, !custom);
        StyleTab(customTab, custom);
    }

    public void SetPage(int page)
    {
        Page = Mathf.Clamp(page, 0, PageCount - 1);
        var list = director.CustomScenarios;
        for (var i = 0; i < slots.Length; i++)
        {
            var index = Page * slots.Length + i;
            var slot = slots[i];
            var used = index < list.Count;
            slot.gameObject.SetActive(used);
            if (!used)
                continue;
            var scenario = list[index];
            slot.ConfigureCustom(director, scenario.id);
            var label = slot.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = $"{scenario.name}\n<size=68%><alpha=#B3>{TitleScenarioDetail.ModeLabel(scenario.PlayerMode)} ・ {TitleScenarioDetail.SettingLabel(scenario.Setting)}</size>";
            var accent = slot.transform.Find("Accent");
            if (accent != null)
                accent.GetComponent<Image>().color = TitleScenarioDetail.SettingColor(scenario.Setting);
        }
        var total = list.Count;
        customTabLabel.text = $"カスタム（{total}）";
        emptyText.gameObject.SetActive(total == 0);
        emptyText.text = total == 0
            ? "カスタム場面がありません。\nPC のシナリオエディタで作って「Questに送る」を押してから「読み直す」を押してください。"
              + (Rejected > 0 ? $"\n（読み込めないファイル {Rejected} 件）" : "")
            : "";
        var paging = PageCount > 1;
        pageText.gameObject.SetActive(paging || Rejected > 0);
        pageText.text = (paging ? $"{Page + 1} / {PageCount}" : "") + (Rejected > 0 && total > 0 ? $"　読めないファイル {Rejected}" : "");
        previousPage.gameObject.SetActive(paging);
        nextPage.gameObject.SetActive(paging);
        previousPage.interactable = Page > 0;
        nextPage.interactable = Page < PageCount - 1;
    }

    static void StyleTab(Button tab, bool active)
    {
        var colors = tab.colors;
        colors.normalColor = active ? TitleOptionToggle.SelectedColor : TitleButtonFeedback.NormalColor;
        colors.selectedColor = colors.normalColor;
        colors.highlightedColor = active ? TitleOptionToggle.SelectedHoverColor : TitleButtonFeedback.HoverColor;
        tab.colors = colors;
    }

#if UNITY_EDITOR
    public void EditorConfigure(GameDirector_Title configuredDirector, GameObject builtin, GameObject custom, GameObject random,
        Button builtinTabButton, Button customTabButton, TMP_Text customTabText, TitleOptionToggle[] slotTiles,
        TMP_Text empty, TMP_Text page, Button previous, Button next, Button refresh)
    {
        director = configuredDirector;
        builtinView = builtin;
        customView = custom;
        randomTile = random;
        builtinTab = builtinTabButton;
        customTab = customTabButton;
        customTabLabel = customTabText;
        slots = slotTiles;
        emptyText = empty;
        pageText = page;
        previousPage = previous;
        nextPage = next;
        refreshButton = refresh;
    }
#endif
}
