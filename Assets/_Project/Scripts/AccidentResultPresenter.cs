using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Presents the selected accident explanation and owns the single return-to-title path.
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
    string impactReport;
    TMP_Text runtimeMetrics;

    public void SetImpactReport(AccidentImpactPhysics impact, float recordedSeconds)
    {
        impactReport = $"接触時の車速 {impact.ImpactSpeedMetersPerSecond * 3.6f:0.0} km/h   •   記録 {recordedSeconds:0.0} 秒\n軌跡と接触位置を振り返り、同じ条件でもう一度体験できます。";
    }

    public void RetryScenario()
    {
        if (returning || director == null) return;
        returning = true;
        visible = false;
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
        if (visible && OpenXRInput.PrimaryButtonDown)
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

    public void Show(int scenarioId)
    {
        if (returning)
            return;

        Time.timeScale = 1f;
        visible = true;
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
        var definition = scenarios != null ? scenarios.Active?.asset : null;
        if (runtimeTitle != null)
            runtimeTitle.text = definition != null
                ? $"事故シナリオ {definition.id + 1}：{definition.displayName}"
                : $"事故シナリオ {scenarioId + 1}";
        if (runtimeSummary != null)
            runtimeSummary.text = definition != null && !string.IsNullOrWhiteSpace(definition.eventSummary)
                ? definition.eventSummary
                : "車両との接触が発生しました。周囲確認と安全な横断判断を振り返ってください。";
        if (runtimeMetrics != null)
            runtimeMetrics.text = impactReport ?? "接触速度の記録はありません。周囲の確認と進入のタイミングを振り返りましょう。";
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
        var panelRect = CreateImage(canvasRect, "ResultPanel", new Color32(16, 34, 50, 250));
        readablePanel = panelRect;
        panelRect.GetComponent<Image>().raycastTarget = false;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1040f, 660f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.localScale = Vector3.one * ResultPanelScale;

        var heading = CreateRuntimeText(
            panelRect, "Heading", new Vector2(0f, 278f), new Vector2(820f, 58f), 34f, FontStyles.Bold);
        heading.text = "事故の説明";
        heading.color = new Color32(119, 216, 181, 255);
        runtimeTitle = CreateRuntimeText(
            panelRect, "Title", new Vector2(0f, 203f), new Vector2(820f, 76f), 40f, FontStyles.Bold);
        runtimeTitle.enableAutoSizing = true;
        runtimeTitle.fontSizeMin = 28f;
        runtimeTitle.fontSizeMax = 40f;
        runtimeSummary = CreateRuntimeText(
            panelRect, "Summary", new Vector2(0f, 48f), new Vector2(900f, 210f), 30f, FontStyles.Normal);
        runtimeSummary.alignment = TextAlignmentOptions.TopLeft;
        runtimeSummary.lineSpacing = 8f;

        runtimeMetrics = CreateRuntimeText(panelRect, "Metrics", new Vector2(0f, -106f), new Vector2(900f, 85f), 24f, FontStyles.Normal);
        runtimeMetrics.color = new Color32(119, 216, 181, 255);

        var footer = CreateRuntimeText(
            panelRect, "Footer", new Vector2(0f, -284f), new Vector2(860f, 40f), 22f, FontStyles.Normal);
        footer.text = "A / X・Enter：メニューへ　　R：同じ条件で再体験";
        footer.color = new Color32(190, 208, 220, 255);

        var buttonRect = CreateImage(panelRect, "Button_ReturnToMenu", new Color32(220, 184, 92, 255));
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(300f, 70f);
        buttonRect.anchoredPosition = new Vector2(180f, -213f);
        readableReturnButton = buttonRect.gameObject.AddComponent<Button>();
        readableReturnButton.targetGraphic = buttonRect.GetComponent<Image>();
        readableReturnButton.onClick.AddListener(ReturnToMenu);
        var buttonLabel = CreateRuntimeText(
            buttonRect, "Label", Vector2.zero, new Vector2(286f, 64f), 29f, FontStyles.Bold);
        buttonLabel.text = "メニューに戻る";
        buttonLabel.color = new Color32(18, 32, 44, 255);

        var retryRect = CreateImage(panelRect, "Button_RetryScenario", new Color32(119, 216, 181, 255));
        retryRect.anchorMin = retryRect.anchorMax = new Vector2(0.5f, 0.5f);
        retryRect.sizeDelta = new Vector2(300f, 70f);
        retryRect.anchoredPosition = new Vector2(-180f, -213f);
        var retry = retryRect.gameObject.AddComponent<Button>();
        retry.targetGraphic = retryRect.GetComponent<Image>();
        retry.onClick.AddListener(RetryScenario);
        var retryLabel = CreateRuntimeText(retryRect, "Label", Vector2.zero, new Vector2(286f, 64f), 29f, FontStyles.Bold);
        retryLabel.text = "同じ条件で再体験";
        retryLabel.color = new Color32(18, 32, 44, 255);
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

    RectTransform CreateImage(RectTransform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.GetComponent<RectTransform>();
        imageObject.GetComponent<Image>().color = color;
        return rect;
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

    TMP_Text CreateRuntimeText(
        RectTransform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize,
        FontStyles style)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = textObject.GetComponent<TextMeshProUGUI>();
        if (runtimeFont != null)
            text.font = runtimeFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.extraPadding = true;
        text.raycastTarget = false;
        return text;
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
