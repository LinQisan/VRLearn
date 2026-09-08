using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BicycleController : MonoBehaviour
{
    //音を格納する変数
    AudioSource audiosource;
    public AudioClip bicyclesound;

    //アクシデントを管理する変数(CenterEyeから変更される)
    public int Acident;

    //RigidBodyを格納する変数
    Rigidbody rigid;
    bool metaRide;

    // Start is called before the first frame update
    void Start()
    {
        //音を格納
        audiosource = this.gameObject.GetComponent<AudioSource>();

        //RigidBodyを格納
        rigid = this.gameObject.GetComponent<Rigidbody>();

        //フラグを格納
        Acident = 0;
        if (audiosource != null)
            audiosource.volume = 0f;
    }

    public void BeginMetaRide(Transform xrOrigin)
    {
        if (rigid == null)
            rigid = GetComponent<Rigidbody>();
        if (rigid != null)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
            rigid.useGravity = false;
            rigid.isKinematic = true;
            rigid.interpolation = RigidbodyInterpolation.Interpolate;
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        transform.SetParent(xrOrigin, true);
        metaRide = true;
        Acident = 0;
        if (audiosource != null)
            audiosource.volume = 0f;
    }

    public void BeginMetaCrash(Vector3 impactDirection, float impactSpeed)
    {
        if (!metaRide)
            return;
        transform.SetParent(null, true);
        if (rigid == null)
            rigid = GetComponent<Rigidbody>();
        if (rigid != null)
        {
            rigid.isKinematic = false;
            rigid.useGravity = true;
            rigid.constraints = RigidbodyConstraints.None;
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 horizontal = Vector3.ProjectOnPlane(impactDirection, Vector3.up).normalized;
            float speed = Mathf.Clamp(impactSpeed * 0.2f, 1.5f, 3.5f);
            rigid.AddForce(horizontal * speed + Vector3.up * 0.35f, ForceMode.VelocityChange);
            rigid.AddTorque(Vector3.forward * 1.2f, ForceMode.VelocityChange);
        }
        metaRide = false;
        Acident = 1;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //一定以上の速さなら音を鳴らす
        if(audiosource == null)
            return;

        bool pedalling = Mathf.Abs(OpenXRInput.LeftStick.y) > 0.12f && Acident == 0;
        if(pedalling)
        {
            audiosource.volume = 0.8f;
            //コントローラーを振動させる
            OpenXRInput.SetControllerVibration(0.06f);
        }
        else if(Acident == 1)
        {
            audiosource.volume = 0.35f;
        }
        else
        {
            audiosource.volume = 0f;
            //コントローラーを振動を止める
            OpenXRInput.StopControllerVibration();
        }
    }
}
