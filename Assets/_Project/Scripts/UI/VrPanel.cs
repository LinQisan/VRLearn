using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// World-locked VR panels (replay, feedback): placed in front of the viewer at a comfortable
/// distance, level (yaw only) and slightly below eye height. The panel stays where it is while
/// the viewer looks around it, and glides back in front only when they turn away by more than
/// <see cref="RecenterAngle"/>. Head-locked panels that fill the lens were unreadable at the edges
/// on Quest 2.
/// </summary>
[DisallowMultipleComponent]
public sealed class VrPanel : MonoBehaviour
{
    public const float RecenterAngle = 45f;

    Transform head;
    float distance;
    float drop;
    bool recentering;

    /// <summary>Angular width (degrees) of a panel of this world width seen from this distance.</summary>
    public static float AngularWidth(float widthMeters, float distanceMeters) =>
        2f * Mathf.Atan(widthMeters * 0.5f / distanceMeters) * Mathf.Rad2Deg;

    /// <summary>Scale for a canvas of <paramref name="canvasWidth"/> units to span <paramref name="degrees"/>.</summary>
    public static float ScaleFor(float canvasWidth, float degrees, float distanceMeters) =>
        2f * distanceMeters * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad) / canvasWidth;

    public void Attach(Transform viewer, float viewingDistance, float dropBelowEyes = 0.08f)
    {
        head = viewer;
        distance = viewingDistance;
        drop = dropBelowEyes;
        SnapInFront();
    }

    public void SnapInFront()
    {
        if (head == null)
            return;
        transform.SetPositionAndRotation(TargetPosition(), TargetRotation());
        recentering = false;
    }

    Vector3 Forward()
    {
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
    }

    Vector3 TargetPosition() => head.position + Forward() * distance + Vector3.down * drop;
    Quaternion TargetRotation() => Quaternion.LookRotation(Forward(), Vector3.up);

    void LateUpdate()
    {
        if (head == null)
            return;
        var toPanel = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
        var off = Vector3.Angle(Forward(), toPanel);
        if (off > RecenterAngle)
            recentering = true;
        if (!recentering)
            return;
        var k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, TargetPosition(), k),
            Quaternion.Slerp(transform.rotation, TargetRotation(), k));
        if (Vector3.Distance(transform.position, TargetPosition()) < 0.02f)
            recentering = false;
    }

    /// <summary>
    /// Sharper text on menu-only screens: a larger eye buffer costs little when almost nothing
    /// else is drawn. Restored to 1 for gameplay.
    /// </summary>
    public static void SetEyeResolution(float scale)
    {
        if (XRSettings.enabled)
            XRSettings.eyeTextureResolutionScale = Mathf.Clamp(scale, 0.5f, 1.6f);
    }
}
