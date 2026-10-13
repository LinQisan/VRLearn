using System.Collections.Generic;
using UnityEngine;

/// <summary>Wheel rotation follows distance actually travelled, including post-impact coasting.</summary>
[DisallowMultipleComponent]
public sealed class VehicleWheelVisuals : MonoBehaviour
{
    readonly List<Transform> wheels = new List<Transform>(4);
    readonly List<Quaternion> restRotations = new List<Quaternion>(4);
    Vector3 previousPosition;
    float roll;
    float radius = 0.32f;

    void Awake() => RefreshWheels();

    /// <summary>Body swaps may create new wheels after Awake; hidden legacy wheels never roll.</summary>
    public void RefreshWheels()
    {
        ResetVisuals();
        wheels.Clear();
        restRotations.Clear();
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.StartsWith("Wheel_") || !child.gameObject.activeInHierarchy) continue;
            var renderers = child.GetComponentsInChildren<Renderer>();
            if (!System.Array.Exists(renderers, r => r.enabled && r.gameObject.activeInHierarchy)) continue;
            wheels.Add(child);
            restRotations.Add(child.localRotation);
            foreach (var renderer in renderers)
                if (renderer.name == "Tire")
                    radius = Mathf.Clamp(renderer.bounds.extents.y, 0.2f, 0.6f);
        }
        ResetVisuals();
    }

    public void ResetVisuals()
    {
        previousPosition = transform.position;
        roll = 0f;
        for (int i = 0; i < wheels.Count; i++)
            if (wheels[i] != null) wheels[i].localRotation = restRotations[i];
    }
    void OnEnable() => RefreshWheels();

    void LateUpdate()
    {
        Vector3 displacement = transform.position - previousPosition;
        previousPosition = transform.position;
        if (displacement.sqrMagnitude > 25f) return;
        roll = (roll + Vector3.Dot(displacement, transform.forward) / radius * Mathf.Rad2Deg) % 360f;
        for (int i = 0; i < wheels.Count; i++)
            if (wheels[i] != null)
                wheels[i].localRotation = restRotations[i] * Quaternion.AngleAxis(roll, Vector3.right);
    }
}
