using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficLightUP : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    GameObject gamedirector;
    GameObject red;
    GameObject yellow;
    GameObject green;

    //イベントナンバーを管理する変数
    int EventNumber;

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        this.gamedirector = GameObject.Find("GameDirector");
        this.red = this.gameObject.transform.Find("RedLight").gameObject;
        if (this.gameObject.name != "TrafficLight_Walk")
        {
            //歩行者信号は黄信号がない
            this.yellow = this.gameObject.transform.Find("YellowLight").gameObject;
        }
        this.green = this.gameObject.transform.Find("GreenLight").gameObject;

        //イベントナンバーを格納
        EventNumber = gamedirector.GetComponent<GameDirector>().EventNumber;

        //点灯
        //イベントナンバー0
        if(EventNumber == 0)
        {
            //縦横の信号機で光を変更
            if (transform.parent.gameObject.name == "Beside")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    yellow.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
            }
            if (transform.parent.gameObject.name == "Vertical")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    yellow.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
            }
        }
        //イベントナンバー1,4
        if (EventNumber == 1 || EventNumber == 4)
        {
            //縦横の信号機で光を変更
            if (transform.parent.gameObject.name == "Vertical")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    yellow.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
            }
            if (transform.parent.gameObject.name == "Beside")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    yellow.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
            }
        }
        //イベントナンバー2と8
        if (EventNumber == 2 || EventNumber == 8)
        {
            //縦横の信号機で光を変更
            if (transform.parent.gameObject.name == "Vertical")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    yellow.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
            }
            if (transform.parent.gameObject.name == "Beside")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    yellow.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
            }
        }
        //イベントナンバー3,5,6
        if (EventNumber == 3 || EventNumber == 5 || EventNumber == 6)
        {
            //縦横の信号機で光を変更
            if (transform.parent.gameObject.name == "Beside")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    yellow.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
            }
            if (transform.parent.gameObject.name == "Vertical")
            {
                //車用信号
                if (this.gameObject.tag == "Light_Car")
                {
                    //赤
                    red.GetComponent<TrafficLightController>().ChangeRed();
                    yellow.GetComponent<TrafficLightController>().ChangeRed();
                    green.GetComponent<TrafficLightController>().ChangeRed();
                }
                //歩行者用信号
                if (this.gameObject.tag == "Light_Walk")
                {
                    //青
                    red.GetComponent<TrafficLightController>().ChangeGreen();
                    green.GetComponent<TrafficLightController>().ChangeGreen();
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
