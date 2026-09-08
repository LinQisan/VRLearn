using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RootMotion.FinalIK; // 名前空間をインポート

//人のアバターの移動は、FinalIKを用い、カメラに追従させているため、スクリプトとしては記載していない

public class HumanController : MonoBehaviour
{
    //Rigidbodyを格納する変数
    Rigidbody rigidhips;

    //charactercontrollerを格納する変数
    private CharacterController characterController;
    private Vector3 previousPosition;
    private Vector3 velocity;

    //ゲームオブジェクトを格納する変数
    GameObject ovrcamera;

    //現在スピードを取得
    [SerializeField] Vector3 CurrentSpeed;
    [SerializeField] float SpeedMagnitude;

    //コントローラーで出る最高速
    float MaxSpeedMagnitude;

    //最高速に対する現在速度の割合を格納する変数
    float SpeedMagnitudeRatio;

    //フラグを格納する変数
    [SerializeField] int HumanMove;

    //操作したいアバターに付けているAnimatorを格納する変数
    [SerializeField] private Animator anim;

    //animatorの変数を格納する変数
    //[SerializeField] float speed;

    //VRIKを格納する変数
    VRIK _vrik;

    //音を取得する変数
    AudioSource audioSource;

    //イベントナンバー
    int EventNumber;
    Renderer[] avatarRenderers;

    //動くフラグを消すプロパティ
    public int HumanFlagSet { set { HumanMove = value; } }

    public void ConfigureMetaRig(Transform trackedHead, int eventNumber)
    {
        EventNumber = eventNumber;
        if (_vrik == null)
            _vrik = GetComponent<VRIK>();
        var headTarget = _vrik != null ? _vrik.solver.spine.headTarget : null;
        if (headTarget != null && trackedHead != null)
            headTarget.SetParent(trackedHead, false);
    }

    public void SetVisible(bool visible)
    {
        if (avatarRenderers == null)
            avatarRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var avatarRenderer in avatarRenderers)
            avatarRenderer.enabled = visible;
    }

    //アニメーションリセット用
    void ResetAnimation()
    {
        anim.ResetTrigger("walkF");
        anim.ResetTrigger("walkB");
        anim.ResetTrigger("walkR");
        anim.ResetTrigger("walkL");
        // anim.ResetTrigger("stop");
    }

    // Start is called before the first frame update
    void Start()
    {
        //Rigidbodyを取得
        this.rigidhips = this.transform.GetChild(1).gameObject. //metarigを取得
            transform.GetChild(0).gameObject. //hipsを取得
            GetComponent<Rigidbody>(); 

        //Animatorを格納
        this.anim = this.GetComponent<Animator>();

        //ゲームオブジェクトを格納
        this.ovrcamera = OpenXRScene.CameraControllerObject;

        //フラグを格納
        HumanMove = 0;

        //イベントナンバーを格納
        // Title-to-gameplay transfer always completes on sceneLoaded, which
        // runs before any Start, so the director value observed here is final.
        // (ConfigureMetaRig runs in Awake and can only see the scene-authored
        // default; it must not win over the transferred scenario.)
        {
            var director = GameplaySceneContext.Instance != null
                ? GameplaySceneContext.Instance.Director
                : FindFirstObjectByType<GameDirector>();
            if (director != null)
                EventNumber = director.EventNumber;
        }

        //推定最高速を格納(大体1.3m/s)
        MaxSpeedMagnitude = 1.3f;

        //charactercontrollerを格納
        characterController = GetComponent<CharacterController>();
        previousPosition = transform.position;

        //ゲーム開始時は止まるアニメーション
        anim.SetBool("InWalk", false);

        ResetAnimation();
        anim.SetTrigger("stop");

        //VRIKを取得
        _vrik = GetComponent<VRIK>();

        //AudioComponentを取得
        audioSource = GetComponent<AudioSource>();
    }

    //ポーズ中は動かないように
    void FixedUpdate()
    {
        //物体がキーHで移動するかのフラグ操作
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            if (HumanMove == 0)
            {
                HumanMove = 1;
            }
            else if (HumanMove == 1)
            {
                HumanMove = 0;
            }
        }

        if (HumanMove == 1)
        {
            /*//何も押さなければ止まる
            this.rigid.velocity = new Vector3(0.0f, rigid.velocity.y, 0.0f);

            //指定のキーを押すと力がかかる
            if (Input.GetKey(KeyCode.W))
            {
                this.rigid.velocity += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 5.0f; // 前方向に移動
            }
            if (Input.GetKey(KeyCode.S))
            {
                this.rigid.velocity += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * -5.0f; // 後ろ方向に移動
            }
            if (Input.GetKey(KeyCode.A))
            {
                this.rigid.velocity += -transform.right * 5.0f; // ローカル右方向に基づいて左へ移動
            }
            if (Input.GetKey(KeyCode.D))
            {
                this.rigid.velocity += transform.right * 5.0f; // ローカル右方向に基づいて右へ移動
            }*/
        }

        //0除算を防ぐため、時が止まっていたら速さの計算は行わない
        if(Time.deltaTime != 0)
        {
            // 現在の速度を計算 (現在の位置 - 前回の位置) / 経過時間
            velocity = (transform.position - previousPosition) / Time.deltaTime;

            // 次のフレームで使用するために現在の位置を保存
            previousPosition = transform.position;

        }
        //スピード変数に数値を格納
        CurrentSpeed = velocity;
        SpeedMagnitude = velocity.magnitude;

        //車とぶつかったらスピード調整を行わない
        if (this.ovrcamera.GetComponent<CenterEyeCamera>().Acident == 0) {

            //最高速を超えていたらスピード数値を調整
            if (SpeedMagnitude > MaxSpeedMagnitude)
            {
                SpeedMagnitude = MaxSpeedMagnitude;
            }
        }

        //最高速に対する現在速度の割合
        SpeedMagnitudeRatio = SpeedMagnitude/MaxSpeedMagnitude;
        if (EventNumber == 6 || EventNumber == 7)
        {
            audioSource.pitch = 0;
        }
        if (EventNumber != 6 && EventNumber != 7)
        {
            //速度割合をアニメーションのパラメータに代入
            anim.SetFloat("SpeedRatio", SpeedMagnitudeRatio);

            //歩行音を調整
            audioSource.pitch = SpeedMagnitudeRatio;

            //歩行速度が一定以上ならアニメーターに変数を設定
            /*if (SpeedMagnitude >= 0.2f)
            {
                anim.SetBool("InWalk", true);
            }
            else
            {
                anim.SetBool("InWalk", false);
            }*/

            // 前進Animation
            if (OpenXRInput.LeftStickUp)
            {
                ResetAnimation();
                anim.SetTrigger("walkF");
            }

            // 後退Animation
            if (OpenXRInput.LeftStickDown)
            {
                ResetAnimation();
                anim.SetTrigger("walkB");
            }

            // 右横歩き
            if (OpenXRInput.LeftStickRight)
            {
                ResetAnimation();
                anim.SetTrigger("walkR");
            }

            // 左横歩き
            if (OpenXRInput.LeftStickLeft)
            {
                ResetAnimation();
                anim.SetTrigger("walkL");
            }

            // idle状態
            if (OpenXRInput.LeftStickDirectionReleased)
            {
                ResetAnimation();
                anim.SetTrigger("stop");
            }
        }
    }
    //自然に歩くため、アバターのtransformを更新し続ける
    private void LateUpdate()
    {
        //車とぶつかったら処理を行わない
        if (this.ovrcamera.GetComponent<CenterEyeCamera>().Acident == 0 && this.ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress <= 3)
        {
            if (_vrik == null) return;

            // Bicycle events are anchored by the seat and pedal IK targets. Keep
            // the avatar root at the seat in all three axes instead of applying
            // the walking-only horizontal head-follow correction.
            if (EventNumber == 6 || EventNumber == 7 || EventNumber == 9)
            {
                var pelvisTarget = _vrik.solver.spine.pelvisTarget;
                if (pelvisTarget != null && _vrik.references.pelvis != null)
                    _vrik.references.root.position +=
                        pelvisTarget.position - _vrik.references.pelvis.position;
                return;
            }

            var d = _vrik.solver.spine.headTarget.position - _vrik.references.root.position;
            d.y = 0f;
            var mag = d.magnitude;
            _vrik.references.root.position += d.normalized * mag;
        }
    }
}
