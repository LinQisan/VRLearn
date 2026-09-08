using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CanvasController : MonoBehaviour
{
    //トランスフォームを格納する変数
    RectTransform canvasrect;

    //ゲームオブジェクトを格納する変数
    [SerializeField]GameObject centereye;

    //スクリプトを格納する変数
    CenterEyeCamera script_centereye;
    bool resultAnchored;

    // Start is called before the first frame update
    void Start()
    {
        // The title menu is a fixed world-space panel. Moving it with the headset
        // makes its initial position depend on the Quest tracking origin and can
        // leave it behind or below the user's view.
        if (SceneManager.GetActiveScene().name == SceneRoute.MetaTitle)
        {
            enabled = false;
            return;
        }

        //トランスフォームを格納
        canvasrect = transform as RectTransform;

        //ゲームオブジェクトを格納
        centereye = OpenXRScene.MainCamera.gameObject;

        var cameraController = OpenXRScene.CameraControllerObject;
        script_centereye = cameraController != null
            ? cameraController.GetComponent<CenterEyeCamera>()
            : null;
        if (script_centereye == null)
        {
            Debug.LogError("CanvasController requires CenterEyeCamera presentation state.", this);
            enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (centereye == null || script_centereye == null)
            return;

        // The result panel must use the tracked Meta camera, never the inactive
        // legacy camera controller. Anchor it once so it remains stable while
        // the user looks around the accident scene.
        if (script_centereye.AcidentProgress == 5)
        {
            if (!resultAnchored)
            {
                Vector3 forward = Vector3.ProjectOnPlane(centereye.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f)
                    forward = centereye.transform.forward;
                forward.Normalize();

                canvasrect.position = centereye.transform.position
                    + forward * 1.35f
                    + Vector3.up * -0.15f;
                canvasrect.rotation = Quaternion.LookRotation(forward, Vector3.up);
                resultAnchored = true;
            }
        }
        else
        {
            resultAnchored = false;
            canvasrect.position = centereye.transform.position
                + centereye.transform.forward * 1.0f
                + centereye.transform.up * -0.2f;
            canvasrect.rotation = centereye.transform.rotation;
        }
            
    }
}
