using UnityEngine;

public class ColliderDebugger : MonoBehaviour
{
    void Start()
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider col in colliders)
        {
            Debug.Log($"Collider: {col.name}, Type: {col.GetType()}, Attached to: {col.gameObject.name}");
        }
    }
}
