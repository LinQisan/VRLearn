using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMainController : MonoBehaviour
{
    //Rigidbodyを格納する変数
    Rigidbody rigid;

    //ゲームオブジェクトを格納する変数
    GameObject cameraphisic;
    GameObject car1;
    GameObject human;
    GameObject humanbody;
    GameObject humanneck;

    //現在スピードを取得
    Vector3 CurrentSpeed;

    //マウスの移動量を格納する変数
    float mx;
    float my;

    //回転量を格納する変数
    [SerializeField] private float _sensX = 5f;
    [SerializeField] private float _sensY = 5f;
    float _yRotation, _xRotation;

    //マウスの移動量の係数
    [SerializeField] float MouseCoefficient;
    //フラグ管理
    [SerializeField]int CameraPhisicFollow;
    [SerializeField] int Car1Move;
    [SerializeField] int HumanMove;

    //視点移動用のメソッド
    private void Look()
    {
        var mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 mouseInput = new Vector2(mouseDelta.x * _sensX, mouseDelta.y * _sensY);

        _xRotation -= mouseInput.y;
        _yRotation += mouseInput.x;
        _yRotation %= 360; // 絶対値が大きくなりすぎないように

        // 上下の視点移動量をClamp
        _xRotation = Mathf.Clamp(_xRotation, -90, 90);

        // 頭、体の向きの適用
        this.gameObject.transform.localRotation = Quaternion.Euler(_xRotation, _yRotation, 0);
        //_body.transform.localRotation = Quaternion.Euler(0, _yRotation, 0);
    }

    //動くフラグのリセット
    void MoveFlagReset()
    {
        CameraPhisicFollow = 0;
        Car1Move = 0;
        HumanMove = 0;

        //オブジェクトの動くフラグもリセット
        cameraphisic.GetComponent <CameraObjectController>().PhisicsFlagSet = 0;
        human.GetComponent <HumanController>().HumanFlagSet = 0;
        car1.GetComponent <CarController>().CarFlagSet = 0;

    }

    // Start is called before the first frame update
    void Start()
    {
        //Rigidbodyを取得
        this.rigid = this.GetComponent<Rigidbody>();

        //ゲームオブジェクトを格納
        this.cameraphisic = GameObject.Find("CameraPhisic");
        this.car1 = GameObject.Find("Car1");
        this.human = GameObject.Find("Human");
        this.humanbody = human.transform.GetChild(1).gameObject. //metarigを取得
                        transform.GetChild(0).gameObject; //hipsを取得
        this.humanneck = humanbody.
                transform.GetChild(0).gameObject. //spineを取得
                transform.GetChild(0).gameObject. //chestを取得
                transform.GetChild(0).gameObject; //neckを取得
        //フラグの数値を格納
        MoveFlagReset();

        //マウスの移動量の係数を格納
        //MouseCoefficient = 2.0f;

        //初期位置に移動
        //this.gameObject.transform.position = new Vector3(42f,2f,15f);

        //初期を人間の視点に
        this.HumanMove = 0;
    }

    //ポーズ中は動かないように
    void FixedUpdate()
    {
        //Nキーでキューブのオブジェクトにカメラ移動するか切り替え
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            if (CameraPhisicFollow == 0)
            {
                MoveFlagReset();
                CameraPhisicFollow = 1;
                //カメラ物体の回転に合わせる
                this.gameObject.transform.localRotation = cameraphisic.transform.localRotation;
            }
            else if (CameraPhisicFollow == 1)
            {
                CameraPhisicFollow = 0;
            }
        }

        //Cキーで車のオブジェクトにカメラ移動
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (Car1Move == 0)
            {
                MoveFlagReset();
                Car1Move = 1;
                //車の回転に合わせる
                this.gameObject.transform.localRotation = car1.transform.localRotation;
            }
            else if (Car1Move == 1)
            {
                Car1Move = 0;
            }
        }

        //Hキーで人のオブジェクトにカメラ移動
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            if (HumanMove == 0)
            {
                MoveFlagReset();
                HumanMove = 1;
                //人の回転に合わせる
                this.gameObject.transform.localRotation = human.transform.localRotation;
            }
            else if (HumanMove == 1)
            {
                HumanMove = 0;
            }
        }

        //Yキーで人のオブジェクトにカメラ移動し、車を操作
        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
        {
            if (HumanMove == 0)
            {
                MoveFlagReset();
                HumanMove = 1;
                //車の回転に合わせる
                //this.gameObject.transform.localRotation = human.transform.localRotation;
            }
            else if (HumanMove == 1)
            {
                HumanMove = 0;
            }
        }

        //カメラ追従
        if (CameraPhisicFollow == 1)
        {
            this.gameObject.transform.position = cameraphisic.transform.position;
            //視点移動
            Look();
            //視点に合わせて物体も回転
            cameraphisic.transform.localRotation = Quaternion.Euler(_xRotation, _yRotation, 0);
        }
        if (Car1Move == 1)
        {
            //視点の位置調整
            this.gameObject.transform.position = 
                car1.transform.position 
                + car1.transform.forward * 0.20f 
                + car1.transform.up * 0.65f
                + car1.transform.right * 0.4f;

            //視点移動
            Look();

            // 右左折時に視点を車の回転に追従させる
            if (Keyboard.current != null && Keyboard.current.aKey.isPressed)
            {
                _yRotation -= 0.5f; // 左回転を_yRotationに反映
            }
            if (Keyboard.current != null && Keyboard.current.dKey.isPressed)
            {
                _yRotation += 0.5f; // 右回転を_yRotationに反映
            }

        }
        if (HumanMove == 1)
        {
            //視点の位置調整
            this.gameObject.transform.position =
                humanneck.transform.position
                + humanneck.transform.forward * -0.15f
                + humanneck.transform.up * 0.10f;

            //視点移動
            //Look();
            //視点に合わせて物体も回転
            //human.transform.localRotation = Quaternion.Euler(0, _yRotation, 0);
            /*this.gameObject.transform.localRotation = humanbody.
                transform.GetChild(0).gameObject. //spineを取得
                transform.GetChild(0).gameObject. //chestを取得
                transform.GetChild(0).gameObject. //neckを取得
                transform.localRotation;*/
            this.gameObject.transform.eulerAngles = humanneck.transform.eulerAngles;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
