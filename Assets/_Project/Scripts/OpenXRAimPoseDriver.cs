using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Keeps the controller object on the OpenXR grip pose while driving the ray
/// origin from the OpenXR aim pose. This prevents the controller model and the
/// UI ray from incorrectly sharing two different kinds of tracked pose.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionBasedController))]
[RequireComponent(typeof(XRRayInteractor))]
[DefaultExecutionOrder(XRInteractionUpdateOrder.k_Controllers + 1)]
public sealed class OpenXRAimPoseDriver : MonoBehaviour
{
    private ActionBasedController controller;
    private XRRayInteractor rayInteractor;
    private Transform aimTransform;
    private InputAction aimPositionAction;
    private InputAction aimRotationAction;

    public static void ConfigureController(ActionBasedController targetController)
    {
        if (targetController == null)
            return;

        // Scene loading and TitleSceneXRSetup can both request configuration.
        // Keep this idempotent so actions are not replaced twice.
        if (targetController.GetComponent<OpenXRAimPoseDriver>() != null)
            return;

        var hand = targetController.name.Contains("Left") ? "LeftHand" : "RightHand";
        var controllerPath = $"<XRController>{{{hand}}}";
        var wasEnabled = targetController.enabled;

        targetController.enabled = false;
        targetController.positionAction = CreateAction(
            $"{hand} Grip Position", "Vector3", controllerPath + "/devicePosition");
        targetController.rotationAction = CreateAction(
            $"{hand} Grip Rotation", "Quaternion", controllerPath + "/deviceRotation");
        targetController.enabled = wasEnabled;

        targetController.gameObject.AddComponent<OpenXRAimPoseDriver>();
    }

    private void Awake()
    {
        controller = GetComponent<ActionBasedController>();
        rayInteractor = GetComponent<XRRayInteractor>();

        var hand = controller.name.Contains("Left") ? "LeftHand" : "RightHand";
        var controllerPath = $"<XRController>{{{hand}}}";
        aimPositionAction = CreateInputAction(
            $"{hand} Aim Position", "Vector3", controllerPath + "/pointerPosition");
        aimRotationAction = CreateInputAction(
            $"{hand} Aim Rotation", "Quaternion", controllerPath + "/pointerRotation");

        var aimObject = new GameObject($"{hand} OpenXR Aim Pose");
        aimObject.layer = gameObject.layer;
        aimTransform = aimObject.transform;
        aimTransform.SetParent(transform.parent, false);

        rayInteractor.rayOriginTransform = aimTransform;

        var lineVisual = GetComponent<XRInteractorLineVisual>();
        if (lineVisual != null)
            lineVisual.overrideInteractorLineOrigin = false;
    }

    private void OnEnable()
    {
        aimPositionAction?.Enable();
        aimRotationAction?.Enable();
        UpdateAimPose();
    }

    private void Update()
    {
        UpdateAimPose();
    }

    private void OnDisable()
    {
        aimPositionAction?.Disable();
        aimRotationAction?.Disable();
    }

    private void OnDestroy()
    {
        aimPositionAction?.Dispose();
        aimRotationAction?.Dispose();

        if (aimTransform != null)
            Destroy(aimTransform.gameObject);
    }

    private void UpdateAimPose()
    {
        if (aimTransform == null || aimPositionAction == null || aimRotationAction == null)
            return;

        aimTransform.localPosition = aimPositionAction.ReadValue<Vector3>();

        var rotation = aimRotationAction.ReadValue<Quaternion>();
        var magnitude = rotation.x * rotation.x + rotation.y * rotation.y +
                        rotation.z * rotation.z + rotation.w * rotation.w;
        if (magnitude > 0.0001f)
            aimTransform.localRotation = rotation;
    }

    private static InputActionProperty CreateAction(
        string name, string expectedControlType, string binding)
    {
        return new InputActionProperty(CreateInputAction(name, expectedControlType, binding));
    }

    private static InputAction CreateInputAction(
        string name, string expectedControlType, string binding)
    {
        return new InputAction(
            name: name,
            type: InputActionType.Value,
            binding: binding,
            expectedControlType: expectedControlType);
    }
}

internal static class OpenXRAimPoseInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= ConfigureSceneControllers;
        SceneManager.sceneLoaded += ConfigureSceneControllers;
    }

    private static void ConfigureSceneControllers(Scene scene, LoadSceneMode mode)
    {
        var controllers = Object.FindObjectsByType<ActionBasedController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var controller in controllers)
            OpenXRAimPoseDriver.ConfigureController(controller);
    }
}
