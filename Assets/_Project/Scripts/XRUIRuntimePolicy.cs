using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Keeps XR UI input lean on device while preserving mouse testing in the Editor.
/// </summary>
public static class XRUIRuntimePolicy
{
    public const float MaxUIRayDistance = 5f;
    public const string UILayerName = "UI";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    public static void Apply()
    {
        var editorInput = Application.isEditor;
        var hasMetaInput = Object.FindFirstObjectByType<OVRInputModule>(FindObjectsInactive.Include) != null;
        var usesMetaInput = hasMetaInput && !editorInput;
        var uiLayer = LayerMask.NameToLayer(UILayerName);
        if (uiLayer < 0)
        {
            Debug.LogError($"Required layer '{UILayerName}' does not exist.");
            return;
        }

        var uiMask = (LayerMask)(1 << uiLayer);

        foreach (var inputModule in Object.FindObjectsByType<XRUIInputModule>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            inputModule.enabled = !hasMetaInput;
            inputModule.enableXRInput = true;
            inputModule.enableMouseInput = editorInput;
            inputModule.enableTouchInput = false;
            inputModule.enableGamepadInput = false;
            inputModule.enableJoystickInput = false;
            inputModule.enableBuiltinActionsAsFallback = editorInput;
        }

        foreach (var inputModule in Object.FindObjectsByType<BaseInputModule>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inputModule is OVRInputModule)
            {
                inputModule.enabled = usesMetaInput;
                continue;
            }

            if (inputModule is InputSystemUIInputModule)
            {
                inputModule.enabled = hasMetaInput && editorInput;
                continue;
            }

            if (inputModule is not XRUIInputModule)
                inputModule.enabled = !hasMetaInput && editorInput;
        }

        foreach (var ray in Object.FindObjectsByType<XRRayInteractor>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!ray.enableUIInteraction)
                continue;

            ray.maxRaycastDistance = MaxUIRayDistance;
            ray.raycastMask = uiMask;
        }

        foreach (var trackedRaycaster in Object.FindObjectsByType<TrackedDeviceGraphicRaycaster>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            trackedRaycaster.enabled = !hasMetaInput;
            trackedRaycaster.ignoreReversedGraphics = true;
            // UI graphics are resolved by the raycaster itself. A UI physics mask
            // here can make a collider on the Canvas block the graphic behind it.
            trackedRaycaster.blockingMask = 0;
            SetLayerRecursively(trackedRaycaster.gameObject, uiLayer);
        }

        foreach (var graphicRaycaster in Object.FindObjectsByType<GraphicRaycaster>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (graphicRaycaster is OVRRaycaster)
            {
                graphicRaycaster.enabled = usesMetaInput;
                graphicRaycaster.ignoreReversedGraphics = true;
                graphicRaycaster.blockingMask = 0;
                continue;
            }

            graphicRaycaster.enabled = editorInput;
            graphicRaycaster.ignoreReversedGraphics = true;
            graphicRaycaster.blockingMask = 0;
        }
    }

    public static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
