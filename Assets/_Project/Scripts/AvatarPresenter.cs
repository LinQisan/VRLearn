using RootMotion.FinalIK;
using UnityEngine;

/// <summary>
/// Keeps the first-person avatar dormant until the accident presentation needs it.
/// Player collision is owned by PlayerActor, never by this visual model.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class AvatarPresenter : MonoBehaviour
{
    [SerializeField] Renderer[] renderers;
    [SerializeField] Collider[] colliders;
    [SerializeField] Rigidbody[] bodies;
    [SerializeField] Animator animator;
    [SerializeField] HumanController humanController;
    [SerializeField] VRIK vrik;
    [SerializeField] FullBodyBipedIK fullBodyIk;
    [SerializeField] GrounderFBBIK grounder;
    [SerializeField] AudioSource movementAudio;
    float[] originalMasses;
    float configuredWeightKg = AccidentImpactPhysics.DefaultPersonMassKg;

    public float ConfiguredWeightKg => configuredWeightKg;
    public float DynamicBodyMassKg => CalculateDynamicBodyMass();
    public AccidentImpactPhysics LastImpactPhysics { get; private set; }

    public void Configure()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        bodies = GetComponentsInChildren<Rigidbody>(true);
        animator = GetComponent<Animator>();
        humanController = GetComponent<HumanController>();
        vrik = GetComponent<VRIK>();
        fullBodyIk = GetComponent<FullBodyBipedIK>();
        grounder = GetComponent<GrounderFBBIK>();
        movementAudio = GetComponent<AudioSource>();
        CaptureOriginalMasses();
    }

    public void ConfigureBodyProfile(int heightCentimeters, int weightKilograms)
    {
        if (bodies == null || bodies.Length == 0)
            Configure();
        else
            CaptureOriginalMasses();

        var heightScale = Mathf.Clamp(heightCentimeters / 170f, 0.7f, 1.24f);
        transform.localScale = Vector3.one * (0.27f * heightScale);
        configuredWeightKg = Mathf.Clamp(weightKilograms, 30f, 180f);

        float originalTotal = 0f;
        for (var index = 0; index < bodies.Length; index++)
        {
            if (bodies[index] != null && bodies[index].transform != transform)
                originalTotal += originalMasses[index];
        }
        if (originalTotal <= 0f)
            return;

        for (var index = 0; index < bodies.Length; index++)
        {
            if (bodies[index] == null || bodies[index].transform == transform)
                continue;
            bodies[index].mass = Mathf.Max(
                0.25f,
                configuredWeightKg * originalMasses[index] / originalTotal);
        }
    }

    void CaptureOriginalMasses()
    {
        if (bodies == null || (originalMasses != null && originalMasses.Length == bodies.Length))
            return;

        originalMasses = new float[bodies.Length];
        for (var index = 0; index < bodies.Length; index++)
            originalMasses[index] = bodies[index] != null
                ? Mathf.Max(0.01f, bodies[index].mass)
                : 0f;
    }

    private void Start()
    {
        EnterFirstPersonMode();
    }

    public void EnterFirstPersonMode()
    {
        if (renderers == null || renderers.Length == 0
            || colliders == null || colliders.Length == 0
            || bodies == null || bodies.Length == 0)
            Configure();

        SetRenderers(false);
        SetColliders(false);
        SetBodiesKinematic(true);
        SetComponentEnabled(animator, false);
        SetComponentEnabled(humanController, false);
        SetComponentEnabled(vrik, false);
        SetComponentEnabled(fullBodyIk, false);
        SetComponentEnabled(grounder, false);
        if (movementAudio != null)
            movementAudio.enabled = false;
    }

    public void ShowForAccident(PlayerActor player)
    {
        ShowForAccident(player, Vector3.zero, 0f);
    }

    public void ShowForAccident(PlayerActor player, Vector3 impactDirection, float impactSpeed)
    {
        var impact = AccidentImpactPhysics.Calculate(
            impactDirection,
            AccidentImpactPhysics.DefaultVehicleMassKg,
            DynamicBodyMassKg,
            impactSpeed);
        ShowForAccident(player, impact);
    }

    public void ShowForAccident(PlayerActor player, AccidentImpactPhysics impact)
    {
        if (renderers == null || renderers.Length == 0
            || colliders == null || colliders.Length == 0
            || bodies == null || bodies.Length == 0)
            Configure();

        // Gameplay setup can enable IK again after this presenter starts. The
        // ragdoll must have exactly one owner at impact time or animation/IK
        // will overwrite the physics pose and keep the character standing.
        SetComponentEnabled(animator, false);
        SetComponentEnabled(humanController, false);
        SetComponentEnabled(vrik, false);
        SetComponentEnabled(fullBodyIk, false);
        SetComponentEnabled(grounder, false);

        // Lock the complete pose before relocating the hidden body. Moving a
        // hierarchy that still contains dynamic child rigidbodies can leave the
        // physics representation at its previous first-person location.
        SetBodiesKinematic(true);

        if (player != null)
        {
            var camera = OpenXRScene.MainCameraTransform;
            var forward = camera != null
                ? Vector3.ProjectOnPlane(camera.forward, Vector3.up)
                : Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            var position = player.transform.position;
            if (Physics.Raycast(
                position + Vector3.up * 2f,
                Vector3.down,
                out var hit,
                6f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y;
            }

            Vector3 horizontalImpact = Vector3.ProjectOnPlane(impact.Direction, Vector3.up);
            if (horizontalImpact.sqrMagnitude > 0.001f)
            {
                // Place the visual body just beyond the stopped bumper. This
                // avoids an overlapping spawn that would make PhysX launch the
                // ragdoll vertically before the controlled impact impulse.
                position += horizontalImpact.normalized * 0.55f;
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        SetRenderers(true);
        SetColliders(true);
        Physics.SyncTransforms();
        SetBodiesKinematic(false);
        LastImpactPhysics = impact;
        ApplyImpact(impact);
        if (movementAudio != null)
            movementAudio.enabled = true;
    }

    public Transform GetAccidentViewAnchor()
    {
        if (bodies == null || bodies.Length == 0)
            Configure();

        // Prefer the physical head so the XR origin inherits the actual ragdoll
        // fall. Some humanoid imports only put a body on the neck or chest, so
        // retain ordered fallbacks instead of relying on one authored bone name.
        string[] preferredNames = { "head", "neck", "upperchest", "chest", "spine", "hips", "pelvis" };
        foreach (string preferredName in preferredNames)
        {
            foreach (var body in bodies)
            {
                if (body != null
                    && body.name.Replace("_", string.Empty).ToLowerInvariant().Contains(preferredName))
                {
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    return body.transform;
                }
            }
        }

        foreach (var body in bodies)
            if (body != null)
                return body.transform;
        return null;
    }

    private void ApplyImpact(AccidentImpactPhysics impact)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(impact.Direction, Vector3.up);
        if (bodies == null || bodies.Length == 0)
            return;
        if (horizontal.sqrMagnitude < 0.001f)
            horizontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (horizontal.sqrMagnitude < 0.001f)
            horizontal = Vector3.forward;
        horizontal.Normalize();

        Rigidbody pelvis = null;
        Rigidbody chest = null;
        Rigidbody head = null;
        foreach (var body in bodies)
        {
            if (body == null || body.transform == transform)
                continue;
            string bodyName = body.name.ToLowerInvariant();
            if (pelvis == null && (bodyName.Contains("hips") || bodyName.Contains("pelvis")))
                pelvis = body;
            if (chest == null && (bodyName.Contains("chest") || bodyName.Contains("spine2")))
                chest = body;
            if (head == null && bodyName.Contains("head"))
                head = body;
        }
        if (pelvis == null)
        {
            foreach (var body in bodies)
            {
                if (body != null && body.transform != transform)
                {
                    pelvis = body;
                    break;
                }
            }
        }
        if (pelvis == null)
        {
            Debug.LogError("No dynamic ragdoll body was found for the accident reaction.", this);
            return;
        }

        // Split one physically calculated impulse between the major contact
        // regions. ForceMode.Impulse deliberately preserves mass: a lighter
        // configured body gains more velocity than a heavier body from the same
        // contact, while gravity itself remains mass-independent.
        float impulseMagnitude = impact.PersonImpulseNewtonSeconds;
        Vector3 contactImpulse = horizontal * impulseMagnitude
            + Vector3.up * (impulseMagnitude * 0.1f);
        Vector3 fallAxis = Vector3.Cross(Vector3.up, horizontal).normalized;
        float angularImpulse = Mathf.Clamp(impulseMagnitude * 0.025f, 2.5f, 12f);

        pelvis.maxAngularVelocity = 12f;
        pelvis.AddForce(contactImpulse * 0.48f, ForceMode.Impulse);
        pelvis.AddTorque(fallAxis * (angularImpulse * 0.3f), ForceMode.Impulse);
        if (chest != null && chest != pelvis)
        {
            chest.maxAngularVelocity = 12f;
            chest.AddForce(contactImpulse * 0.42f, ForceMode.Impulse);
            chest.AddTorque(fallAxis * (angularImpulse * 0.7f), ForceMode.Impulse);
        }
        if (head != null && head != chest && head != pelvis)
        {
            head.maxAngularVelocity = 10f;
            head.AddForce(contactImpulse * 0.1f, ForceMode.Impulse);
        }
    }

    float CalculateDynamicBodyMass()
    {
        if (bodies == null || bodies.Length == 0)
            Configure();

        float total = 0f;
        foreach (var body in bodies)
        {
            if (body != null && body.transform != transform)
                total += Mathf.Max(0f, body.mass);
        }
        return total > 0f ? total : configuredWeightKg;
    }

    private void SetRenderers(bool visible)
    {
        foreach (var item in renderers)
            if (item != null)
                item.enabled = visible;
    }

    private void SetColliders(bool enabled)
    {
        foreach (var item in colliders)
            if (item != null)
                item.enabled = enabled;
    }

    private void SetBodiesKinematic(bool kinematic)
    {
        foreach (var body in bodies)
        {
            if (body == null)
                continue;

            bool wasKinematic = body.isKinematic;

            // Human's root Rigidbody is an authored kinematic hierarchy anchor,
            // not a ragdoll limb. Making both it and its child pelvis dynamic
            // creates nested competing bodies and prevents a clean collapse.
            if (body.transform == transform)
            {
                if (!wasKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
                continue;
            }

            if (kinematic)
            {
                // Clear dynamic state before switching to kinematic. Unity
                // rejects velocity writes made after the switch on some
                // backends, which used to spam the accident transition log.
                if (!wasKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
            }
            else
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.constraints = RigidbodyConstraints.None;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.WakeUp();
            }
        }
    }

    private static void SetComponentEnabled(Behaviour behaviour, bool enabled)
    {
        if (behaviour != null)
            behaviour.enabled = enabled;
    }
}
