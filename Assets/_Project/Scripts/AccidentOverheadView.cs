using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Renders the post-impact scene from a stable overhead camera into a
/// head-locked canvas. The tracked XR camera remains under the user's control,
/// so the accident replay does not force or tilt the user's head pose.
/// </summary>
[DisallowMultipleComponent]
public sealed class AccidentOverheadView : MonoBehaviour
{
    private const int TextureWidth = 1280;
    private const int TextureHeight = 720;
    private const int OverlaySortingOrder = 31000;

    [SerializeField, Range(35f, 65f)] private float fieldOfView = 48f;
    [SerializeField, Range(0.15f, 0.8f)] private float horizontalOffsetRatio = 0.42f;
    [SerializeField, Min(3f)] private float minimumHeight = 5.5f;
    [SerializeField, Min(5f)] private float maximumHeight = 14f;
    [SerializeField, Min(0f)] private float followSharpness = 7f;

    private Camera displayCamera;
    private Camera overheadCamera;
    private RenderTexture overheadTexture;
    private Canvas overlayCanvas;
    private Vector3 impactPoint;
    private Vector3 vehicleDirection = Vector3.forward;
    private Transform impactVehicle;
    private AvatarPresenter accidentAvatar;
    private bool isVisible;
    private int savedDisplayMask;
    private Renderer[] avatarRenderers;
    private Renderer[] vehicleRenderers;
    private Bounds? replayBounds;

    public void SetReplayBounds(Bounds bounds)
    {
        replayBounds = bounds;
        UpdateCameraPose(true);
    }

    public void ClearReplayBounds()
    {
        replayBounds = null;
        if (overheadCamera != null) overheadCamera.orthographic = false;
        UpdateCameraPose(true);
    }

    public bool IsVisible => isVisible;
    public Camera ViewCamera => overheadCamera;
    public Canvas OverlayCanvas => overlayCanvas;

    public void Configure(Camera trackedCamera)
    {
        displayCamera = trackedCamera;

        if (overlayCanvas != null)
        {
            ConfigureOverlayCanvas();
        }
    }

    public void Show(
        Vector3 worldImpactPoint,
        Vector3 travelDirection,
        Transform vehicle,
        AvatarPresenter avatar)
    {
        if (displayCamera == null)
        {
            displayCamera = OpenXRScene.MainCamera;
        }

        if (displayCamera == null)
        {
            Debug.LogWarning("事故俯视镜头无法显示：未找到 XR 主相机。", this);
            return;
        }

        EnsureResources();

        impactPoint = worldImpactPoint;
        impactVehicle = vehicle;
        accidentAvatar = avatar;
        avatarRenderers = avatar != null ? avatar.GetComponentsInChildren<Renderer>(true) : null;
        vehicleRenderers = vehicle != null ? vehicle.GetComponentsInChildren<Renderer>(true) : null;
        vehicleDirection = Vector3.ProjectOnPlane(travelDirection, Vector3.up);
        if (vehicleDirection.sqrMagnitude < 0.0001f)
        {
            vehicleDirection = Vector3.ProjectOnPlane(displayCamera.transform.forward, Vector3.up);
        }

        if (vehicleDirection.sqrMagnitude < 0.0001f)
        {
            vehicleDirection = Vector3.forward;
        }

        vehicleDirection.Normalize();
        UpdateCameraPose(true);

        if (!isVisible)
            savedDisplayMask = displayCamera.cullingMask;
        // The review is already rendered by overheadCamera. Nearby ragdoll
        // geometry must not depth-test over its display canvas.
        displayCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        overheadCamera.enabled = true;
        overlayCanvas.gameObject.SetActive(true);
        isVisible = true;
    }

    public void Hide()
    {
        if (isVisible && displayCamera != null)
            displayCamera.cullingMask = savedDisplayMask;
        isVisible = false;
        ClearReplayBounds();
        impactVehicle = null;
        accidentAvatar = null;

        if (overheadCamera != null)
        {
            overheadCamera.enabled = false;
        }

        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (isVisible)
        {
            UpdateCameraPose(false);
        }
    }

    private void EnsureResources()
    {
        if (overheadTexture == null)
        {
            overheadTexture = new RenderTexture(
                TextureWidth,
                TextureHeight,
                24,
                RenderTextureFormat.ARGB32)
            {
                name = "RT_AccidentOverhead",
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            overheadTexture.Create();
        }

        if (overheadCamera == null)
        {
            var cameraObject = new GameObject("Camera_AccidentOverhead");
            cameraObject.transform.SetParent(transform, false);
            overheadCamera = cameraObject.AddComponent<Camera>();
            overheadCamera.tag = "Untagged";
            overheadCamera.enabled = false;
            // Keep the replay out of the XR eye texture. URP additionally uses
            // allowXRRendering below; the property is retained for Unity's
            // camera contract and the existing scene validation.
            overheadCamera.stereoTargetEye = StereoTargetEyeMask.None;
            overheadCamera.fieldOfView = fieldOfView;
            overheadCamera.nearClipPlane = 0.1f;
            overheadCamera.farClipPlane = 100f;
            overheadCamera.clearFlags = CameraClearFlags.Skybox;
            overheadCamera.targetTexture = overheadTexture;
            overheadCamera.allowHDR = true;
            overheadCamera.allowMSAA = false;

            var uiLayer = LayerMask.NameToLayer("UI");
            overheadCamera.cullingMask = displayCamera.cullingMask;
            if (uiLayer >= 0)
            {
                overheadCamera.cullingMask &= ~(1 << uiLayer);
            }

            var cameraData = overheadCamera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.allowXRRendering = false;
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
        }

        if (overlayCanvas == null)
        {
            var canvasObject = new GameObject(
                "Canvas_AccidentOverhead",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(displayCamera.transform, false);

            overlayCanvas = canvasObject.GetComponent<Canvas>();
            ConfigureOverlayCanvas();

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateGraphic<Image>(canvasObject.transform, "Background");
            StretchToParent(background.rectTransform);
            background.color = Color.black;
            background.raycastTarget = false;

            var view = CreateGraphic<RawImage>(canvasObject.transform, "OverheadView");
            StretchToParent(view.rectTransform, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f));
            view.texture = overheadTexture;
            view.color = Color.white;
            view.raycastTarget = false;

            canvasObject.SetActive(false);
        }
    }

    private void ConfigureOverlayCanvas()
    {
        if (overlayCanvas == null || displayCamera == null)
        {
            return;
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        overlayCanvas.worldCamera = displayCamera;
        overlayCanvas.planeDistance = Mathf.Max(
            displayCamera.nearClipPlane + 0.012f,
            0.04f);
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = OverlaySortingOrder;

        var overlayTransform = overlayCanvas.transform;
        if (overlayTransform.parent != displayCamera.transform)
        {
            overlayTransform.SetParent(displayCamera.transform, false);
        }

        overlayTransform.localPosition = Vector3.zero;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one;
    }

    private void UpdateCameraPose(bool immediate)
    {
        if (overheadCamera == null)
        {
            return;
        }

        if (replayBounds.HasValue)
        {
            var replay = replayBounds.Value;
            overheadCamera.orthographic = true;
            overheadCamera.orthographicSize = Mathf.Max(6f, replay.size.z * 0.62f, replay.size.x / overheadCamera.aspect * 0.62f);
            overheadCamera.farClipPlane = 200f;
            overheadCamera.transform.SetPositionAndRotation(replay.center + Vector3.up * 80f, Quaternion.Euler(90f, 0f, 0f));
            return;
        }
        overheadCamera.orthographic = false;
        var bounds = new Bounds(impactPoint + Vector3.up * 0.75f, new Vector3(1f, 1.5f, 1f));
        EncapsulateRenderers(avatarRenderers, ref bounds);
        EncapsulateRenderers(vehicleRenderers, ref bounds);

        var focus = bounds.center;
        focus.y = Mathf.Max(focus.y, impactPoint.y + 0.55f);

        var horizontalSpan = Mathf.Max(bounds.size.x, bounds.size.z, 4.5f);
        var requiredHeight = (horizontalSpan * 0.72f) /
                             Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        var height = Mathf.Clamp(requiredHeight, minimumHeight, maximumHeight);
        var targetPosition = focus +
                             Vector3.up * height -
                             vehicleDirection * (height * horizontalOffsetRatio);
        var targetRotation = Quaternion.LookRotation(focus - targetPosition, Vector3.up);

        overheadCamera.fieldOfView = fieldOfView;
        overheadCamera.farClipPlane = Mathf.Max(50f, height * 5f);

        if (immediate || followSharpness <= 0f)
        {
            overheadCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
            return;
        }

        var blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        overheadCamera.transform.position = Vector3.Lerp(
            overheadCamera.transform.position,
            targetPosition,
            blend);
        overheadCamera.transform.rotation = Quaternion.Slerp(
            overheadCamera.transform.rotation,
            targetRotation,
            blend);
    }

    private void EncapsulateRenderers(Renderer[] renderers, ref Bounds aggregate)
    {
        if (renderers == null)
        {
            return;
        }

        for (var i = 0; i < renderers.Length; i++)
        {
            var current = renderers[i];
            if (current == null || !current.enabled || !current.gameObject.activeInHierarchy)
            {
                continue;
            }

            var currentBounds = current.bounds;
            if ((currentBounds.center - impactPoint).sqrMagnitude > 225f ||
                currentBounds.size.sqrMagnitude > 2500f)
            {
                continue;
            }

            aggregate.Encapsulate(currentBounds);
        }
    }

    private static T CreateGraphic<T>(Transform parent, string objectName) where T : Graphic
    {
        var graphicObject = new GameObject(objectName, typeof(RectTransform), typeof(T));
        graphicObject.layer = parent.gameObject.layer;
        graphicObject.transform.SetParent(parent, false);
        return graphicObject.GetComponent<T>();
    }

    private static void StretchToParent(
        RectTransform rectTransform,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null)
    {
        rectTransform.anchorMin = anchorMin ?? Vector2.zero;
        rectTransform.anchorMax = anchorMax ?? Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (overheadCamera != null)
        {
            overheadCamera.targetTexture = null;
        }

        if (overheadTexture != null)
        {
            overheadTexture.Release();
            Destroy(overheadTexture);
            overheadTexture = null;
        }

        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
            overlayCanvas = null;
        }
    }
}
