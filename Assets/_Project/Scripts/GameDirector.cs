using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using RootMotion.FinalIK; // 名前空間をインポート
using System;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Unity.XR.CoreUtils;
/*using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;*/

public class GameDirector : MonoBehaviour
{
    //Rigidbodyを格納する変数
    Rigidbody rigid;

    //ゲームオブジェクトを格納する変数
    [Header("Scene references")]
    [SerializeField] GameObject human;
    GameObject humanbody;
    GameObject humanneck;
    GameObject ovrplayer;
    GameObject ovrcamera;
    GameObject centereye;
    [SerializeField] GameObject canvas_main;
    [SerializeField] GameObject buttongroupe_main;
    [SerializeField] GameObject bicycle;
    [SerializeField] GameObject smartphonecontainer;
    [SerializeField] GameObject directionallight;

    //スクリプトを格納する変数
    CSVPrinter csvprinter;

    //シーン切り替え時に引き継ぐ変数(タイトルのゲームディレクターから受け取る)
    public int EventNumber;
    public int Height;
    public int Weight;
    public bool DieFlash;
    public int Gender;
    public int Age;
    public int License;
    public float Hz;
    public int SmartPhone;
    public int Incident;
    public int Weather;
    public int SkyTime;

    //走馬灯フラグ用の変数
    public int DieFlashNumber;

    //現在スピードを取得
    Vector3 CurrentSpeed;

    //CSVに登録する間隔を司る変数群
    float time;
    float TimeSpan;

    //フラグを格納する変数
    public int GoalFlag; //ゴールから受け取る

    /*//レコーダーの設定用変数
    private RecorderController recorderController;
    private RecorderControllerSettings recorderSettings;*/

    //フラグ管理
    [SerializeField] int HumanMove;
    [SerializeField] int Acident;

    [SerializeField] ScenarioRuntime scenarios;
    [SerializeField] GameplayFlowController flow;
    [SerializeField] AvatarPresenter avatarPresenter;

    //車のIDを保管(車工場に渡す)
    public int CarID;

    //昼と夜と天気のスカイボックス
    public Material DaySky;
    public Material NightSky;
    public Material DayRain;
    public Material NightRain;

    //キャンバスをオンオフするメソッド
    public void CanvasOnOff()
    {
        if (buttongroupe_main.activeInHierarchy)
        {
            buttongroupe_main.SetActive(false);
        }
        else if (!buttongroupe_main.activeInHierarchy)
        {
            buttongroupe_main.SetActive(true);
        }
    }

    /*//レコーダーの設定メソッド
    void RecorderSetting()
    {
        *//*// 設定の生成
        var setting = ScriptableObject.CreateInstance<RecorderControllerSettings>();

        // ScriptableObjectの生成
        var movieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        var audioRecorderSettings = ScriptableObject.CreateInstance<AudioRecorderSettings>();

        // 動画の記録設定
        movieRecorderSettings.FrameRate = 30f;
        movieRecorderSettings.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = 640,
            OutputHeight = 480,
        };
        // エンコード周りはIEncoderSettingというinterfaceで管理されていて、特定の設定のテンプレートが派生クラスとして用意されている
        movieRecorderSettings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Medium,
        };
        movieRecorderSettings.OutputFile = "Assets/Movies/VRTraficMovie";
        movieRecorderSettings.Enabled = true;

        // 音声の記録設定
        audioRecorderSettings.OutputFile = Application.temporaryCachePath;
        audioRecorderSettings.Enabled = true;

        setting.AddRecorderSettings(movieRecorderSettings);
        setting.AddRecorderSettings(audioRecorderSettings);

        recorderController = new RecorderController(setting);*//*
    }

    // 録画開始メソッド
    public void StartRecording()
    {
        if (recorderController != null && !recorderController.IsRecording())  // 録画中でない場合
        {
            recorderController.PrepareRecording();  // 録画準備
            recorderController.StartRecording();   // 録画開始
            Debug.Log("録画開始！");
        }
        else
        {
            Debug.Log("すでに録画中です。");
        }
    }

    // 録画停止メソッド
    public void StopRecording()
    {
        if (recorderController != null && recorderController.IsRecording())  // 録画中の場合
        {
            recorderController.StopRecording();   // 録画停止
            Debug.Log("録画停止！");
        }
        else
        {
            Debug.Log("録画が開始されていません。");
        }
    }*/
    
    //車を勝手に動かすメソッド
    public void CarAutoMove()
    {
        var vehicles = FindObjectsByType<CarController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (vehicles.Length == 0)
        {
            Debug.LogWarning("No active vehicle is available for manual movement.", this);
            return;
        }

        // Accident vehicles are pooled at runtime, so the old unassigned Car1
        // reference could never reliably identify the button's target. Prefer the
        // active accident vehicle and retain a fallback for diagnostic scenes.
        CarController target = null;
        foreach (var vehicle in vehicles)
        {
            if (vehicle.AcidentCar)
            {
                target = vehicle;
                break;
            }
        }
        (target != null ? target : vehicles[0]).CarAutoFlagSet();
    }

    //動くフラグのリセット
    void MoveFlagReset()
    {
        HumanMove = 0;

        //オブジェクトの動くフラグもリセット
        human.GetComponent<HumanController>().HumanFlagSet = 0;
        //car1.GetComponent<CarController>().CarFlagSet = 0;

        // CameraSub was a desktop-only debug camera. OpenXR keeps Main Camera active.
    }

    //少し遅れてテレポート
    IEnumerator LateRespawn()
    {
        yield return null;
        var characterController = ovrplayer.GetComponent<CharacterController>();
        var xrOrigin = OpenXRScene.Origin;
        var trackedCamera = OpenXRScene.MainCamera;

        if (characterController == null || xrOrigin == null || trackedCamera == null ||
            scenarios == null || scenarios.Active == null)
        {
            Debug.LogError("Cannot place the OpenXR player: the origin, camera, spawn, or goal is missing.");
            yield break;
        }

        OpenXRScene.SetPlayerLocomotionEnabled(false);
        // The CharacterController remains grounded in every mode. Disabling
        // gravity for bicycle scenarios allowed the whole XR rig to drift over
        // small height changes in the road.
        OpenXRScene.SetPlayerGravityEnabled(true);

        var playerActor = ovrplayer.GetComponent<PlayerActor>();
        if (playerActor != null)
            playerActor.ConfigureProfile(Height, Weight);

        // The selected profile now owns both the first-person collision capsule
        // and the third-person accident body. Uniform scaling avoids the old
        // stretched silhouette, while mass is distributed over the ragdoll.
        if (avatarPresenter != null)
            avatarPresenter.ConfigureBodyProfile(Height, Weight);
        else
            human.transform.localScale = Vector3.one * (0.27f * Height / 170f);

        //CSV用の時間の初期値
        this.time = 0;
        this.TimeSpan = 6f / 60f; //6フレームに一回

        var spawn = scenarios.Active.playerSpawn;
        var goal = scenarios.Active.goal.transform;
        var spawnXZ = new Vector3(spawn.position.x, 0f, spawn.position.z);
        var goalForward = Vector3.ProjectOnPlane(goal.position - spawn.position, Vector3.up);
        if (goalForward.sqrMagnitude < 0.001f)
            goalForward = Vector3.ProjectOnPlane(spawn.forward, Vector3.up);
        goalForward.Normalize();

        if (!TryFindGroundHeight(spawn.position, xrOrigin.transform, out var groundHeight))
        {
            groundHeight = 0f;
            Debug.LogWarning($"No ground was found below {spawn.name}; using world Y=0.");
        }

        // Rotate around the tracked camera, rather than assuming the Quest HMD's
        // local yaw is zero. This makes the user's real forward face the goal.
        xrOrigin.MatchOriginUpCameraForward(Vector3.up, goalForward);

        //自転車用のVRIK
        if (IsBicycleEvent(EventNumber))
        {
            bicycle.SetActive(true);
            // The old hierarchy placed the bicycle under Camera Offset. Detach it
            // while positioning so tracked-head movement cannot change its height.
            bicycle.transform.SetParent(null, true);
            AlignBicycleToDirection(bicycle.transform, goalForward);

            var headTarget = bicycle.transform.Find("HeadTarget");
            var hipTarget = bicycle.transform.Find("HipTarget");
            if (headTarget == null || hipTarget == null)
            {
                Debug.LogError("Bicycle IK targets are missing.");
                OpenXRScene.SetPlayerLocomotionEnabled(true);
                yield break;
            }

            bicycle.transform.position += spawnXZ -
                new Vector3(headTarget.position.x, 0f, headTarget.position.z);
            AlignBicycleWheelsToGround(bicycle.transform, groundHeight);
            ConfigureBicycleIK(hipTarget);
            AlignHumanRootToSeat(hipTarget, goalForward);

            // Seat height and bicycle targets determine the eye height. The Main
            // Camera remains fully owned by OpenXR; only XROrigin is moved.
            xrOrigin.MoveCameraToWorldLocation(headTarget.position);

            // The XR CharacterController owns locomotion. Keep the bicycle as a
            // kinematic visual child during riding; a dynamic Rigidbody under a
            // moving XR Origin lagged, tilted and fought the player controller.
            var bicycleController = bicycle.GetComponent<BicycleController>();
            if (bicycleController != null)
                bicycleController.BeginMetaRide(xrOrigin.transform);
            else
                bicycle.transform.SetParent(xrOrigin.transform, true);

            // A bicycle uses seat and pedal targets instead of foot grounding.
            human.GetComponent<GrounderFBBIK>().enabled = false;
            OpenXRScene.SetPlayerMinimumAcceleration(0.1f);
        }
        else
        {
            var trackedEyeHeight = Vector3.Dot(
                trackedCamera.transform.position - xrOrigin.transform.position, Vector3.up);
            if (trackedEyeHeight < 0.5f || trackedEyeHeight > 2.5f)
                trackedEyeHeight = Mathf.Clamp(Height * 0.01f, 1.2f, 2.1f);

            var desiredEyePosition = new Vector3(
                spawn.position.x, groundHeight + trackedEyeHeight, spawn.position.z);
            xrOrigin.MoveCameraToWorldLocation(desiredEyePosition);

            human.transform.rotation = Quaternion.LookRotation(goalForward, Vector3.up);
            human.transform.position = new Vector3(
                trackedCamera.transform.position.x, human.transform.position.y,
                trackedCamera.transform.position.z);
            AlignHumanFeetToGround(groundHeight);
            human.GetComponent<GrounderFBBIK>().enabled = true;
        }

        if (TrafficAccidentState.IsFrozen)
        {
            OpenXRScene.SetPlayerLocomotionEnabled(false);
        }
        else
        {
            OpenXRScene.SetPlayerLocomotionEnabled(true);
            if (avatarPresenter != null)
                avatarPresenter.EnterFirstPersonMode();
        }
    }

    private bool IsBicycleEvent(int eventNumber)
    {
        if (scenarios != null && scenarios.Active != null)
            return scenarios.Active.PlayerMode == ScenarioPlayerMode.Bicycle;
        return eventNumber == 6 || eventNumber == 7 || eventNumber == 9;
    }

    private bool TryFindGroundHeight(Vector3 spawnPosition, Transform xrOriginTransform, out float groundHeight)
    {
        var rayOrigin = new Vector3(spawnPosition.x, spawnPosition.y + 10f, spawnPosition.z);
        var hits = Physics.RaycastAll(
            rayOrigin, Vector3.down, 30f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(xrOriginTransform) ||
                hitTransform.IsChildOf(human.transform) ||
                (bicycle != null && hitTransform.IsChildOf(bicycle.transform)))
                continue;
            if (hit.collider.GetComponentInParent<GroundSurface>() == null)
                continue;

            groundHeight = hit.point.y;
            return true;
        }

        groundHeight = 0f;
        return false;
    }

    private static void AlignBicycleToDirection(Transform bicycleTransform, Vector3 desiredForward)
    {
        var hipTarget = bicycleTransform.Find("HipTarget");
        var headTarget = bicycleTransform.Find("HeadTarget");
        var currentForward = hipTarget != null && headTarget != null
            ? Vector3.ProjectOnPlane(headTarget.position - hipTarget.position, Vector3.up)
            : Vector3.ProjectOnPlane(-bicycleTransform.right, Vector3.up);

        if (currentForward.sqrMagnitude < 0.001f)
            return;

        var correction = Quaternion.FromToRotation(currentForward.normalized, desiredForward);
        bicycleTransform.rotation = correction * bicycleTransform.rotation;
    }

    private static void AlignBicycleWheelsToGround(Transform bicycleTransform, float groundHeight)
    {
        var colliders = bicycleTransform.GetComponentsInChildren<Collider>(true);
        var foundBounds = false;
        var lowestPoint = float.PositiveInfinity;

        foreach (var bikeCollider in colliders)
        {
            if (!bikeCollider.enabled || bikeCollider.isTrigger)
                continue;

            lowestPoint = Mathf.Min(lowestPoint, bikeCollider.bounds.min.y);
            foundBounds = true;
        }

        if (foundBounds)
            bicycleTransform.position += Vector3.up * (groundHeight - lowestPoint);
        else
            Debug.LogWarning("The bicycle has no enabled solid collider for ground alignment.");
    }

    private void ConfigureBicycleIK(Transform hipTarget)
    {
        var solver = human.GetComponent<VRIK>().solver;
        solver.rightArm.target = bicycle.transform.Find("RightHandTarget");
        solver.leftArm.target = bicycle.transform.Find("LeftHandTarget");
        solver.rightLeg.target = bicycle.transform.Find("RightLegTarget");
        solver.leftLeg.target = bicycle.transform.Find("LeftLegTarget");
        solver.spine.pelvisTarget = hipTarget;
        solver.spine.pelvisPositionWeight = 1f;
        solver.spine.pelvisRotationWeight = 1f;

        var fullBodySolver = human.GetComponent<FullBodyBipedIK>().solver;
        fullBodySolver.bodyEffector.target = hipTarget;
        fullBodySolver.bodyEffector.positionWeight = 1f;
    }

    private void AlignHumanRootToSeat(Transform hipTarget, Vector3 forward)
    {
        var vrik = human.GetComponent<VRIK>();
        human.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        if (vrik.references.pelvis != null)
            human.transform.position += hipTarget.position - vrik.references.pelvis.position;
    }

    private void AlignHumanFeetToGround(float groundHeight)
    {
        var references = human.GetComponent<VRIK>().references;
        if (references.leftFoot == null || references.rightFoot == null)
            return;

        var lowestFoot = Mathf.Min(references.leftFoot.position.y, references.rightFoot.position.y);
        human.transform.position += Vector3.up * (groundHeight - lowestFoot);
    }

    // Start is called before the first frame update
    void Start()
    {
        //Rigidbodyを取得
        this.rigid = this.GetComponent<Rigidbody>();

        //ゲームオブジェクトを格納
        var humanRig = human != null ? human.GetComponent<VRIK>() : null;
        if (humanRig == null || humanRig.references.pelvis == null || humanRig.references.head == null)
        {
            Debug.LogError("GameDirector Human VRIK references are incomplete.", this);
            enabled = false;
            return;
        }
        this.humanbody = humanRig.references.pelvis.gameObject;
        this.humanneck = humanRig.references.head.parent != null
            ? humanRig.references.head.parent.gameObject
            : humanRig.references.head.gameObject;
        this.ovrcamera = OpenXRScene.CameraControllerObject;
        this.ovrplayer = OpenXRScene.PlayerObject;
        this.centereye = OpenXRScene.MainCamera.gameObject;

        if (canvas_main == null || buttongroupe_main == null ||
            bicycle == null || smartphonecontainer == null ||
            directionallight == null || scenarios == null)
        {
            Debug.LogError("GameDirector scene references are incomplete.", this);
            enabled = false;
            return;
        }

        //スクリプトを格納
        this.csvprinter = this.gameObject.GetComponent<CSVPrinter>();

        /*//レコーダーのセッティング
        this.RecorderSetting();*/

        //フラグの数値を格納
        MoveFlagReset();

        // OpenXR uses a single Main Camera and AudioListener.
        human.gameObject.GetComponent<VRIK>().enabled = true;
        centereye.GetComponent<AudioListener>().enabled = true;

        //マウスの移動量の係数を格納
        //MouseCoefficient = 2.0f;

        //初期位置に移動
        //this.gameObject.transform.position = new Vector3(42f,2f,15f);

        //初期を人間の視点に
        this.HumanMove = 0;

        //最初はCanvasを消しておく
        this.buttongroupe_main.SetActive(false);

        //イベントナンバーをセット
        ovrcamera.GetComponent<CenterEyeCamera>().EventNumber = EventNumber;

        //ゴールフラグをセット
        this.GoalFlag = 0;

        //車IDの初期値をセット
        this.CarID = 0;

        //走馬灯をセット
        if(DieFlashNumber == 0)
        {
            DieFlash = false;
        }
        else
        {
            DieFlash = true;
        }
        ovrcamera.GetComponent<CenterEyeCamera>().DieFlash = this.DieFlash;

        if (scenarios == null || !scenarios.Activate(EventNumber))
        {
            Debug.LogError($"Scenario {EventNumber} is not configured.", this);
            enabled = false;
            return;
        }
        if (flow == null)
            flow = GetComponent<GameplayFlowController>();
        if (flow != null)
            flow.BeginScenario(EventNumber);

        //centereyeに音をセットする
        ovrcamera.GetComponent<CenterEyeCamera>().DieSoundSet(Hz);

        //スマホを置くか置かないか
        if (SmartPhone == 1)
        {
            smartphonecontainer.SetActive(true);
            if (!SceneRoute.IsMetaScene(SceneManager.GetActiveScene().name))
            {
                //スマホを右手に固定させる
                //VRIKの中身を取得
                var solver = human.GetComponent<VRIK>().solver;
                //IKの対象を変える
                solver.rightArm.target = smartphonecontainer.transform.Find("RightHandTarget");
            }
        }
        else
        {
            smartphonecontainer.SetActive(false);
        }

        if (SceneRoute.IsMetaScene(SceneManager.GetActiveScene().name))
        {
            var phonePresentation = GetComponent<SmartPhoneDistractionPresenter>();
            if (phonePresentation == null)
                phonePresentation = gameObject.AddComponent<SmartPhoneDistractionPresenter>();
            phonePresentation.Configure(
                OpenXRScene.MainCamera,
                smartphonecontainer,
                SmartPhone == 1);
        }

        

        ApplyEnvironmentLighting();

        //歩きスマホの焦点を合わせる


        //リスポーンコルーチン
        StartCoroutine("LateRespawn");
    }

    /// <summary>
    /// Configures one realtime sun for every time/weather combination. Keeping
    /// the sun active gives static buildings direct lighting on Quest without
    /// requiring a separate baked lightmap for each environment preset.
    /// </summary>
    private void ApplyEnvironmentLighting()
    {
        var sun = directionallight != null ? directionallight.GetComponent<Light>() : null;
        if (sun == null)
        {
            Debug.LogWarning("Directional Light is missing; environment lighting was not applied.");
            return;
        }

        var isNight = SkyTime == 1;
        var isRaining = Weather == 1;
        var rain = transform.Find("Rain");
        if (rain != null)
            rain.gameObject.SetActive(isRaining);

        directionallight.SetActive(true);
        sun.shadows = LightShadows.Hard;
        sun.shadowStrength = 0.7f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1f;

        if (isNight)
        {
            RenderSettings.skybox = isRaining ? NightRain : NightSky;
            RenderSettings.ambientSkyColor = isRaining
                ? new Color32(42, 48, 72, 255)
                : new Color32(48, 55, 88, 255);
            RenderSettings.ambientEquatorColor = new Color32(30, 35, 55, 255);
            RenderSettings.ambientGroundColor = new Color32(18, 21, 34, 255);
            sun.color = new Color32(92, 108, 168, 255);
            sun.intensity = isRaining ? 0.15f : 0.25f;

            //喧騒音を切る
            GetComponent<AudioSource>().clip = null;
        }
        else
        {
            RenderSettings.skybox = isRaining ? DayRain : DaySky;
            RenderSettings.ambientSkyColor = isRaining
                ? new Color32(150, 164, 180, 255)
                : new Color32(184, 205, 228, 255);
            RenderSettings.ambientEquatorColor = isRaining
                ? new Color32(112, 124, 138, 255)
                : new Color32(137, 154, 174, 255);
            RenderSettings.ambientGroundColor = isRaining
                ? new Color32(72, 78, 88, 255)
                : new Color32(88, 94, 105, 255);
            sun.color = isRaining
                ? new Color32(196, 207, 219, 255)
                : new Color32(255, 244, 214, 255);
            sun.intensity = isRaining ? 0.7f : 1.2f;
            directionallight.transform.rotation = Quaternion.Euler(50f, 60f, 0f);
        }

        // A modest sky reflection is cheaper than a realtime Reflection Probe
        // and prevents URP Lit windows and smooth facades from looking flat.
        RenderSettings.reflectionIntensity = 0.5f;

        // Runtime skybox replacement does not refresh the ambient probe and
        // default reflection cubemap until the environment is explicitly updated.
        DynamicGI.UpdateEnvironment();
    }

    //ポーズ中は動かないように
    void FixedUpdate()
    {
        //ovrcameraのaccidentフラグを格納
        this.Acident = this.ovrcamera.GetComponent<CenterEyeCamera>().Acident; 

        /*//Nキーでキューブのオブジェクトにカメラ移動するか切り替え
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (CameraPhisicFollow == 0)
            {
                MoveFlagReset();
                CameraPhisicFollow = 1;
                //カメラ物体の回転に合わせる
                ovrcamera.gameObject.transform.localRotation = cameraphisic.transform.localRotation;
            }
            else if (CameraPhisicFollow == 1)
            {
                CameraPhisicFollow = 0;
            }
        }*/

        // CameraSub desktop debug movement is intentionally disabled in OpenXR.

        /*//Cキーで車のオブジェクトにカメラ移動
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (Car1Move == 0)
            {
                MoveFlagReset();
                Car1Move = 1;
                //車の回転に合わせる
                ovrcamera.gameObject.transform.localRotation = car1.transform.localRotation;
            }
            else if (Car1Move == 1)
            {
                Car1Move = 0;
            }
        }*/

        /*//Hキーで人のオブジェクトにカメラ移動
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (HumanMove == 0)
            {
                MoveFlagReset();
                HumanMove = 1;
                human.GetComponent<PlayerMovement>().HumanFlagSet = 1;
                //人の回転に合わせる
                ovrcamera.gameObject.transform.localRotation = human.transform.localRotation;
                //VRIKをオン
                human.gameObject.GetComponent<VRIK>().enabled = true;
                centereye.GetComponent<AudioListener>().enabled = true;

                //VRカメラに切り替え
                camerasub.GetComponent<Camera>().depth = -10;
                camerasub.GetComponent<AudioListener>().enabled = false;
            }
            else if (HumanMove == 1)
            {
                HumanMove = 0;
                human.GetComponent<PlayerMovement>().HumanFlagSet = 0;
                //VRIKをオフ
                human.gameObject.GetComponent<VRIK>().enabled = false;
                centereye.GetComponent<AudioListener>().enabled = false;

                //サブカメラに切り替え
                camerasub.GetComponent<Camera>().depth = 10;
                camerasub.GetComponent<AudioListener>().enabled = true;
            }
        }*/

        /*//Yキーで人のオブジェクトにカメラ移動し、車を操作
        if (Input.GetKeyDown(KeyCode.Y))
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
        }*/

        // CameraSub desktop debug follow is intentionally disabled in OpenXR.
        /*if (Car1Move == 1)
        {
            //視点の位置調整
            ovrcamera.gameObject.transform.position =
                car1.transform.position
                + car1.transform.forward * 0.20f
                + car1.transform.up * 0.65f
                + car1.transform.right * 0.4f;

            //視点移動
            Look();

            // 右左折時に視点を車の回転に追従させる
            if (Input.GetKey(KeyCode.A))
            {
                _yRotation -= 0.5f; // 左回転を_yRotationに反映
            }
            if (Input.GetKey(KeyCode.D))
            {
                _yRotation += 0.5f; // 右回転を_yRotationに反映
            }

        }*/
        //人に視点追従
        if (HumanMove == 1)
        {
            //視点の位置調整
            /*ovrcamera.gameObject.transform.position =
                humanneck.transform.position
                + humanneck.transform.forward * -0.15f
                + humanneck.transform.up * 0.10f;*/
            /*//指定のキーを押すと力がかかる
            if (Input.GetKey(KeyCode.W))
            {
                ovrcamera.transform.position += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 0.1f; // 前方向に移動
            }
            if (Input.GetKey(KeyCode.S))
            {
                ovrcamera.transform.position += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * -0.1f; // 後ろ方向に移動
            }
            if (Input.GetKey(KeyCode.A))
            {
                ovrcamera.transform.position += transform.right * -0.1f; // ローカル右方向に基づいて左へ移動
            }
            if (Input.GetKey(KeyCode.D))
            {
                ovrcamera.transform.position += transform.right * 0.1f; // ローカル右方向に基づいて右へ移動
            }*/

            //視点移動
            //Look();

            //視点に合わせて物体も回転
            //human.transform.localRotation = Quaternion.Euler(0, _yRotation, 0);
            /*this.gameObject.transform.localRotation = humanbody.
                transform.GetChild(0).gameObject. //spineを取得
                transform.GetChild(0).gameObject. //chestを取得
                transform.GetChild(0).gameObject. //neckを取得
                transform.localRotation;*/
            //ovrcamera.gameObject.transform.eulerAngles = humanneck.transform.eulerAngles;
        }
        //CSV出力用の時間を進める
        this.time += Time.deltaTime;
        if (time >= TimeSpan)
        {
            //時間をリセット
            time = 0;

            //ここにCSV出力用のものを書く(CenterEyeCameraのポジションとローテーション、時間)
            csvprinter.HumanDataReciever(centereye.transform.position, 
                centereye.transform.eulerAngles);

        }
    }
    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Debug.Log("リセット");
            // 事故演出のスローモーション中にリロードしても次シーンに引き継がない
            Time.timeScale = 1f;
            ScenarioRetry.Reload(this);
        }

        // 左手Y / 右手B ボタン押下
        if(OpenXRInput.SecondaryButtonDown)
        {
            this.CanvasOnOff();
        }

        // Recover the XR Origin if the whole rig falls below the environment.
        // Never write to the tracked Main Camera transform directly.
        var xrOrigin = OpenXRScene.Origin;
        var trackedCamera = OpenXRScene.MainCamera;
        if (xrOrigin != null && trackedCamera != null && trackedCamera.transform.position.y < -0.25f)
        {
            var cameraPosition = trackedCamera.transform.position;
            if (!TryFindGroundHeight(cameraPosition, xrOrigin.transform, out var groundHeight))
                groundHeight = 0f;

            var trackedEyeHeight = Vector3.Dot(
                cameraPosition - xrOrigin.transform.position, Vector3.up);
            trackedEyeHeight = Mathf.Clamp(trackedEyeHeight, 1.2f, 2.1f);
            xrOrigin.MoveCameraToWorldLocation(new Vector3(
                cameraPosition.x, groundHeight + trackedEyeHeight, cameraPosition.z));

            if (!IsBicycleEvent(EventNumber))
                AlignHumanFeetToGround(groundHeight);
        }
    }
}
