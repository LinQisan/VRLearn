using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK; // 名前空間をインポート

public class BicycleCrash : MonoBehaviour
{
    bool accidentStarted;
    //ゲームオブジェクトを格納する変数
    GameObject ovrcamera;
    GameObject ovrplayer;
    GameObject bicycle_afteracident;
    GameObject human;
    GameObject gamedirector;

    //音を鳴らすための変数
    //音声自体は直接設定
    AudioSource audioSource;
    public AudioClip Crash;
    public AudioClip Bell;

    //縁石に自転車が当たった
    void OnTriggerEnter(Collider collider)
    {
        bool isTrackedPlayer = PlayerActor.TryResolve(collider, out var playerActor);
        bool isLegacyHuman = collider.CompareTag("Human")
            && collider.name == "Human"
            && collider.transform.parent == null;
        bool isBicycle = collider.name == "Bicycle"
            && collider.transform.parent != null
            && collider.transform.parent.name == "BicycleContainer";
        var cameraPresentation = ovrcamera != null
            ? ovrcamera.GetComponent<CenterEyeCamera>()
            : null;
        if ((isTrackedPlayer || isLegacyHuman || isBicycle)
            && !accidentStarted
            && cameraPresentation != null
            && cameraPresentation.Acident == 0)
        {
            var hybrid = OpenXRScene.PlayerObject != null
                ? OpenXRScene.PlayerObject.GetComponent<HybridAccidentPresentation>()
                : null;
            if (hybrid != null)
            {
                var flow = gamedirector != null
                    ? gamedirector.GetComponent<GameplayFlowController>()
                    : null;
                if (flow != null && !flow.TryTriggerAccident())
                    return;

                accidentStarted = true;
                if (playerActor == null)
                    playerActor = FindFirstObjectByType<PlayerActor>();
                var bicycle = FindFirstObjectByType<BicycleController>();
                float speed = bicycle != null && bicycle.TryGetComponent<Rigidbody>(out var bicycleBody)
                    ? bicycleBody.linearVelocity.magnitude
                    : 0f;
                Vector3 impactDirection = Vector3.ProjectOnPlane(
                    collider.transform.position - transform.position,
                    Vector3.up);
                if (impactDirection.sqrMagnitude < 0.001f)
                    impactDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                Vector3 impactPoint = playerActor != null
                    ? playerActor.transform.position
                    : collider.transform.position;
                var vehicleController = collider.GetComponentInParent<CarController>();
                var impactVehicle = vehicleController != null
                    ? vehicleController.transform
                    : collider.attachedRigidbody != null
                        ? collider.attachedRigidbody.transform
                        : collider.transform;
                if (!hybrid.BeginImpact(
                        playerActor,
                        impactPoint,
                        impactDirection,
                        speed,
                        impactVehicle))
                    hybrid.ForceResults();
                if (audioSource != null)
                {
                    if (Crash != null)
                        audioSource.PlayOneShot(Crash);
                    if (Bell != null)
                        audioSource.PlayOneShot(Bell);
                }
                return;
            }

            accidentStarted = true;
            //アニメーションとVRIKを切って人をぐにゃぐにゃにする
            human.gameObject.GetComponent<Animator>().enabled = false;
            human.gameObject.GetComponent<HumanController>().SetVisible(true);
            human.gameObject.GetComponent<VRIK>().enabled = false;
            human.gameObject.GetComponent<FullBodyBipedIK>().enabled = false;
            human.gameObject.GetComponent<GrounderFBBIK>().enabled = false;

            //事故フラグをオン
            ovrcamera.GetComponent<CenterEyeCamera>().Acident = 1;

            //人のTriggerColliderを消す
            human.gameObject.GetComponent<BoxCollider>().enabled = false;
            human.gameObject.GetComponent<CharacterController>().enabled = false;

            //OVRPlayerControllerのコンポーネントをオフ
            OpenXRScene.SetPlayerLocomotionEnabled(false);

            //ずれたカメラを元に戻す
            OpenXRScene.CameraOffset.position = ovrcamera.transform.position;

            //親を変える
            ovrcamera.transform.Find("BicycleContainer/Bicycle").SetParent(GameObject.Find("Bicycle_AfterAcident").transform);

            bicycle_afteracident.transform.Find("Bicycle").GetComponent<BicycleController>().Acident = 2;
            //自転車が倒れるようにする
            bicycle_afteracident.transform.Find("Bicycle").GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;

            /*StartCoroutine("Wait");
            //自転車を倒す
            Vector3 dir = transform.position - collider.transform.position;

            // Z軸方向のみに力を加える
            Vector3 forceDir = new Vector3(0, 0, dir.z).normalized;

            bicycle_afteracident.transform.Find("Bicycle").gameObject.GetComponent<Rigidbody>().AddForce(forceDir * 20000f);*/


            // 自転車のローカル横方向にトルクを与えて倒す
            Vector3 localRight = bicycle_afteracident.transform.Find("Bicycle").transform.right;

            float torqueAmount;

            if (bicycle_afteracident.transform.Find("Bicycle").position.z - transform.position.z > 0)
            {
                //自転車が右から当たる時
                torqueAmount = -20f;
            }
            else
            {
                //自転車が左から当たる時
                torqueAmount = 20f;
            }
            
            bicycle_afteracident.transform.Find("Bicycle").gameObject.GetComponent<Rigidbody>().AddTorque(localRight * torqueAmount, ForceMode.Impulse);

            //車の生産をやめる
            GameObject.Destroy(GameObject.Find("CarFactory_Acident_Right"));
            GameObject.Destroy(GameObject.Find("CarFactory_Acident_Left"));

            audioSource.PlayOneShot(Crash);
            audioSource.PlayOneShot(Bell);

            //スマホ機能がオン
            if (gamedirector.GetComponent<GameDirector>().SmartPhone == 1)
            {
                //スマホを消す
                GameObject.Find("SmartPhoneContainer").gameObject.SetActive(false);
            }

            //ゲーム終了メソッド
            StartCoroutine("AcidentProgressOn");
        }
    }

    //少し遅延させる
    IEnumerator Wait()
    {
        yield return null;
    }

    //ゲームを終了させる
    IEnumerator AcidentProgressOn()
    {
        yield return new WaitForSeconds(4f);
        ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress = 2;
    }

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        this.ovrcamera = OpenXRScene.CameraControllerObject;
        this.ovrplayer = OpenXRScene.PlayerObject;
        this.bicycle_afteracident = GameObject.Find("Bicycle_AfterAcident");
        this.human = GameObject.Find("Human");
        this.gamedirector = GameObject.Find("GameDirector");

        //音を格納
        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
