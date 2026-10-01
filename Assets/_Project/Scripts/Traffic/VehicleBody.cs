using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which body a car wears: the prefab's own sedan (id "") or a body from the catalog
/// (Resources/VehicleBodies: the Japanese kei cars). Behaviour (route, speed, accident logic) always
/// comes from the car prefab; the body sets the look, the collision box, the mass and where the
/// lights sit. Background traffic mixes the bodies in (<see cref="ApplyTrafficMix"/>); accident cars of
/// the built-in scenarios keep the sedan; custom scenarios pick one per vehicle. Every car goes
/// back to the sedan when it returns to the pool (hard rule 10).
/// </summary>
[DisallowMultipleComponent]
public sealed class VehicleBody : MonoBehaviour
{
    public const string Sedan = "";
    public const string SedanCsvName = "sedan";
    public const string CatalogResource = "VehicleBodies";

    static VehicleBodyCatalog catalog;
    // own random source: the traffic timing uses UnityEngine.Random, whose sequence must not change
    static System.Random mixRandom = new System.Random();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        catalog = null;
        mixRandom = new System.Random();
    }

    public static VehicleBodyCatalog Catalog => catalog != null ? catalog : (catalog = Resources.Load<VehicleBodyCatalog>(CatalogResource));

    /// <summary>"" (sedan) and every catalog id.</summary>
    public static IEnumerable<string> KnownIds
    {
        get
        {
            yield return Sedan;
            if (Catalog != null)
                foreach (var entry in Catalog.bodies)
                    yield return entry.id;
        }
    }

    public string Id { get; private set; } = Sedan;
    public string CsvName => string.IsNullOrEmpty(Id) ? SedanCsvName : Id;

    bool captured;
    readonly List<Renderer> sedanRenderers = new List<Renderer>();
    BoxCollider bodyCollider;
    Vector3 sedanColliderCenter, sedanColliderSize;
    Rigidbody rigid;
    float sedanMass;
    Transform lights;
    Vector3 sedanLightScale;
    Bounds sedanBounds;          // in the car's local space
    readonly Dictionary<string, GameObject> built = new Dictionary<string, GameObject>();

    public static VehicleBody For(GameObject car)
    {
        if (car == null) return null;
        var body = car.GetComponent<VehicleBody>();
        return body != null ? body : car.AddComponent<VehicleBody>();
    }

    /// <summary>Ordinary traffic: a body drawn by the catalog's traffic shares (the rest stay sedans).</summary>
    public static void ApplyTrafficMix(GameObject car)
    {
        var body = For(car);
        if (body == null) return;
        var pick = Sedan;
        if (Catalog != null)
        {
            var roll = mixRandom.NextDouble();
            foreach (var entry in Catalog.bodies)
            {
                if (roll < entry.trafficShare) { pick = entry.id; break; }
                roll -= entry.trafficShare;
            }
        }
        body.Apply(pick, mixRandom.Next());
    }

    /// <summary>Puts on body <paramref name="id"/> ("" = the prefab's sedan); unknown ids fall back to the sedan.</summary>
    public void Apply(string id, int colorSeed = 0)
    {
        Capture();
        var entry = string.IsNullOrEmpty(id) || Catalog == null ? null : Catalog.Find(id);
        foreach (var kv in built)
            if (kv.Value != null) kv.Value.SetActive(false);
        if (entry == null || entry.prefab == null)
        {
            RestoreSedan();
            return;
        }

        foreach (var r in sedanRenderers)
            if (r != null) r.enabled = false;
        if (!built.TryGetValue(entry.id, out var visual) || visual == null)
        {
            visual = Instantiate(entry.prefab, transform, false);
            // the damage deformation works on meshes named BodyMain
            visual.name = "BodyMain";
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = gameObject.layer;
            foreach (var c in visual.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            built[entry.id] = visual;
        }
        // same footprint centre and ground contact as the sedan
        visual.transform.localPosition = new Vector3(sedanBounds.center.x, sedanBounds.min.y, sedanBounds.center.z);
        visual.transform.localRotation = Quaternion.identity;
        visual.SetActive(true);
        Paint(visual, entry, colorSeed);

        var ratio = new Vector3(entry.size.x / Mathf.Max(0.01f, sedanBounds.size.x),
            entry.size.y / Mathf.Max(0.01f, sedanBounds.size.y), entry.size.z / Mathf.Max(0.01f, sedanBounds.size.z));
        if (bodyCollider != null)
        {
            // the sedan's box scaled to the new body, standing on the same bottom
            var bottom = sedanColliderCenter.y - sedanColliderSize.y * 0.5f;
            var size = Vector3.Scale(sedanColliderSize, ratio);
            bodyCollider.size = size;
            bodyCollider.center = new Vector3(
                sedanBounds.center.x + (sedanColliderCenter.x - sedanBounds.center.x) * ratio.x,
                bottom + size.y * 0.5f,
                sedanBounds.center.z + (sedanColliderCenter.z - sedanBounds.center.z) * ratio.z);
        }
        if (rigid != null && entry.massKg > 0f)
            rigid.mass = entry.massKg;
        if (lights != null)
            lights.localScale = Vector3.Scale(sedanLightScale, ratio);
        Id = entry.id;
    }

    public void RestoreSedan()
    {
        Capture();
        foreach (var kv in built)
            if (kv.Value != null) kv.Value.SetActive(false);
        foreach (var r in sedanRenderers)
            if (r != null) r.enabled = true;
        if (bodyCollider != null)
        {
            bodyCollider.size = sedanColliderSize;
            bodyCollider.center = sedanColliderCenter;
        }
        if (rigid != null) rigid.mass = sedanMass;
        if (lights != null) lights.localScale = sedanLightScale;
        Id = Sedan;
    }

    /// <summary>The sedan as authored in the prefab (read once, before any body is applied).</summary>
    void Capture()
    {
        if (captured) return;
        captured = true;
        lights = transform.Find("LightContainer");
        var first = true;
        // every visible part of the sedan, the driver figure (skinned) included; the lights stay
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || (lights != null && r.transform.IsChildOf(lights))) continue;
            // invisible helpers (collision boxes, trigger volumes) are not part of the look
            if (Array.Exists(r.sharedMaterials, m => m != null && m.name.StartsWith("Material_Invisible"))) continue;
            sedanRenderers.Add(r);
            // the footprint comes from the car's own meshes
            var mf = r.GetComponent<MeshFilter>();
            if (!(r is MeshRenderer) || mf == null || mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            var toLocal = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var p = toLocal.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3(sx, sy, sz)));
                if (first) { sedanBounds = new Bounds(p, Vector3.zero); first = false; }
                else sedanBounds.Encapsulate(p);
            }
        }
        var body = transform.Find("CarCollider1");
        bodyCollider = body != null ? body.GetComponent<BoxCollider>() : null;
        if (bodyCollider == null)
            foreach (var box in GetComponents<BoxCollider>())
                if (!box.isTrigger) { bodyCollider = box; break; }
        if (bodyCollider != null)
        {
            sedanColliderCenter = bodyCollider.center;
            sedanColliderSize = bodyCollider.size;
        }
        rigid = GetComponent<Rigidbody>();
        sedanMass = rigid != null ? rigid.mass : 0f;
        sedanLightScale = lights != null ? lights.localScale : Vector3.one;
    }

    static void Paint(GameObject visual, VehicleBodyCatalog.Entry entry, int seed)
    {
        if (entry.colors == null || entry.colors.Length == 0) return;
        var color = entry.colors[Mathf.Abs(seed) % entry.colors.Length];
        var block = new MaterialPropertyBlock();
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (var i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].name != entry.paintMaterial) continue;
                r.GetPropertyBlock(block, i);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block, i);
            }
        }
    }
}
