using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // コレ重要
using System;
using RootMotion.FinalIK; // 名前空間をインポート
using DG.Tweening;
using UnityEngine.Audio;

//カメラの移動はコントローラーで行う

public class CenterEyeCamera : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    [Header("Gameplay scene references")]
    [SerializeField] GameObject head;
    GameObject centereye;
    [SerializeField] GameObject dieflashimageroot;
    [SerializeField] GameObject blackoutimageroot;
    Image dieflashimage;
    Image blackoutimage;
    [SerializeField] GameObject button_title;
    [SerializeField] GameObject canvas_main;
    [SerializeField] GameObject buttongroupe_main;
    [SerializeField] GameObject gamedirector;
    GameObject trackingspace;
    [SerializeField] GameObject dieflash;
    [SerializeField] GameObject blackout;
    [SerializeField] Color dieflashcolor;
    [SerializeField] Color blackoutcolor;
    [SerializeField] GameObject human;
    GameObject ovrplayer;
    [SerializeField] GameObject invisiblewallcontainer;
    [SerializeField] GameObject bicycle_afteracident; //車から、子に渡される
    GameObject acidentdetailecontainer;
    GameObject acidentdetaillight;
    [SerializeField] GameObject acidentdetailset;
    [SerializeField] AccidentResultPresenter resultPresenter;
    GameObject acidentdetailcount;
    //事故のフラグを管理する変数(車から変更される)
    public int Acident;

    //吹き飛び時間を管理する変数
    float AcidentTime;
    public int AcidentProgress;
    float DieFlashTime;

    //一時保存するための変数
    public Vector3 BicycleSpeedTemp;

    //走馬灯の補完過程用変数
    [SerializeField] float DieStep;

    //イベントナンバー(ゲームディレクターから受け取る)
    public int EventNumber;

    //走馬灯のオンオフ(ゲームディレクターから受け取る)
    public bool DieFlash;

    //音を鳴らすための変数
    //音声自体は直接設定
    AudioSource audioSource;
    public AudioClip DieNoise;
    float DieNoiseTimeStep;
    public AudioMixer mixer;
    public float Hz;

    //事故車と接触したかどうか(自転車用)
    public int BycicleAcidentCarHit;

    //ゲームオブジェクトを格納するリスト
    GameObject[] AcidentDetailList;

    //テキストを格納する変数
    Text acidentdetailcount_text;
    bool legacyResultVisible;
    bool externalAccidentPresentation;
    bool preserveTrackedCameraPose;

    public void PreserveTrackedCameraPose(bool enabled)
    {
        preserveTrackedCameraPose = enabled;
    }

    /// <summary>
    /// Meta scenes keep head tracking active and let HybridAccidentPresentation
    /// own the blackout and observer relocation.
    /// </summary>
    public void UseExternalAccidentPresentation(bool enabled)
    {
        externalAccidentPresentation = enabled;
        if (enabled)
        {
            legacyResultVisible = false;
            Time.timeScale = 1f;
        }
    }

    //不快音を出す(走馬灯時)
    public void DieNoiseSounds()
    {
        // The unpleasant-sound choice is independent from the flashback option.
        // Previously a selected tone stayed silent whenever DieFlash was off.
        if (Hz > 0f && DieNoise != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = DieNoise;
            audioSource.Play();
        }
    }

    //ゲームディレクターより起動
    public void DieSoundTest( float Sound )
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (Sound <= 0f)
        {
            if (audioSource != null)
                audioSource.Stop();
            Hz = 0f;
            return;
        }

        //不快音をストップ
        audioSource.Stop();

        Hz = Sound;

        int sampleRate = 48000;
        float durationSeconds = 2f;
        float[] waveform = CreateToneWaveform(Hz, sampleRate, durationSeconds, 0.22f);

        // 波形を AudioClip オブジェクトに格納 (1 秒間あたり 48000 サンプル)
        DieNoise = AudioClip.Create("HighSound", waveform.Length, 1, sampleRate, false);
        DieNoise.SetData(waveform, 0);

        audioSource.clip = DieNoise;
        audioSource.Play();

    }

    //ゲームディレクターから受け取る
    public void DieSoundSet(float Sound)
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (Sound <= 0f)
        {
            if (audioSource != null)
                audioSource.Stop();
            Hz = 0f;
            DieNoise = null;
            return;
        }

        Hz = Sound;

        int sampleRate = 48000;
        float durationSeconds = 2f;
        float[] waveform = CreateToneWaveform(Hz, sampleRate, durationSeconds, 0.22f);

        // 波形を AudioClip オブジェクトに格納 (1 秒間あたり 48000 サンプル)
        DieNoise = AudioClip.Create("HighSound", waveform.Length, 1, sampleRate, false);
        DieNoise.SetData(waveform, 0);

    }

    static float[] CreateToneWaveform(float frequency, int sampleRate, float durationSeconds, float amplitude)
    {
        frequency = Mathf.Clamp(frequency, 100f, sampleRate * 0.45f);
        int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        var waveform = new float[sampleCount];
        float phaseStep = 2f * Mathf.PI * frequency / sampleRate;
        const float attackSeconds = 0.025f;
        const float releaseSeconds = 0.18f;
        for (int sample = 0; sample < sampleCount; sample++)
        {
            float time = sample / (float)sampleRate;
            float remaining = durationSeconds - time;
            float envelope = Mathf.Min(
                Mathf.Clamp01(time / attackSeconds),
                Mathf.Clamp01(remaining / releaseSeconds));
            waveform[sample] = Mathf.Sin(phaseStep * sample) * envelope * amplitude;
        }
        return waveform;
    }

    //走馬灯のアニメーションメソッド(CarControllerより再生)
    public void AcidentAnimation(Vector3 CarPosition)
    {
        //走馬灯がオフなら行わない
        if (DieStep <= 1f && DieFlash && AcidentProgress == 1)
        {
            //Debug.Log("走馬灯中");

            dieflashimageroot.SetActive(true);
            dieflash.SetActive(true);
            blackout.SetActive(true);
            //時を遅くする
            Time.timeScale = 0.25f; //一旦0.25倍速

            //補完スピードを決める
            float speed = 0.4f; //一旦0.5倍速

            //補完過程
            DieStep = speed * DieFlashTime;

            DieFlashTime += Time.deltaTime;

            // ターゲット方向のベクトルを取得
            Vector3 relativePos = CarPosition - this.transform.position;
            // 方向を、回転情報に変換
            Quaternion rotation = Quaternion.LookRotation(relativePos);
            // 現在の回転情報と、ターゲット方向の回転情報を補完する
            if (!preserveTrackedCameraPose)
                this.transform.rotation = Quaternion.Slerp(this.transform.rotation, rotation, DieStep);

            //色の補完
            dieflashcolor = Color.Lerp(dieflashcolor, new Color(1f,0,0,100f/255f), Time.deltaTime);
            dieflash.GetComponent<Renderer>().material.color = dieflashcolor;

            //タイマーを開始
            //AcidentProgress = 1;
        }

    }

    //事故の詳細をスキップ(ボタンより受け取る)
    public void AcidentClose()
    {
        if (resultPresenter != null)
            resultPresenter.ReturnToMenu();
        else
            AcidentTime = 60f;
    }

    //事故の過程ステップ
    void AcidentStep()
    {
        //轢かれそうになる
        if (AcidentProgress == 1)
        {
            //事故時間を進める
            AcidentTime += Time.deltaTime;

            //不快音時間を進める
            DieNoiseTimeStep += Time.deltaTime;

            //不快音を少しずつ小さくする(最後の数字は元の音量を調節するため)
            audioSource.volume = (1 - (DieNoiseTimeStep / 1f)) * 1f;

            //透明壁を消す
            invisiblewallcontainer.SetActive(false);

            //自転車かつ、ぶつかるまで
            if((EventNumber == 6 || EventNumber == 7) && Acident == 0 && gamedirector.GetComponent<GameDirector>().GoalFlag == 0 && DieFlash)
            {
                //人を自転車に追従させる
                this.gameObject.transform.position = bicycle_afteracident.transform.Find("Bicycle/HeadTarget").position;
                /*//自転車を勝手に動かす
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().AddForce(
                    new Vector3(
                    -15f *15f //* (1 / Time.timeScale)
                    , 0 //* (1 / Time.timeScale)
                    , 0 //* (1 / Time.timeScale)
                    )
                );*/
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<BicycleController>().Acident = 1;
                //コントローラーを振動させる
                OpenXRInput.SetControllerVibration(0.1f);
                //bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().velocity = BicycleSpeedTemp;
            }
            if ((EventNumber == 6 || EventNumber == 7) && Acident == 1 && gamedirector.GetComponent<GameDirector>().GoalFlag == 0)
            {
                //this.gameObject.transform.position = bicycle_afteracident.transform.Find("Bicycle/HeadTarget").position;
                //bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().AddForce(new Vector3(-15f * 15f, 0, 0));
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<BicycleController>().Acident = 2;

                //自転車が倒れるようにする
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;

                //コントローラーを振動させる
                //OVRInput.SetControllerVibration(frequency: 0.1f, amplitude: 0.1f);
                //bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().velocity = BicycleSpeedTemp;
                //コントローラーの振動を止める
                //OVRInput.SetControllerVibration(0, 0);
            }
            
        }
        //轢かれてから一定時間経過
        if (AcidentProgress == 2)
        {
            //不快音をストップ
            audioSource.Stop();

            //色の補完
            dieflashcolor = Color.Lerp(dieflashcolor, Color.clear, Time.deltaTime);
            dieflash.GetComponent<Renderer>().material.color = dieflashcolor;
            dieflash.SetActive(true);
            //画面を暗転
            blackoutcolor = Color.Lerp(blackoutcolor, new Color(0f, 0f, 0f, 1f), Time.deltaTime);
            blackout.GetComponent<Renderer>().material.color = blackoutcolor;

            if (dieflashimage != null)
            {
                //ある程度暗くなったら起動
                if (blackoutcolor.a >= 254f / 255f)
                {
                    dieflash.SetActive(false);

                    blackoutcolor = new Color(0f, 0f, 0f, 1f);
                    blackout.GetComponent<Renderer>().material.color = blackoutcolor;

                    //走馬灯と黒画面を消す
                    blackout.SetActive(false);

                    //VRIKをオフ
                    human.gameObject.GetComponent<VRIK>().enabled = false;
                    human.gameObject.GetComponent<FullBodyBipedIK>().enabled = false;
                    human.gameObject.GetComponent<GrounderFBBIK>().enabled = false;

                    //OVRPlayerControllerのコンポーネントをオフ
                    OpenXRScene.SetPlayerLocomotionEnabled(false);

                    //ずれたカメラを元に戻す
                    OpenXRScene.CameraOffset.position = this.gameObject.transform.position;

                    //次のゲームステップへ
                    AcidentProgress = 4;
                    
                }
            }
        }
        //ゴールした時
        if (AcidentProgress == 3)
        {
            //画面を暗転
            blackout.SetActive(true);
            blackoutcolor = Color.Lerp(blackoutcolor, new Color(0f, 0f, 0f, 1f), Time.deltaTime);
            blackout.GetComponent<Renderer>().material.color = blackoutcolor;

            //ある程度暗くなったら起動
            if (blackoutcolor.a >= 254f / 255f)
            {
                blackoutcolor = new Color(0f, 0f, 0f, 1f);
                blackout.GetComponent<Renderer>().material.color = blackoutcolor;

                //タイトルに戻る
                buttongroupe_main.SetActive(true);
                this.gamedirector.GetComponent<CSVPrinter>().CSVPrint(); //CSVに保存
                button_title.GetComponent<ButtonOption>().BackTitle();
            }
        }
        //3人称視点
        if(AcidentProgress == 4)
        {
            AcidentTime += Time.deltaTime;
            //視点を頭の上に固定
            if(Acident == 0)
            {
                //事故を起こしていない時の参照元を少し変える
                this.transform.position = human.transform.position + new Vector3(0, 6f, 0);
            }
            else
            {
                this.transform.position = head.transform.position + new Vector3(0, 6f, 0);
            }
            this.transform.eulerAngles = new Vector3(90,0,0);


            //規定秒数経過
            if (AcidentTime >= 6f)
            {
                AcidentProgress = 5;
                AcidentTime = 0;
                if(Acident == 0)
                {
                    if (resultPresenter != null)
                        resultPresenter.ReturnToMenu();
                    else
                        ReturnWithLegacyButton();
                }
                else
                {
                    acidentdetaillight.SetActive(true);
                    if (gamedirector.GetComponent<GameDirector>().SkyTime == 1)
                        acidentdetaillight.GetComponent<Light>().intensity = 2.5f;
                    if (resultPresenter != null)
                    {
                        resultPresenter.Show(EventNumber);
                    }
                    else
                    {
                        for (var index = 0; index < AcidentDetailList.Length; index++)
                            AcidentDetailList[index].SetActive(index == EventNumber);
                        acidentdetailcount.SetActive(true);
                        acidentdetailcount_text = acidentdetailcount.GetComponent<Text>();
                        legacyResultVisible = true;
                    }
                }
            }
        }
        if (legacyResultVisible)
        {
            AcidentTime += Time.unscaledDeltaTime;
            if (acidentdetailcount_text != null)
                acidentdetailcount_text.text = "残り " + Mathf.Floor(60f - AcidentTime) + " 秒";
            if (OpenXRInput.PrimaryButtonDown || AcidentTime >= 60f)
                ReturnWithLegacyButton();
        }
    }

    void ReturnWithLegacyButton()
    {
        legacyResultVisible = false;
        Time.timeScale = 1f;
        buttongroupe_main.SetActive(true);
        gamedirector.GetComponent<CSVPrinter>().CSVPrint();
        button_title.GetComponent<ButtonOption>().BackTitle();
    }

    void BlackOutSetNonActive()
    {
        //最初は走馬灯エフェクトを消しておく
        if (dieflashimageroot != null)
        {
            dieflashimageroot.SetActive(false);
        }
        if (blackoutimageroot != null)
        {
            blackoutimageroot.SetActive(false);
        }
        if (this.dieflash != null)
        {
            blackout.GetComponent<Renderer>().material.color = new Color(0, 0, 0, 0);
            dieflash.SetActive(false);
            blackout.SetActive(false);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // The title scene only uses this component to preview the configured sound.
        // Accident-only objects such as DieImage do not exist there, so do not run
        // the gameplay initialization path in that scene.
        audioSource = GetComponent<AudioSource>();
        if (SceneManager.GetActiveScene().name == SceneRoute.MetaTitle)
        {
            centereye = OpenXRScene.MainCamera != null
                ? OpenXRScene.MainCamera.gameObject
                : null;
            trackingspace = OpenXRScene.CameraOffset != null
                ? OpenXRScene.CameraOffset.gameObject
                : null;
            canvas_main = GameObject.Find("Canvas_MainButton");
            buttongroupe_main = canvas_main != null
                ? canvas_main.transform.Find("ButtonGroupe_Main")?.gameObject
                : null;
            gamedirector = GameObject.Find("GameDirector");
            AcidentProgress = 0;
            return;
        }

        //ゲームオブジェクトを格納
        this.centereye = OpenXRScene.MainCamera.gameObject;
        this.trackingspace = OpenXRScene.CameraOffset.gameObject;
        if (dieflashimageroot != null)
        {
            this.dieflashimage = dieflashimageroot.GetComponent<Image>();
        }
        if (blackoutimageroot != null)
        {
            this.blackoutimage = blackoutimageroot.GetComponent<Image>();
        }

        if (head == null || canvas_main == null || buttongroupe_main == null ||
            button_title == null || gamedirector == null || dieflash == null || blackout == null ||
            human == null || invisiblewallcontainer == null || bicycle_afteracident == null ||
            acidentdetailset == null)
        {
            Debug.LogError("CenterEyeCamera gameplay references are incomplete.", this);
            enabled = false;
            return;
        }
        if (this.dieflash != null)
        {
            this.dieflashcolor = dieflash.GetComponent<Renderer>().material.color;
            this.blackoutcolor = blackout.GetComponent<Renderer>().material.color;
        }
        this.ovrplayer = OpenXRScene.PlayerObject;
        if(this.acidentdetailset != null)
        {
            this.acidentdetailecontainer = acidentdetailset.transform.Find("AcidentDetailContainer").gameObject;
            this.acidentdetaillight = acidentdetailset.transform.Find("AcidentDetailLight").gameObject;
            this.acidentdetailcount = acidentdetailset.transform.Find("Count").gameObject;
            //リストに格納
            this.AcidentDetailList = new GameObject[acidentdetailecontainer.transform.childCount];
            for (var i = 0; i < AcidentDetailList.Length; ++i)
            {
                AcidentDetailList[i] = acidentdetailecontainer.transform.GetChild(i).gameObject;
            }
        }      
        /*//残り時間のテキストを格納
        for (var i = 0; i < AcidentDetailList.Length; ++i)
        {
            if (i == EventNumber)
            {
                AcidentDetailList[i].SetActive(true);
                this.acidentdetailcount_text = AcidentDetailList[i].transform.Find("Count").gameObject.GetComponent<Text>();
            }
        }*/

        //自転車用初期値
        BycicleAcidentCarHit = 0;

        //音を大きくする(3倍くらい)
        mixer.SetFloat("HighSound", 10f);

        //フラグを設定
        Acident = 0;
        //時間を設定
        AcidentTime = 0;
        AcidentProgress = 0;
        DieFlashTime = 0;
        DieStep = 0;
        DieNoiseTimeStep = 0;

        DOVirtual.Color(
                from: new Color(0, 0, 0, 1), //Tween開始時の値
                to: new Color(0, 0, 0, 0), //終了時の値
                duration: 0.5f,//Tween時間
                               //値が変わった時の処理
                onVirtualUpdate: (tweenValue) =>
                {
                    blackout.GetComponent<Renderer>().material.color = tweenValue;
                }
                //tweenが終わった時
            ).OnComplete(BlackOutSetNonActive);

        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (externalAccidentPresentation)
        {
            if (mixer != null)
                mixer.SetFloat("HighSound", 0f);
            return;
        }

        if(Acident == 1 && AcidentProgress <= 3)
        {
            //事故を起こした後の視界を変化
            this.transform.position = head.transform.position 
                + head.transform.up * 0;
            this.transform.rotation = this.head.transform.rotation;

            
        }

        //自転車で車に近づくも事故を起こさずに時間経過
        if(Acident == 0 && AcidentProgress == 2 && (EventNumber == 6 || EventNumber == 7))
        {
            //人を自転車に追従させる
            this.gameObject.transform.position = bicycle_afteracident.transform.Find("Bicycle/HeadTarget").position;
        }

        //アクシデントタイマー
        AcidentStep();

        //事故から規定秒数経過で次のゲームステップへ
        if (AcidentTime >= 3f && AcidentProgress == 1)
        {
            //時間をもとに戻す
            Time.timeScale = 1f;
            AcidentTime = 0f;

            //ラグドールを解除(ひき逃げ時は解除しない)
            if (EventNumber != 5)
            {
                foreach (var rigid in human.GetComponentsInChildren<Rigidbody>())
                {
                    rigid.isKinematic = true;
                    rigid.detectCollisions = false;
                }
            }
            if(EventNumber == 5)
            {
                //ひき逃げの時だけ3人称の時間を少し減らす
                AcidentTime = 4f;
            }

            AcidentProgress = 2;

            // 演出終了時
            //this.gameObject.GetComponent<OVRManager>().usePositionTracking = true;
        }

        /*//時間の進みによってピッチを変える
        if (audioSource != null && Time.timeScale > 0f)
        {
            audioSource.pitch = 1 / Time.timeScale;
        }*/
        mixer.SetFloat("HighSound", 0f); // ピッチを1倍に保つ

    }

    void FixedUpdate()
    {
        //自転車にAddForceを加えるためのもの(事故車とぶつかった場合のみ)
        if (AcidentProgress == 1)
        {
            //自転車かつ、ぶつかるまで
            if ((EventNumber == 6 || EventNumber == 7) && Acident == 0 && gamedirector.GetComponent<GameDirector>().GoalFlag == 0 && DieFlash && BycicleAcidentCarHit == 1)
            {
                //自転車を勝手に動かす
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().AddForce(
                    new Vector3(
                    -10f * 15f * 4f //* (1 / Time.timeScale)
                    , 0 //* (1 / Time.timeScale)
                    , 0 //* (1 / Time.timeScale)
                    )
                );
            }
            //自転車かつ、ぶつかるまで(走馬灯なし)
            if ((EventNumber == 6 || EventNumber == 7) && Acident == 0 && gamedirector.GetComponent<GameDirector>().GoalFlag == 0 && !DieFlash && BycicleAcidentCarHit == 1)
            {
                //人を自転車に追従させる
                this.gameObject.transform.position = bicycle_afteracident.transform.Find("Bicycle/HeadTarget").position;
                //自転車を勝手に動かす
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().AddForce(
                    new Vector3(
                    -10f * 15f * 4f //* (1 / Time.timeScale)
                    , 0 //* (1 / Time.timeScale)
                    , 0 //* (1 / Time.timeScale)
                    )
                );
                bicycle_afteracident.transform.Find("Bicycle").GetComponent<BicycleController>().Acident = 1;
                //コントローラーを振動させる
                OpenXRInput.SetControllerVibration(0.1f);
                //bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().velocity = BicycleSpeedTemp;
            }
        }
    }
}
