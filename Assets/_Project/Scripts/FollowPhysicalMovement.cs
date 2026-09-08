using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class FollowPhysicalMovement : MonoBehaviour
{
    [Tooltip("XR Origin の Main Camera。未設定の場合は自動取得します。")]
    public Transform centerEyeAnchor;
    Transform trackingspace;

    Vector3 initialLocalPos;

    //アクシデント変数
    public int Acident;

    void Start()
    {
        Acident = 0;
        centerEyeAnchor = OpenXRScene.MainCameraTransform;
        trackingspace = OpenXRScene.CameraOffset;

        if (centerEyeAnchor == null)
        {
            Debug.LogError("CenterEyeAnchor がセットされていません！");
            enabled = false;
            return;
        }
        // 演出中は毎フレームここを基準にリセットしていく
        initialLocalPos = centerEyeAnchor.localPosition;
    }

    void LateUpdate()
    {
        if (Acident == 0)
        {
            // ① 今フレームのローカル位置
            Vector3 currentLocalPos = centerEyeAnchor.localPosition;

            // ② ルートがまだ追従していない「物理移動分のデルタ」
            Vector3 delta = currentLocalPos - initialLocalPos;

            float maxMovePerFrame = 0.01f; // 1フレームあたり最大10cm移動まで

            delta.y = 0;

            // delta を制限してから加算
            Vector3 limitedDelta = Vector3.ClampMagnitude(delta, maxMovePerFrame);
            transform.position += limitedDelta;

            // ③ 世界座標にそのまま足し込む（回転をかけない）
            //transform.position += delta;

            // ④ 毎フレーム Anchor を初期位置に戻しておく
            initialLocalPos = centerEyeAnchor.localPosition;

            /*this.transform.position = centerEyeAnchor.position;
            trackingspace.localPosition = new Vector3(0, 0, 0);
            centerEyeAnchor.localPosition = new Vector3(0,0, 0);*/
        }
    }
}
