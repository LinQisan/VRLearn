using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccidentAreaContainer : MonoBehaviour
{
    //アクシデントエリアリストを格納する変数
    [SerializeField] GameObject[] AcidentAreas;

    //ゲームオブジェクトを格納する変数
    GameObject gamedirector;

    //イベントナンバーを格納する変数
    int EventNumber;

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        this.gamedirector = GameObject.Find("GameDirector");

        //イベントナンバーを格納
        EventNumber = gamedirector.GetComponent<GameDirector>().EventNumber;

        // 親オブジェクトのTransformを取得
        var parentTransform = this.gameObject.transform;

        // 子オブジェクトを格納する配列作成
        AcidentAreas = new GameObject[parentTransform.childCount];

        // 0～個数-1までの子を順番に配列に格納
        for (var i = 0; i < AcidentAreas.Length; ++i)
        {
            // Transformからゲームオブジェクトを取得して格納
            AcidentAreas[i] = parentTransform.GetChild(i).gameObject;

            //指定のエリア以外無効にする
            if(i != EventNumber)
            {
                AcidentAreas[i].SetActive(false);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
