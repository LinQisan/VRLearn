using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Editor-only keyboard, mouse and UI adapter for the Meta scenes. The component
/// is present in player assemblies so scene setup code can call it without editor
/// assembly dependencies, but it never installs or updates in a player build.
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaEditorSimulationController : MonoBehaviour
{
    const float LookSensitivity = 0.12f;
    const float PitchLimit = 80f;

    Transform viewPivot;
    Quaternion baseLocalRotation = Quaternion.identity;
    float yaw;
    float pitch;

    public static bool IsActive => Application.isEditor;

    public static void ConfigureTitle(
        OVRCameraRig cameraRig,
        Canvas titleCanvas,
        OVRInputModule metaInputModule)
    {
        if (!IsActive)
            return;

        ConfigureEventSystem(metaInputModule);
        ConfigureCanvas(titleCanvas);
        ShowCursor();
    }

    public static void ConfigureGameplay(
        XROrigin origin,
        Camera centerEyeCamera,
        OVRInputModule metaInputModule,
        Canvas[] canvases)
    {
        if (!IsActive || origin == null)
            return;

        ConfigureEventSystem(metaInputModule);
        if (canvases != null)
        {
            foreach (var canvas in canvases)
                ConfigureCanvas(canvas);
        }

        var metaController = origin.GetComponent<OVRPlayerController>();
        var editorController = origin.GetComponent<OpenXRPlayerController>();
        if (editorController == null)
            editorController = origin.gameObject.AddComponent<OpenXRPlayerController>();
        if (metaController != null)
            editorController.Acceleration = metaController.Acceleration;

        editorController.enabled = true;
        if (metaController != null)
        {
            metaController.EnableLinearMovement = false;
            metaController.enabled = false;
        }

        var simulation = origin.GetComponent<MetaEditorSimulationController>();
        if (simulation == null)
            simulation = origin.gameObject.AddComponent<MetaEditorSimulationController>();
        simulation.InstallViewPivot(origin, centerEyeCamera);
        ShowCursor();
    }

    public static void ConfigureCanvas(Canvas canvas)
    {
        if (!IsActive || canvas == null)
            return;

        foreach (var metaRaycaster in canvas.GetComponents<OVRRaycaster>())
            metaRaycaster.enabled = false;

        GraphicRaycaster standardRaycaster = null;
        foreach (var candidate in canvas.GetComponents<GraphicRaycaster>())
        {
            if (candidate is OVRRaycaster)
                continue;
            standardRaycaster = candidate;
            break;
        }

        if (standardRaycaster == null)
            standardRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

        standardRaycaster.enabled = true;
        standardRaycaster.ignoreReversedGraphics = true;
        standardRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        standardRaycaster.blockingMask = 0;
    }

    public static void ShowCursor()
    {
        if (!IsActive)
            return;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    static void ConfigureEventSystem(OVRInputModule metaInputModule)
    {
        if (metaInputModule == null)
            return;

        var eventSystem = metaInputModule.GetComponent<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogError("OVRInputModule must be attached to an EventSystem.", metaInputModule);
            return;
        }

        foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            module.enabled = false;

        var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputSystemModule.AssignDefaultActions();
        }

        eventSystem.enabled = true;
        inputSystemModule.enabled = true;
    }

    void InstallViewPivot(XROrigin origin, Camera centerEyeCamera)
    {
        if (viewPivot != null)
            return;

        var rig = origin.GetComponentInChildren<OVRCameraRig>(true);
        if (rig == null || centerEyeCamera == null)
        {
            Debug.LogError("Editor simulation requires an OVRCameraRig and CenterEye camera.", origin);
            enabled = false;
            return;
        }

        var rigTransform = rig.transform;
        var pivotObject = new GameObject("MetaEditorViewPivot");
        viewPivot = pivotObject.transform;
        viewPivot.SetParent(rigTransform.parent, false);
        viewPivot.localPosition = rigTransform.localPosition;
        viewPivot.localRotation = rigTransform.localRotation;
        viewPivot.localScale = Vector3.one;
        baseLocalRotation = viewPivot.localRotation;

        var rigScale = rigTransform.localScale;
        rigTransform.SetParent(viewPivot, false);
        rigTransform.localPosition = Vector3.zero;
        rigTransform.localRotation = Quaternion.identity;
        rigTransform.localScale = rigScale;
    }

    void Update()
    {
        if (!IsActive || viewPivot == null)
            return;

        ShowCursor();
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed)
            return;

        var delta = mouse.delta.ReadValue();
        yaw += delta.x * LookSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * LookSensitivity, -PitchLimit, PitchLimit);
        viewPivot.localRotation = baseLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
    }
}
