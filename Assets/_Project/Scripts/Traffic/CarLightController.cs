using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarLightController : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    GameObject gamedirector;

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        gamedirector = GameObject.Find("GameDirector");

        //車のライトのオンオフ
        if(gamedirector.GetComponent<GameDirector>().SkyTime == 0)
        {
            this.gameObject.transform.Find("LightContainer").gameObject.SetActive(false);
        }
        else if(gamedirector.GetComponent<GameDirector>().SkyTime == 1)
        {
            this.gameObject.transform.Find("LightContainer").gameObject.SetActive(true);
            //スマホのぼやけを取る
            if (this.gameObject.name == "SmartPhoneContainer")
            {
                this.gameObject.transform.Find("WhiteWall").gameObject.SetActive(false);
            }
        }

        

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
