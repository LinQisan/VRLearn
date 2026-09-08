using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalTextController : MonoBehaviour
{
    private Transform _target;

    // Start is called before the first frame update
    void Start()
    {
        //プレイヤーを指定
        _target = OpenXRScene.MainCameraTransform;
    }

    private void Update()
    {
        // カメラ方向を計算
        Vector3 lookDir = _target.position - transform.position;
        lookDir.y = 0f; // Y軸回転のみなので、Y成分を0に設定する

        // オブジェクトのY軸のみカメラの方向に向ける
        if (lookDir != Vector3.zero)
        {
            transform.forward = lookDir.normalized;
        }

        //文字を反転
        Quaternion rot = Quaternion.AngleAxis(180, Vector3.up);
        // 現在の自信の回転の情報を取得する。
        Quaternion q = this.transform.rotation;
        // 合成して、自身に設定
        this.transform.rotation = q * rot;

    }
    
}
