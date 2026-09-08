using UnityEngine;

/// <summary>
/// The single gameplay identity for the tracked Meta XR player.
/// Triggers must resolve this component instead of relying on object names or tags.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerActor : MonoBehaviour
{
    [SerializeField] float selectedHeightMeters = 1.7f;
    [SerializeField] float selectedWeightKg = 70f;
    [SerializeField] float bodyRadius = 0.28f;

    public float SelectedHeightMeters => selectedHeightMeters;
    public float SelectedWeightKg => selectedWeightKg;
    public float BodyRadius => bodyRadius;

    public void ConfigureProfile(int heightCentimeters, int weightKilograms)
    {
        selectedHeightMeters = Mathf.Clamp(heightCentimeters * 0.01f, 1.2f, 2.1f);
        selectedWeightKg = Mathf.Clamp(weightKilograms, 30f, 180f);

        // Weight changes shoulder/torso breadth far less than it changes mass.
        // A square-root curve keeps the collision silhouette plausible at the
        // ends of the menu range without turning it into a huge cylinder.
        bodyRadius = Mathf.Clamp(0.28f * Mathf.Sqrt(selectedWeightKg / 70f), 0.22f, 0.36f);
        bodyRadius = Mathf.Min(bodyRadius, selectedHeightMeters * 0.22f);

        var controller = GetComponent<CharacterController>();
        controller.height = Mathf.Max(selectedHeightMeters, bodyRadius * 2f + 0.1f);
        controller.radius = bodyRadius;
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        controller.stepOffset = Mathf.Clamp(controller.height * 0.16f, 0.2f, 0.32f);
        controller.skinWidth = Mathf.Clamp(bodyRadius * 0.12f, 0.03f, 0.05f);
    }

    public static bool TryResolve(Collider collider, out PlayerActor actor)
    {
        actor = null;
        if (collider == null)
            return false;

        actor = collider.GetComponentInParent<PlayerActor>();
        if (actor == null && collider.attachedRigidbody != null)
            actor = collider.attachedRigidbody.GetComponentInParent<PlayerActor>();
        return actor != null;
    }
}
