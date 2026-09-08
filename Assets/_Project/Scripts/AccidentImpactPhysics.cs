using UnityEngine;

/// <summary>
/// Scene-safe vehicle/person impact data derived from a one-dimensional
/// impulse model. Units are kilograms, metres, seconds and newton-seconds.
/// </summary>
public readonly struct AccidentImpactPhysics
{
    public const float DefaultVehicleMassKg = 1200f;
    public const float DefaultPersonMassKg = 70f;

    public Vector3 Direction { get; }
    public float VehicleMassKg { get; }
    public float PersonMassKg { get; }
    public float ImpactSpeedMetersPerSecond { get; }
    public float PersonImpulseNewtonSeconds { get; }
    public float PersonLaunchSpeedMetersPerSecond { get; }
    public float VehiclePostImpactSpeedMetersPerSecond { get; }

    AccidentImpactPhysics(
        Vector3 direction,
        float vehicleMassKg,
        float personMassKg,
        float impactSpeedMetersPerSecond,
        float personImpulseNewtonSeconds,
        float personLaunchSpeedMetersPerSecond,
        float vehiclePostImpactSpeedMetersPerSecond)
    {
        Direction = direction;
        VehicleMassKg = vehicleMassKg;
        PersonMassKg = personMassKg;
        ImpactSpeedMetersPerSecond = impactSpeedMetersPerSecond;
        PersonImpulseNewtonSeconds = personImpulseNewtonSeconds;
        PersonLaunchSpeedMetersPerSecond = personLaunchSpeedMetersPerSecond;
        VehiclePostImpactSpeedMetersPerSecond = vehiclePostImpactSpeedMetersPerSecond;
    }

    public static AccidentImpactPhysics Calculate(
        Vector3 direction,
        float vehicleMassKg,
        float personMassKg,
        float impactSpeedMetersPerSecond)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;
        direction.Normalize();

        vehicleMassKg = Mathf.Clamp(vehicleMassKg, 80f, 3500f);
        personMassKg = Mathf.Clamp(personMassKg, 30f, 180f);
        impactSpeedMetersPerSecond = Mathf.Clamp(impactSpeedMetersPerSecond, 0f, 25f);

        // Low restitution represents the energy absorbed by the vehicle body,
        // clothing and soft tissue. Only part of the ideal collision impulse is
        // coupled to the ragdoll; the rest is deformation and braking loss.
        const float restitution = 0.08f;
        const float contactCoupling = 0.48f;
        float idealImpulse = (1f + restitution) * impactSpeedMetersPerSecond
            / (1f / vehicleMassKg + 1f / personMassKg);
        float personImpulse = idealImpulse * contactCoupling;
        float personLaunchSpeed = personImpulse / personMassKg;
        float vehiclePostImpactSpeed = Mathf.Max(
            0f,
            impactSpeedMetersPerSecond - personImpulse / vehicleMassKg);

        return new AccidentImpactPhysics(
            direction,
            vehicleMassKg,
            personMassKg,
            impactSpeedMetersPerSecond,
            personImpulse,
            personLaunchSpeed,
            vehiclePostImpactSpeed);
    }
}
