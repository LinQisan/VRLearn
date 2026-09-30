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
    const string TitleScene = "Assets/_Project/Scenes/Title.unity";
    const string Root = "MenuWorldRoot/Canvas_TitleMenu";
    const float W = 120f, H = 76f;
    public const float MenuWidthMeters = 2.0f;
    public const float ViewingDistance = 1.6f;

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
        // 2.0 m wide at 1.6 m (MetaTitleSceneSetup): 1 unit ≈ 0.6°, body text 2.2 ≈ 1.3° (legible on Quest 2)
        canvas.localScale = Vector3.one * (MenuWidthMeters / W / canvas.parent.lossyScale.x);
        var setup = UnityEngine.Object.FindFirstObjectByType<MetaTitleSceneSetup>(FindObjectsInactive.Include);
        if (setup != null)
        {
            var so = new SerializedObject(setup);
            so.FindProperty("viewingDistance").floatValue = ViewingDistance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var fontSource = page.GetComponentInChildren<TMP_Text>(true);
        Transform FindDeep(string name) => page.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        // ---------------------------------------------------------------- frame + header
        Place(menu.Find("Background"), 0, 0, W, H);
        Style(menu.Find("Background").GetComponent<Image>(), Panel, 3f);
        var outline = menu.Find("Background").GetComponent<Outline>();
        if (outline) { outline.effectColor = CardEdge; outline.effectDistance = new Vector2(0.2f, -0.2f); }
        menu.Find("BackgroundInner").gameObject.SetActive(false);
        menu.Find("Header_Background").gameObject.SetActive(false);
        var header = menu.Find("Header_Content") as RectTransform;
        Place(header, 0, H / 2 - 5f, W - 6, 8);
        var brand = Ensure(header, "Accent_Bar", null, typeof(Image));
        Place(brand, -(W - 6) / 2 + 0.7f, 0f, 1.4f, 6.6f);
        Style(brand.GetComponent<Image>(), Accent, 20f);
        brand.GetComponent<Image>().raycastTarget = false;
        var title = header.Find("Title");
        Place(title, -28f, 1.3f, 54f, 4.6f);
        Text(title, "Deadly Bounce Simulator", 3.4f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        var subtitle = header.Find("Subtitle");
        Place(subtitle, -28f, -2.6f, 54f, 2.6f);
        Text(subtitle, "彦根・京橋 ・ Aoi Miyamura - Shiga Univ. Kawai Lab.", 1.7f, TextSub, TextAlignmentOptions.Left);
        foreach (var n in new[] { "Chip_Environment", "Hint_Controls" })
            header.Find(n)?.gameObject.SetActive(false);

        // ---------------------------------------------------------------- steps
        var steps = new[] { FullRect(page, "Step_1"), FullRect(page, "Step_2"), FullRect(page, "Step_3") };
        var user = FindDeep("Card_UserInformation"); user.SetParent(steps[0], false);
        var survey = FindDeep("Card_SurveyInformation"); survey.SetParent(steps[0], false);
        var cond = FindDeep("Card_ScenarioConditions"); cond.SetParent(steps[1], false);
        var tone = FindDeep("Card_SoundQuickSelect"); tone.SetParent(steps[1], false);
        var evt = FindDeep("Card_EventNumber"); evt.SetParent(steps[2], false);
        const float bodyY = 1f, bodyH = 54f;
        Card(user.parent, user.name, -29.5f, bodyY, 57, bodyH, "参加者 / Participant");
        Card(survey.parent, survey.name, 29.5f, bodyY, 57, bodyH, "調査 / Survey");
        Card(cond.parent, cond.name, -29.5f, bodyY, 57, bodyH, "場面条件 / Conditions");
        Card(tone.parent, tone.name, 29.5f, bodyY, 57, bodyH, "不快音 / Unpleasant tone");
        Card(evt.parent, evt.name, 0f, bodyY, 116, bodyH, "");

        // step tabs in the header
        var reset0 = page.Find("Button_Reset");
        var tabNames = new[] { "① 参加者", "② 条件", "③ 場面" };
        var tabs = new Button[3];
        for (var i = 0; i < 3; i++)
        {
            tabs[i] = PlainButton(reset0, header, "Tab_Step" + (i + 1), 13f + i * 16.5f, 0f, 15.5f, 6.4f, tabNames[i]);
            tabs[i].transform.Find("Label").GetComponent<TMP_Text>().fontSize = 2.3f;
        }

        // ① participant: steppers + gender
        float[] rows = { 13.5f, 4f, -5.5f, -15f };
        string[] stepped = { "Height", "Weight", "Age" };
        for (var i = 0; i < stepped.Length; i++)
        {
            var y = rows[i];
            RowLabel(user.Find("Label_" + stepped[i]), -19.5f, y, 13f);
            Command(user.Find($"Button_{stepped[i]}_Down"), -5f, y, 8f, 7.4f, "-5");
            var field = user.Find("Field_" + stepped[i]);
            Place(field, 7.5f, y, 14f, 7.4f);
            Style(field.GetComponent<Image>(), FieldColor, 10f);
            var value = field.Find("Value_" + stepped[i]);
            Place(value, -1.6f, 0, 9.5f, 7.4f);
            Text(value, null, 3.6f, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            var unit = field.Find("Unit_" + stepped[i]);
            Place(unit, 4.9f, -0.8f, 3.6f, 3.4f);
            Text(unit, null, 1.7f, TextSub, TextAlignmentOptions.Left);
            Command(user.Find($"Button_{stepped[i]}_Up"), 20f, y, 8f, 7.4f, "+5");
            foreach (var b in new[] { "Down", "Up" })
                user.Find($"Button_{stepped[i]}_{b}/Label").GetComponent<TMP_Text>().fontSize = 2.6f;
        }
        RowLabel(user.Find("Label_Gender"), -19.5f, rows[3], 13f);
        Tile(user.Find("Gender_Male"), -4.5f, rows[3], 11.8f, 7.4f, "男\n<size=80%>Male</size>", 2.1f);
        Tile(user.Find("Gender_Female"), 8f, rows[3], 11.8f, 7.4f, "女\n<size=80%>Female</size>", 2.1f);
        Tile(user.Find("Gender_Unknown"), 20.5f, rows[3], 11.8f, 7.4f, "不明\n<size=80%>Other</size>", 2.1f);

        // ① survey: two three-way groups
        foreach (var (label, y) in new[] { ("Label_License", 15.5f), ("Label_Incident", -1.5f) })
        {
            var l = survey.Find(label);
            Place(l, 0, y, 50, 3.4f);
            Text(l, null, 2.1f, TextSub, TextAlignmentOptions.Center);
        }
        foreach (var (prefix, y) in new[] { ("License", 8f), ("Incident", -9f) })
        {
            var names = prefix == "License" ? new[] { "License_Have", "License_None", "License_Unknown" } : new[] { "Incident_Yes", "Incident_No", "Incident_Unknown" };
            var labels = new[] { "有\n<size=80%>Yes</size>", "無\n<size=80%>No</size>", "不明\n<size=80%>Unknown</size>" };
            for (var i = 0; i < 3; i++)
                Tile(survey.Find(names[i]), -17f + i * 17f, y, 15.5f, 7.4f, labels[i], 2.1f);
        }

        // ② conditions: four two-way rows (the environment row is gone: Hikone only)
        foreach (var n in new[] { "Label_Scene", "Scene_First", "Scene_Second" })
            Remove(cond, n);
        string[] cRows = { "DieFlash", "Smartphone", "Weather", "Time" };
        for (var i = 0; i < cRows.Length; i++)
        {
            RowLabel(cond.Find("Label_" + cRows[i]), -16.5f, rows[i], 19f);
            Tile(cond.Find(cRows[i] + "_First"), 6.5f, rows[i], 13f, 7.4f, null, 2.1f);
            Tile(cond.Find(cRows[i] + "_Second"), 20.5f, rows[i], 13f, 7.4f, null, 2.1f);
        }

        // ② unpleasant tones: none + 3000..17000 Hz in four rows of four
        var template = FindDeep("DieFlash_First");
        var sel = tone.Find("Value_SelectedSound");
        Place(sel, 19.5f, 23.6f, 14f, 3.4f);
        Text(sel, null, 2.2f, Accent, TextAlignmentOptions.Right, FontStyles.Bold);
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
            t.GetComponent<Toggle>().group = null;
            var text = i == 0 ? "なし" : $"{(i + 2) * 1000}<size=70%> Hz</size>";
            Tile(t, -19.5f + (i % 4) * 13f, 15.5f - (i / 4) * 8.5f, 12.2f, 7.4f, text, 2.1f);
        }
        Command(tone.Find("Button_PreviewSound"), 0f, -20.5f, 51f, 6.4f, "試聴 / Play");
        tone.Find("Button_PreviewSound/Label").GetComponent<TMP_Text>().fontSize = 2.2f;
        var toneGroup = tone.GetComponent<ToggleGroup>();
        if (toneGroup) UnityEngine.Object.DestroyImmediate(toneGroup);

        // ③ scenarios
        // grouped by where they happen; each tile shows number, Japanese and English name
        var definitions = AssetDatabase.FindAssets("t:ScenarioDefinitionAsset", new[] { "Assets/_Project/ScenarioDefinitions" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ScenarioDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null).OrderBy(d => d.id).ToArray();
        foreach (var n in new[] { "Button_EventDown", "Button_EventUp", "Field_EventNumber" })
            Remove(evt, n);
        // two views share the card's left area: built-in groups, and the custom JSON scenarios
        var builtinView = FullRect(evt, "View_Builtin");
        foreach (var child in evt.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Group_") || (child.name.StartsWith("Event_") && child.name != "Event_Random"))
                child.SetParent(builtinView, false);
        var groups = new[] { ScenarioSetting.Crossing, ScenarioSetting.MidBlock, ScenarioSetting.Bicycle };
        float ColumnX(int c) => -44f + c * 27f;
        float RowY(int r) => 15f - r * 9.2f;
        for (var g = 0; g < groups.Length; g++)
        {
            var x = ColumnX(g);
            var head = builtinView.Find("Group_" + groups[g])
                ?? NewText(builtinView, "Group_" + groups[g], fontSource, 0f, 0f, 1f, 1f, "", 1f, TextMain, TextAlignmentOptions.Left).transform;
            Place(head, x + 1f, 22.6f, 25, 3.4f);
            var count = definitions.Count(d => d.setting == groups[g]);
            Text(head, $"<color={Hex(TitleScenarioDetail.SettingColor(groups[g]))}>●</color> {TitleScenarioDetail.SettingLabel(groups[g])}"
                + $"<color={Hex(TextSub)}><size=75%>  {count}</size></color>", 2.2f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
            var i = 0;
            foreach (var d in definitions.Where(d => d.setting == groups[g]))
            {
                var t = Clone(template, builtinView, "Event_" + d.id);
                Option(t, TitleOptionKind.EventNumber, d.id, 0f);
                var label = $"<alpha=#99>{d.id + 1:00}<alpha=#FF> {d.shortTitle}\n<size=72%><alpha=#B3>{d.shortTitleEn}</size>";
                ScenarioTile(t, x, RowY(i), label, TitleScenarioDetail.SettingColor(d.setting));
                i++;
            }
        }

        var customView = FullRect(evt, "View_Custom");
        var customHead = customView.Find("Heading")
            ?? NewText(customView, "Heading", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextMain, TextAlignmentOptions.Left).transform;
        Place(customHead, -30f, 22.6f, 54f, 3.4f);
        Text(customHead, "<color=#7DD3FC>●</color> カスタム場面<color=#94A3B8><size=75%>　PC のシナリオエディタで作成</size></color>",
            2.2f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        var slots = new List<TitleOptionToggle>();
        for (var n = 0; n < 12; n++)
        {
            var t = Clone(template, customView, "Custom_" + n);
            Option(t, TitleOptionKind.CustomScenario, n, 0f);
            ScenarioTile(t, ColumnX(n % 3), RowY(n / 3), "", Accent);
            t.gameObject.SetActive(false);
            slots.Add(t.GetComponent<TitleOptionToggle>());
        }
        var empty = customView.Find("Empty")?.GetComponent<TMP_Text>()
            ?? NewText(customView, "Empty", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextSub, TextAlignmentOptions.Center);
        Place(empty.transform, -17.5f, 0f, 76f, 20f);
        Text(empty.transform, "カスタム場面がありません。", 2.1f, TextSub, TextAlignmentOptions.Center);
        empty.textWrappingMode = TextWrappingModes.Normal;
        var pageText = customView.Find("Page")?.GetComponent<TMP_Text>()
            ?? NewText(customView, "Page", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextSub, TextAlignmentOptions.Center);
        Place(pageText.transform, 11.5f, 22.6f, 8f, 3.4f);
        Text(pageText.transform, "1 / 1", 1.9f, TextSub, TextAlignmentOptions.Center);
        var previous = PlainButton(reset0, customView, "Button_PagePrevious", 4f, 22.6f, 6.2f, 5.6f, "◀");
        var next = PlainButton(reset0, customView, "Button_PageNext", 19f, 22.6f, 6.2f, 5.6f, "▶");

        // right column: tabs, RANDOM (built-in) or reload (custom), then the detail panel
        const float detailX = 42f, detailW = 30f;
        var builtinTab = PlainButton(reset0, evt, "Tab_Builtin", detailX - 7.6f, 22.6f, 14.8f, 6f, "組み込み（10）");
        var customTab = PlainButton(reset0, evt, "Tab_Custom", detailX + 7.6f, 22.6f, 14.8f, 6f, "カスタム（0）");
        foreach (var tab in new[] { builtinTab, customTab })
            tab.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 1.8f;
        var random = Clone(template, evt, "Event_Random");
        Option(random, TitleOptionKind.EventNumber, SceneRoute.RandomScenarioId, 0f);
        Tile(random, detailX, 14.8f, detailW, 7f, "ランダム / Random", 2.2f);
        var refresh = PlainButton(reset0, evt, "Button_RefreshCustom", detailX, 14.8f, detailW, 7f, "読み直す / Reload");
        refresh.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 2.2f;
        refresh.gameObject.SetActive(false);
        customView.gameObject.SetActive(false);

        // detail of the selected scenario (the number readout is bound to GameDirector_Title: keep it)
        var readout = evt.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Value_EventNumber").transform;
        readout.SetParent(evt, false);
        Remove(evt, "ScenarioDetail");
        var detail = NewBox(evt, "ScenarioDetail", detailX, -7f, detailW, 34f, FieldColor);
        var caption = NewText(detail, "Caption", fontSource, 0f, 14.6f, 27f, 2.6f, "選択中の場面 / Selected", 1.6f, TextSub, TextAlignmentOptions.Left);
        readout.SetParent(detail, false);
        Place(readout, 0f, 10.4f, 27f, 5f);
        Text(readout, null, 4f, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        var detailTitle = NewText(detail, "Title", fontSource, 0f, 4.6f, 27f, 6.4f, "", 2.4f, TextMain, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        detailTitle.textWrappingMode = TextWrappingModes.Normal;
        var detailEn = NewText(detail, "English", fontSource, 0f, 0.2f, 27f, 2.6f, "", 1.6f, TextSub, TextAlignmentOptions.Left);
        var settingChip = NewBox(detail, "SettingChip", 0f, -3.4f, 27f, 3.4f, Accent);
        Style(settingChip.GetComponent<Image>(), Accent, 20f);
        var settingText = NewText(settingChip, "Text", fontSource, 0f, 0f, 26f, 3.4f, "", 1.7f, Dark, TextAlignmentOptions.Center, FontStyles.Bold);
        var goal = NewText(detail, "Goal", fontSource, 0f, -11.4f, 27f, 11f, "", 1.9f, TextMain, TextAlignmentOptions.TopLeft);
        goal.textWrappingMode = TextWrappingModes.Normal;
        var detailComponent = detail.gameObject.AddComponent<TitleScenarioDetail>();
        detailComponent.EditorConfigure(director, definitions, detailTitle, detailEn, goal, settingText, settingChip.GetComponent<Image>());
        caption.raycastTarget = false;

        var customList = evt.GetComponent<TitleCustomScenarios>() ?? evt.gameObject.AddComponent<TitleCustomScenarios>();
        customList.EditorConfigure(director, builtinView.gameObject, customView.gameObject, random.gameObject,
            builtinTab, customTab, customTab.transform.Find("Label").GetComponent<TMP_Text>(), slots.ToArray(),
            empty, pageText, previous, next, refresh);
        EditorUtility.SetDirty(customList);

        // ---------------------------------------------------------------- footer: summary, reset, start
        var summary = page.Find("Footer_Summary")?.GetComponent<TMP_Text>()
            ?? NewText(page, "Footer_Summary", fontSource, 0f, 0f, 1f, 1f, "", 1f, TextMain, TextAlignmentOptions.Left);
        Place(summary.transform, -26f, -H / 2 + 5.2f, 64f, 5f);
        Text(summary.transform, "場面 ランダム　不快音 なし", 2.0f, TextMain, TextAlignmentOptions.Left);
        var start = page.Find("Button_Start");
        Command(start, 43.5f, -H / 2 + 5.2f, 27f, 7.6f, "開始 / Start");
        start.Find("Label").GetComponent<TMP_Text>().fontSize = 3.2f;
        Primary(start, new Color32(245, 158, 11, 255), new Color32(251, 191, 36, 255), new Color32(217, 119, 6, 255), Dark);
        var reset = page.Find("Button_Reset") ?? Clone(start, page, "Button_Reset");
        reset.GetComponent<TitleCommandButton>().EditorConfigure(director, TitleCommand.ResetDefaults);
        Command(reset, 18f, -H / 2 + 5.2f, 20f, 6.6f, "リセット / Reset");
        reset.Find("Label").GetComponent<TMP_Text>().fontSize = 2.1f;
        var resetStyle = reset.GetComponent<TitleButtonStyle>();
        if (resetStyle) UnityEngine.Object.DestroyImmediate(resetStyle);
        Primary(reset, TitleButtonFeedback.NormalColor, TitleButtonFeedback.HoverColor, TitleButtonFeedback.PressedColor, TextMain);
        start.SetAsLastSibling();
        reset.SetAsLastSibling();

        var pagesComponent = page.GetComponent<TitleMenuPages>() ?? page.gameObject.AddComponent<TitleMenuPages>();
        pagesComponent.EditorConfigure(director, steps.Select(t => t.gameObject).ToArray(), tabs, summary);
        EditorUtility.SetDirty(pagesComponent);
        for (var i = 0; i < steps.Length; i++)
            steps[i].gameObject.SetActive(i == 0);

        // readouts & selection state as the participant will see them
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
        Tile(t, x, y, 26f, 8.2f, label, 2.1f);
        var labelText = t.Find("Label").GetComponent<TMP_Text>();
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.margin = new Vector4(1.8f, 0f, 0.6f, 0f);
        labelText.lineSpacing = -8f;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        // long names shrink a little rather than being cut off
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 1.7f;
        labelText.fontSizeMax = 2.1f;
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
        var reserve = name == "Card_SoundQuickSelect" ? 15f : 0f;
        t.gameObject.SetActive(!string.IsNullOrEmpty(heading));
        Place(t, -reserve / 2, h / 2 - 3.4f, w - 4f - reserve, 4f);
        Text(t, heading, 2.6f, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        return card;
    }

    static void RowLabel(Transform t, float x, float y, float width)
    {
        Place(t, x, y, width, 7f);
        Text(t, null, 2.1f, TextMain, TextAlignmentOptions.Left);
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
            + AccidentExperienceValidation.ReadScript("TitleCustomScenarios")
            + AccidentExperienceValidation.ReadScript("TitleMenuPages")
            + "①②③参加者条件場面組み込みカスタム読み直す"
            + AccidentExperienceValidation.ReadScript("TitleScenarioDetail");
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
