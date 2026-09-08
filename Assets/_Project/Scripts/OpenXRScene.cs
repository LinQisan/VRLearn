using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Stable access to the OpenXR rig while legacy gameplay variable names are migrated.
/// </summary>
public static class OpenXRScene
{
    public static XROrigin Origin => Object.FindFirstObjectByType<XROrigin>();

    public static GameObject PlayerObject => Origin != null ? Origin.gameObject : null;

    public static OVRPlayerController PlayerController =>
        PlayerObject != null ? PlayerObject.GetComponent<OVRPlayerController>() : null;

    public static void SetPlayerLocomotionEnabled(bool enabled)
    {
        var player = PlayerObject;
        if (player == null)
            return;

        var fallbackControllers = player.GetComponents<OpenXRPlayerController>();
        var useEditorController = MetaEditorSimulationController.IsActive && fallbackControllers.Length > 0;
        var controller = player.GetComponent<OVRPlayerController>();
        if (controller != null)
        {
            controller.EnableLinearMovement = enabled && !useEditorController;
            controller.enabled = enabled && !useEditorController;
        }
        foreach (var fallbackController in fallbackControllers)
            fallbackController.enabled = enabled && (useEditorController || controller == null);
        foreach (var legacyController in player.GetComponents<PlayerMovement>())
        {
            legacyController.HumanFlagSet = 0;
            legacyController.enabled = enabled;
        }
        var character = player.GetComponent<CharacterController>();
        if (character != null)
            character.enabled = enabled;
    }

    public static void SetPlayerGravityEnabled(bool enabled)
    {
        var controller = PlayerController;
        if (controller != null)
            controller.GravityModifier = enabled ? 1f : 0f;
        var player = PlayerObject;
        if (player == null)
            return;
        foreach (var fallbackController in player.GetComponents<OpenXRPlayerController>())
            fallbackController.SetGravityEnabled(enabled);
    }

    public static void SetPlayerMinimumAcceleration(float minimumAcceleration)
    {
        var player = PlayerObject;
        if (player == null)
            return;

        var controller = player.GetComponent<OVRPlayerController>();
        if (controller != null)
            controller.Acceleration = Mathf.Max(controller.Acceleration, minimumAcceleration);
        foreach (var fallbackController in player.GetComponents<OpenXRPlayerController>())
            fallbackController.Acceleration = Mathf.Max(
                fallbackController.Acceleration, minimumAcceleration);
    }

    public static Camera MainCamera
    {
        get
        {
            var origin = Origin;
            if (origin != null && origin.Camera != null)
                return origin.Camera;
            return Camera.main;
        }
    }

    public static Transform MainCameraTransform => MainCamera != null ? MainCamera.transform : null;

    public static GameObject CameraControllerObject
    {
        get
        {
            var controller = Object.FindFirstObjectByType<CenterEyeCamera>();
            return controller != null ? controller.gameObject : PlayerObject;
        }
    }

    public static Transform CameraOffset
    {
        get
        {
            var origin = Origin;
            return origin != null && origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform
                : null;
        }
    }

    public static void SetTrackedCameraEnabled(bool enabled)
    {
        var camera = MainCamera;
        if (camera != null)
            camera.enabled = enabled;
    }

    /// <summary>
    /// Freezes the current tracked head pose and converts the XR rig into a
    /// single-camera cinematic rig. CenterEyeCamera can then move the origin
    /// without disabling the only rendering camera in the scene.
    /// </summary>
    public static void BeginCinematicCamera()
    {
        SetPlayerLocomotionEnabled(false);
        var origin = Origin;
        var camera = MainCamera;
        if (origin == null || camera == null)
            return;

        var cameraTransform = camera.transform;
        var worldPosition = cameraTransform.position;
        var worldRotation = cameraTransform.rotation;

        var poseDriver = camera.GetComponent<TrackedPoseDriver>();
        if (poseDriver != null)
            poseDriver.enabled = false;

        var metaRig = origin.GetComponentInChildren<OVRCameraRig>(true);
        if (metaRig != null)
            metaRig.enabled = false;

        origin.transform.SetPositionAndRotation(worldPosition, worldRotation);

        var cameraOffset = CameraOffset;
        if (cameraOffset != null)
        {
            cameraOffset.localPosition = Vector3.zero;
            cameraOffset.localRotation = Quaternion.identity;
        }

        cameraTransform.localPosition = Vector3.zero;
        cameraTransform.localRotation = Quaternion.identity;
        camera.enabled = true;
    }

    public static void SetControllersVisible(bool visible)
    {
        var origin = Origin;
        if (origin == null)
            return;

        foreach (var controller in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRBaseController>(true))
            controller.gameObject.SetActive(visible);

        foreach (var controller in origin.GetComponentsInChildren<OVRControllerHelper>(true))
            controller.gameObject.SetActive(visible);
    }

    public static void SetControllerVisualsVisible(bool visible)
    {
        var origin = Origin;
        if (origin == null)
            return;

        foreach (var controller in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRBaseController>(true))
            foreach (var renderer in controller.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;

        foreach (var controller in origin.GetComponentsInChildren<OVRControllerHelper>(true))
            foreach (var renderer in controller.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
    }
}
