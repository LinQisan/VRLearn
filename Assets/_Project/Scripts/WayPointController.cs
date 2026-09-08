using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

public class WayPointController : MonoBehaviour
{
    //スピードを格納する変数
    float Speed;

    void OnTriggerEnter(Collider collider)
    {
        //車に当たった
        if (collider.gameObject.tag == "Car")
        {
            //スピードを入れる
            Speed = collider.gameObject.GetComponent<CarController>().Speed;

            /*//40km/hまで加速
            if (this.gameObject.tag == "Acceleration")
            {
                DOVirtual.Float(
                    from: Speed,//Tween開始時の値
                    to: 40f / 3.6f,//終了時の値
                    duration: 2.0f,//Tween時間
                    //値が変わった時の処理
                    onVirtualUpdate: (tweenvalue) =>
                    {
                        //Debug.Log($"値が変化 : {tweenvalue}");
                        Speed = tweenvalue;
                    }
                );
            }
            //8km/hまで減速
            if (this.gameObject.tag == "Slow")
            {
                DOVirtual.Float(
                    from: Speed,//Tween開始時の値
                    to: 8f / 3.6f,//終了時の値
                    duration: 1.0f,//Tween時間
                                   //値が変わった時の処理
                    onVirtualUpdate: (tweenvalue) =>
                    {
                        Debug.Log($"値が変化 : {tweenvalue}");
                        Speed = tweenvalue;
                    }
                );
            }*/
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
