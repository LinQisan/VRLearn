using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/// <summary>Runtime guardrails for Touch-controller UI in the duplicated Meta accident scene.</summary>
[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public sealed class MetaGameplaySceneSetup : MonoBehaviour
{
    [SerializeField] private Camera centerEyeCamera;
    [SerializeField] private OVRInputModule inputModule;
    [SerializeField] private HumanController avatar;
    [SerializeField] private GameDirector director;
    [SerializeField] private Canvas[] worldCanvases;

    private void Awake()
    {
        if (centerEyeCamera == null || inputModule == null || avatar == null || director == null || worldCanvases == null)
        {
            Debug.LogError("Meta gameplay scene references are incomplete.", this);
            enabled = false;
            return;
        }

        var metaCenterEyeCamera = ResolveMetaCenterEyeCamera();
        if (metaCenterEyeCamera == null)
        {
            Debug.LogError("Meta gameplay OVRCameraRig CenterEye camera was not found.", this);
            enabled = false;
            return;
        }
        centerEyeCamera = metaCenterEyeCamera;

        inputModule.allowActivationOnMobileDevice = true;
        inputModule.joyPadClickButton = OVRInput.Button.PrimaryIndexTrigger;

        ConfigureSingleTrackedPlayer();
        if (MetaEditorSimulationController.IsActive)
        {
            MetaEditorSimulationController.ConfigureGameplay(
                GetComponent<XROrigin>(), centerEyeCamera, inputModule, worldCanvases);
        }
        ConfigureHybridAccidentPresentation();

        foreach (var canvas in worldCanvases)
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                continue;

            canvas.worldCamera = centerEyeCamera;
            if (MetaEditorSimulationController.IsActive)
                MetaEditorSimulationController.ConfigureCanvas(canvas);
            else
                ConfigureCanvas(canvas);
        }
    }

    private Camera ResolveMetaCenterEyeCamera()
    {
        var origin = GetComponent<XROrigin>();
        var rig = origin != null ? origin.GetComponentInChildren<OVRCameraRig>(true) : null;
        if (rig == null)
            return null;

        var centerEye = rig.centerEyeAnchor;
        if (centerEye == null)
        {
            foreach (var child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "CenterEyeAnchor")
                    continue;
                centerEye = child;
                break;
            }
        }
        return centerEye != null ? centerEye.GetComponent<Camera>() : null;
    }

    private void ConfigureHybridAccidentPresentation()
    {
        var presentation = GetComponent<HybridAccidentPresentation>();
        if (presentation == null)
            presentation = gameObject.AddComponent<HybridAccidentPresentation>();

        var avatarPresenter = avatar.GetComponent<AvatarPresenter>();
        var flow = director.GetComponent<GameplayFlowController>();
        var results = FindFirstObjectByType<AccidentResultPresenter>(FindObjectsInactive.Include);
        // CenterEyeCamera is the legacy accident state/presentation component,
        // not the rendering camera. Its Camera component remains disabled while
        // Meta CenterEyeAnchor owns rendering and tracked head pose.
        var legacyPresentation = FindFirstObjectByType<CenterEyeCamera>(FindObjectsInactive.Include);
        if (legacyPresentation != null)
            legacyPresentation.PreserveTrackedCameraPose(true);
        presentation.Configure(
            centerEyeCamera,
            avatarPresenter,
            director,
            flow,
            results,
            legacyPresentation);
    }

    private void ConfigureSingleTrackedPlayer()
    {
        var origin = GetComponent<XROrigin>();
        if (origin == null)
        {
            Debug.LogError("Meta gameplay XROrigin was not found.", this);
            return;
        }

        // The copied legacy scene contained its own Main Camera. OVRCameraRig owns
        // tracking in the Meta scene, so every consumer must use the same CenterEye.
        origin.Camera = centerEyeCamera;
        centerEyeCamera.enabled = true;
        centerEyeCamera.tag = "MainCamera";

        foreach (var otherCamera in FindObjectsByType<Camera>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (otherCamera != centerEyeCamera)
            {
                otherCamera.enabled = false;
                if (otherCamera.CompareTag("MainCamera"))
                    otherCamera.tag = "Untagged";
            }
        }

        foreach (var listener in FindObjectsByType<AudioListener>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            listener.enabled = listener.gameObject == centerEyeCamera.gameObject;
        if (centerEyeCamera.GetComponent<AudioListener>() == null)
            centerEyeCamera.gameObject.AddComponent<AudioListener>();

        var officialController = origin.GetComponent<OVRPlayerController>();
        if (officialController == null)
        {
            Debug.LogError("Meta_XR_Player requires the official OVRPlayerController component.", origin);
            return;
        }
        if (origin.GetComponent<PlayerActor>() == null)
            Debug.LogError("Meta_XR_Player requires an authored PlayerActor component.", origin);
        if (avatar == null)
            return;

        // Meta_XR_Player is the only locomotion/physics owner. Human is visual IK.
        avatar.ConfigureMetaRig(centerEyeCamera.transform, director.EventNumber);
        avatar.SetVisible(false);
    }

    private static void ConfigureCanvas(Canvas canvas)
    {
        var raycaster = canvas.GetComponent<OVRRaycaster>();
        if (raycaster == null)
        {
            Debug.LogError($"{canvas.name} requires an authored OVRRaycaster.", canvas);
            return;
        }
        raycaster.enabled = true;
        raycaster.ignoreReversedGraphics = true;
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        raycaster.blockingMask = 0;
        raycaster.pointer = null;

        // UI layer and raycast targets are authored in the scene.
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        Camera configuredCamera,
        OVRInputModule configuredInputModule,
        HumanController configuredAvatar,
        GameDirector configuredDirector,
        Canvas[] configuredCanvases)
    {
        centerEyeCamera = configuredCamera;
        inputModule = configuredInputModule;
        avatar = configuredAvatar;
        director = configuredDirector;
        worldCanvases = configuredCanvases;
    }
#endif
}
