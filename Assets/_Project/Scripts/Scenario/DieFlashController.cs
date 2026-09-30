using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DieFlashController : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    GameObject car;

    //走馬灯を再生したかどうか
    bool DieFlashOn;

    //人に当たった
    void OnTriggerEnter(Collider collider)
    {
        var isTrackedPlayer = PlayerActor.TryResolve(collider, out _);
        var isLegacyHuman = collider.CompareTag("Human")
            && collider.gameObject.name == "Human"
            && collider.gameObject.transform.parent == null;
        var isBicycle = collider.gameObject.name == "Bicycle";
        if ((isTrackedPlayer || isLegacyHuman || isBicycle) && !DieFlashOn)
        {
            //走馬灯
            Debug.Log("走馬灯");
            //Debug.Log(collider.gameObject.name);

            //フラグをオン
            DieFlashOn = true;

            //車の座標を渡して走馬灯を再生
            car.GetComponent<CarController>().DieFlashStart(this.gameObject.transform.position);
        }
    }

    //人に当たっている間
    void OnTriggerStay(Collider collider)
    {
        var isTrackedPlayer = PlayerActor.TryResolve(collider, out _);
        var isLegacyHuman = collider.CompareTag("Human")
            && collider.gameObject.name == "Human"
            && collider.gameObject.transform.parent == null;
        if (isTrackedPlayer || isLegacyHuman || collider.gameObject.name == "Bicycle")
        {
            car.GetComponent<CarController>().DieFlashProgress(this.gameObject.transform.position);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        car = transform.parent.gameObject;

        //フラグを管理
        DieFlashOn = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
