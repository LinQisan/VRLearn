using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class XRUIProjectMigration
{
    private static readonly string[] ScenePaths =
    {
        "Assets/_Project/Scenes/TraficAcidentTitle.unity",
        "Assets/_Project/Scenes/TraficAcident.unity",
    };

    [MenuItem("Tools/VRLearn/Apply Quest UI Policy")]
    public static void Run()
    {
        var uiLayer = LayerMask.NameToLayer(XRUIRuntimePolicy.UILayerName);
        if (uiLayer < 0)
            throw new System.InvalidOperationException("The UI layer is missing.");

        var uiMask = (LayerMask)(1 << uiLayer);
        foreach (var path in ScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (var module in Object.FindObjectsByType<XRUIInputModule>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                module.enabled = true;
                module.enableXRInput = true;
                module.enableMouseInput = false;
                module.enableTouchInput = false;
                module.enableGamepadInput = false;
                module.enableJoystickInput = false;
                module.enableBuiltinActionsAsFallback = false;

                var serializedModule = new SerializedObject(module);
                serializedModule.FindProperty("m_MaxTrackedDeviceRaycastDistance").floatValue =
                    XRUIRuntimePolicy.MaxUIRayDistance;
                serializedModule.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(module);
            }

            foreach (var module in Object.FindObjectsByType<BaseInputModule>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (module is not XRUIInputModule)
                {
                    module.enabled = false;
                    EditorUtility.SetDirty(module);
                }
            }

            foreach (var ray in Object.FindObjectsByType<XRRayInteractor>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ray.enableUIInteraction)
                    continue;

                ray.maxRaycastDistance = XRUIRuntimePolicy.MaxUIRayDistance;
                ray.raycastMask = uiMask;
                EditorUtility.SetDirty(ray);
            }

            foreach (var trackedRaycaster in Object.FindObjectsByType<TrackedDeviceGraphicRaycaster>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                trackedRaycaster.enabled = true;
                trackedRaycaster.ignoreReversedGraphics = true;
                trackedRaycaster.blockingMask = uiMask;
                XRUIRuntimePolicy.SetLayerRecursively(trackedRaycaster.gameObject, uiLayer);
                EditorUtility.SetDirty(trackedRaycaster);
            }

            foreach (var graphicRaycaster in Object.FindObjectsByType<GraphicRaycaster>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                graphicRaycaster.enabled = false;
                graphicRaycaster.ignoreReversedGraphics = true;
                graphicRaycaster.blockingMask = uiMask;
                EditorUtility.SetDirty(graphicRaycaster);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Applied Quest UI policy to {path}");
        }
    }
}
