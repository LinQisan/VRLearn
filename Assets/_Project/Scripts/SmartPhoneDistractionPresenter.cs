using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presents a recognisable, body-locked phone for the walking-distraction
/// scenario. The old scene light is intentionally disabled: the screen uses a
/// restrained emissive material and never illuminates the whole headset view.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class SmartPhoneDistractionPresenter : MonoBehaviour
{
    [SerializeField] Vector3 viewOffset = new Vector3(0.16f, -0.22f, 0.55f);

    readonly List<Material> runtimeMaterials = new List<Material>();
    Camera trackedCamera;
    GameObject phoneRoot;
    Transform phoneVisual;
    GameObject distractionScreen;
    bool requestedVisible;

    public bool IsPresenting => requestedVisible
        && phoneRoot != null
        && phoneRoot.activeInHierarchy;
    public Transform PhoneVisual => phoneVisual;
    public GameObject DistractionScreen => distractionScreen;
    public bool HasEnabledPhoneLight
    {
        get
        {
            if (phoneRoot == null)
                return false;
            foreach (var phoneLight in phoneRoot.GetComponentsInChildren<Light>(true))
                if (phoneLight != null && phoneLight.enabled)
                    return true;
            return false;
        }
    }

    public void Configure(Camera camera, GameObject authoredPhoneRoot, bool visible)
    {
        trackedCamera = camera;
        phoneRoot = authoredPhoneRoot;
        requestedVisible = visible;

        if (phoneRoot == null)
        {
            Debug.LogError("SmartPhoneContainer is missing; walking-phone mode cannot be presented.", this);
            return;
        }

        DisableAuthoredPlaceholder();
        EnsurePhoneModel();
        var screenTransform = FindDescendant(phoneRoot.transform, "WhiteWall");
        distractionScreen = screenTransform != null ? screenTransform.gameObject : null;
        phoneRoot.SetActive(visible);

        if (!visible)
            return;
        if (trackedCamera == null || phoneVisual == null)
        {
            Debug.LogError("Walking-phone presentation references are incomplete.", this);
            phoneRoot.SetActive(false);
            requestedVisible = false;
            return;
        }

        if (distractionScreen != null)
            distractionScreen.SetActive(true);
        UpdatePhonePose();
    }

    public void Hide()
    {
        requestedVisible = false;
        if (phoneRoot != null)
            phoneRoot.SetActive(false);
    }

    void LateUpdate()
    {
        if (!IsPresenting || trackedCamera == null || phoneVisual == null)
            return;

        // CarLightController historically toggled children under this container.
        // Reassert the intended state without bringing the obsolete spotlight back.
        DisablePhoneLights();
        if (distractionScreen != null && !distractionScreen.activeSelf)
            distractionScreen.SetActive(true);
        UpdatePhonePose();
    }

    void DisableAuthoredPlaceholder()
    {
        DisablePhoneLights();
        SetNamedChildActive("LightContainer", false);
        SetNamedChildActive("SmartPhone", false);
        SetNamedChildActive("RightHandTarget", false);
        SetNamedChildActive("FrostedGlass", false);
    }

    void DisablePhoneLights()
    {
        foreach (var phoneLight in phoneRoot.GetComponentsInChildren<Light>(true))
        {
            if (phoneLight != null)
            {
                phoneLight.enabled = false;
                phoneLight.gameObject.SetActive(false);
            }
        }
    }

    void SetNamedChildActive(string childName, bool active)
    {
        var child = FindDescendant(phoneRoot.transform, childName);
        if (child != null)
            child.gameObject.SetActive(active);
    }

    void EnsurePhoneModel()
    {
        var existing = FindDescendant(phoneRoot.transform, "RuntimePhoneModel");
        if (existing != null)
        {
            phoneVisual = existing;
            existing.gameObject.SetActive(true);
            return;
        }

        var model = new GameObject("RuntimePhoneModel");
        model.layer = phoneRoot.layer;
        model.transform.SetParent(phoneRoot.transform, false);
        model.transform.localPosition = new Vector3(0f, -0.55f, 0.26f);
        model.transform.localRotation = Quaternion.identity;
        // The authored container cancels a world-space Canvas scale and ends up
        // roughly six times larger than world units. Compensate here so the
        // finished handset is a real-world 9 x 16.5 cm instead of a giant prop.
        Vector3 parentScale = phoneRoot.transform.lossyScale;
        model.transform.localScale = new Vector3(
            SafeInverse(parentScale.x),
            SafeInverse(parentScale.y),
            SafeInverse(parentScale.z));
        phoneVisual = model.transform;

        var bodyMaterial = CreateMaterial(new Color(0.018f, 0.022f, 0.03f, 1f), false);
        var bezelMaterial = CreateMaterial(new Color(0.07f, 0.08f, 0.095f, 1f), false);
        var screenMaterial = CreateMaterial(new Color(0.035f, 0.12f, 0.17f, 1f), true);
        var accentMaterial = CreateMaterial(new Color(0.18f, 0.68f, 0.78f, 1f), true);
        var notificationMaterial = CreateMaterial(new Color(0.82f, 0.88f, 0.9f, 1f), false);

        CreateCube("PhoneBody", model.transform, Vector3.zero,
            new Vector3(0.09f, 0.165f, 0.012f), bodyMaterial);
        CreateCube("PhoneBezel", model.transform, new Vector3(0f, 0f, -0.007f),
            new Vector3(0.084f, 0.158f, 0.003f), bezelMaterial);
        CreateCube("PhoneScreen", model.transform, new Vector3(0f, 0f, -0.009f),
            new Vector3(0.075f, 0.137f, 0.002f), screenMaterial);

        // A compact status/header and notification cards make the silhouette
        // immediately readable as a phone instead of another glowing prop.
        CreateCube("ScreenHeader", model.transform, new Vector3(0f, 0.054f, -0.0105f),
            new Vector3(0.058f, 0.008f, 0.001f), accentMaterial);
        CreateCube("NotificationCardA", model.transform, new Vector3(0f, 0.019f, -0.0105f),
            new Vector3(0.058f, 0.021f, 0.001f), notificationMaterial);
        CreateCube("NotificationCardB", model.transform, new Vector3(0f, -0.014f, -0.0105f),
            new Vector3(0.058f, 0.021f, 0.001f), notificationMaterial);
        CreateCube("HomeBar", model.transform, new Vector3(0f, -0.059f, -0.0105f),
            new Vector3(0.027f, 0.003f, 0.001f), accentMaterial);
        CreateCube("SideButton", model.transform, new Vector3(0.046f, 0.025f, 0f),
            new Vector3(0.003f, 0.026f, 0.008f), bezelMaterial);
    }

    Material CreateMaterial(Color color, bool emissive)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        var material = new Material(shader)
        {
            name = emissive ? "RuntimePhoneScreenMaterial" : "RuntimePhoneBodyMaterial"
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.35f);
        }
        runtimeMaterials.Add(material);
        return material;
    }

    static void CreateCube(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        var collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
    }

    void UpdatePhonePose()
    {
        var body = OpenXRScene.Origin != null
            ? OpenXRScene.Origin.transform
            : trackedCamera.transform;
        var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(trackedCamera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward).normalized;

        phoneRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        var desiredPhonePosition = trackedCamera.transform.position
            + right * viewOffset.x
            + Vector3.up * viewOffset.y
            + forward * viewOffset.z;
        phoneRoot.transform.position += desiredPhonePosition - phoneVisual.position;
    }

    void OnDestroy()
    {
        foreach (var material in runtimeMaterials)
            if (material != null)
                Destroy(material);
        runtimeMaterials.Clear();
    }

    static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName)
                return child;
        return null;
    }

    static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / Mathf.Abs(value) : 1f;
    }
}
