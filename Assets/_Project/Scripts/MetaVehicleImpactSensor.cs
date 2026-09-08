using UnityEngine;

/// <summary>
/// Body-sized Meta collision sensor. It reports a real vehicle/player overlap
/// without allowing the kinematic car colliders to push the XR player upward.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class MetaVehicleImpactSensor : MonoBehaviour
{
    CarController owner;

    public void Configure(CarController configuredOwner)
    {
        owner = configuredOwner;
        var sensor = GetComponent<BoxCollider>();
        sensor.isTrigger = true;
        enabled = owner != null;
    }

    void OnTriggerEnter(Collider other)
    {
        Report(other);
    }

    void OnTriggerStay(Collider other)
    {
        Report(other);
    }

    void Report(Collider other)
    {
        if (owner == null || owner.OnAcident)
            return;
        owner.NotifyMetaImpact(other);
    }
}
