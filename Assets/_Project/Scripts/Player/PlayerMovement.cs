using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed;
    private Vector3 movement;
    private CharacterController controller;

    public GameObject cameraC;
    private Vector3 moveDir = Vector3.zero;
    private float gravity = 9.8f;
    private float moveH;
    private float moveV;

    //動くフラグ
    [SerializeField] int HumanMove;

    //動くフラグを消すプロパティ
    public int HumanFlagSet { set { HumanMove = value; } }

    void Start()
    {
        //コントローラーを取得
        controller = GetComponent<CharacterController>();
        //フラグを設定
        HumanMove = 0;
    }

    void Update()
    {
        //前後移動と横移動
        if (HumanMove == 1)
        {
            moveH = OpenXRInput.LeftStick.x;
            moveV = OpenXRInput.RightStick.y;
            movement = new Vector3(moveH, 0, moveV);

            Vector3 desiredMove = //cameraC.transform.forward * movement.z + 
                cameraC.transform.right * movement.x;
            //実際に移動する力
            moveDir.x = desiredMove.x * 3f;
            moveDir.z = desiredMove.z * 3f;
            moveDir.y -= gravity * Time.deltaTime;

            controller.Move(moveDir * Time.deltaTime * speed);
        }
    }
}
