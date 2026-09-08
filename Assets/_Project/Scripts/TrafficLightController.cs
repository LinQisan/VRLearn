using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class TrafficLightController : MonoBehaviour
{
    //赤信号に変える
    public void ChangeRed()
    {
        //赤ランプ
        if (this.gameObject.tag == "RedLight")
        {
            DOVirtual.Color(
                from: new Color32(80, 0, 0, 1), //Tween開始時の値
                to: new Color32(255, 0, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            // Emissionを有効化
            GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor",new Color(255, 0, 0, 1) * 1f);
        }
        //黄ランプ
        if (this.gameObject.tag == "YellowLight")
        {
            DOVirtual.Color(
                from: GetComponent<Renderer>().material.color, //Tween開始時の値
                to: new Color32(80, 80, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            //オフ
            GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.black);
        }
        //緑ランプ
        if (this.gameObject.tag == "GreenLight")
        {
            DOVirtual.Color(
                from: new Color32(0, 80, 0, 1), //Tween開始時の値
                to: new Color32(0, 80, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            //オフ
            GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.black);
        }
    }
    //黄色信号に変える
    public void ChangeYellow()
    {
        //赤ランプ
        if (this.gameObject.tag == "RedLight")
        {
            DOVirtual.Color(
                from: new Color32(80, 0, 0, 1), //Tween開始時の値
                to: new Color32(80, 0, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            //オフ
            GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.black);
        }
        //黄ランプ
        if (this.gameObject.tag == "YellowLight")
        {
            DOVirtual.Color(
                from: new Color32(80, 80, 0, 1), //Tween開始時の値
                to: new Color32(255, 255, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            // Emissionを有効化
            GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(255, 255, 0, 1) * 1f);
        }
        //緑ランプ
        if (this.gameObject.tag == "GreenLight")
        {
            DOVirtual.Color(
                from: new Color32(0, 255, 0, 1), //Tween開始時の値
                to: new Color32(0, 80, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            //オフ
            GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.black);
        }
    }
    //青信号に変える
    public void ChangeGreen()
    {
        //赤ランプ
        if (this.gameObject.tag == "RedLight")
        {
            DOVirtual.Color(
                from: new Color32(255, 0, 0, 1), //Tween開始時の値
                to: new Color32(80, 0, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            //オフ
            GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.black);
        }
        //黄ランプ
        if (this.gameObject.tag == "YellowLight")
        {
            DOVirtual.Color(
                from: new Color32(80, 80, 0, 1), //Tween開始時の値
                to: new Color32(80, 80, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            //オフ
            GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", Color.black);
        }
        //緑ランプ
        if (this.gameObject.tag == "GreenLight")
        {
            DOVirtual.Color(
                from: new Color32(0, 80, 0, 1), //Tween開始時の値
                to: new Color32(0, 255, 0, 1), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) => {
                    GetComponent<Renderer>().material.color = tweenValue;
                }
            );
            // Emissionを有効化
            GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
            GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(0, 255, 0, 1) * 1f);
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
