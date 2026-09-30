using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyArea : MonoBehaviour
{
    //車が侵入した
    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.tag == "Car")
        {
            var controller = collider.GetComponentInParent<CarController>();
            var vehicle = controller != null ? controller.gameObject : collider.gameObject;
            if (!VehiclePool.Instance.Release(vehicle))
                Destroy(vehicle);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
