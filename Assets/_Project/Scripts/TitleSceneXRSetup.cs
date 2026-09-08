using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Makes the Quest title menu independent of the user's Guardian origin.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XROrigin))]
public sealed class TitleSceneXRSetup : MonoBehaviour
{
    [SerializeField] private Canvas settingsCanvas;
    [SerializeField, Range(2f, 5f)] private float viewingDistance = 2.8f;
    [SerializeField, Min(0f)] private float trackingStabilizationSeconds = 2f;

    private XROrigin xrOrigin;
    private OVROverlayCanvas overlayCanvas;

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
        if (settingsCanvas == null)
            return;

        // Do not expose the authored world position before the HMD pose is stable.
        // Keep the Meta compositor overlay disabled: when used as a world-locked
        // layer it can update from a different pose than the OpenXR scene camera,
        // which makes this Canvas drift sideways and fade independently of the world.
        overlayCanvas = settingsCanvas.GetComponent<OVROverlayCanvas>();
        NormalizeNumericControlLayout();
        NormalizeEventDisplay();
        settingsCanvas.enabled = false;
        if (overlayCanvas != null)
            overlayCanvas.enabled = false;
    }

    private IEnumerator Start()
    {
        EnsureInteractionIsEnabled();

        if (settingsCanvas == null)
            yield break;

        // Quest can report a default camera pose for several frames. Wait for an
        // actual tracked HMD pose, with a bounded fallback for Editor simulation.
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        var maximumWaitFrames = Application.isEditor ? 2 : 90;
        for (var frame = 0; frame < maximumWaitFrames; frame++)
        {
            if (!head.isValid)
                head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.isValid &&
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked)
                break;
            yield return null;
        }

        // isTracked can become true while the initial OpenXR pose is still settling.
        // Keep the Canvas hidden and follow the latest pose for a short bounded period,
        // then publish one final, world-locked transform.
        // The XR Device Simulator applies its camera pose after Start. It needs the
        // same stabilization window as a headset; the previous 0.05 second Editor
        // shortcut locked the menu to the authored direction before that pose arrived.
        var stabilizationDuration = trackingStabilizationSeconds;
        var stabilizationDeadline = Time.realtimeSinceStartup + stabilizationDuration;
        do
        {
            yield return new WaitForEndOfFrame();
            AlignSettingsWithCamera();
        }
        while (Time.realtimeSinceStartup < stabilizationDeadline);

        AlignSettingsWithCamera();
        Canvas.ForceUpdateCanvases();
        settingsCanvas.enabled = true;
    }

    private void EnsureInteractionIsEnabled()
    {
        var inputModule = FindFirstObjectByType<XRUIInputModule>();
        if (inputModule != null)
        {
            inputModule.enabled = true;
            inputModule.enableXRInput = true;
        }

        foreach (var controller in GetComponentsInChildren<ActionBasedController>(true))
        {
            OpenXRAimPoseDriver.ConfigureController(controller);
            ConfigureQuestUIActions(controller);
            controller.enabled = true;
            controller.enableInputTracking = true;
            controller.enableInputActions = true;
        }

        foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
        {
            ray.enabled = true;
            ray.enableUIInteraction = true;

            // Keep the rendered line on the exact sample ray used by the UI
            // module. Blending replaces its origin with a newer controller pose
            // and projects the older UI hit onto that new line, so the visible
            // endpoint can differ from the point that actually receives input.
            ray.blendVisualLinePoints = false;

            var lineVisual = ray.GetComponent<XRInteractorLineVisual>();
            if (lineVisual != null)
                lineVisual.enabled = true;
        }

        if (settingsCanvas != null)
        {
            var raycaster = settingsCanvas.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = true;
        }
    }

    private void NormalizeNumericControlLayout()
    {
        NormalizeNumericField(
            "10_EventParameters/Field_Height", "Height",
            TitleCommand.HeightDown, TitleCommand.HeightUp);
        NormalizeNumericField(
            "10_EventParameters/Field_Weight", "Weight",
            TitleCommand.WeightDown, TitleCommand.WeightUp);
        NormalizeNumericField(
            "10_EventParameters/Field_Age", "Age",
            TitleCommand.AgeDown, TitleCommand.AgeUp);
    }

    private void NormalizeEventDisplay()
    {
        var eventText = settingsCanvas.transform
            .Find("10_EventParameters/Field_EventCount/EventNumber")
            ?.GetComponent<TMP_Text>();
        if (eventText == null)
            return;

        eventText.enableAutoSizing = false;
        eventText.fontSize = 24f;
        eventText.fontSizeMin = 24f;
        eventText.fontSizeMax = 24f;
        eventText.alignment = TextAlignmentOptions.Center;
        eventText.margin = Vector4.zero;
        eventText.rectTransform.anchorMin = Vector2.zero;
        eventText.rectTransform.anchorMax = Vector2.one;
        eventText.rectTransform.offsetMin = new Vector2(4f, 4f);
        eventText.rectTransform.offsetMax = new Vector2(-4f, -4f);
        eventText.rectTransform.localScale = Vector3.one;
        eventText.SetAllDirty();
    }

    private void NormalizeNumericField(
        string fieldPath, string valueName, TitleCommand decreaseCommand, TitleCommand increaseCommand)
    {
        var field = settingsCanvas.transform.Find(fieldPath);
        if (field == null)
            return;

        foreach (var commandButton in field.GetComponentsInChildren<TitleCommandButton>(true))
        {
            if (commandButton.transform.parent != field || !commandButton.gameObject.activeSelf)
                continue;

            var decrease = commandButton.Command == decreaseCommand;
            if (!decrease && commandButton.Command != increaseCommand)
                continue;

            var rect = (RectTransform)commandButton.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = new Vector3(decrease ? -100f : 100f, 0f, 0f);
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = new Vector3(1.66f, 1.66f, 1f);

            var label = commandButton.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;
            label.text = decrease ? "-5" : "+5";
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(2f, 2f);
            label.rectTransform.offsetMax = new Vector2(-2f, -2f);
            label.rectTransform.localScale = Vector3.one;
            label.SetAllDirty();
        }

        var valueText = field.Find(valueName)?.GetComponent<TMP_Text>();
        if (valueText == null)
            return;
        valueText.enableAutoSizing = false;
        valueText.fontSize = 36f;
        valueText.fontSizeMin = 36f;
        valueText.fontSizeMax = 36f;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.SetAllDirty();
    }

    private static void ConfigureQuestUIActions(ActionBasedController controller)
    {
        var hand = controller.name.Contains("Left") ? "LeftHand" : "RightHand";
        var controllerPath = $"<XRController>{{{hand}}}";

        controller.enabled = false;
        controller.enableInputTracking = true;
        controller.enableInputActions = true;
        controller.uiPressAction = CreateAction(
            $"{hand} UI Press", InputActionType.Button, "Button",
            controllerPath + "/triggerPressed");
        controller.uiPressActionValue = CreateAction(
            $"{hand} UI Press Value", InputActionType.Value, "Axis",
            controllerPath + "/trigger");
        controller.enabled = true;
    }

    private static InputActionProperty CreateAction(
        string name, InputActionType type, string expectedControlType, string binding)
    {
        var action = new InputAction(
            name: name,
            type: type,
            binding: binding,
            expectedControlType: expectedControlType);
        return new InputActionProperty(action);
    }

    private void AlignSettingsWithCamera()
    {
        if (xrOrigin == null || xrOrigin.Camera == null || settingsCanvas == null)
            return;

        var cameraTransform = xrOrigin.Camera.transform;
        var canvasTransform = settingsCanvas.transform;
        var rectTransform = canvasTransform as RectTransform;

        // Use the full gaze direction. Projecting onto the horizontal plane discarded
        // simulator/headset pitch and placed the menu above or below the initial view.
        var forward = cameraTransform.forward.normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        // Keep the panel upright without copying head roll.
        var upright = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
        if (upright.sqrMagnitude < 0.001f)
            upright = cameraTransform.up;

        // Keep the tracking origin untouched. Moving the XR Origin while OpenXR is
        // still refining the head pose caused a second lateral correction on Quest.
        canvasTransform.rotation = Quaternion.LookRotation(forward, upright);
        var currentCenter = rectTransform != null
            ? rectTransform.TransformPoint(rectTransform.rect.center)
            : canvasTransform.position;
        var desiredCenter = cameraTransform.position + forward * viewingDistance;
        canvasTransform.position += desiredCenter - currentCenter;
    }
}
