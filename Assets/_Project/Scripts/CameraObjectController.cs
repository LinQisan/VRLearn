using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraObjectController : MonoBehaviour
{
    //Rigidbody���i�[����ϐ�
    Rigidbody rigid;

    //�t���O�Ǘ�
    [SerializeField] int PhisicsMove;

    //�����t���O���������\�b�h
    public int PhisicsFlagSet { set { PhisicsMove = value; } }

    //�}�E�X�̈ړ��ʂ��i�[����ϐ�
    float mx;
    float my;

    //���݂̊p�x���i�[����ϐ�
    Vector3 theta;

    //�}�E�X�̈ړ��ʂ̌W��
    [SerializeField] float MouseCoefficient;

    // Start is called before the first frame update
    void Start()
    {
        //Rigidbody���擾
        this.rigid = this.GetComponent<Rigidbody>();

        //�t���O�̐��l���i�[
        PhisicsMove = 0;

        //�}�E�X�̈ړ��ʂ̌W�����i�[
        //MouseCoefficient = 2.0f;
    }

    //�|�[�Y���͓����Ȃ��悤��
    void FixedUpdate()
    {
        //���������Ȃ���Ύ~�܂�
        this.rigid.linearVelocity = new Vector3(0.0f, 0.0f, 0.0f);

        //���݂̊p�x���i�[
        this.theta = this.gameObject.transform.localEulerAngles;

        //���̂��L�[�ňړ����邩�̃t���O����
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            if(PhisicsMove ==0)
            {
                PhisicsMove = 1;
            }
            else if(PhisicsMove ==1)
            {
                PhisicsMove = 0;
            }
        }

        if (PhisicsMove == 1)
        {
            //�w��̃L�[�������Ɨ͂�������
            if (Keyboard.current != null && Keyboard.current.wKey.isPressed)
            {
                this.rigid.linearVelocity += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * 5.0f; // �O�����Ɉړ�
            }
            if (Keyboard.current != null && Keyboard.current.sKey.isPressed)
            {
                this.rigid.linearVelocity += new Vector3(transform.forward.x, 0.0f, transform.forward.z) * -5.0f; // �������Ɉړ�
            }
            if (Keyboard.current != null && Keyboard.current.aKey.isPressed)
            {
                this.rigid.linearVelocity += -transform.right * 5.0f; // ���[�J���E�����Ɋ�Â��č��ֈړ�
            }
            if (Keyboard.current != null && Keyboard.current.dKey.isPressed)
            {
                this.rigid.linearVelocity += transform.right * 5.0f; // ���[�J���E�����Ɋ�Â��ĉE�ֈړ�
            }
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            {
                this.rigid.linearVelocity += new Vector3(0.0f, -5.0f, 0.0f);
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            {
                this.rigid.linearVelocity += new Vector3(0.0f, 5.0f, 0.0f);
            }
        }
    }
    // Update is called once per frame
    void Update()
    {

    }
}
