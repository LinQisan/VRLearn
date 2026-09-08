using System.Collections.Generic;
using UnityEngine;

/// <summary>Small shared pool used by traffic factories and road exit triggers.</summary>
[DisallowMultipleComponent]
public sealed class VehiclePool : MonoBehaviour
{
    static VehiclePool instance;
    readonly Dictionary<GameObject, Stack<GameObject>> inactive = new();
    readonly Dictionary<GameObject, GameObject> prefabByInstance = new();
    // Instances currently sitting in the pool. A vehicle must appear here at
    // most once; this set makes double-Release observable without trusting
    // callers to call exactly once.
    readonly HashSet<GameObject> pooledInstances = new();

    public static VehiclePool Instance
    {
        get
        {
            if (instance != null)
                return instance;
            instance = FindFirstObjectByType<VehiclePool>();
            if (instance != null)
                return instance;
            var root = new GameObject("VehiclePool");
            instance = root.AddComponent<VehiclePool>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (!inactive.TryGetValue(prefab, out var available))
        {
            available = new Stack<GameObject>();
            inactive.Add(prefab, available);
        }

        GameObject vehicle;
        if (available.Count > 0)
        {
            vehicle = available.Pop();
            pooledInstances.Remove(vehicle);
            vehicle.transform.SetParent(null, true);
            vehicle.transform.SetPositionAndRotation(position, rotation);
            vehicle.SetActive(true);
            // A vehicle disabled by a failed route load gets a second chance
            // with the new spawn configuration via OnEnable reset.
            if (vehicle.TryGetComponent<CarController>(out var pooledCar) && !pooledCar.enabled)
                pooledCar.enabled = true;
        }
        else
        {
            vehicle = Instantiate(prefab, position, rotation);
            prefabByInstance[vehicle] = prefab;
        }
        return vehicle;
    }

    /// <summary>
    /// Returns true when the vehicle is now pooled OR was already pooled
    /// (idempotent: repeated Release never creates a duplicate entry and must
    /// never lead callers to Destroy a pooled instance). Returns false only
    /// for instances this pool never created.
    /// </summary>
    public bool Release(GameObject vehicle)
    {
        if (vehicle == null || !prefabByInstance.TryGetValue(vehicle, out var prefab))
            return false;

        if (!pooledInstances.Add(vehicle))
            return true;

        // Explicit lifecycle drive: cleanup runs here, before deactivation.
        // CarController.OnDisable repeats the same idempotent cleanup as a
        // fallback for non-pool deactivations (route-load disable, unload).
        if (vehicle.TryGetComponent<CarController>(out var pooledCar))
            pooledCar.PrepareForPoolReuse();
        vehicle.SetActive(false);
        vehicle.transform.SetParent(transform, false);
        if (!inactive.TryGetValue(prefab, out var available))
        {
            available = new Stack<GameObject>();
            inactive.Add(prefab, available);
        }
        available.Push(vehicle);
        return true;
    }

    public bool IsPooled(GameObject vehicle) => vehicle != null && pooledInstances.Contains(vehicle);
}
