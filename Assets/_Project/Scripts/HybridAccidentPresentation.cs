using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Meta-only accident presentation:
/// tracked first-person impact, an opaque transition, then a stable overhead
/// accident view. Head tracking remains active throughout.
/// </summary>
[DisallowMultipleComponent]
public sealed class HybridAccidentPresentation : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0.05f)] float impactFlashDuration = 0.16f;
    [SerializeField, Min(0.4f)] float bodyViewDuration = 1.15f;
    [SerializeField, Min(0.05f)] float fadeToBlackDuration = 0.28f;
    [SerializeField, Min(0.05f)] float fadeFromBlackDuration = 0.4f;
    [SerializeField, Min(0.5f)] float observationDuration = 4f;

    [Header("Body-bound View")]
    [SerializeField, Min(0f)] float bodyViewForwardOffset = 0.12f;

    [Header("Fallback Fallen View")]
    [SerializeField, Min(0f)] float fallenSideOffset = 0.45f;
    [SerializeField, Min(0f)] float fallenBackOffset = 0.3f;
    [SerializeField, Min(0.15f)] float fallenEyeHeight = 0.32f;
    [SerializeField, Range(0f, 80f)] float fallenRollAngle = 62f;
    [SerializeField, Range(-20f, 20f)] float fallenPitchAngle = 8f;

    [Header("VR comfort")]
    [SerializeField] bool followBodyRotation = false;
    AccidentMotionHistory motionHistory;
    AccidentTrajectoryReplay trajectoryReplay;
    Camera trackedCamera;
    AvatarPresenter avatar;
    GameDirector director;
    GameplayFlowController flow;
    AccidentResultPresenter results;
    CenterEyeCamera legacyPresentation;
    AccidentOverheadView overheadView;
    Image transitionImage;
    bool configured;
    bool presenting;
    bool resultsVisible;
    float resultsDeadline;
    Transform bodyViewAnchor;
    Vector3 previousBodyViewPosition;
    Quaternion previousBodyViewRotation;
    bool bodyViewActive;
    float bodyViewStartedAt;
    Volume visionVolume;
    VolumeProfile visionProfile;
    DepthOfField visionDepthOfField;
    Vignette visionVignette;
    ChromaticAberration visionChromaticAberration;
    float visionEffectStartedAt;

    public bool IsPresenting => presenting;
    public bool IsBodyViewActive => bodyViewActive;
    public AccidentImpactPhysics LastImpactPhysics { get; private set; }

    void Update()
    {
        // Keep the locomotion lock authoritative for the whole accident and
        // result phases. This also defeats late initialization code that might
        // otherwise re-enable OVRPlayerController after the collision frame.
        if (presenting || resultsVisible)
            OpenXRScene.SetPlayerLocomotionEnabled(false);
        if (presenting)
            AnimateVisionImpairment();

        // Every Meta accident must converge on this presentation, including
        // legacy triggers that only changed the shared gameplay phase.
        if (configured
            && !presenting
            && !resultsVisible
            && flow.Phase == GameplayPhase.AccidentTriggered)
        {
            var player = FindFirstObjectByType<PlayerActor>();
            Vector3 fallbackPoint = player != null
                ? player.transform.position
                : trackedCamera.transform.position;
            BeginImpact(
                player,
                fallbackPoint,
                Vector3.ProjectOnPlane(trackedCamera.transform.forward, Vector3.up),
                0f);
        }

        // A presentation error must never strand the user without a way back.
        // This deadline uses real time and is independent of Time.timeScale.
        if (presenting
            && !resultsVisible
            && resultsDeadline > 0f
            && Time.realtimeSinceStartup >= resultsDeadline)
        {
            CompleteResults();
        }
    }

    void LateUpdate()
    {
        FollowAccidentBody();
    }

    public void Configure(
        Camera camera,
        AvatarPresenter configuredAvatar,
        GameDirector configuredDirector,
        GameplayFlowController configuredFlow,
        AccidentResultPresenter configuredResults,
        CenterEyeCamera configuredLegacyPresentation)
    {
        trackedCamera = camera;
        avatar = configuredAvatar;
        director = configuredDirector;
        flow = configuredFlow;
        results = configuredResults;
        legacyPresentation = configuredLegacyPresentation;
        overheadView = GetComponent<AccidentOverheadView>();
        if (overheadView == null)
            overheadView = gameObject.AddComponent<AccidentOverheadView>();
        overheadView.Configure(trackedCamera);
        configured = trackedCamera != null
            && avatar != null
            && director != null
            && flow != null
            && results != null
            && legacyPresentation != null;

        if (!configured)
        {
            Debug.LogError("Hybrid accident presentation references are incomplete.", this);
            enabled = false;
            return;
        }

        motionHistory = GetComponent<AccidentMotionHistory>();
        if (motionHistory == null) motionHistory = gameObject.AddComponent<AccidentMotionHistory>();
        trajectoryReplay = GetComponent<AccidentTrajectoryReplay>();
        if (trajectoryReplay == null) trajectoryReplay = gameObject.AddComponent<AccidentTrajectoryReplay>();
        CreateTransitionOverlay();
    }

    public bool BeginImpact(
        PlayerActor player,
        Vector3 impactPoint,
        Vector3 vehicleDirection,
        float vehicleSpeed,
        Transform impactVehicle = null)
    {
        if (!configured || presenting || flow.Phase != GameplayPhase.AccidentTriggered)
            return false;

        trajectoryReplay.Capture(motionHistory, impactVehicle);
        presenting = true;
        resultsVisible = false;
        resultsDeadline = Time.realtimeSinceStartup
            + impactFlashDuration
            + bodyViewDuration
            + fadeToBlackDuration
            + fadeFromBlackDuration
            + observationDuration
            + trajectoryReplay.Duration + 15f;
        vehicleDirection = Vector3.ProjectOnPlane(vehicleDirection, Vector3.up);
        if (vehicleDirection.sqrMagnitude < 0.001f)
            vehicleDirection = trackedCamera.transform.forward;
        vehicleDirection = Vector3.ProjectOnPlane(vehicleDirection, Vector3.up).normalized;

        OpenXRScene.SetPlayerLocomotionEnabled(false);
        OpenXRScene.SetControllersVisible(false);
        var phone = FindFirstObjectByType<SmartPhoneDistractionPresenter>(FindObjectsInactive.Include);
        if (phone != null)
            phone.Hide();

        var impactController = impactVehicle != null
            ? impactVehicle.GetComponentInParent<CarController>()
            : null;
        float vehicleMass = impactController != null
            ? impactController.ImpactVehicleMassKg
            : AccidentImpactPhysics.DefaultVehicleMassKg;
        LastImpactPhysics = AccidentImpactPhysics.Calculate(
            vehicleDirection,
            vehicleMass,
            avatar.DynamicBodyMassKg,
            vehicleSpeed);

        TrafficAccidentState.FreezeAll();
        if (impactController != null)
        {
            impactController.BeginAccidentResponse(LastImpactPhysics);
            var damage = impactController.GetComponent<AccidentVehicleDamage>();
            if (damage == null) damage = impactController.gameObject.AddComponent<AccidentVehicleDamage>();
            damage.Apply(impactPoint, vehicleDirection, vehicleSpeed);
        }
        results.SetImpactReport(LastImpactPhysics, trajectoryReplay.Duration);
        var bicycle = FindFirstObjectByType<BicycleController>();
        if (bicycle != null && bicycle.gameObject.activeInHierarchy)
            bicycle.BeginMetaCrash(vehicleDirection, vehicleSpeed);
        legacyPresentation.UseExternalAccidentPresentation(true);
        legacyPresentation.Acident = 1;
        legacyPresentation.AcidentProgress = 1;
        flow.MarkImpact();
        EnableVisionImpairment();

        try
        {
            // Switch VRIK/animation ownership to the weighted ragdoll on the
            // collision frame, then attach the tracked XR origin to its head.
            // Head tracking remains additive while the physical body falls.
            avatar.ShowForAccident(player, LastImpactPhysics);
            Physics.SyncTransforms();
            BeginBodyBoundView(vehicleDirection);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, avatar);
            bodyViewActive = false;
            bodyViewAnchor = null;
        }

        OpenXRInput.SetControllerVibration(0.65f);
        StartCoroutine(StopImpactHaptics());
        StartCoroutine(PresentAccident(
            player,
            impactPoint,
            vehicleDirection,
            vehicleSpeed,
            impactVehicle));
        return true;
    }

    IEnumerator StopImpactHaptics()
    {
        yield return new WaitForSecondsRealtime(0.14f);
        OpenXRInput.StopControllerVibration();
    }

    IEnumerator PresentAccident(
        PlayerActor player,
        Vector3 impactPoint,
        Vector3 vehicleDirection,
        float vehicleSpeed,
        Transform impactVehicle)
    {
        transitionImage.color = new Color(1f, 0.12f, 0.04f, 0.62f);
        yield return FadeOverlay(
            transitionImage.color,
            new Color(0.25f, 0f, 0f, 0f),
            impactFlashDuration);

        float visibleBodyTime = Mathf.Max(
            0f,
            bodyViewDuration - (Time.realtimeSinceStartup - bodyViewStartedAt));
        if (visibleBodyTime > 0f)
            yield return new WaitForSecondsRealtime(visibleBodyTime);

        yield return FadeOverlay(
            transitionImage.color,
            Color.black,
            fadeToBlackDuration);
        bodyViewActive = false;
        bodyViewAnchor = null;

        // The replay is rendered by a separate overhead camera so nearby body
        // and vehicle geometry can never trap or occlude the tracked XR view.
        try
        {
            bodyViewActive = false;
            bodyViewAnchor = null;
            // Keep the replay image clean: impact blur/vignette belongs to the
            // first-person hit moment and must not affect the observation view.
            DisableVisionImpairment();
            overheadView.Show(
                impactPoint,
                vehicleDirection,
                impactVehicle,
                avatar);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        legacyPresentation.AcidentProgress = 4;
        flow.MarkReplay();

        yield return FadeOverlay(Color.black, Color.clear, fadeFromBlackDuration);
        yield return trajectoryReplay.Play(overheadView, avatar, results.RuntimeFont);
        yield return new WaitForSecondsRealtime(observationDuration);

        CompleteResults();
    }

    public void ForceResults()
    {
        if (!configured)
        {
            var fallbackResults = FindFirstObjectByType<AccidentResultPresenter>(FindObjectsInactive.Include);
            if (fallbackResults != null)
                fallbackResults.Show(0);
            return;
        }

        presenting = true;
        CompleteResults();
    }

    void CompleteResults()
    {
        if (resultsVisible)
            return;

        StopAllCoroutines();
        OpenXRInput.StopControllerVibration();
        if (trajectoryReplay != null) trajectoryReplay.StopReplay();
        bodyViewActive = false;
        bodyViewAnchor = null;
        if (overheadView != null)
            overheadView.Hide();
        DisableVisionImpairment();
        if (transitionImage != null)
            transitionImage.color = Color.clear;
        if (legacyPresentation != null)
        {
            legacyPresentation.UseExternalAccidentPresentation(true);
            legacyPresentation.Acident = 1;
            legacyPresentation.AcidentProgress = 5;
        }
        if (flow != null)
        {
            flow.MarkReplay();
            flow.MarkResults();
        }

        try
        {
            if (results == null)
                results = FindFirstObjectByType<AccidentResultPresenter>(FindObjectsInactive.Include);
            if (results == null)
                throw new InvalidOperationException("AccidentResultPresenter was not found.");
            results.Show(Mathf.Clamp(director != null ? director.EventNumber : 0, 0, 9));
            resultsVisible = true;
            presenting = false;
            resultsDeadline = 0f;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            // Retry on the next frame instead of leaving the user trapped.
            resultsDeadline = Time.realtimeSinceStartup + 0.25f;
        }
    }

    bool BeginBodyBoundView(Vector3 impactDirection)
    {
        bodyViewAnchor = avatar != null ? avatar.GetAccidentViewAnchor() : null;
        var origin = OpenXRScene.Origin;
        if (bodyViewAnchor == null || origin == null || trackedCamera == null)
            return false;

        // Start at the model's eyes. From this point onward only the rigidbody
        // anchor's pose delta is applied, so the player still has live head
        // tracking while falling and rolling with the simulated body.
        Vector3 eyePosition = bodyViewAnchor.position
            + trackedCamera.transform.forward * bodyViewForwardOffset;
        origin.transform.position += eyePosition - trackedCamera.transform.position;
        previousBodyViewPosition = bodyViewAnchor.position;
        previousBodyViewRotation = bodyViewAnchor.rotation;
        bodyViewStartedAt = Time.realtimeSinceStartup;
        bodyViewActive = true;
        Physics.SyncTransforms();
        return true;
    }

    void FollowAccidentBody()
    {
        if (!bodyViewActive || bodyViewAnchor == null || trackedCamera == null)
            return;

        var origin = OpenXRScene.Origin;
        if (origin == null)
        {
            bodyViewActive = false;
            return;
        }

        Transform originTransform = origin.transform;
        Vector3 cameraPositionBeforeRotation = trackedCamera.transform.position;
        Quaternion rotationDelta = bodyViewAnchor.rotation
            * Quaternion.Inverse(previousBodyViewRotation);
        if (followBodyRotation)
            originTransform.rotation = rotationDelta * originTransform.rotation;
        originTransform.position += cameraPositionBeforeRotation
            - trackedCamera.transform.position;
        originTransform.position += bodyViewAnchor.position
            - previousBodyViewPosition;

        previousBodyViewPosition = bodyViewAnchor.position;
        previousBodyViewRotation = bodyViewAnchor.rotation;
    }

    void EnableVisionImpairment()
    {
        if (trackedCamera == null)
            return;

        if (visionVolume == null)
        {
            var volumeObject = new GameObject("HybridAccidentVisionImpairment");
            volumeObject.transform.SetParent(transform, false);
            visionVolume = volumeObject.AddComponent<Volume>();
            visionVolume.isGlobal = true;
            visionVolume.priority = 1000f;
            visionProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            visionVolume.sharedProfile = visionProfile;

            visionDepthOfField = visionProfile.Add<DepthOfField>(true);
            visionDepthOfField.mode.Override(DepthOfFieldMode.Gaussian);
            visionDepthOfField.gaussianStart.Override(0.05f);
            visionDepthOfField.gaussianEnd.Override(1.6f);
            visionDepthOfField.gaussianMaxRadius.Override(1.35f);
            visionDepthOfField.highQualitySampling.Override(false);

            visionVignette = visionProfile.Add<Vignette>(true);
            visionVignette.color.Override(new Color(0.12f, 0f, 0f));
            visionVignette.intensity.Override(0.48f);
            visionVignette.smoothness.Override(0.72f);

            visionChromaticAberration = visionProfile.Add<ChromaticAberration>(true);
            visionChromaticAberration.intensity.Override(0.38f);
        }

        trackedCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        visionEffectStartedAt = Time.realtimeSinceStartup;
        visionVolume.enabled = true;
        visionVolume.weight = 1f;
    }

    void AnimateVisionImpairment()
    {
        if (visionVolume == null || !visionVolume.enabled)
            return;

        // Slow pulses mimic unstable focus after the impact without moving the
        // tracked camera independently from the user's head.
        float elapsed = Time.realtimeSinceStartup - visionEffectStartedAt;
        float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 3.2f);
        visionVolume.weight = Mathf.Lerp(0.72f, 1f, pulse);
        if (visionDepthOfField != null)
            visionDepthOfField.gaussianMaxRadius.value = Mathf.Lerp(1.05f, 1.5f, pulse);
        if (visionChromaticAberration != null)
            visionChromaticAberration.intensity.value = Mathf.Lerp(0.22f, 0.48f, pulse);
    }

    void DisableVisionImpairment()
    {
        if (visionVolume == null)
            return;
        visionVolume.weight = 0f;
        visionVolume.enabled = false;
    }

    void OnDestroy()
    {
        if (visionProfile != null)
            Destroy(visionProfile);
    }

    IEnumerator FadeOverlay(Color from, Color to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transitionImage.color = Color.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        transitionImage.color = to;
    }

    void MoveRigToFallenView(Vector3 impactPoint, Vector3 vehicleDirection)
    {
        var origin = OpenXRScene.Origin;
        if (origin == null || trackedCamera == null)
            return;

        Vector3 side = Vector3.Cross(Vector3.up, vehicleDirection).normalized;
        Vector3 cameraOffset = trackedCamera.transform.position - impactPoint;
        if (Vector3.Dot(cameraOffset, side) < 0f)
            side = -side;

        Vector3 fallenPosition = impactPoint
            + side * fallenSideOffset
            - vehicleDirection * fallenBackOffset;
        if (Physics.Raycast(
            fallenPosition + Vector3.up * 4f,
            Vector3.down,
            out var groundHit,
            10f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore))
        {
            fallenPosition.y = groundHit.point.y + fallenEyeHeight;
        }
        else
        {
            fallenPosition.y = impactPoint.y + fallenEyeHeight;
        }

        Vector3 desiredForward = Vector3.ProjectOnPlane(
            impactPoint + vehicleDirection * 1.2f - fallenPosition,
            Vector3.up);
        if (desiredForward.sqrMagnitude < 0.001f)
            desiredForward = vehicleDirection;

        Transform originTransform = origin.transform;
        float roll = Vector3.Dot(side, trackedCamera.transform.right) >= 0f
            ? -fallenRollAngle
            : fallenRollAngle;
        Quaternion desiredCameraRotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up)
            * Quaternion.Euler(fallenPitchAngle, 0f, roll);
        Quaternion rotationDelta = desiredCameraRotation
            * Quaternion.Inverse(trackedCamera.transform.rotation);
        originTransform.rotation = rotationDelta * originTransform.rotation;
        originTransform.position += fallenPosition - trackedCamera.transform.position;
        Physics.SyncTransforms();
    }

    void CreateTransitionOverlay()
    {
        var existing = trackedCamera.transform.Find("HybridAccidentTransition");
        if (existing != null)
        {
            transitionImage = existing.GetComponentInChildren<Image>(true);
            if (transitionImage != null)
            {
                transitionImage.color = Color.clear;
                return;
            }
            Destroy(existing.gameObject);
        }

        var overlayObject = new GameObject(
            "HybridAccidentTransition",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        overlayObject.layer = LayerMask.NameToLayer("UI");
        overlayObject.transform.SetParent(trackedCamera.transform, false);

        var canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = trackedCamera;
        canvas.planeDistance = Mathf.Max(trackedCamera.nearClipPlane + 0.02f, 0.05f);
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        var imageObject = new GameObject(
            "FullScreenImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = overlayObject.layer;
        imageObject.transform.SetParent(overlayObject.transform, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        transitionImage = imageObject.GetComponent<Image>();
        transitionImage.raycastTarget = false;
        transitionImage.color = Color.clear;
    }
}
