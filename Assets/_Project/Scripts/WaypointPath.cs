using UnityEngine;

/// <summary>A non-rendering, non-physical ordered route for waypoint vehicles.</summary>
/// <remarks>
/// Immutable route definition shared by all vehicles on the lane. Consumers
/// must copy <see cref="Points"/> before per-vehicle edits; writing into the
/// returned array pollutes every current and future vehicle on this route.
/// </remarks>
[DisallowMultipleComponent]
public sealed class WaypointPath : MonoBehaviour
{
    [SerializeField] Transform[] points;

    public Transform[] Points
    {
        get
        {
            if (points == null || points.Length == 0)
                RebuildFromChildren();
            return points;
        }
    }

    public void RebuildFromChildren()
    {
        points = new Transform[transform.childCount];
        for (var index = 0; index < points.Length; index++)
            points[index] = transform.GetChild(index);
    }

#if UNITY_EDITOR
    public void EditorConfigure()
    {
        RebuildFromChildren();
    }
#endif
}
