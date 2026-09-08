using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Meta XR Core SDK bootstrap for the standalone Meta title scene.
/// Keeps the menu world-locked and configures the fallback Meta UI input.
/// The left and right OVRControllerHelper components in the scene register
/// themselves as independent Touch input sources with OVRInputModule.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class MetaTitleSceneSetup : MonoBehaviour
{
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Canvas titleCanvas;
    [SerializeField] private OVRInputModule inputModule;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField, Range(1.5f, 3f)] private float viewingDistance = 2f;

    private void Awake()
    {
        if (cameraRig == null || titleCanvas == null || inputModule == null)
        {
            Debug.LogError("Meta title scene references are incomplete.", this);
            enabled = false;
            return;
        }

        var centerEye = ResolveRigAnchor("CenterEyeAnchor", cameraRig.centerEyeAnchor);
        var centerCamera = centerEye != null ? centerEye.GetComponent<Camera>() : null;
        if (centerCamera == null)
        {
            Debug.LogError("Meta title CenterEye camera was not found.", cameraRig);
            enabled = false;
            return;
        }
        titleCanvas.worldCamera = centerCamera;
        NormalizeMenuText();
        ConfigureMenuRaycastTargets();
        if (MetaEditorSimulationController.IsActive)
        {
            MetaEditorSimulationController.ConfigureTitle(cameraRig, titleCanvas, inputModule);
        }
        else
        {
            ConfigureMenuForTouchControllers();
            inputModule.allowActivationOnMobileDevice = true;
            inputModule.joyPadClickButton = OVRInput.Button.PrimaryIndexTrigger;
            // Used only if no OVRControllerHelper input source is active.
            inputModule.rayTransform = ResolveRigAnchor(
                "RightControllerAnchor", cameraRig.rightControllerAnchor);
        }
        titleCanvas.enabled = false;
    }

    private void NormalizeMenuText()
    {
        foreach (var text in titleCanvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (uiFont != null)
                text.font = uiFont;
            text.extraPadding = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;

            string value = text.text ?? string.Empty;
            bool numericReadout = int.TryParse(value, out _)
                || value.EndsWith(" Hz")
                || value == "+5"
                || value == "-5";
            if (!numericReadout)
            {
                float authoredSize = Mathf.Max(0.8f, text.fontSize);
                text.enableAutoSizing = true;
                text.fontSizeMax = authoredSize;
                text.fontSizeMin = Mathf.Max(0.65f, authoredSize * 0.55f);
                text.textWrappingMode = value.Contains("\n")
                    ? TextWrappingModes.Normal
                    : TextWrappingModes.NoWrap;
            }
            text.SetAllDirty();
        }
    }

    private void ConfigureMenuForTouchControllers()
    {
        var eventSystem = inputModule.GetComponent<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogError("OVRInputModule must be attached to an EventSystem.", inputModule);
            enabled = false;
            return;
        }

        eventSystem.enabled = true;
        inputModule.enabled = true;

        var raycaster = titleCanvas.GetComponent<OVRRaycaster>();
        if (raycaster == null)
            raycaster = titleCanvas.gameObject.AddComponent<OVRRaycaster>();

        raycaster.enabled = true;
        raycaster.ignoreReversedGraphics = true;
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        raycaster.blockingMask = 0;
        raycaster.pointer = null;

    }

    private void ConfigureMenuRaycastTargets()
    {
        // Only each selectable's actual target graphic may receive pointer hits.
        // Several readout and card graphics overlap the left-side minus controls
        // in canvas space; Quest otherwise reports a hit but no button callback.
        foreach (var graphic in titleCanvas.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (var selectable in titleCanvas.GetComponentsInChildren<Selectable>(true))
            if (selectable.targetGraphic != null)
                selectable.targetGraphic.raycastTarget = true;

        // Small +/- controls are difficult to acquire with a Touch ray at VR
        // distance. Expand only their hit rectangles without changing visuals.
        foreach (var commandButton in titleCanvas.GetComponentsInChildren<TitleCommandButton>(true))
        {
            var selectable = commandButton.GetComponent<Button>();
            if (selectable == null || selectable.targetGraphic == null)
                continue;

            selectable.interactable = true;
            selectable.targetGraphic.raycastTarget = true;
            var padding = commandButton.Command == TitleCommand.EventDown
                || commandButton.Command == TitleCommand.EventUp
                ? 1.25f
                : 0.65f;
            selectable.targetGraphic.raycastPadding = new Vector4(
                -padding,
                -padding,
                -padding,
                -padding);
        }
    }

    private IEnumerator Start()
    {
        // Meta tracking can still contain its default pose during the first frames.
        // Keep the Canvas hidden until the center eye pose has settled, then lock it.
        for (var frame = 0; frame < 12; frame++)
            yield return null;

        AlignOnceWithHeadset();
        Canvas.ForceUpdateCanvases();
        titleCanvas.enabled = true;
    }

    private void AlignOnceWithHeadset()
    {
        var head = ResolveRigAnchor("CenterEyeAnchor", cameraRig.centerEyeAnchor);
        if (head == null)
        {
            Debug.LogError("Meta title CenterEye anchor was not found.", cameraRig);
            return;
        }
        var forward = head.forward.normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        var canvasTransform = titleCanvas.transform;
        canvasTransform.position = head.position + forward * viewingDistance;
        canvasTransform.rotation = Quaternion.LookRotation(forward, head.up);
    }

    private Transform ResolveRigAnchor(string anchorName, Transform cachedAnchor)
    {
        if (cachedAnchor != null)
            return cachedAnchor;
        foreach (var child in cameraRig.GetComponentsInChildren<Transform>(true))
            if (child.name == anchorName)
                return child;
        return null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        OVRCameraRig configuredRig, Canvas configuredCanvas, OVRInputModule configuredInputModule)
    {
        cameraRig = configuredRig;
        titleCanvas = configuredCanvas;
        inputModule = configuredInputModule;
    }
#endif
}
