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
    [SerializeField, Range(1.2f, 3f)] private float viewingDistance = 1.6f;

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
        // Sizes, wrapping and margins are authored by TitleMenuLayout for legibility on Quest 2.
        // (This used to switch every text to auto-size down to 55 %, which made the menu unreadable.)
        foreach (var text in titleCanvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (uiFont != null)
                text.font = uiFont;
            text.extraPadding = true;
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
        // the menu is almost the only thing drawn here: a larger eye buffer makes its text crisp
        VrPanel.SetEyeResolution(1.3f);
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
        // level and upright regardless of how the head is tilted at start, slightly below the eyes
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        forward = forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;

        var canvasTransform = titleCanvas.transform;
        canvasTransform.position = head.position + forward * viewingDistance + Vector3.down * 0.12f;
        canvasTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private void OnDestroy()
    {
        // gameplay renders the full scene: back to the default eye buffer
        VrPanel.SetEyeResolution(1f);
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
