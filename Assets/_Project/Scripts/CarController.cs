using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK; // ���O��Ԃ��C���|�[�g
using System;
using DG.Tweening;
using UnityEngine.Audio;

public class CarController : MonoBehaviour
{
    //Rigidbody���i�[����ϐ�
    Rigidbody rigid;

    //�͂��i�[����ϐ�
    Vector3 CarForce;
    Vector3 CarTorque;

    //�Q�[���I�u�W�F�N�g���i�[����ϐ�
    GameObject ovrcamera;
    GameObject ovrplayer;
    GameObject NextCar;
    GameObject human;
    GameObject gamedirector;
    MetaVehicleImpactSensor metaImpactSensor;

    //�X�N���v�g���i�[����ϐ�
    CSVPrinter csvprinter;

    //�Ԃ̌o�H�ړ��̂��߂̕ϐ�
    public Transform pointsParent;
    int pointsChildIndex;
    [SerializeField] Transform[] points;
    private int destPoint = 0;
    private const float WaypointReachDistance = 0.5f;
    private const float WaypointTurnSpeed = 120f;
    private const float WaypointAcceleration = 8f;
    private const float ImpactBrakingDeceleration = 14f;
    private const float MaximumImpactCoastDistance = 1.2f;
    private float waypointSpeed;
    bool impactCoasting;
    Vector3 impactCoastStartPosition;
    Vector3 impactCoastDirection;
    float impactCoastDistance;
    float impactCoastDuration;
    float impactCoastStartedAt;
    [Header("Road grounding")]
    [SerializeField] bool keepVehicleOnRoad = true;
    [SerializeField, Min(0f)] float roadClearance = 0.02f;
    [SerializeField, Min(0.1f)] float groundProbeHeight = 3f;
    [SerializeField, Min(0.1f)] float groundProbeDistance = 10f;
    [SerializeField] LayerMask drivableSurfaceMask = ~0;
    readonly RaycastHit[] groundHits = new RaycastHit[16];
    float pivotToVehicleBottom;
    public float CarSpace;
    [SerializeField] float Distance;
    float MaxDistance;

    // Pool-reuse bookkeeping. Prefab-authored values are captured on first
    // Start; per-life spawn configuration comes from the factory after Get;
    // everything else below is runtime mutable state restored on reuse.
    //
    // Speed baseline: all four vehicle prefabs author Speed=0 and no spawner
    // ever writes Speed, so Start applies the established 10 m/s fallback and
    // captures it here. The field default mirrors that fallback so that
    // InitializeForSpawn (which runs before the deferred Start on new
    // instances) already restores the correct value.
    float initialSpawnSpeed = 10f;
    float initialCarSoundDb;
    Tweener activeSpeedTween;
    Coroutine agentStopBootRoutine;
    Coroutine carSpaceFixRoutine;
    readonly List<(Collider vehicleCollider, Collider otherCollider)> ignoredCollisionPairs = new();

    //���݃X�s�[�h���擾
    [SerializeField] Vector3 CurrentSpeed;

    //�Ԃ̃X�s�[�h���i�[����ϐ�
    [SerializeField] float MaxSpeed;
    public float Speed;

    public float ImpactVehicleMassKg
    {
        get
        {
            if (rigid == null)
                rigid = GetComponent<Rigidbody>();
            return rigid != null
                ? Mathf.Clamp(rigid.mass, 80f, 3500f)
                : AccidentImpactPhysics.DefaultVehicleMassKg;
        }
    }
    public bool IsImpactCoasting => impactCoasting;

    //��������t���O��ݒ�
    [SerializeField] int Car1Move;

    //�����œ����t���O
    [SerializeField] int CarAutoMove;

    //�����t���O���������\�b�h
    public int CarFlagSet { set { Car1Move = value; } }

    void OnEnable()
    {
        if (TrafficAccidentState.IsFrozen)
        {
            FreezeForAccident();
            return;
        }
        // Per-spawn initialization is driven explicitly by the factory via
        // InitializeForSpawn after spawn configuration; nothing is scheduled
        // here so new and reused instances share one timing-independent path.
    }

    /// <summary>
    /// Explicit second phase of spawning. The factory must call this
    /// synchronously after writing CarID/pointsParent/AcidentCar (and
    /// orientation). Runs identically for brand-new and pooled instances, so
    /// first-use and reuse observe the same spawn configuration.
    /// </summary>
    public void InitializeForSpawn()
    {
        var history = GetComponent<AccidentMotionHistory>();
        if (history == null) history = gameObject.AddComponent<AccidentMotionHistory>();
        history.ResetHistory();
        var wheelVisuals = GetComponent<VehicleWheelVisuals>();
        if (wheelVisuals == null) wheelVisuals = gameObject.AddComponent<VehicleWheelVisuals>();
        wheelVisuals.ResetVisuals();
        var damage = GetComponent<AccidentVehicleDamage>();
        if (damage == null) damage = gameObject.AddComponent<AccidentVehicleDamage>();
        damage.ResetDamage();
        impactCoasting = false;
        OnAcident = false;
        destPoint = 0;
        waypointSpeed = 0f;
        CarSpace = 1f;
        Speed = initialSpawnSpeed;
        AcidentCarNumber = AcidentCar ? 1 : 0;
        Car1Move = 0;
        CarAutoMove = 0;
        Distance = 0f;
        time = 0f;
        activeSpeedTween = null;
        agentStopBootRoutine = null;
        carSpaceFixRoutine = null;
        if (ovrcamera != null)
        {
            var eye = ovrcamera.GetComponent<CenterEyeCamera>();
            if (eye != null)
                EventNumber = eye.EventNumber;
        }
        if (mixer != null)
            mixer.SetFloat("CarSound", initialCarSoundDb);
        if (rigid != null)
        {
            if (!rigid.isKinematic)
            {
                rigid.linearVelocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
            }
            rigid.isKinematic = true;
            rigid.interpolation = RigidbodyInterpolation.Interpolate;
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pivotToVehicleBottom = CalculatePivotToVehicleBottom();
            Physics.SyncTransforms();
            SnapToRoad();
        }
        // Previous life's IgnoreCollision pairs were restored by
        // PrepareForPoolReuse on the way into the pool; establish this life's.
        ConfigureMetaImpactDetection();
        LoadWaypointPath();
    }

    // Defensive fallback only: the normal path is VehiclePool.Release calling
    // PrepareForPoolReuse explicitly before deactivation. This also covers
    // behaviour-level disables (route-load failure) and scene teardown, where
    // no Release happens but tweens/coroutines/ignores must still stop.
    // The method is idempotent, so explicit + fallback double-invokes are safe.
    void OnDisable()
    {
        PrepareForPoolReuse();
    }

    /// <summary>
    /// Explicit end-of-life cleanup, driven by VehiclePool.Release before the
    /// object is deactivated and pooled. Stops this life's tweens/coroutines
    /// and restores every IgnoreCollision pair it established.
    /// </summary>
    public void PrepareForPoolReuse()
    {
        if (activeSpeedTween != null)
        {
            activeSpeedTween.Kill();
            activeSpeedTween = null;
        }
        if (agentStopBootRoutine != null)
        {
            StopCoroutine(agentStopBootRoutine);
            agentStopBootRoutine = null;
        }
        if (carSpaceFixRoutine != null)
        {
            StopCoroutine(carSpaceFixRoutine);
            carSpaceFixRoutine = null;
        }
        RestoreIgnoredCollisions();
        if (rigid != null && !rigid.isKinematic)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }
    }

    void RestoreIgnoredCollisions()
    {
        for (int i = 0; i < ignoredCollisionPairs.Count; i++)
        {
            var pair = ignoredCollisionPairs[i];
            if (pair.vehicleCollider != null && pair.otherCollider != null)
                Physics.IgnoreCollision(pair.vehicleCollider, pair.otherCollider, false);
        }
        ignoredCollisionPairs.Clear();
    }

    bool LoadWaypointPath()
    {
        if (pointsParent == null)
        {
            Debug.LogError($"{name} has no waypoint path.", this);
            enabled = false;
            return false;
        }

        var authoredPath = pointsParent.GetComponent<WaypointPath>();
        if (authoredPath != null)
        {
            // Defensive copy: the authored route definition is shared by every
            // vehicle on this lane. Per-vehicle edits (e.g. the player waypoint
            // below) must never leak into other vehicles or future spawns.
            var sharedPoints = authoredPath.Points;
            points = new Transform[sharedPoints.Length];
            Array.Copy(sharedPoints, points, sharedPoints.Length);
        }
        else
        {
            pointsChildIndex = 0;
            points = new Transform[pointsParent.childCount];
            foreach (Transform pointsChild in pointsParent)
                points[pointsChildIndex++] = pointsChild;
        }

        if (points == null || points.Length == 0)
        {
            Debug.LogError($"{name} waypoint path is empty.", this);
            enabled = false;
            return false;
        }
        return true;
    }

    //���������̎Ԃ��ǂ���
    public bool AcidentCar;
    int AcidentCarNumber;
    //���̂��N���������ǂ���
    public bool OnAcident;

    //����炷���߂̕ϐ�
    //�������̂͒��ڐݒ�
    AudioSource audioSource;
    public AudioClip AcidentCarSound;
    public AudioClip AcidentHuman;
    public AudioClip Acidentbreak;
    public AudioClip DieNoise;
    public AudioMixer mixer;

    //���Y���Ԃ�\��ID(�Ԃ̍H�ꂩ��󂯎��)
    public int CarID;

    //CSV�ɓo�^����Ԋu���i��ϐ��Q
    float time;
    float TimeSpan;

    //�C�x���g�i���o�[���i�[����ϐ�
    int EventNumber;

    //�Ԃ𓮂����t���O���I���ɂ��郁�\�b�h
    public void CarAutoFlagSet()
    {
        if (CarAutoMove == 0)
        {
            CarAutoMove = 1;
        }
        else if (CarAutoMove == 1)
        {
            CarAutoMove = 0;
        }
    }

    //�G�[�W�F���g���~�߂郁�\�b�h���N�����郁�\�b�h
    IEnumerator AgentStopBoot()
    {
        //�{���l�b�g�ɂ̂�����悤�ɒ���
        yield return new WaitForSeconds(0.09f);
        AgentStop();
        agentStopBootRoutine = null;
    }

    //�G�[�W�F���g���~�߂郁�\�b�h
    public void AgentStop()
    {
        //�Ђ��������͎~�߂Ȃ�
        if (true)
        {
            //�L�l�}�e�B�b�N���O��
            rigid.isKinematic = false;

            //�l��������΂����悤�ɂ��邽�߁A�����Ԃɗ͂�������
            if (EventNumber == 6 || EventNumber == 7)
            {
                //�|���Z�̍Ō�̐����́A�K��
                rigid.AddForce(new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 15.0f * this.rigid.mass * 20f);
            }
            
            OnAcident = true;
            
        }
        
        //�Ђ�����
        if (EventNumber == 5)
        {
            //CarAutoMove = 1;
            //�����ߗp�̃R���C�_�[���o��
            gamedirector.transform.Find("CarWallContainer").gameObject.SetActive(true);
            rigid.linearVelocity = new Vector3(-70f / 3.6f , rigid.linearVelocity.y, rigid.linearVelocity.z);
        }
    }

    // Trigger volumes are used for traffic controls and the legacy accident
    // flow. Meta vehicles also have a much larger DieFlashArea trigger, so a
    // tracked player entering a trigger is not necessarily a real impact.
    void OnTriggerEnter(Collider collider)
    {
        HandleTriggerEnter(collider, false);
    }

    // A solid vehicle/player contact is always a confirmed impact. Keeping the
    // accident entry point here also prevents the warning volume from being
    // mistaken for the vehicle body.
    void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        if (IsAccidentTarget(collision.collider))
            HandleTriggerEnter(collision.collider, true);
    }

    void HandleTriggerEnter(Collider collider, bool confirmedPhysicalContact)
    {
        var isTrackedPlayer = PlayerActor.TryResolve(collider, out var playerActor);
        var isLegacyHuman = collider.CompareTag("Human");
        bool isAccidentTarget = isTrackedPlayer
            || isLegacyHuman
            || IsBicycleCollider(collider);
        var hybridPresentation = GetHybridAccidentPresentation();

        // Meta uses a dedicated body-sized sensor. Its large warning trigger
        // must never start the accident by itself.
        if (hybridPresentation != null
            && isAccidentTarget
            && !confirmedPhysicalContact)
        {
            return;
        }

        if (isAccidentTarget
            && !OnAcident
            && this.gameObject.transform.parent == null)
        {
            if (playerActor == null && IsBicycleCollider(collider))
                playerActor = FindFirstObjectByType<PlayerActor>();
            var flow = gamedirector != null
                ? gamedirector.GetComponent<GameplayFlowController>()
                : null;
            // Capture actual route velocity BEFORE the phase lock zeroes it.
            float impactSpeed = Mathf.Max(0f, waypointSpeed);
            if (rigid != null && !rigid.isKinematic)
                impactSpeed = rigid.linearVelocity.magnitude;
            if (flow != null && !flow.TryTriggerAccident())
                return;

            Debug.Log("��������");
            Debug.Log(collider.gameObject.name);
            var presenter = human != null ? human.GetComponent<AvatarPresenter>() : null;
            Vector3 impactDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 impactPoint = playerActor != null
                ? playerActor.transform.position
                : collider.transform.position;

            // Meta owns the complete accident transaction. Lock the vehicle
            // before starting any visual work, then leave the legacy branch
            // entirely. Missing legacy IK components can no longer prevent the
            // accident flag or vehicle stop from being applied.
            if (hybridPresentation != null)
            {
                OnAcident = true;
                bool started = hybridPresentation.BeginImpact(
                    playerActor,
                    impactPoint,
                    impactDirection,
                    impactSpeed,
                    transform);
                if (!started)
                {
                    Debug.LogError("Meta accident presentation could not start.", hybridPresentation);
                    hybridPresentation.ForceResults();
                }

                if (audioSource != null)
                {
                    audioSource.volume = 1f;
                    if (AcidentHuman != null)
                        audioSource.PlayOneShot(AcidentHuman);
                    if (EventNumber == 6 && Acidentbreak != null)
                        audioSource.PlayOneShot(Acidentbreak);
                }
                if (mixer != null)
                    mixer.SetFloat("CarSound", 10f);
                return;
            }

            bool usesHybridPresentation = hybridPresentation != null
                && hybridPresentation.BeginImpact(
                    playerActor,
                    impactPoint,
                    impactDirection,
                    impactSpeed);
            if (!usesHybridPresentation && presenter != null)
                presenter.ShowForAccident(playerActor);
            //�A�j���[�V������VRIK��؂��Đl�����ɂႮ�ɂ�ɂ���
            human.gameObject.GetComponent<Animator>().enabled = false;
            human.gameObject.GetComponent<HumanController>().SetVisible(true);
            human.gameObject.GetComponent<VRIK>().enabled = false;
            human.gameObject.GetComponent<FullBodyBipedIK>().enabled = false;
            human.gameObject.GetComponent<GrounderFBBIK>().enabled = false;

            //���̃t���O���I��
            ovrcamera.GetComponent<CenterEyeCamera>().Acident = 1;

            //�Ԃɏ���ɗ͂�������̂���߂�
            //CarAutoMove = 0;

            //�l��TriggerCollider������
            var humanTrigger = human.gameObject.GetComponent<BoxCollider>();
            if (humanTrigger != null)
                humanTrigger.enabled = false;
            var humanCharacter = human.gameObject.GetComponent<CharacterController>();
            if (humanCharacter != null)
                humanCharacter.enabled = false;

            //OVRPlayerController�̃R���|�[�l���g���I�t
            OpenXRScene.SetPlayerLocomotionEnabled(false);

            //���ꂽ�J���������ɖ߂�
            if (!usesHybridPresentation && OpenXRScene.CameraOffset != null)
                OpenXRScene.CameraOffset.position = ovrcamera.transform.position;

            OnAcident = true;

            
            

            //�������]�Ԃ𓮂���
            if(EventNumber == 7)
            {
                var bicycleObject = GameplaySceneContext.Instance != null
                    ? GameplaySceneContext.Instance.Bicycle
                    : GameObject.Find("Bicycle");
                Rigidbody rigidbicycle = bicycleObject != null
                    ? bicycleObject.GetComponent<Rigidbody>()
                    : null;
                if (rigidbicycle != null)
                {
                    rigidbicycle.constraints = RigidbodyConstraints.None;
                    rigidbicycle.linearVelocity = new Vector3(0, 0, 0);
                    rigidbicycle.AddForce(new Vector3(-150f * 15f, 0, 300f * 20f));
                }
            }

            //�����x��ăG�[�W�F���g���~�߂�
            if (usesHybridPresentation)
            {
                // Waypoint motion stops on this frame. Keep hit-and-run scenario 5
                // moving; every other scenario holds the vehicle at its impact pose.
                if (EventNumber != 5 && rigid != null)
                {
                    if (!rigid.isKinematic)
                    {
                        rigid.linearVelocity = Vector3.zero;
                        rigid.angularVelocity = Vector3.zero;
                    }
                    rigid.isKinematic = true;
                }
                else if (EventNumber == 5 && rigid != null)
                {
                    rigid.isKinematic = false;
                    rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rigid.linearVelocity = impactDirection * (55f / 3.6f);
                }
            }
            else
            {
                agentStopBootRoutine = StartCoroutine(AgentStopBoot());
            }

            //瀂�������炷
            audioSource.volume = 1f;

            audioSource.PlayOneShot(AcidentHuman);
            if (EventNumber == 6)
            {
                audioSource.PlayOneShot(Acidentbreak);
            }
            
            //����傫������(3�{���炢)
            mixer.SetFloat("CarSound", 10f);

        }

        //���̎Ԃ��ʏ�Ԃɓ����������͒ʏ�Ԃ�����
        if(collider.gameObject.tag == "Car" && AcidentCar)
        {
            var otherController = collider.GetComponentInParent<CarController>();
            var otherVehicle = otherController != null ? otherController.gameObject : collider.gameObject;
            if (!VehiclePool.Instance.Release(otherVehicle))
                Destroy(otherVehicle);
            //�Ԋԋ������J�����̂ŁA�X�s�[�h��߂�
            carSpaceFixRoutine = StartCoroutine(CarSpaceFix());
        }

        //���̎ԗp�̃E�F�C�|�C���g�ł͉��������Ȃ�
        if ((collider.gameObject.transform.parent != null && collider.gameObject.transform.parent.gameObject.name != "WayPointContainer_Acident") 
            || AcidentCar && this.gameObject.transform.parent == null)
        {
            //50km/h�܂ŉ���
            if (collider.gameObject.tag == "Acceleration")
            {
                if (activeSpeedTween != null)
                    activeSpeedTween.Kill();
                activeSpeedTween = DOVirtual.Float(
                    from: Speed,//Tween�J�n���̒l
                    to: 50f / 3.6f,//�I�����̒l
                    duration: 2.0f,//Tween����
                                   //�l���ς�������̏���
                    onVirtualUpdate: (tweenvalue) =>
                    {
                        //Debug.Log($"�l���ω� : {tweenvalue}");
                        Speed = tweenvalue;
                    }
                );
            }
            //10km/h�܂Ō���
            if (collider.gameObject.tag == "Slow")
            {
                Debug.Log("�X���[");

                if (activeSpeedTween != null)
                    activeSpeedTween.Kill();
                activeSpeedTween = DOVirtual.Float(
                    from: Speed,//Tween�J�n���̒l
                    to: 10f / 3.6f,//�I�����̒l
                    duration: 2.0f,//Tween����
                                   //�l���ς�������̏���
                    onVirtualUpdate: (tweenvalue) =>
                    {
                        //Debug.Log($"�l���ω� : {tweenvalue}");
                        Speed = tweenvalue;
                    }
                );
            }
        }
    }
    //�ԂƂ̋����𑪂�
    void OnTriggerStay(Collider collider)
    {
        if(collider.gameObject.tag == "CarDistance")
        {
            //�O�̎Ԃ��擾
            NextCar = collider.gameObject.transform.parent.gameObject;

            Distance = Vector3.Distance(this.gameObject.transform.position, NextCar.transform.position);

            //�Ԋԋ�����ۂ�
            if(Distance <= MaxDistance)
            {
                CarSpace = (Distance - MaxDistance) / MaxDistance;
            }
            if(Distance > MaxDistance)
            {
                CarSpace = 1f;
            }
            //�߂��Ȃ������~
            if(Distance - MaxDistance <= 0f)
            {
                CarSpace = 0f;
            }
        }
    }

    //���n�����Đ����郁�\�b�h
    public void DieFlashStart(Vector3 CarPosition)
    {
        audioSource.PlayOneShot(AcidentCarSound);
        if(EventNumber != 5)
        {
            audioSource.PlayOneShot(Acidentbreak);
        }
        
        //audioSource.PlayOneShot(DieNoise);
        ovrcamera.GetComponent<CenterEyeCamera>().DieNoiseSounds();

        //���̎ԂƏՓ˂��Ă邩�ǂ���(���]�ԗp)
        if (AcidentCar)
        {
            ovrcamera.GetComponent<CenterEyeCamera>().BycicleAcidentCarHit = 1;
        }

        //�X�}�z�@�\���I��
        if (gamedirector.GetComponent<GameDirector>().SmartPhone == 1)
        {
            //�X�}�z������
            var smartPhone = GameplaySceneContext.Instance != null
                ? GameplaySceneContext.Instance.SmartPhone
                : GameObject.Find("SmartPhoneContainer");
            if (smartPhone != null)
                smartPhone.SetActive(false);
        }

        // Meta keeps head tracking active during the first-person warning.
        // The actual collision is presented by HybridAccidentPresentation;
        // starting the old cinematic here disabled tracking and moved/froze
        // the view before the car had reached the player.
        if (GetHybridAccidentPresentation() != null)
            return;
        

        // ���o�J�n��
        //ovrcamera.GetComponent<OVRManager>().usePositionTracking = false;

        //�����ŕ����Ƃ��ɁA�J�������ςȈʒu�֍s���̂�h��
        //ovrcamera.transform.position = ovrcamera.transform.Find("TrackingSpace").position;
        //ovrcamera.transform.Find("TrackingSpace").position = ovrcamera.transform.position;
        //Vector3 cameratemp = ovrcamera.transform.Find("TrackingSpace/CenterEyeAnchor").position;

        //OVRPlayerController�̃R���|�[�l���g���I�t
        OpenXRScene.SetPlayerLocomotionEnabled(false);

        // Freeze the tracked pose and keep the OpenXR Main Camera rendering while
        // CenterEyeCamera drives the accident cinematic.
        OpenXRScene.BeginCinematicCamera();
        var canvas = GameplaySceneContext.Instance != null
            ? GameplaySceneContext.Instance.GameplayCanvas
            : GameObject.Find("Canvas_MainButton");
        var dieFlashImage = canvas != null ? canvas.transform.Find("DieFlashImage") : null;
        if (dieFlashImage != null)
            dieFlashImage.position = ovrcamera.transform.position;
        OpenXRScene.SetControllersVisible(false);

        //�e��ς���
        var bicycle = GameplaySceneContext.Instance != null
            ? GameplaySceneContext.Instance.Bicycle
            : ovrcamera.transform.Find("BicycleContainer/Bicycle")?.gameObject;
        var bicycleAfterAccident = GameplaySceneContext.Instance != null
            ? GameplaySceneContext.Instance.BicycleAfterAccident
            : GameObject.Find("Bicycle_AfterAcident");
        if (bicycle != null && bicycleAfterAccident != null)
            bicycle.transform.SetParent(bicycleAfterAccident.transform);

        //���n��
        //ovrcamera.GetComponent<CenterEyeCamera>().AcidentAnimation(CarPosition);
        //�J�����̑��n���^�C�}�[���N��
        ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress = 1;
        //���]�Ԃ̑��x��ۑ�
        if(EventNumber == 6)
        {
            Rigidbody rigidbicycle = bicycle != null ? bicycle.GetComponent<Rigidbody>() : null;
            if (rigidbicycle != null)
            {
                ovrcamera.GetComponent<CenterEyeCamera>().BicycleSpeedTemp = rigidbicycle.linearVelocity;
                rigidbicycle.constraints = RigidbodyConstraints.None;
            }
        }

        //�ǌ���h��
        if(EventNumber == 6 || EventNumber == 7 || EventNumber == 9)
        {
            foreach (var factory in FindObjectsByType<CarFactory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (factory.name == "CarFactory_Acident_Right"
                    || factory.name == "CarFactory_Acident_Left")
                    factory.gameObject.SetActive(false);
            }
        }
    }
    
    //���n����A���ōĐ�(DieFlashController���Đ�)
    public void DieFlashProgress(Vector3 CarPosition)
    {
        if (GetHybridAccidentPresentation() != null)
            return;

        ovrcamera.GetComponent<CenterEyeCamera>().AcidentAnimation(this.transform.position);
    }

    HybridAccidentPresentation GetHybridAccidentPresentation()
    {
        return OpenXRScene.PlayerObject != null
            ? OpenXRScene.PlayerObject.GetComponent<HybridAccidentPresentation>()
            : null;
    }

    static bool IsAccidentTarget(Collider collider)
    {
        if (collider == null)
            return false;

        return PlayerActor.TryResolve(collider, out _)
            || collider.CompareTag("Human")
            || IsBicycleCollider(collider);
    }

    static bool IsBicycleCollider(Collider collider)
    {
        if (collider == null)
            return false;

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.name == "Bicycle")
                return true;
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// Called only by the body-sized Meta impact sensor. Warning volumes and
    /// traffic-control triggers continue through the legacy trigger path.
    /// </summary>
    public void NotifyMetaImpact(Collider collider)
    {
        if (!OnAcident
            && transform.parent == null
            && GetHybridAccidentPresentation() != null
            && IsAccidentTarget(collider))
            HandleTriggerEnter(collider, true);
    }

    public void FreezeForAccident()
    {
        impactCoasting = false;
        OnAcident = true;
        CarAutoMove = 0;
        waypointSpeed = 0f;
        CarSpace = 0f;
        if (rigid == null)
            rigid = GetComponent<Rigidbody>();
        if (rigid != null)
        {
            if (!rigid.isKinematic)
            {
                rigid.linearVelocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
            }
            rigid.isKinematic = true;
        }
    }

    /// <summary>
    /// Continues the striking vehicle for a short, bounded emergency-braking
    /// distance after the momentum exchange. The car remains kinematic so the
    /// pooled waypoint system and PhysX never compete for ownership.
    /// </summary>
    public void BeginAccidentResponse(AccidentImpactPhysics impact)
    {
        FreezeForAccident();
        if (rigid == null)
            return;

        float postImpactSpeed = impact.VehiclePostImpactSpeedMetersPerSecond;
        float physicalStoppingDistance = postImpactSpeed * postImpactSpeed
            / (2f * ImpactBrakingDeceleration);
        impactCoastDistance = Mathf.Min(
            MaximumImpactCoastDistance,
            physicalStoppingDistance);
        if (impactCoastDistance <= 0.01f || postImpactSpeed <= 0.05f)
            return;

        impactCoastDirection = Vector3.ProjectOnPlane(impact.Direction, Vector3.up);
        if (impactCoastDirection.sqrMagnitude < 0.001f)
            impactCoastDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (impactCoastDirection.sqrMagnitude < 0.001f)
            impactCoastDirection = Vector3.forward;
        impactCoastDirection.Normalize();
        impactCoastStartPosition = rigid.position;
        impactCoastDuration = Mathf.Clamp(
            2f * impactCoastDistance / postImpactSpeed,
            0.12f,
            0.65f);
        impactCoastStartedAt = Time.realtimeSinceStartup;
        impactCoasting = true;
    }

    void UpdateAccidentResponse()
    {
        if (!impactCoasting || rigid == null)
            return;

        float progress = Mathf.Clamp01(
            (Time.realtimeSinceStartup - impactCoastStartedAt)
            / Mathf.Max(0.01f, impactCoastDuration));
        // Constant-deceleration displacement: s / sMax = 2t - t^2.
        float distanceProgress = 2f * progress - progress * progress;
        Vector3 nextPosition = impactCoastStartPosition
            + impactCoastDirection * (impactCoastDistance * distanceProgress);
        if (keepVehicleOnRoad && TryGetRoadY(nextPosition, out float roadY))
            nextPosition.y = roadY + pivotToVehicleBottom + roadClearance;
        rigid.MovePosition(nextPosition);

        if (progress >= 1f)
        {
            impactCoasting = false;
            if (!rigid.isKinematic)
            {
                rigid.linearVelocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
            }
        }
    }

    void ConfigureMetaImpactDetection()
    {
        if (GetHybridAccidentPresentation() == null)
            return;

        var player = FindFirstObjectByType<PlayerActor>();
        var playerCollider = player != null ? player.GetComponent<CharacterController>() : null;
        if (playerCollider == null)
        {
            Debug.LogError($"{name} could not configure Meta impact detection: PlayerActor collider is missing.", this);
            return;
        }

        // The CharacterController must not be physically pushed over the bonnet.
        // The dedicated trigger below owns accident detection instead.
        // Every ignored pair is recorded so the pool re-entry can restore it.
        foreach (var vehicleCollider in GetComponentsInChildren<Collider>(true))
        {
            if (vehicleCollider != null && !vehicleCollider.isTrigger)
            {
                Physics.IgnoreCollision(vehicleCollider, playerCollider, true);
                ignoredCollisionPairs.Add((vehicleCollider, playerCollider));
            }
        }

        var bicycle = GameplaySceneContext.Instance != null
            ? GameplaySceneContext.Instance.Bicycle
            : null;
        if (bicycle != null)
        {
            foreach (var vehicleCollider in GetComponentsInChildren<Collider>(true))
            foreach (var bicycleCollider in bicycle.GetComponentsInChildren<Collider>(true))
            {
                if (vehicleCollider != null
                    && bicycleCollider != null
                    && !vehicleCollider.isTrigger)
                {
                    Physics.IgnoreCollision(vehicleCollider, bicycleCollider, true);
                    ignoredCollisionPairs.Add((vehicleCollider, bicycleCollider));
                }
            }
        }

        metaImpactSensor = GetComponentInChildren<MetaVehicleImpactSensor>(true);
        if (metaImpactSensor == null)
        {
            var sensorObject = new GameObject("Meta_ImpactSensor");
            sensorObject.layer = gameObject.layer;
            sensorObject.transform.SetParent(transform, false);
            var sensorCollider = sensorObject.AddComponent<BoxCollider>();
            sensorCollider.isTrigger = true;
            CalculateMetaImpactBounds(out Vector3 sensorCenter, out Vector3 sensorSize);
            sensorCollider.center = sensorCenter;
            sensorCollider.size = sensorSize;
            metaImpactSensor = sensorObject.AddComponent<MetaVehicleImpactSensor>();
        }
        metaImpactSensor.Configure(this);
    }

    void CalculateMetaImpactBounds(out Vector3 center, out Vector3 size)
    {
        bool found = false;
        Bounds localBounds = default;
        foreach (var box in GetComponents<BoxCollider>())
        {
            if (box == null || box.isTrigger || !box.enabled)
                continue;

            var boxBounds = new Bounds(box.center, box.size);
            if (!found)
            {
                localBounds = boxBounds;
                found = true;
            }
            else
            {
                localBounds.Encapsulate(boxBounds.min);
                localBounds.Encapsulate(boxBounds.max);
            }
        }

        if (!found)
            localBounds = new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 1.5f, 4f));

        center = localBounds.center;
        size = localBounds.size + new Vector3(0.12f, 0.05f, 0.12f);
    }

    //���̎Ԃ̃X�s�[�h�𒼂�
    IEnumerator CarSpaceFix()
    {
        yield return null ;
        CarSpace = 1f;
        carSpaceFixRoutine = null;
    }

    // Follow the authored lane points directly. Rigidbody movement keeps trigger
    // callbacks working without a second pathfinding system controlling the car.
    void CarMoveRoute()
    {
        if (points == null || points.Length == 0)
        {
            return;
        }
        if (destPoint >= points.Length || points[destPoint] == null)
        {
            ReleaseAtRouteEnd();
            return;
        }

        Vector3 currentPosition = rigid.position;
        Vector3 targetPosition = points[destPoint].position;
        Vector3 toTarget = targetPosition - currentPosition;
        toTarget.y = 0f;
        float targetSpeed = Mathf.Max(0f, Speed * CarSpace);
        waypointSpeed = Mathf.MoveTowards(
            waypointSpeed,
            targetSpeed,
            WaypointAcceleration * Time.fixedDeltaTime);
        float moveDistance = waypointSpeed * Time.fixedDeltaTime;

        if (toTarget.sqrMagnitude <= WaypointReachDistance * WaypointReachDistance)
        {
            destPoint++;
            return;
        }

        Vector3 horizontalDirection = toTarget;
        if (horizontalDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
            rigid.MoveRotation(Quaternion.RotateTowards(
                rigid.rotation,
                targetRotation,
                WaypointTurnSpeed * Time.fixedDeltaTime));
        }

        Vector3 horizontalTarget = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, horizontalTarget, moveDistance);
        if (keepVehicleOnRoad && TryGetRoadY(nextPosition, out float roadY))
        {
            nextPosition.y = roadY + pivotToVehicleBottom + roadClearance;
        }

        rigid.MovePosition(nextPosition);
    }

    /// <summary>
    /// End-of-life for ordinary traffic that finished its route. The vehicle
    /// itself is the single owner of this release; DestroyArea covers physical
    /// road exits, and the pool guard makes whichever fires first win.
    /// Accident participants (including the event-5 hit-and-run car) are scene
    /// dressing for the presentation and must never be recycled here.
    /// </summary>
    void ReleaseAtRouteEnd()
    {
        if (OnAcident || impactCoasting)
            return;
        if (!VehiclePool.Instance.Release(gameObject))
            gameObject.SetActive(false);
    }

    // Waypoints define the lane in XZ only. The road collider owns vehicle height,
    // so incorrectly authored waypoint/factory Y values cannot make cars float.
    bool TryGetRoadY(Vector3 position, out float roadY)
    {
        Vector3 origin = position + Vector3.up * groundProbeHeight;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            groundHits,
            groundProbeHeight + groundProbeDistance,
            drivableSurfaceMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        roadY = 0f;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null
                || hitCollider.transform == transform
                || hitCollider.transform.IsChildOf(transform)
                || hitCollider.attachedRigidbody != null
                || hitCollider.GetComponentInParent<CharacterController>() != null
                || groundHits[i].normal.y < 0.5f)
            {
                continue;
            }

            if (groundHits[i].distance < nearestDistance)
            {
                nearestDistance = groundHits[i].distance;
                roadY = groundHits[i].point.y;
                found = true;
            }
        }
        return found;
    }

    float CalculatePivotToVehicleBottom()
    {
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Collider vehicleCollider in GetComponentsInChildren<Collider>(true))
        {
            if (vehicleCollider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = vehicleCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(vehicleCollider.bounds);
            }
        }

        if (!hasBounds)
        {
            foreach (Renderer vehicleRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = vehicleRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(vehicleRenderer.bounds);
                }
            }
        }

        return hasBounds ? transform.position.y - bounds.min.y : 0f;
    }

    void SnapToRoad()
    {
        if (!keepVehicleOnRoad || !TryGetRoadY(rigid.position, out float roadY))
        {
            return;
        }

        Vector3 groundedPosition = rigid.position;
        groundedPosition.y = roadY + pivotToVehicleBottom + roadClearance;
        rigid.position = groundedPosition;
    }

    // Start is called before the first frame update
    void Start()
    {
        //Rigidbody���擾
        this.rigid = this.GetComponent<Rigidbody>();
        rigid.isKinematic = true;
        rigid.interpolation = RigidbodyInterpolation.Interpolate;
        rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        pivotToVehicleBottom = CalculatePivotToVehicleBottom();
        Physics.SyncTransforms();
        SnapToRoad();

        //AudioComponent���擾
        audioSource = GetComponent<AudioSource>();

        //�t���O��ݒ�
        Car1Move = 0;
        CarAutoMove = 0;
        OnAcident = false;
        // AcidentCarNumber depends on the factory-injected AcidentCar flag and
        // is resolved in InitializeForSpawn; AcidentCar is still the prefab
        // default (false) at this point.

        //�͂�ݒ�
        this.CarForce = new Vector3(0.0f, 0.0f, 0.0f);
        this.CarTorque = new Vector3(0.0f, 0.0f, 0.0f);

        //�ő�X�s�[�h���Ƃ肠��������70km�ɕύX
        this.MaxSpeed = 70f / 3.6f; //velocity�̒P�ʂ�m/s�Ȃ̂ŁA70km/s��m/s�ɕϊ�

        //�Ԋԋ����̖ڈ�?
        MaxDistance = 3f;

        //�Q�[���I�u�W�F�N�g���i�[
        this.ovrcamera = OpenXRScene.CameraControllerObject;
        this.ovrplayer = OpenXRScene.PlayerObject;
        var context = GameplaySceneContext.Instance;
        this.gamedirector = context != null && context.Director != null
            ? context.Director.gameObject
            : FindFirstObjectByType<GameDirector>()?.gameObject;
        this.human = context != null && context.Human != null
            ? context.Human
            : FindFirstObjectByType<HumanController>()?.gameObject;
        if (gamedirector == null || human == null)
        {
            Debug.LogError($"{name} requires gameplay scene references.", this);
            enabled = false;
            return;
        }

        //�X�N���v�g���i�[
        this.csvprinter = gamedirector.GetComponent<CSVPrinter>();

        //�C�x���g�i���o�[���i�[
        this.EventNumber = ovrcamera.GetComponent<CenterEyeCamera>().EventNumber;

        // Route loading and the per-life impact-sensor setup run in
        // InitializeForSpawn after the factory finishes spawn configuration.
        // NOTE (verified 2026-09): the former Event-0 "points[2] = player"
        // hijack lived here but could never execute in any shipped build --
        // all four prefabs author AcidentCar=false while the factory injects
        // AcidentCar=true only after Get returns, so its condition was always
        // false at Start time. It was removed (not relocated) to preserve the
        // observed routes; the per-vehicle route copy already isolates any
        // future runtime waypoint edits.

        // Keep the established 10 m/s starting speed used by the vehicle prefabs.
        if (Speed <= 0f)
        {
            Speed = 10f;
        }
        // Prefab-authored spawn speed captured once; tweens may rewrite Speed
        // at runtime, but reuse must start from this value again.
        initialSpawnSpeed = Speed;
        if (mixer != null)
            mixer.GetFloat("CarSound", out initialCarSoundDb);
        waypointSpeed = 0f;
        CarSpace = 1f;

        //CSV�p�̎��Ԃ̏����l
        this.time = 0;
        this.TimeSpan = 6f / 60f; //6�t���[���Ɉ��

        //�Ђ������̕����ߗp�̃R���C�_�[������
        if (EventNumber == 5)
        {
            this.gameObject.transform.Find("HumanLockContainer").gameObject.SetActive(false);
        }
    }

    //�|�[�Y���͓����Ȃ��悤��
    void FixedUpdate()
    {
        if (TrafficAccidentState.IsFrozen)
        {
            if (impactCoasting)
            {
                UpdateAccidentResponse();
                return;
            }
            if (!OnAcident || (rigid != null && !rigid.isKinematic))
                FreezeForAccident();
            return;
        }

        //���������Ȃ���Ύ~�܂�
        //this.rigid.velocity = new Vector3(0.0f, this.rigid.velocity.y, 0.0f);

        this.CurrentSpeed = this.rigid.linearVelocity;

        this.CarForce = new Vector3(0.0f, 0.0f, 0.0f);
        this.CarTorque = new Vector3(0.0f, 0.0f, 0.0f);

        /*//���̂��L�[�ňړ����邩�̃t���O����
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Y))
        {
            if (Car1Move == 0)
            {
                Car1Move = 1;
            }
            else if (Car1Move == 1)
            {
                Car1Move = 0;
            }
        }

        //�Ԃ��������i
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (CarAutoMove == 0)
            {
                CarAutoMove = 1;
            }
            else if (CarAutoMove == 1)
            {
                CarAutoMove = 0;
            }
        }*/


        /*if (Car1Move == 1)
        {
            //�w��̃L�[�������Ɨ͂�������
            if (Input.GetKey(KeyCode.W))
            {
                //this.rigid.velocity += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 10.0f; // �O�����Ɉړ�
                this.CarForce += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 30.0f;
            }
            if (Input.GetKey(KeyCode.S))
            {
                //this.rigid.velocity += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * -10.0f; // �������Ɉړ�
                this.CarForce += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * -30.0f;
            }
            if (Input.GetKey(KeyCode.A))
            {
                //this.rigid.velocity += -transform.right * 5.0f; // ���[�J���E�����Ɋ�Â��č��ֈړ�
                //����
                this.gameObject.transform.localRotation *= Quaternion.Euler(0, -0.5f, 0);
                this.CarTorque += new Vector3(0, -0.5f, 0);
            }
            if (Input.GetKey(KeyCode.D))
            {
                //this.rigid.velocity += transform.right * 5.0f; // ���[�J���E�����Ɋ�Â��ĉE�ֈړ�
                //�E��
                this.gameObject.transform.localRotation *= Quaternion.Euler(0, 0.5f, 0);
                this.CarTorque += new Vector3(0, 0.5f, 0);
            }
        }*/

        //�Ԃ̎������i�A�u���[�L
        if (CarAutoMove == 1 && (EventNumber == 5))
        {
            this.CarForce += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 20.0f * this.rigid.mass;
        }
        /*else if (CarAutoMove == 0 && rigid.velocity.magnitude >= Mathf.Abs(0.01f)) //�x���Ȃ����猸������߂�
        {
            this.CarForce += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * -7.5f * this.rigid.mass;
        }*/
        //�͂�������
        if (!rigid.isKinematic && CarForce.sqrMagnitude > 0f)
            rigid.AddForce(CarForce);
        //���������琧��
        if (!rigid.isKinematic && rigid.linearVelocity.magnitude >= MaxSpeed)
        {
            rigid.linearVelocity = rigid.linearVelocity.normalized * MaxSpeed;
        }

        //rigid.AddTorque(CarTorque, ForceMode.Acceleration);

        //�Ԃ̌o�H�ړ�
        if (!OnAcident)
        {
            CarMoveRoute();
        }

        //�Ђ��������̃X�s�[�h
        if (OnAcident && EventNumber == 5 && !rigid.isKinematic)
        {
            rigid.linearVelocity = new Vector3(-55f / 3.6f, rigid.linearVelocity.y, rigid.linearVelocity.z);
        }

        //���ʒ���
        audioSource.volume = ((Speed) / (50f / 3.6f)) * 0.25f;

        //CSV�o�͗p�̎��Ԃ�i�߂�
        this.time += Time.deltaTime;
        if(time >= TimeSpan)
        {
            //���Ԃ����Z�b�g
            time = 0;

            //������CSV�o�͗p�̂��̂�����(�Ԃ̃|�W�V�����AID�A���̎Ԃ��ǂ����A����)
            //CSVPrinter�ɓn��
            csvprinter.CarDataReciever(this.transform.position , this.CarID , this.AcidentCarNumber);
        }

    }

    // Update is called once per frame
    void Update()
    {
        //�^�C���X�P�[���ɉ����ăs�b�`�𒲐�
        audioSource.pitch = Time.timeScale;
    }
}
