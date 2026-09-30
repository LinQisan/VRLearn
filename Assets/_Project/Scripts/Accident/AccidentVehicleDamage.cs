using System.Collections.Generic;
using UnityEngine;

/// <summary>Local cosmetic deformation; shared meshes and collision geometry stay intact.</summary>
[DisallowMultipleComponent]
public sealed class AccidentVehicleDamage : MonoBehaviour
{
    struct ModifiedMesh
    {
        public MeshFilter filter;
        public Mesh original;
        public Mesh instance;
    }
    readonly List<ModifiedMesh> modified = new List<ModifiedMesh>();
    public int DeformedMeshCount => modified.Count;

    public void Apply(Vector3 contactPoint, Vector3 travelDirection, float speed)
    {
        ResetDamage();
        if (speed < 0.1f)
            return;
        float depth = Mathf.Clamp(speed * 0.012f, 0.015f, 0.18f);
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            // Authored body and bumper meshes only: wheels, glass and interior retain their shape.
            if (filter.name != "BodyMain" && !filter.name.StartsWith("Bumpers"))
                continue;
            var original = filter.sharedMesh;
            if (original == null || !original.isReadable || original.vertexCount > 60000)
                continue;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled)
                continue;
            Vector3 contactAtBodyHeight = contactPoint + Vector3.up * 0.65f;
            if (renderer.bounds.SqrDistance(contactAtBodyHeight) > 0.85f * 0.85f)
                continue;
            var mesh = Instantiate(original);
            mesh.name = original.name + "_ImpactInstance";
            var vertices = mesh.vertices;
            Vector3 surfaceContact = renderer.bounds.ClosestPoint(contactAtBodyHeight);
            Vector3 inward = Vector3.ProjectOnPlane(renderer.bounds.center - surfaceContact, Vector3.up).normalized;
            if (inward.sqrMagnitude < 0.01f) inward = -travelDirection.normalized;
            bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(world, surfaceContact) / 0.85f);
                if (falloff <= 0f)
                    continue;
                world += inward * (depth * falloff * falloff);
                vertices[i] = filter.transform.InverseTransformPoint(world);
                changed = true;
            }
            if (!changed)
            {
                Destroy(mesh);
                continue;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            modified.Add(new ModifiedMesh { filter = filter, original = original, instance = mesh });
        }
    }

    public void ResetDamage()
    {
        foreach (var item in modified)
        {
            if (item.filter != null)
                item.filter.sharedMesh = item.original;
            if (item.instance != null)
                Destroy(item.instance);
        }
        modified.Clear();
    }
    void OnDisable() => ResetDamage();
}
