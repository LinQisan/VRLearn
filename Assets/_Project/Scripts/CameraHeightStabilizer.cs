using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraHeightStabilizer : MonoBehaviour
{
    public Transform headTarget;      // VRIK の Head Target
    private float initY;

    void Start()
    {
        initY = headTarget.position.y;
    }

    void LateUpdate()
    {
        float dy = headTarget.position.y - initY;
        transform.position += Vector3.down * dy;
    }
}
