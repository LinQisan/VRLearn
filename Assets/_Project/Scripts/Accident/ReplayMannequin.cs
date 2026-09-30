using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The participant in the accident replay: a simple articulated figure built from primitives,
/// proportioned from the height and weight entered on the title menu. It walks with the recorded
/// body speed, turns its head with the recorded head pose, sits on the bicycle when riding and
/// falls away from the car after contact. Render-only (no colliders).
/// </summary>
public sealed class ReplayMannequin : MonoBehaviour
{
    // body proportions as fractions of standing height (adult anthropometric averages)
    public const float EyeRatio = 0.936f;
    public const float ShoulderRatio = 0.818f;
    public const float HipRatio = 0.53f;
    public const float KneeRatio = 0.285f;
    public const float AnkleRatio = 0.039f;

    public static readonly Color ShirtColor = new Color(0.49f, 1f, 0.2f);   // participant lime
    static readonly Color TrouserColor = new Color32(51, 65, 85, 255);
    static readonly Color SkinColor = new Color32(233, 196, 164, 255);
    static readonly Color ShoeColor = new Color32(30, 30, 36, 255);

    public float HeightMeters { get; private set; }
    public float WeightKg { get; private set; }
    /// <summary>Width/depth multiplier from BMI (1 = BMI 22).</summary>
    public float Girth { get; private set; }
    public Transform Head => head;
    public Vector3 EyePosition => head.TransformPoint(new Vector3(0f, 0.06f * HeightMeters, 0.05f * HeightMeters));

    Transform body;          // yaw + fall pivot at the feet
    Transform pelvis, chest, neck, head;
    Transform hipL, hipR, kneeL, kneeR, shoulderL, shoulderR, elbowL, elbowR;
    float phase;
    float bodyYaw;
    bool hasYaw;

    public static float GirthFor(float heightMeters, float weightKg)
    {
        var bmi = weightKg / Mathf.Max(0.5f, heightMeters * heightMeters);
        return Mathf.Clamp(Mathf.Sqrt(bmi / 22f), 0.78f, 1.45f);
    }

    public static ReplayMannequin Build(Transform parent, float heightMeters, float weightKg, int layer)
    {
        var root = new GameObject("Replay_Participant");
        root.transform.SetParent(parent, false);
        var mannequin = root.AddComponent<ReplayMannequin>();
        mannequin.Construct(Mathf.Clamp(heightMeters, 0.9f, 2.1f), Mathf.Clamp(weightKg, 20f, 180f));
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
        return mannequin;
    }

    void Construct(float h, float w)
    {
        HeightMeters = h;
        WeightKg = w;
        Girth = GirthFor(h, w);
        var g = Girth;
        var shirt = MakeMaterial(ShirtColor);
        var trousers = MakeMaterial(TrouserColor);
        var skin = MakeMaterial(SkinColor);
        var shoes = MakeMaterial(ShoeColor);

        // ground ring: finds the participant at a glance in the wide overview (does not tilt when falling)
        var ringMaterial = MakeMaterial(ShirtColor, unlit: true);
        Part(PrimitiveType.Cylinder, "GroundRing", transform, new Vector3(0f, 0.02f, 0f),
            new Vector3(0.75f * h, 0.004f, 0.75f * h), ringMaterial);
        Part(PrimitiveType.Cylinder, "GroundRingHole", transform, new Vector3(0f, 0.024f, 0f),
            new Vector3(0.6f * h, 0.004f, 0.6f * h), MakeMaterial(new Color32(20, 28, 40, 255), unlit: true));

        body = Pivot("Body", transform, Vector3.zero);
        var hipY = HipRatio * h;
        var shoulderY = ShoulderRatio * h;
        pelvis = Pivot("Pelvis", body, new Vector3(0f, hipY, 0f));
        Part(PrimitiveType.Capsule, "Hips", pelvis, new Vector3(0f, 0.03f * h, 0f),
            new Vector3(0.19f * h * g, 0.07f * h, 0.12f * h * g), trousers);
        chest = Pivot("Chest", pelvis, new Vector3(0f, 0.06f * h, 0f));
        var torsoLength = shoulderY - hipY - 0.06f * h;
        Part(PrimitiveType.Capsule, "Torso", chest, new Vector3(0f, torsoLength * 0.5f, 0f),
            new Vector3(0.2f * h * Mathf.Lerp(1f, g, 0.8f), torsoLength * 0.55f, 0.13f * h * g), shirt);
        neck = Pivot("Neck", chest, new Vector3(0f, torsoLength, 0f));
        Part(PrimitiveType.Capsule, "NeckPart", neck, new Vector3(0f, 0.025f * h, 0f),
            new Vector3(0.055f * h * g, 0.03f * h, 0.055f * h * g), skin);
        head = Pivot("Head", neck, new Vector3(0f, 0.055f * h, 0f));
        Part(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0.06f * h, 0f),
            new Vector3(0.09f * h, 0.12f * h, 0.105f * h), skin);
        // cap brim and a nose so the facing direction reads from far above
        Part(PrimitiveType.Sphere, "Hair", head, new Vector3(0f, 0.09f * h, -0.01f * h),
            new Vector3(0.095f * h, 0.07f * h, 0.105f * h), trousers);
        Part(PrimitiveType.Cube, "Visor", head, new Vector3(0f, 0.085f * h, 0.055f * h),
            new Vector3(0.08f * h, 0.008f * h, 0.05f * h), shirt);
        Part(PrimitiveType.Sphere, "Nose", head, new Vector3(0f, 0.055f * h, 0.052f * h),
            new Vector3(0.018f * h, 0.022f * h, 0.02f * h), skin);

        var shoulderHalf = 0.115f * h * Mathf.Lerp(1f, g, 0.4f);
        var upperArm = 0.186f * h;
        var foreArm = 0.146f * h + 0.05f * h;
        var armT = 0.045f * h * g;
        shoulderL = Pivot("ShoulderL", neck, new Vector3(-shoulderHalf, -0.02f * h, 0f));
        shoulderR = Pivot("ShoulderR", neck, new Vector3(shoulderHalf, -0.02f * h, 0f));
        elbowL = Limb(shoulderL, "L", upperArm, armT, shirt, out _);
        elbowR = Limb(shoulderR, "R", upperArm, armT, shirt, out _);
        Limb(elbowL, "ForeL", foreArm, armT * 0.85f, skin, out _);
        Limb(elbowR, "ForeR", foreArm, armT * 0.85f, skin, out _);

        var hipHalf = 0.05f * h * Mathf.Lerp(1f, g, 0.6f);
        var thigh = hipY - KneeRatio * h;
        var shin = KneeRatio * h - AnkleRatio * h;
        var legT = 0.07f * h * g;
        hipL = Pivot("HipL", pelvis, new Vector3(-hipHalf, 0f, 0f));
        hipR = Pivot("HipR", pelvis, new Vector3(hipHalf, 0f, 0f));
        kneeL = Limb(hipL, "ThighL", thigh, legT, trousers, out _);
        kneeR = Limb(hipR, "ThighR", thigh, legT, trousers, out _);
        var ankleL = Limb(kneeL, "ShinL", shin, legT * 0.8f, trousers, out _);
        var ankleR = Limb(kneeR, "ShinR", shin, legT * 0.8f, trousers, out _);
        Part(PrimitiveType.Cube, "FootL", ankleL, new Vector3(0f, -AnkleRatio * h * 0.5f, 0.04f * h),
            new Vector3(0.055f * h, AnkleRatio * h, 0.15f * h), shoes);
        Part(PrimitiveType.Cube, "FootR", ankleR, new Vector3(0f, -AnkleRatio * h * 0.5f, 0.04f * h),
            new Vector3(0.055f * h, AnkleRatio * h, 0.15f * h), shoes);
    }

    /// <summary>
    /// Poses the figure for one replay frame.
    /// </summary>
    /// <param name="headPose">recorded HMD pose</param>
    /// <param name="groundY">height of the ground under the participant</param>
    /// <param name="velocity">horizontal body velocity (m/s)</param>
    /// <param name="seated">riding the bicycle</param>
    /// <param name="fall">0 upright … 1 lying, after contact</param>
    /// <param name="fallDirection">horizontal direction the car pushed the participant</param>
    /// <param name="seat">on a bicycle: where the hips sit and which way the bicycle points; the
    /// figure is then fixed to the bicycle and only the head follows the recorded head</param>
    public void Pose(AccidentReplayRecording.Pose headPose, float groundY, Vector3 velocity, bool seated,
        float fall, Vector3 fallDirection, float deltaTime, Vector3? seat = null, Vector3 seatForward = default)
    {
        var h = HeightMeters;
        var speed = new Vector2(velocity.x, velocity.z).magnitude;
        var headForward = Vector3.ProjectOnPlane(headPose.rotation * Vector3.forward, Vector3.up);
        var headYaw = headForward.sqrMagnitude > 1e-4f
            ? Mathf.Atan2(headForward.x, headForward.z) * Mathf.Rad2Deg
            : bodyYaw;
        // the body faces where it moves; standing still it follows the head lazily
        var targetYaw = speed > 0.35f ? Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg : bodyYaw;
        if (!hasYaw) { bodyYaw = speed > 0.35f ? targetYaw : headYaw; hasYaw = true; }
        if (Mathf.Abs(Mathf.DeltaAngle(targetYaw, headYaw)) > 70f && speed <= 0.35f)
            targetYaw = headYaw;
        bodyYaw = Mathf.LerpAngle(bodyYaw, targetYaw, 1f - Mathf.Exp(-6f * Mathf.Max(0.001f, deltaTime)));

        Vector3 feet;
        if (seated && seat.HasValue && seatForward.sqrMagnitude > 1e-4f)
        {
            // riding: hips on the saddle, body along the bicycle
            bodyYaw = Mathf.Atan2(seatForward.x, seatForward.z) * Mathf.Rad2Deg;
            feet = seat.Value - Vector3.up * (HipRatio * h);
        }
        else
        {
            feet = new Vector3(headPose.position.x, groundY, headPose.position.z);
            // put the head (not the feet) under the recorded eyes horizontally
            feet -= Quaternion.Euler(0f, bodyYaw, 0f) * new Vector3(0f, 0f, seated ? 0.15f * h : 0.02f * h);
            if (seated)
                feet.y = headPose.position.y - 0.906f * h;   // eye height with the chest leaning 22°
        }
        var yaw = Quaternion.Euler(0f, bodyYaw, 0f);
        transform.SetPositionAndRotation(feet, yaw);

        // falling pivots the whole body about the feet, away from the car
        var tilt = Quaternion.identity;
        if (fall > 0f && fallDirection.sqrMagnitude > 1e-4f)
        {
            var local = Quaternion.Inverse(yaw) * fallDirection.normalized;
            tilt = Quaternion.AngleAxis(Mathf.SmoothStep(0f, 82f, fall), Vector3.Cross(Vector3.up, local));
        }
        body.localRotation = tilt;

        // head: recorded rotation relative to the body, limited to what a neck can do
        var headLocal = Quaternion.Inverse(yaw * tilt) * headPose.rotation;
        var e = headLocal.eulerAngles;
        headLocal = Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -50f, 60f),
            Mathf.Clamp(Mathf.DeltaAngle(0f, e.y), -80f, 80f), Mathf.Clamp(Mathf.DeltaAngle(0f, e.z), -30f, 30f));
        head.localRotation = headLocal;

        if (seated)
        {
            phase += speed * deltaTime / (0.9f * h) * 2f * Mathf.PI;
            var pedal = Mathf.Sin(phase) * 18f;
            chest.localRotation = Quaternion.Euler(22f, 0f, 0f);
            hipL.localRotation = Quaternion.Euler(-62f + pedal, 0f, 0f);
            hipR.localRotation = Quaternion.Euler(-62f - pedal, 0f, 0f);
            kneeL.localRotation = Quaternion.Euler(70f - pedal, 0f, 0f);
            kneeR.localRotation = Quaternion.Euler(70f + pedal, 0f, 0f);
            shoulderL.localRotation = shoulderR.localRotation = Quaternion.Euler(-58f, 0f, 0f);
            elbowL.localRotation = elbowR.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            return;
        }

        // walking: stride ~0.83 × height per cycle, amplitude grows with speed
        var amount = Mathf.Clamp01(speed / 1.4f) * (1f - fall);
        phase += speed * deltaTime / (0.83f * h) * 2f * Mathf.PI;
        var s = Mathf.Sin(phase);
        var swing = 26f * amount;
        chest.localRotation = Quaternion.Euler(4f * amount, 6f * s * amount, 0f);
        hipL.localRotation = Quaternion.Euler(-swing * s, 0f, 0f);
        hipR.localRotation = Quaternion.Euler(swing * s, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Cos(phase)) * 40f * amount, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Cos(phase)) * 40f * amount, 0f, 0f);
        var armSwing = 20f * amount + 30f * fall;
        shoulderL.localRotation = Quaternion.Euler(swing * 0.7f * s - 30f * fall, 0f, -6f - 40f * fall);
        shoulderR.localRotation = Quaternion.Euler(-swing * 0.7f * s - 30f * fall, 0f, 6f + 40f * fall);
        elbowL.localRotation = elbowR.localRotation = Quaternion.Euler(-12f - armSwing * 0.3f, 0f, 0f);
    }

    static Transform Pivot(string name, Transform parent, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    /// <summary>Capsule hanging below <paramref name="joint"/>; returns the joint at its far end.</summary>
    static Transform Limb(Transform joint, string name, float length, float thickness, Material material, out Transform part)
    {
        part = Part(PrimitiveType.Capsule, name, joint, new Vector3(0f, -length * 0.5f, 0f),
            new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness), material);
        return Pivot(name + "_End", joint, new Vector3(0f, -length, 0f));
    }

    static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    static Material MakeMaterial(Color color, bool unlit = false)
    {
        var shader = (unlit ? null : Shader.Find("Universal Render Pipeline/Lit")) ?? Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader) { name = "ReplayMannequin", color = color };
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.25f);
        return material;
    }

    /// <summary>World-space top of the head (for tests and the overview framing).</summary>
    public float TopOfHeadY => head.TransformPoint(new Vector3(0f, 0.12f * HeightMeters, 0f)).y;
}
