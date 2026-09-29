using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the Meta title menu as a flat, VR-controller friendly layout.
/// Every choice is a tile that can be hit with one trigger pull: scenario numbers and the
/// unpleasant-sound frequencies are tiled instead of stepped. Existing controls are reused so
/// their GameDirector_Title bindings, commands and test lookups stay intact. Idempotent.
/// </summary>
public static class TitleMenuLayout
{
    const string TitleScene = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
    const string Root = "MenuWorldRoot/Canvas_TitleMenu";
    const float W = 140f, H = 92f;

    static readonly Color Panel = new Color32(11, 18, 32, 245);
    static readonly Color CardColor = new Color32(22, 33, 58, 255);
    static readonly Color CardEdge = new Color32(44, 60, 94, 255);
    static readonly Color FieldColor = new Color32(9, 14, 26, 255);
    static readonly Color TextMain = new Color32(241, 245, 249, 255);
    static readonly Color TextSub = new Color32(148, 163, 184, 255);
    static readonly Color Accent = new Color32(125, 211, 252, 255);
    static readonly Color Dark = new Color32(15, 23, 42, 255);

    static Sprite rounded;
    static GameDirector_Title director;

    [MenuItem("Tools/VRLearn/Rebuild Title Menu (VR tiles)")]
    public static void Rebuild()
    {
        var scene = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Single);
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        director = UnityEngine.Object.FindFirstObjectByType<GameDirector_Title>(FindObjectsInactive.Include);
        var canvas = GameObject.Find(Root).transform as RectTransform;
        var menu = canvas.Find("MenuRoot") as RectTransform;
        var page = menu.Find("Page_Main") as RectTransform;
        canvas.sizeDelta = menu.sizeDelta = page.sizeDelta = new Vector2(W, H);
        // the layout grew from 124 to 140 units wide; keep the panel's size in the headset
        canvas.localScale = Vector3.one * (124f / W);
        var fontSource = page.GetComponentInChildren<TMP_Text>(true);

        // ---------------------------------------------------------------- frame + header
        Place(menu.Find("Background"), 0, 0, W, H);
        Style(menu.Find("Background").GetComponent<Image>(), Panel, 3f);
        var outline = menu.Find("Background").GetComponent<Outline>();
        if (outline) { outline.effectColor = CardEdge; outline.effectDistance = new Vector2(0.2f, -0.2f); }
        menu.Find("BackgroundInner").gameObject.SetActive(false);
        menu.Find("Header_Background").gameObject.SetActive(false);
        var header = menu.Find("Header_Content") as RectTransform;
        Place(header, 0, H / 2 - 6f, W - 8, 8);
        var brand = Ensure(header, "Accent_Bar", null, typeof(Image));
        Place(brand, -(W - 8) / 2 + 0.6f, 0f, 1.2f, 6.4f);
        Style(brand.GetComponent<Image>(), Accent, 20f);
        brand.GetComponent<Image>().raycastTarget = false;
        var title = header.Find("Title");
        Place(title, -20f, 1.4f, W - 52, 4.6f);
        Text(title, null, 3.3f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        var subtitle = header.Find("Subtitle");
        Place(subtitle, -20f, -2.3f, W - 52, 2.4f);
        Text(subtitle, null, 1.3f, TextSub, TextAlignmentOptions.Left);
        var chip = Ensure(header, "Chip_Environment", subtitle);
        Place(chip, 44, 1.4f, 42, 3.4f);
        Text(chip, "彦根・京橋 / Hikone Kyobashi", 1.9f, Accent, TextAlignmentOptions.Right, FontStyles.Bold);
        var hint = Ensure(header, "Hint_Controls", subtitle);
        Place(hint, 44, -2.3f, 42, 2.4f);
        Text(hint, "コントローラーで指してトリガー / Point & pull the trigger", 1.2f, TextSub, TextAlignmentOptions.Right);

        // ---------------------------------------------------------------- cards
        const float topY = 20.5f, topH = 28f;
        var user = Card(page, "Card_UserInformation", -46, topY, 44, topH, "参加者 / Participant");
        var cond = Card(page, "Card_ScenarioConditions", 0, topY, 44, topH, "場面条件 / Conditions");
        var survey = Card(page, "Card_SurveyInformation", 46, topY, 44, topH, "調査 / Survey");
        var evt = Card(page, "Card_EventNumber", 0, -10f, 136, 30, "");
        var tone = Card(page, "Card_SoundQuickSelect", -21f, -35f, 94, 17, "不快音 / Unpleasant tone");

        // participant: steppers + gender
        float[] rows = { 6.2f, 0.6f, -5.0f, -10.6f };
        string[] steps = { "Height", "Weight", "Age" };
        for (var i = 0; i < steps.Length; i++)
        {
            var y = rows[i];
            RowLabel(user.Find("Label_" + steps[i]), -14.8f, y);
            Command(user.Find($"Button_{steps[i]}_Down"), -4f, y, 6.4f, 4.8f, "-5");
            var field = user.Find("Field_" + steps[i]);
            Place(field, 5.5f, y, 11, 4.8f);
            Style(field.GetComponent<Image>(), FieldColor, 10f);
            var value = field.Find("Value_" + steps[i]);
            Place(value, -1.3f, 0, 7.4f, 4.8f);
            Text(value, null, 2.5f, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            var unit = field.Find("Unit_" + steps[i]);
            Place(unit, 3.9f, -0.6f, 2.8f, 3);
            Text(unit, null, 1.1f, TextSub, TextAlignmentOptions.Left);
            Command(user.Find($"Button_{steps[i]}_Up"), 15f, y, 6.4f, 4.8f, "+5");
        }
        RowLabel(user.Find("Label_Gender"), -14.8f, rows[3]);
        Tile(user.Find("Gender_Male"), -3.6f, rows[3], 8.4f, 4.8f, "男\nMale");
        Tile(user.Find("Gender_Female"), 5.4f, rows[3], 8.4f, 4.8f, "女\nFemale");
        Tile(user.Find("Gender_Unknown"), 14.4f, rows[3], 8.4f, 4.8f, "不明\nOther");

        // conditions: four two-way rows (the environment row is gone: Hikone only)
        foreach (var n in new[] { "Label_Scene", "Scene_First", "Scene_Second" })
            Remove(cond, n);
        string[] cRows = { "DieFlash", "Smartphone", "Weather", "Time" };
        for (var i = 0; i < cRows.Length; i++)
        {
            RowLabel(cond.Find("Label_" + cRows[i]), -14.8f, rows[i]);
            Tile(cond.Find(cRows[i] + "_First"), 2.2f, rows[i], 11f, 4.8f, null);
            Tile(cond.Find(cRows[i] + "_Second"), 14f, rows[i], 11f, 4.8f, null);
        }

        // survey: two three-way groups
        foreach (var (label, y) in new[] { ("Label_License", 7.2f), ("Label_Incident", -3.2f) })
        {
            var l = survey.Find(label);
            Place(l, 0, y, 40, 3.2f);
            Text(l, null, 1.4f, TextSub, TextAlignmentOptions.Center);
        }
        Tile(survey.Find("License_Have"), -13.2f, 2.6f, 12.4f, 4.8f, "有\nYes");
        Tile(survey.Find("License_None"), 0f, 2.6f, 12.4f, 4.8f, "無\nNo");
        Tile(survey.Find("License_Unknown"), 13.2f, 2.6f, 12.4f, 4.8f, "不明\nUnknown");
        Tile(survey.Find("Incident_Yes"), -13.2f, -8.4f, 12.4f, 4.8f, "有\nYes");
        Tile(survey.Find("Incident_No"), 0f, -8.4f, 12.4f, 4.8f, "無\nNo");
        Tile(survey.Find("Incident_Unknown"), 13.2f, -8.4f, 12.4f, 4.8f, "不明\nUnknown");

        // ---------------------------------------------------------------- scenarios
        // grouped by where they happen; each tile shows number, Japanese and English name
        var definitions = AssetDatabase.FindAssets("t:ScenarioDefinitionAsset", new[] { "Assets/_Project/ScenarioDefinitions" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ScenarioDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null).OrderBy(d => d.id).ToArray();
        var template = cond.Find("DieFlash_First");
        foreach (var n in new[] { "Button_EventDown", "Button_EventUp", "Field_EventNumber" })
            Remove(evt, n);
        // two views share the card's left area: built-in groups, and the custom JSON scenarios
        var builtinView = FullRect(evt, "View_Builtin");
        foreach (var child in evt.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Group_") || (child.name.StartsWith("Event_") && child.name != "Event_Random"))
                child.SetParent(builtinView, false);
        var groups = new[] { ScenarioSetting.Crossing, ScenarioSetting.MidBlock, ScenarioSetting.Bicycle };
        for (var g = 0; g < groups.Length; g++)
        {
            var x = -51.5f + g * 30.5f;
            var head = builtinView.Find("Group_" + groups[g])
                ?? NewText(builtinView, "Group_" + groups[g], fontSource, 0f, 0f, 1f, 1f, "", 1f, TextMain, TextAlignmentOptions.Left).transform;
            Place(head, x + 1.2f, 11.6f, 28, 3f);
            var count = definitions.Count(d => d.setting == groups[g]);
            Text(head, $"<color={Hex(TitleScenarioDetail.SettingColor(groups[g]))}>●</color> {TitleScenarioDetail.SettingLabel(groups[g])}"
                + $"<color={Hex(TextSub)}><size=75%>  {count}</size></color>", 1.45f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
            var i = 0;
            foreach (var d in definitions.Where(d => d.setting == groups[g]))
            {
                var t = Clone(template, builtinView, "Event_" + d.id);
                Option(t, TitleOptionKind.EventNumber, d.id, 0f);
                var label = $"<alpha=#99>{d.id + 1:00}<alpha=#FF>  {d.shortTitle}\n<size=68%><alpha=#B3>{d.shortTitleEn}</size>";
                ScenarioTile(t, x, 6.8f - i * 5.8f, label, TitleScenarioDetail.SettingColor(d.setting));
                i++;
            }
        }

        var customView = FullRect(evt, "View_Custom");
        var customHead = customView.Find("Heading")
            ?? NewText(customView, "Heading", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextMain, TextAlignmentOptions.Left).transform;
        Place(customHead, -30f, 11.6f, 70f, 3f);
        Text(customHead, "<color=#7DD3FC>●</color> カスタム場面<color=#94A3B8><size=75%>　PC のシナリオエディタで作った場面</size></color>",
            1.45f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        var slots = new List<TitleOptionToggle>();
        for (var n = 0; n < 12; n++)
        {
            var t = Clone(template, customView, "Custom_" + n);
            Option(t, TitleOptionKind.CustomScenario, n, 0f);
            ScenarioTile(t, -51.5f + (n % 3) * 30.5f, 6.8f - (n / 3) * 5.8f, "", Accent);
            t.gameObject.SetActive(false);
            slots.Add(t.GetComponent<TitleOptionToggle>());
        }
        var empty = customView.Find("Empty")?.GetComponent<TMP_Text>()
            ?? NewText(customView, "Empty", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextSub, TextAlignmentOptions.Center);
        Place(empty.transform, -21f, -1f, 84f, 12f);
        Text(empty.transform, "カスタム場面がありません。", 1.5f, TextSub, TextAlignmentOptions.Center);
        empty.textWrappingMode = TextWrappingModes.Normal;
        var pageText = customView.Find("Page")?.GetComponent<TMP_Text>()
            ?? NewText(customView, "Page", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextSub, TextAlignmentOptions.Center);
        Place(pageText.transform, 17f, 11.6f, 6f, 3f);
        Text(pageText.transform, "1 / 1", 1.2f, TextSub, TextAlignmentOptions.Center);
        var reset0 = page.Find("Button_Reset");
        var previous = PlainButton(reset0, customView, "Button_PagePrevious", 11.5f, 11.6f, 4.6f, 4.6f, "◀");
        var next = PlainButton(reset0, customView, "Button_PageNext", 22.5f, 11.6f, 4.6f, 4.6f, "▶");

        // right column: tabs, RANDOM (built-in) or refresh (custom), then the detail panel
        var builtinTab = PlainButton(reset0, evt, "Tab_Builtin", 36.2f, 11.6f, 19.4f, 4.6f, "組み込み（10）");
        var customTab = PlainButton(reset0, evt, "Tab_Custom", 55.8f, 11.6f, 19.4f, 4.6f, "カスタム（0）");
        var random = Clone(template, evt, "Event_Random");
        Option(random, TitleOptionKind.EventNumber, SceneRoute.RandomScenarioId, 0f);
        Tile(random, 46f, 6.4f, 40f, 4.6f, "ランダム / Random", 1.6f);
        var refresh = PlainButton(reset0, evt, "Button_RefreshCustom", 46f, 6.4f, 40f, 4.6f, "読み直す / Reload（Quest 内のファイル）");
        refresh.gameObject.SetActive(false);
        customView.gameObject.SetActive(false);

        // detail of the selected scenario (the number readout is bound to GameDirector_Title: keep it)
        var readout = evt.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Value_EventNumber").transform;
        readout.SetParent(evt, false);
        Remove(evt, "ScenarioDetail");
        var detail = NewBox(evt, "ScenarioDetail", 46f, -4.8f, 40f, 16.6f, FieldColor);
        var caption = NewText(detail, "Caption", fontSource, 0f, 6.6f, 36f, 2.2f, "選択中の場面 / Selected", 1.1f, TextSub, TextAlignmentOptions.Left);
        readout.SetParent(detail, false);
        Place(readout, -12.6f, 2.8f, 10f, 5.4f);
        Text(readout, null, 4.2f, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        var detailTitle = NewText(detail, "Title", fontSource, 4.4f, 3.6f, 27f, 3f, "", 1.8f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        var detailEn = NewText(detail, "English", fontSource, 4.4f, 1.1f, 27f, 2.2f, "", 1.1f, TextSub, TextAlignmentOptions.Left);
        var settingChip = NewBox(detail, "SettingChip", -6.8f, -1.9f, 22f, 2.6f, Accent);
        Style(settingChip.GetComponent<Image>(), Accent, 20f);
        var settingText = NewText(settingChip, "Text", fontSource, 0f, 0f, 21f, 2.6f, "", 1.1f, Dark, TextAlignmentOptions.Center, FontStyles.Bold);
        var goal = NewText(detail, "Goal", fontSource, 0f, -5.6f, 36f, 4.6f, "", 1.2f, TextMain, TextAlignmentOptions.TopLeft);
        goal.textWrappingMode = TextWrappingModes.Normal;
        var detailComponent = detail.gameObject.AddComponent<TitleScenarioDetail>();
        detailComponent.EditorConfigure(director, definitions, detailTitle, detailEn, goal, settingText, settingChip.GetComponent<Image>());
        caption.raycastTarget = false;

        var customList = evt.GetComponent<TitleCustomScenarios>() ?? evt.gameObject.AddComponent<TitleCustomScenarios>();
        customList.EditorConfigure(director, builtinView.gameObject, customView.gameObject, random.gameObject,
            builtinTab, customTab, customTab.transform.Find("Label").GetComponent<TMP_Text>(), slots.ToArray(),
            empty, pageText, previous, next, refresh);
        EditorUtility.SetDirty(customList);

        // unpleasant tones: none + 3000..17000 Hz, two rows of eight
        var sel = tone.Find("Value_SelectedSound");
        Place(sel, 14f, 5.8f, 24, 3.4f);
        Text(sel, null, 1.8f, Accent, TextAlignmentOptions.Right, FontStyles.Bold);
        Command(tone.Find("Button_PreviewSound"), 38.5f, 5.8f, 13, 3.8f, "試聴 / Play");
        var none = tone.Find("Button_Hz_None") ?? Clone(template, tone, "Button_Hz_None");
        Option(none, TitleOptionKind.Hz, 0, 0f);
        var hzTiles = new List<Transform> { none };
        for (var hz = 3000; hz <= 17000; hz += 1000)
            hzTiles.Add(tone.Find("Button_Hz_" + hz));
        for (var i = 0; i < hzTiles.Count; i++)
        {
            var t = hzTiles[i];
            if (t.Find("Label") == null)
            {
                var label = UnityEngine.Object.Instantiate(template.Find("Label").gameObject, t, false);
                label.name = "Label";
            }
            var toggle = t.GetComponent<Toggle>();
            toggle.group = null;
            var text = i == 0 ? "なし / None" : $"{(i + 2) * 1000}<size=70%> Hz</size>";
            Tile(t, -39.4f + (i % 8) * 11.25f, i < 8 ? 0.4f : -5.4f, 10.6f, 5f, text, 1.4f);
        }
        var toneGroup = tone.GetComponent<ToggleGroup>();
        if (toneGroup) UnityEngine.Object.DestroyImmediate(toneGroup);

        // ---------------------------------------------------------------- actions
        var start = page.Find("Button_Start");
        Command(start, 47f, -31.5f, 40, 10, "開始 / Start");
        start.Find("Label").GetComponent<TMP_Text>().fontSize = 2.8f;
        Primary(start, new Color32(245, 158, 11, 255), new Color32(251, 191, 36, 255), new Color32(217, 119, 6, 255), Dark);
        var reset = page.Find("Button_Reset") ?? Clone(start, page, "Button_Reset");
        reset.GetComponent<TitleCommandButton>().EditorConfigure(director, TitleCommand.ResetDefaults);
        Command(reset, 47f, -40.5f, 40, 5.4f, "リセット / Reset");
        var resetStyle = reset.GetComponent<TitleButtonStyle>();
        if (resetStyle) UnityEngine.Object.DestroyImmediate(resetStyle);
        Primary(reset, TitleButtonFeedback.NormalColor, TitleButtonFeedback.HoverColor, TitleButtonFeedback.PressedColor, TextMain);

        // drawing order: cards first, buttons on top
        start.SetAsLastSibling();
        reset.SetAsLastSibling();

        // readouts & selection state as the participant will see them
        director.Environment = SceneRoute.DefaultEnvironment;
        EditorUtility.SetDirty(director);
        foreach (var option in canvas.GetComponentsInChildren<TitleOptionToggle>(true))
        {
            option.RefreshFromModel();
            EditorUtility.SetDirty(option);
            EditorUtility.SetDirty(option.GetComponent<Toggle>());
        }
        detailComponent.Refresh();
        readout.GetComponent<TMP_Text>().text = director.EventNumber == SceneRoute.RandomScenarioId
            ? string.Empty : (director.EventNumber + 1).ToString("00");
        BakeGlyphs(canvas, definitions);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TitleMenu] rebuilt: " + canvas.GetComponentsInChildren<Selectable>(true).Length + " controls");
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>An empty RectTransform covering the card (a switchable view).</summary>
    static RectTransform FullRect(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void ScenarioTile(Transform t, float x, float y, string label, Color accent)
    {
        Tile(t, x, y, 29f, 5.2f, label, 1.35f);
        var labelText = t.Find("Label").GetComponent<TMP_Text>();
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.margin = new Vector4(1.8f, 0f, 0.6f, 0f);
        labelText.lineSpacing = -12f;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.textWrappingMode = TextWrappingModes.Normal;
        AccentBar(t, accent);
    }

    /// <summary>A plain button cloned from a command button (no command, keeps hover/click feedback).</summary>
    static Button PlainButton(Transform template, Transform parent, string name, float x, float y, float w, float h, string label)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            t = UnityEngine.Object.Instantiate(template.gameObject, parent, false).transform;
            t.name = name;
        }
        foreach (var c in t.GetComponents<TitleCommandButton>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in t.GetComponents<TitleButtonStyle>()) UnityEngine.Object.DestroyImmediate(c);
        var button = t.GetComponent<Button>();
        for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
        Command(t, x, y, w, h, label);
        t.Find("Label").GetComponent<TMP_Text>().fontSize = h < 5f ? 1.35f : 1.8f;
        t.gameObject.SetActive(true);
        return button;
    }

    static void AccentBar(Transform tile, Color color)
    {
        var bar = tile.Find("Accent") ?? Ensure(tile, "Accent", null, typeof(Image));
        var rt = (RectTransform)bar;
        rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0.6f, ((RectTransform)tile).sizeDelta.y - 1.6f);
        rt.localScale = Vector3.one;
        var image = bar.GetComponent<Image>();
        Style(image, color, 40f);
        image.raycastTarget = false;
        bar.SetAsFirstSibling();
    }

    static RectTransform NewBox(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Place(go.transform, x, y, w, h);
        var image = go.GetComponent<Image>();
        Style(image, color, 8f);
        image.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    static TMP_Text NewText(Transform parent, string name, TMP_Text fontSource, float x, float y, float w, float h, string value,
        float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Place(go.transform, x, y, w, h);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = fontSource.font;
        text.fontSharedMaterial = fontSource.fontSharedMaterial;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        Text(go.transform, value, size, color, align, style);
        return text;
    }

    // ------------------------------------------------------------------ helpers
    static Transform Card(Transform page, string name, float x, float y, float w, float h, string heading)
    {
        var card = page.Find(name);
        Place(card, x, y, w, h);
        Style(card.GetComponent<Image>(), CardColor, 5f);
        var o = card.GetComponent<Outline>();
        if (o) { o.effectColor = CardEdge; o.effectDistance = new Vector2(0.15f, -0.15f); }
        var head = card.Find("Header");
        if (head) head.gameObject.SetActive(false);
        var t = card.Find("Title");
        // cards with a readout on the right keep that space free
        var reserve = name == "Card_SoundQuickSelect" ? 40f : 0f;
        t.gameObject.SetActive(!string.IsNullOrEmpty(heading));
        Place(t, -reserve / 2, h / 2 - 2.7f, w - 3.2f - reserve, 3.4f);
        Text(t, heading, 1.9f, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        return card;
    }

    static void RowLabel(Transform t, float x, float y)
    {
        Place(t, x, y, 11, 4.6f);
        Text(t, null, 1.4f, TextMain, TextAlignmentOptions.Left);
    }

    static void Tile(Transform t, float x, float y, float w, float h, string label, float fontSize = 0f)
    {
        Place(t, x, y, w, h);
        Style(t.GetComponent<Image>(), Color.white, 10f);
        var l = t.Find("Label");
        if (l != null)
            Text(l, label, fontSize > 0f ? fontSize : (h >= 6f ? 1.6f : 1.35f), TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        var selectable = t.GetComponent<Selectable>();
        TitleButtonFeedback.ConfigureSelectable(selectable);
        EditorUtility.SetDirty(selectable);
        var o = t.GetComponent<Outline>();
        if (o) { o.effectColor = Color.white; o.effectDistance = new Vector2(0.18f, -0.18f); }
    }

    static void Command(Transform t, float x, float y, float w, float h, string label)
    {
        Place(t, x, y, w, h);
        Style(t.GetComponent<Image>(), Color.white, 10f);
        var l = t.Find("Label");
        Text(l, label, h >= 6f ? 2.1f : 1.8f, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        var selectable = t.GetComponent<Selectable>();
        TitleButtonFeedback.ConfigureSelectable(selectable);
        EditorUtility.SetDirty(selectable);
    }

    static void Primary(Transform t, Color normal, Color hover, Color pressed, Color textColor)
    {
        var style = t.GetComponent<TitleButtonStyle>() ?? t.gameObject.AddComponent<TitleButtonStyle>();
        style.normal = normal; style.hover = hover; style.pressed = pressed;
        if (normal == TitleButtonFeedback.NormalColor)
            UnityEngine.Object.DestroyImmediate(style);
        TitleButtonFeedback.ConfigureSelectable(t.GetComponent<Selectable>());
        t.Find("Label").GetComponent<TMP_Text>().color = textColor;
        EditorUtility.SetDirty(t.gameObject);
    }

    static void Option(Transform t, TitleOptionKind kind, int value, float f)
    {
        var option = t.GetComponent<TitleOptionToggle>();
        option.EditorConfigure(director, kind, value, f);
        option.EditorBindOutline(t.GetComponent<Outline>());
        var toggle = t.GetComponent<Toggle>();
        toggle.group = null;
        for (var i = toggle.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(toggle.onValueChanged, i);
    }

    static Transform Clone(Transform template, Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        go.name = name;
        return go.transform;
    }

    static Transform Ensure(Transform parent, string name, Transform template, Type component = null)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        if (template != null) return Clone(template, parent, name);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), component);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Remove(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
    }

    static void Place(Transform t, float x, float y, float w, float h)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
    }

    static void Style(Image image, Color color, float cornerMultiplier)
    {
        if (image == null) return;
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = cornerMultiplier;
        image.color = color;
        EditorUtility.SetDirty(image);
    }

    static void Text(Transform t, string value, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var text = t.GetComponent<TMP_Text>();
        if (value != null) text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.fontStyle = style;
        text.richText = true;
        text.margin = Vector4.zero;
        var rt = (RectTransform)t;
        if (t.parent.GetComponent<Selectable>() != null && t.name == "Label")
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        }
        EditorUtility.SetDirty(text);
    }

    /// <summary>Adds any missing glyphs to the static title font (same approach as AccidentExperienceValidation).</summary>
    static void BakeGlyphs(Transform canvas, IEnumerable<ScenarioDefinitionAsset> definitions)
    {
        var texts = canvas.GetComponentsInChildren<TMP_Text>(true);
        var font = texts.First().font;
        var all = string.Concat(texts.Select(t => t.text))
            + string.Concat(definitions.Select(d => d.shortTitle + d.shortTitleEn + d.learningGoal))
            + "ランダムRandom scenario10の場面から1つを選びます。どの場面かは始まるまでわかりません。横断歩道・交差点道路の途中自転車?0123456789"
            + System.IO.File.ReadAllText("Assets/_Project/Scripts/TitleCustomScenarios.cs")
            + System.IO.File.ReadAllText("Assets/_Project/Scripts/TitleScenarioDetail.cs");
        all = System.Text.RegularExpressions.Regex.Replace(all, "<[^>]+>", "");
        var missing = new string(all.Where(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c)).Distinct().ToArray());
        if (missing.Length == 0) return;
        var mode = font.atlasPopulationMode;
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        var so = new SerializedObject(font);
        if (so.FindProperty("m_SourceFontFile").objectReferenceValue == null)
        {
            so.FindProperty("m_SourceFontFile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/UI/Fonts/NotoSansJP-SemiBold.ttf");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        try
        {
            if (!font.TryAddCharacters(missing, out var failed))
                Debug.LogWarning("[TitleMenu] glyphs not available in the source font: " + failed);
        }
        finally { font.atlasPopulationMode = mode; }
        EditorUtility.SetDirty(font);
        foreach (var atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
    }
}
