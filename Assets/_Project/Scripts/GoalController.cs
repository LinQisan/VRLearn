using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK; // ���O��Ԃ��C���|�[�g

public class GoalController : MonoBehaviour
{
    //�S�[���t���O
    bool GoalFlag;

    //�S�[���̉�
    AudioSource audioSource;
    public AudioClip GoalSound;

    //�Q�[���I�u�W�F�N�g���i�[����ϐ�
    GameObject ovrplayer;
    GameObject ovrcamera;
    GameObject gamedirector;
    GameplayFlowController flow;

    //�l�ɓ�������
    void OnTriggerEnter(Collider collider)
    {
        var isTrackedPlayer = PlayerActor.TryResolve(collider, out _);
        var isLegacyHuman = collider.CompareTag("Human")
            && collider.transform.parent == null
            && collider.name == "Human";
        var isBicycle = collider.name == "Bicycle";
        if ((isTrackedPlayer || isLegacyHuman || isBicycle)
            && !GoalFlag
            && ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress <= 1
            && ovrcamera.GetComponent<CenterEyeCamera>().Acident == 0)
        {
            if (flow != null && !flow.TryReachGoal())
                return;
            //Debug.Log("�p�[�e�B�N��");

            if(collider.gameObject.name == "Bicycle")
            {
                collider.gameObject.GetComponent<Rigidbody>().linearVelocity = new Vector3(0,0,0);
                collider.GetComponent<BicycleController>().Acident = 2;
            }
            else if (isTrackedPlayer)
            {
                var activeBicycle = FindFirstObjectByType<BicycleController>();
                if (activeBicycle != null && activeBicycle.gameObject.activeInHierarchy)
                    activeBicycle.Acident = 2;
            }

            //�p�[�e�B�N���Đ�
            gameObject.transform.Find("GoalParticle").gameObject.GetComponent<ParticleSystem>().Play();
            //���Đ�
            audioSource.PlayOneShot(GoalSound);
            //�S�[���t���O���I��
            GoalFlag = true;
            

            //�Q�[���f�B���N�^�[�̃S�[���t���O��ύX
            gamedirector.GetComponent<GameDirector>().GoalFlag = 1;

            //OVRPlayerController�̃R���|�[�l���g���I�t
            OpenXRScene.SetPlayerLocomotionEnabled(false);

            // Meta keeps head tracking active at the goal. The old goal camera
            // disabled OVRCameraRig and could leave bicycle users frozen without
            // returning to the menu.
            var metaPresentation = OpenXRScene.PlayerObject != null
                ? OpenXRScene.PlayerObject.GetComponent<HybridAccidentPresentation>()
                : null;
            if (metaPresentation != null)
            {
                OpenXRScene.SetControllersVisible(true);
                StartCoroutine(ReturnFromMetaGoal());
                return;
            }

            // Freeze the tracked pose and keep the OpenXR Main Camera rendering
            // while CenterEyeCamera drives the goal cinematic.
            OpenXRScene.BeginCinematicCamera();
            var canvas = GameplaySceneContext.Instance != null
                ? GameplaySceneContext.Instance.GameplayCanvas
                : GameObject.Find("Canvas_MainButton");
            var dieFlashImage = canvas != null ? canvas.transform.Find("DieFlashImage") : null;
            if (dieFlashImage != null)
                dieFlashImage.position = ovrcamera.transform.position;
            OpenXRScene.SetControllersVisible(false);

            //�Ó]���A�^�C�g����
            ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress = 3;
        }
    }

    IEnumerator ReturnFromMetaGoal()
    {
        OpenXRInput.SetControllerVibration(0.18f);
        yield return new WaitForSecondsRealtime(0.18f);
        OpenXRInput.StopControllerVibration();
        yield return new WaitForSecondsRealtime(1.4f);
        var resultPresenter = FindFirstObjectByType<AccidentResultPresenter>(FindObjectsInactive.Include);
        if (resultPresenter != null)
            resultPresenter.ReturnToMenu();
        else
        {
            // 結果Presenter欠落時のフォールバック遷移でも倍率を残さない
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneRoute.TitleForCurrentScene);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        //�S�[���t���O�̏����l
        GoalFlag = false;

        //AudioComponent���擾
        audioSource = GetComponent<AudioSource>();

        //�Q�[���I�u�W�F�N�g���i�[
        this.ovrplayer = OpenXRScene.PlayerObject;
        this.ovrcamera = OpenXRScene.CameraControllerObject;
        var context = GameplaySceneContext.Instance;
        this.gamedirector = context != null && context.Director != null
            ? context.Director.gameObject
            : FindFirstObjectByType<GameDirector>()?.gameObject;
        flow = gamedirector != null ? gamedirector.GetComponent<GameplayFlowController>() : null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
