using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Feedback page after the accident replay: what happened in this scenario, the safety point
/// to remember, and how the participant actually behaved (from the recorded replay).
/// Owns the single return-to-title path and the same-conditions retry.
/// </summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class AccidentResultPresenter : MonoBehaviour
{
    const float ResultViewingDistance = 1.25f;
    const float ResultPanelScale = 0.8f;

    [SerializeField] GameObject[] scenarioPanels;
    [SerializeField] GameObject instructionRoot;
    [SerializeField] Text instructionText;
    [SerializeField] Button returnButton;
    [SerializeField] GameDirector director;
    [SerializeField] GameplayFlowController flow;
    [SerializeField] ScenarioRuntime scenarios;
    [SerializeField] TMP_FontAsset runtimeFont;
    TMP_Text runtimeTitle;
    TMP_Text runtimeSummary;
    Canvas readableCanvas;
    Canvas blackoutCanvas;
    RectTransform readablePanel;
    Button readableReturnButton;

    public TMP_FontAsset RuntimeFont => runtimeFont;
    TMP_Text runtimeMetrics;
    TMP_Text runtimeSituation;
    TMP_Text runtimeAdvice;
    TMP_Text runtimeVerdict;
    TMP_Text runtimeHeading;
    TMP_Text runtimeChip;
    TMP_Text storyHeading;
    TMP_Text adviceHeading;
    Image accentStrip;
    Image chipFill;
    Image verdictFill;
    float impactSpeedKmh = -1f;
    AccidentReplayAnalysis evaluation;
    bool hasEvaluation;
    CrossingAnalysis crossing;
    bool success;
    float shownAt;

    /// <summary>True when the page shows a safe arrival instead of an accident.</summary>
    public bool IsSuccess => success;

    /// <summary>Contact speed measured before the traffic freeze (the reliable number).</summary>
    public void SetImpactReport(AccidentImpactPhysics impact)
    {
        impactSpeedKmh = impact.ImpactSpeedMetersPerSecond * 3.6f;
    }

    public void SetEvaluation(AccidentReplayAnalysis analysis)
    {
        evaluation = analysis;
        hasEvaluation = true;
    }

    public void RetryScenario()
    {
        if (returning || director == null) return;
        returning = true;
        visible = false;
        // a retry is a new trial: save this one first
        var csv = director.GetComponent<CSVPrinter>();
        if (csv != null)
            csv.CSVPrint();
        ScenarioRetry.Reload(director);
    }

    Camera isolatedCamera;
    int savedCameraMask;

    bool visible;
    bool returning;
    int eventNumber;
    int dieFlashNumber;
    int height;
    int weight;
    int gender;
    int age;
    int license;
    float hz;
    int smartPhone;
    int incident;
    int weather;
    int skyTime;

    private void Awake()
    {
        Hide();
        if (returnButton != null)
        {
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(ReturnToMenu);
        }
    }

    private void Update()
    {
        // ignore the press that skipped the replay on the previous page
        if (visible && Time.unscaledTime - shownAt > 0.6f && OpenXRInput.PrimaryButtonDown)
            ReturnToMenu();
    }

    private void LateUpdate()
    {
        if (visible)
        {
            AnchorReadableCanvas();
            OpenXRScene.SetControllerVisualsVisible(false);
        }
    }

    /// <summary>Feedback after an accident (replay has already been shown).</summary>
    public void Show(int scenarioId)
    {
        success = false;
        Present(scenarioId);
    }

    /// <summary>Feedback after reaching the goal safely: what the participant checked on the way.</summary>
    public void ShowSuccess(int scenarioId, CrossingAnalysis analysis)
    {
        success = true;
        crossing = analysis;
        Present(scenarioId);
    }

    void Present(int scenarioId)
    {
        if (returning)
            return;

        Time.timeScale = 1f;
        visible = true;
        shownAt = Time.unscaledTime;
        MetaEditorSimulationController.ShowCursor();
        for (var index = 0; scenarioPanels != null && index < scenarioPanels.Length; index++)
            if (scenarioPanels[index] != null)
                scenarioPanels[index].SetActive(false);

        // The return button already carries an explicit label. The old Count
        // object occupied the same space as the explanation and made it unreadable.
        if (instructionRoot != null)
            instructionRoot.SetActive(false);
        if (instructionText != null)
            instructionText.text = "A / X またはボタンでメニューに戻る";
        if (returnButton != null)
            returnButton.gameObject.SetActive(false);

        EnsureRuntimeExplanation();
        if (readableCanvas == null)
        {
            Debug.LogError("The accident result canvas could not be created.", this);
            return;
        }
        AnchorReadableCanvas();
        if (isolatedCamera == null && OpenXRScene.MainCamera != null)
        {
            isolatedCamera = OpenXRScene.MainCamera;
            savedCameraMask = isolatedCamera.cullingMask;
            isolatedCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        }
        if (blackoutCanvas != null)
            blackoutCanvas.gameObject.SetActive(true);
        readableCanvas.gameObject.SetActive(true);

        var definition = scenarios != null ? scenarios.Active : null;
        var numberLabel = definition != null ? definition.NumberLabel : (scenarioId + 1).ToString("00");
        var bicycle = definition != null && definition.PlayerMode == ScenarioPlayerMode.Bicycle;
        var accent = success ? UiKit.Mint : UiKit.Coral;
        accentStrip.color = accent;
        chipFill.color = accent;
        verdictFill.color = new Color(accent.r, accent.g, accent.b, 0.14f);
        runtimeVerdict.color = Color.Lerp(accent, Color.white, 0.35f);
        runtimeChip.text = definition != null && definition.IsCustom ? "カスタム" : $"シナリオ {numberLabel}";
        runtimeHeading.text = success
            ? (bicycle ? "ゴール！ 安全に走れました" : "ゴール！ 安全に渡れました")
            : "事故が起きました / What happened";
        runtimeHeading.color = accent;
        runtimeTitle.text = definition == null
            ? $"シナリオ {numberLabel}"
            : definition.IsCustom
                ? definition.DisplayName
                : $"<color={UiKit.Hex(UiKit.Muted)}>{numberLabel}</color>  {definition.DisplayName}";

        var summary = definition != null && !string.IsNullOrWhiteSpace(definition.EventSummary)
            ? definition.EventSummary
            : "事故の状況：車両との接触が発生しました。\n安全確認のポイント：周囲をよく確認してから渡りましょう。";
        SplitSummary(summary, out var situation, out var advice);
        runtimeSummary.text = summary;
        if (success)
        {
            storyHeading.text = "このシナリオで学ぶこと";
            runtimeSituation.text = definition != null && !string.IsNullOrWhiteSpace(definition.LearningGoal)
                ? definition.LearningGoal
                : advice;
            adviceHeading.text = "安全確認のポイント";
            runtimeAdvice.text = advice;
            runtimeMetrics.text = BuildSuccessMetrics();
            runtimeVerdict.text = BuildSuccessVerdict(bicycle);
        }
        else
        {
            storyHeading.text = "何が起きたか";
            runtimeSituation.text = situation;
            adviceHeading.text = "安全確認のポイント";
            runtimeAdvice.text = advice;
            runtimeMetrics.text = BuildMetrics();
            runtimeVerdict.text = BuildVerdict();
        }
        if (readableReturnButton != null)
            readableReturnButton.Select();

        var listener = OpenXRScene.MainCamera != null
            ? OpenXRScene.MainCamera.GetComponent<AudioListener>()
            : null;
        if (listener != null)
            listener.enabled = true;
        OpenXRScene.SetControllersVisible(true);
        OpenXRScene.SetControllerVisualsVisible(false);
    }

    /// <summary>Scenario summaries are written as "事故の状況：…\n安全確認のポイント：…".</summary>
    static void SplitSummary(string summary, out string situation, out string advice)
    {
        situation = summary;
        advice = string.Empty;
        foreach (var raw in summary.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("事故の状況："))
                situation = line.Substring("事故の状況：".Length);
            else if (line.StartsWith("安全確認のポイント："))
                advice = line.Substring("安全確認のポイント：".Length);
        }
    }

    static string Row(string label, string value, Color status) =>
        $"<color={UiKit.Hex(status)}>●</color>  {label}<pos=64%>{value}\n";

    string BuildMetrics()
    {
        var carSpeed = impactSpeedKmh >= 0f ? impactSpeedKmh : hasEvaluation ? evaluation.VehicleSpeedKmh : -1f;
        var lines = new System.Text.StringBuilder();
        lines.Append(Row("車の速度（接触時）", carSpeed >= 0f ? $"<b>{carSpeed:0.0} km/h</b>" : "記録なし", UiKit.Coral));
        if (hasEvaluation && evaluation.HasVehicle)
        {
            lines.Append(Row("あなたの速度", $"<b>{evaluation.ParticipantSpeedKmh:0.0} km/h</b>", UiKit.Sky));
            var looked = evaluation.SecondsLookingAtVehicle >= 0.3f;
            lines.Append(Row("車の方を見ていた時間", $"<b>{evaluation.SecondsLookingAtVehicle:0.0} 秒</b>",
                looked ? UiKit.Mint : UiKit.Amber));
            lines.Append(Row("最後に車を見たのは", evaluation.LookedAtVehicle
                ? $"<b>接触の {evaluation.LastLookBeforeImpact:0.0} 秒前</b>"
                : $"<b><color={UiKit.Hex(UiKit.Coral)}>見ていません</color></b>",
                evaluation.LookedAtVehicle ? UiKit.Mint : UiKit.Coral));
        }
        else
        {
            lines.Append($"<color={UiKit.Hex(UiKit.Muted)}>視線の記録がないため、行動の振り返りは表示できません。</color>");
        }
        return lines.ToString().TrimEnd('\n');
    }

    string BuildVerdict()
    {
        if (!hasEvaluation || !evaluation.HasVehicle)
            return "リプレイを思い出して、どこで止まって確認すればよかったか考えてみましょう。";
        if (!evaluation.LookedAtVehicle)
            return "ぶつかった車を見ていませんでした。渡る前に、左右をしっかり確認しましょう。";
        if (evaluation.LastLookBeforeImpact > 1.5f)
            return "車は見ていましたが、確認のあとも車は近づいていました。渡る直前にもう一度確認しましょう。";
        return "車に気づいていました。気づいたときに止まれる距離と速さかを考えましょう。";
    }

    string BuildSuccessMetrics()
    {
        var lines = new System.Text.StringBuilder();
        if (!crossing.HasData)
            return $"<color={UiKit.Hex(UiKit.Muted)}>視線の記録がないため、行動の振り返りは表示できません。</color>";
        lines.Append(Row("左の確認", crossing.CheckedLeft
                ? $"<b>{crossing.SecondsLookingLeft:0.0} 秒</b>"
                : $"<b><color={UiKit.Hex(UiKit.Amber)}>少なめ</color></b>",
            crossing.CheckedLeft ? UiKit.Mint : UiKit.Amber));
        lines.Append(Row("右の確認", crossing.CheckedRight
                ? $"<b>{crossing.SecondsLookingRight:0.0} 秒</b>"
                : $"<b><color={UiKit.Hex(UiKit.Amber)}>少なめ</color></b>",
            crossing.CheckedRight ? UiKit.Mint : UiKit.Amber));
        var near = crossing.ClosestVehicleMeters;
        lines.Append(Row("いちばん近づいた車", near >= 0f && near < 40f ? $"<b>{near:0.0} m</b>" : "<b>近くに車なし</b>",
            near >= 0f && near < 3f ? UiKit.Amber : UiKit.Sky));
        lines.Append($"<size=80%><color={UiKit.Hex(UiKit.Muted)}>ゴール前 {crossing.DurationSeconds:0} 秒間の記録</color></size>");
        return lines.ToString();
    }

    string BuildSuccessVerdict(bool bicycle)
    {
        var goal = bicycle ? "安全に走れました" : "安全に渡れました";
        string text;
        if (!crossing.HasData)
            text = $"{goal}。いつも左右を確認するくせをつけましょう。";
        else if (crossing.CheckedLeft && crossing.CheckedRight)
            text = $"左右をよく確認して、{goal}。この確認をいつも続けましょう。";
        else if (crossing.CheckedLeft || crossing.CheckedRight)
            text = $"{goal}が、{(crossing.CheckedLeft ? "右" : "左")}の確認が少なめでした。両側を見るくせをつけましょう。";
        else
            text = "今回は無事でしたが、左右をほとんど見ていませんでした。次は首を回して確かめましょう。";
        if (crossing.HasData && crossing.ClosestVehicleMeters >= 0f && crossing.ClosestVehicleMeters < 3f)
            text += $"（車が {crossing.ClosestVehicleMeters:0.0} m まで近づきました）";
        return text;
    }

    void EnsureRuntimeExplanation()
    {
        EnsureBlackoutCanvas();
        if (readableCanvas != null && runtimeTitle != null && runtimeSummary != null)
            return;
        if (scenarios == null && director != null)
            scenarios = director.GetComponent<ScenarioRuntime>();
        if (scenarios == null)
            scenarios = FindFirstObjectByType<ScenarioRuntime>();

        var canvasObject = new GameObject(
            "Canvas_AccidentResult_Meta",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        readableCanvas = canvasObject.GetComponent<Canvas>();
        readableCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        readableCanvas.worldCamera = OpenXRScene.MainCamera;
        readableCanvas.planeDistance = ResolveResultPlaneDistance(OpenXRScene.MainCamera);
        readableCanvas.overrideSorting = true;
        readableCanvas.sortingOrder = 32000;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (MetaEditorSimulationController.IsActive)
        {
            MetaEditorSimulationController.ConfigureCanvas(readableCanvas);
        }
        else
        {
            var raycaster = canvasObject.AddComponent<OVRRaycaster>();
            raycaster.ignoreReversedGraphics = true;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            raycaster.blockingMask = 0;
        }

        // The full-screen canvas is rendered immediately in front of the XR
        // near plane. Only this centered panel is visible, while scene geometry
        // can no longer be drawn over it.
        var font = runtimeFont;
        var panelRect = UiKit.Box(canvasRect, "ResultPanel", UiKit.Panel, Vector2.zero, new Vector2(1400f, 820f), 1.4f);
        readablePanel = panelRect;
        panelRect.localScale = Vector3.one * ResultPanelScale;
        accentStrip = UiKit.Box(panelRect, "AccentStrip", UiKit.Coral, new Vector2(-610f, 372f), new Vector2(120f, 10f), 0.3f)
            .GetComponent<Image>();

        // header: scenario chip, page heading, scenario title
        var chip = UiKit.Box(panelRect, "Chip", UiKit.Coral, new Vector2(-575f, 320f), new Vector2(190f, 50f), 2f);
        chipFill = chip.GetComponent<Image>();
        runtimeChip = UiKit.Label(chip, "Text", font, "シナリオ 01", 26f, Vector2.zero, new Vector2(180f, 46f),
            UiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        runtimeHeading = UiKit.Label(panelRect, "Heading", font, "", 30f, new Vector2(110f, 320f),
            new Vector2(1120f, 50f), UiKit.Coral, FontStyles.Bold);
        runtimeTitle = UiKit.Label(panelRect, "Title", font, "", 44f, new Vector2(0f, 250f),
            new Vector2(1300f, 70f), UiKit.Text, FontStyles.Bold);
        runtimeTitle.enableAutoSizing = true;
        runtimeTitle.fontSizeMin = 28f;
        runtimeTitle.fontSizeMax = 44f;
        // full text kept for tooling/tests; the two cards below show it split
        runtimeSummary = UiKit.Label(panelRect, "Summary", font, "", 1f, Vector2.zero, new Vector2(10f, 10f), UiKit.Text);
        runtimeSummary.gameObject.SetActive(false);

        // left card: the story and the point to remember
        var story = UiKit.Box(panelRect, "Card_Story", UiKit.Card, new Vector2(-335f, -5f), new Vector2(630f, 400f));
        CardNumber(story, "1", font);
        storyHeading = UiKit.Label(story, "Head_Situation", font, "何が起きたか", 27f, new Vector2(35f, 160f),
            new Vector2(510f, 40f), UiKit.Sky, FontStyles.Bold);
        runtimeSituation = UiKit.Label(story, "Situation", font, "", 27f, new Vector2(0f, 70f), new Vector2(570f, 130f), UiKit.Text);
        runtimeSituation.alignment = TextAlignmentOptions.TopLeft;
        UiKit.Box(story, "Divider", UiKit.Hairline, new Vector2(0f, -8f), new Vector2(570f, 2f), 0f);
        adviceHeading = UiKit.Label(story, "Head_Advice", font, "安全確認のポイント", 27f, new Vector2(0f, -40f),
            new Vector2(570f, 40f), UiKit.Amber, FontStyles.Bold);
        runtimeAdvice = UiKit.Label(story, "Advice", font, "", 27f, new Vector2(0f, -125f), new Vector2(570f, 130f), UiKit.Text);
        runtimeAdvice.alignment = TextAlignmentOptions.TopLeft;

        // right card: how the participant behaved
        var check = UiKit.Box(panelRect, "Card_Check", UiKit.Card, new Vector2(335f, -5f), new Vector2(630f, 400f));
        CardNumber(check, "2", font);
        UiKit.Label(check, "Head_Check", font, "あなたの行動 / Your check", 27f, new Vector2(35f, 160f),
            new Vector2(510f, 40f), UiKit.Sky, FontStyles.Bold);
        runtimeMetrics = UiKit.Label(check, "Metrics", font, "", 26f, new Vector2(0f, 22f), new Vector2(570f, 200f), UiKit.Text);
        runtimeMetrics.alignment = TextAlignmentOptions.TopLeft;
        runtimeMetrics.lineSpacing = 18f;
        var verdictBox = UiKit.Box(check, "VerdictBox", new Color(1f, 1f, 1f, 0.1f), new Vector2(0f, -135f), new Vector2(590f, 110f), 0.7f);
        verdictFill = verdictBox.GetComponent<Image>();
        runtimeVerdict = UiKit.Label(verdictBox, "Verdict", font, "", 25f, Vector2.zero, new Vector2(550f, 96f),
            UiKit.Amber, FontStyles.Bold);
        runtimeVerdict.alignment = TextAlignmentOptions.MidlineLeft;

        var retry = UiKit.PillButton(panelRect, "Button_RetryScenario", font, "もう一度体験 / Try again",
            UiKit.CardRaised, UiKit.Text, new Vector2(-300f, -290f), new Vector2(560f, 92f));
        retry.onClick.AddListener(TitleButtonFeedback.PlayClickFeedback);
        retry.onClick.AddListener(RetryScenario);
        readableReturnButton = UiKit.PillButton(panelRect, "Button_ReturnToMenu", font, "タイトルへ / Title",
            UiKit.Amber, UiKit.Ink, new Vector2(300f, -290f), new Vector2(560f, 92f));
        readableReturnButton.onClick.AddListener(TitleButtonFeedback.PlayClickFeedback);
        readableReturnButton.onClick.AddListener(ReturnToMenu);

        UiKit.Label(panelRect, "Footer", font, "A / X・Enter：タイトルへ　　R：もう一度体験", 22f,
            new Vector2(0f, -372f), new Vector2(1300f, 36f), UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.Center);

        var returnNavigation = readableReturnButton.navigation;
        returnNavigation.mode = Navigation.Mode.Explicit;
        returnNavigation.selectOnLeft = retry;
        readableReturnButton.navigation = returnNavigation;
        var retryNavigation = retry.navigation;
        retryNavigation.mode = Navigation.Mode.Explicit;
        retryNavigation.selectOnRight = readableReturnButton;
        retry.navigation = retryNavigation;
        readableCanvas.gameObject.SetActive(false);
    }

    static void CardNumber(RectTransform card, string number, TMP_FontAsset font)
    {
        var dot = UiKit.Dot(card, "Number", UiKit.CardRaised, new Vector2(-265f, 160f), 44f);
        UiKit.Label(dot, "Text", font, number, 24f, Vector2.zero, new Vector2(44f, 44f), UiKit.Sky,
            FontStyles.Bold, TextAlignmentOptions.Center);
    }

    void EnsureBlackoutCanvas()
    {
        if (blackoutCanvas != null)
            return;

        var camera = OpenXRScene.MainCamera;
        if (camera == null)
            return;

        var blackoutObject = new GameObject(
            "Canvas_AccidentBlackout",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        blackoutObject.layer = LayerMask.NameToLayer("UI");
        blackoutObject.transform.SetParent(camera.transform, false);
        blackoutCanvas = blackoutObject.GetComponent<Canvas>();
        blackoutCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        blackoutCanvas.worldCamera = camera;
        blackoutCanvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.01f, 0.04f);
        blackoutCanvas.overrideSorting = true;
        blackoutCanvas.sortingOrder = 31990;

        var imageObject = new GameObject(
            "FullScreenBlack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = blackoutObject.layer;
        imageObject.transform.SetParent(blackoutObject.transform, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        blackoutCanvas.gameObject.SetActive(false);
    }

    void AnchorReadableCanvas()
    {
        var camera = OpenXRScene.MainCamera;
        if (readableCanvas == null || camera == null)
            return;

        readableCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        readableCanvas.worldCamera = camera;
        readableCanvas.planeDistance = ResolveResultPlaneDistance(camera);
        if (readableCanvas.transform.parent != camera.transform)
            readableCanvas.transform.SetParent(camera.transform, false);
        readableCanvas.transform.localPosition = Vector3.zero;
        readableCanvas.transform.localRotation = Quaternion.identity;
        readableCanvas.transform.localScale = Vector3.one;
    }

    static float ResolveResultPlaneDistance(Camera camera)
    {
        if (camera == null)
            return ResultViewingDistance;
        return Mathf.Clamp(
            ResultViewingDistance,
            camera.nearClipPlane + 0.05f,
            camera.farClipPlane - 0.1f);
    }

    public void Hide()
    {
        if (isolatedCamera != null)
            isolatedCamera.cullingMask = savedCameraMask;
        isolatedCamera = null;
        visible = false;
        if (scenarioPanels != null)
            foreach (var panel in scenarioPanels)
                if (panel != null)
                    panel.SetActive(false);
        if (instructionRoot != null)
            instructionRoot.SetActive(false);
        if (returnButton != null)
            returnButton.gameObject.SetActive(false);
        if (readableCanvas != null)
            readableCanvas.gameObject.SetActive(false);
        if (blackoutCanvas != null)
            blackoutCanvas.gameObject.SetActive(false);
    }

    public void ReturnToMenu()
    {
        if (returning)
            return;
        returning = true;
        visible = false;
        Time.timeScale = 1f;
        OpenXRInput.StopControllerVibration();
        if (flow != null)
            flow.Finish();
        if (director != null)
        {
            eventNumber = director.EventNumber;
            dieFlashNumber = director.DieFlashNumber;
            height = director.Height;
            weight = director.Weight;
            gender = director.Gender;
            age = director.Age;
            license = director.License;
            hz = director.Hz;
            smartPhone = director.SmartPhone;
            incident = director.Incident;
            weather = director.Weather;
            skyTime = director.SkyTime;
            var csv = director.GetComponent<CSVPrinter>();
            if (csv != null)
                csv.CSVPrint();
        }

        SceneManager.sceneLoaded += TransferValuesToTitle;
        SceneManager.LoadScene(SceneRoute.TitleForCurrentScene);
    }

    private void TransferValuesToTitle(Scene scene, LoadSceneMode mode)
    {
        var destination = FindFirstObjectByType<GameDirector_Title>();
        if (destination != null)
        {
            destination.EventNumber = eventNumber;
            destination.DieFlashNumber = dieFlashNumber;
            destination.Height = height;
            destination.Weight = weight;
            destination.Gender = gender;
            destination.Age = age;
            destination.License = license;
            destination.Hz = hz;
            destination.SmartPhone = smartPhone;
            destination.Incident = incident;
            destination.Weather = weather;
            destination.SkyTime = skyTime;
        }
        SceneManager.sceneLoaded -= TransferValuesToTitle;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject[] panels,
        GameObject configuredInstructionRoot,
        Text configuredInstructionText,
        Button configuredReturnButton,
        GameDirector configuredDirector,
        GameplayFlowController configuredFlow)
    {
        scenarioPanels = panels;
        instructionRoot = configuredInstructionRoot;
        instructionText = configuredInstructionText;
        returnButton = configuredReturnButton;
        director = configuredDirector;
        flow = configuredFlow;
    }
#endif
}
