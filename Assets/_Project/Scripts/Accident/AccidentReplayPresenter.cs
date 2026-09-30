using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Post-accident replay: the recorded ~10 s before and just after contact, shown at once from
/// three synchronised cameras — overview (large, left), the participant's own eyes and the
/// driver's seat (stacked on the right, together half the overview's area). Replay and Skip
/// buttons sit below. At the end the replay stops on its last frame until the participant
/// presses "結果へ進む" (or A/X); it never advances by itself. Everything is render-only: live
/// physics is never rewound.
/// </summary>
[DisallowMultipleComponent]
public sealed class AccidentReplayPresenter : MonoBehaviour
{
    public const float SecondsBeforeImpact = 8f;
    public const float SecondsAfterImpact = 1.6f;
    const float SlowMotionFrom = 0.8f;        // seconds before contact
    const float SlowMotionUntil = 0.4f;       // seconds after contact
    const float SlowMotionRate = 0.45f;
    // world-locked panel 1.7 m away and 64° wide (see VrPanel); a head-locked black backdrop behind it
    public const float PanelDistance = 1.7f;
    public const float PanelAngularWidth = 64f;
    static readonly Vector2 PanelSize = new Vector2(1760f, 990f);
    const int SortingOrder = 31995;

    // lime, because the traffic cars in this project are painted teal
    static readonly Color ParticipantColor = new Color(0.49f, 1f, 0.2f);
    static readonly Color VehicleColor = new Color(1f, 0.72f, 0.15f);
    static readonly Color ContactColor = new Color(1f, 0.25f, 0.2f);
    static readonly Color FrameColor = new Color32(22, 33, 58, 255);

    // ---------------------------------------------------------------- state
    Camera displayCamera;
    TMP_FontAsset font;
    AccidentReplayRecording recording;
    readonly Dictionary<Transform, Transform> vehicleCopies = new Dictionary<Transform, Transform>();
    Transform bicycleCopy;
    ReplayMannequin participant;
    Vector3 fallDirection;
    Transform contactMarker;
    LineRenderer gazeLeft, gazeRight;
    readonly List<LineRenderer> paths = new List<LineRenderer>();
    readonly List<Renderer> hiddenLive = new List<Renderer>();
    Transform copyRoot;
    Material markerMaterial;
    float participantHeight = 1.4f;
    float participantWeight = 43f;

    Camera overviewCamera, participantCamera, driverCamera;
    RenderTexture overviewTexture, participantTexture, driverTexture;
    Canvas canvas;
    Image timelineFill;
    RectTransform contactTick;
    TMP_Text timeLabel, driverMissing, scenarioLabel;
    string pendingScenarioLabel = string.Empty;
    Image[] viewFrames;
    Button replayButton, skipButton;
    TMP_Text skipLabel, hintLabel;
    int savedDisplayMask;
    bool visible;

    float playhead;
    bool restartRequested;
    bool skipRequested;
    Vector3 overviewCenter;
    float overviewSpan;
    Vector3 overviewForward;
    Quaternion participantViewRotation;

    public bool IsVisible => visible;
    public bool IsPlaying { get; private set; }
    /// <summary>The playhead reached the end; waiting for the participant to go on.</summary>
    public bool IsWaitingAtEnd { get; private set; }
    public Camera OverviewCamera => overviewCamera;
    public Camera ParticipantCamera => participantCamera;
    public Camera DriverCamera => driverCamera;
    public Canvas Canvas => canvas;
    public AccidentReplayRecording Recording => recording;
    public float Playhead => playhead;

    public void Configure(Camera trackedCamera, TMP_FontAsset uiFont)
    {
        displayCamera = trackedCamera;
        font = uiFont;
    }

    /// <summary>
    /// Copies the intact vehicle and bicycle models at the moment of contact, before damage
    /// deformation is applied, so the replay shows the cars as they were.
    /// </summary>
    public void PrepareCopies(Transform bicycle)
    {
        ClearCopies();
        copyRoot = new GameObject("AccidentReplayCopies").transform;
        foreach (var car in FindObjectsByType<CarController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var copy = CopyRenderers(car.transform, "Replay_" + car.name);
            if (copy != null)
                vehicleCopies[car.transform] = copy;
        }
        if (bicycle != null && bicycle.gameObject.activeInHierarchy)
            bicycleCopy = CopyRenderers(bicycle, "Replay_Bicycle");
        copyRoot.gameObject.SetActive(false);
    }

    public ReplayMannequin Participant => participant;

    /// <summary>
    /// Runs the replay until the participant skips or, after it ends, presses Next; then hides itself.
    /// </summary>
    /// <param name="weightKg">menu weight; 0 = average build for the height</param>
    public IEnumerator Run(AccidentReplayRecording source, AvatarPresenter avatar, float heightMeters, float weightKg = 0f)
    {
        recording = source;
        participantHeight = Mathf.Clamp(heightMeters, 0.9f, 2.1f);
        participantWeight = weightKg > 0f ? weightKg : 22f * participantHeight * participantHeight;
        if (recording == null || !recording.IsValid || displayCamera == null)
            yield break;

        try
        {
            EnsureCameras();
            EnsureCanvas();
            BuildSceneOverlay();
            HideLive(avatar);
            copyRoot.gameObject.SetActive(true);
            savedDisplayMask = displayCamera.cullingMask;
            var ui = LayerMask.NameToLayer("UI");
            displayCamera.cullingMask = ui >= 0 ? 1 << ui : 0;
            overviewCamera.enabled = participantCamera.enabled = true;
            driverCamera.enabled = recording.ImpactVehicle != null;
            driverMissing.gameObject.SetActive(recording.ImpactVehicle == null);
            AnchorCanvas();
            scenarioLabel.text = pendingScenarioLabel;
            canvas.gameObject.SetActive(true);
            if (backdrop != null) backdrop.gameObject.SetActive(true);
            visible = true;
            OpenXRScene.SetControllersVisible(true);
            OpenXRScene.SetControllerVisualsVisible(false);
            MetaEditorSimulationController.ShowCursor();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Hide();
            yield break;
        }

        IsPlaying = true;
        skipRequested = false;
        var start = recording.StartTime;
        var end = recording.EndTime;
        var impact = Mathf.Clamp(recording.impactTime, start, end);
        PositionContactTick(start, end, impact);

        while (!skipRequested)
        {
            restartRequested = false;
            playhead = start;
            ResetOverviewFraming();
            SetWaitingAtEnd(false);
            while (!skipRequested && !restartRequested)
            {
                if (OpenXRInput.PrimaryButtonDown) skipRequested = true;
                if (OpenXRInput.SecondaryButtonDown) restartRequested = true;

                var rate = playhead > impact - SlowMotionFrom && playhead < impact + SlowMotionUntil ? SlowMotionRate : 1f;
                // a frame hitch must not skip part of the replay
                var delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f) * rate;
                playhead = Mathf.Min(end, playhead + delta);
                try
                {
                    ApplyFrame(playhead, delta, impact, start, end);
                }
                catch (Exception exception)
                {
                    // never trap the participant in a broken replay: fall through to feedback
                    Debug.LogException(exception, this);
                    skipRequested = true;
                }
                // stay on the last frame: the participant decides when to go on
                if (playhead >= end && !IsWaitingAtEnd)
                    SetWaitingAtEnd(true);
                yield return null;
            }
        }
        SetWaitingAtEnd(false);
        IsPlaying = false;
        Hide();
    }

    void SetWaitingAtEnd(bool waiting)
    {
        IsWaitingAtEnd = waiting;
        if (skipLabel != null)
            skipLabel.text = waiting ? "結果へ進む / Next" : "スキップ / Skip";
        if (hintLabel != null)
            hintLabel.text = waiting
                ? "<color=#FBBF24>再生が終わりました。「結果へ進む」か A / X で結果へ。</color>　B / Y：もう一度再生"
                : "B / Y：もう一度再生　　A / X：スキップ";
    }

    public void Skip() => skipRequested = true;
    public void Replay() => restartRequested = true;

    public void Hide()
    {
        IsPlaying = false;
        if (visible && displayCamera != null)
            displayCamera.cullingMask = savedDisplayMask;
        visible = false;
        if (overviewCamera != null) overviewCamera.enabled = false;
        if (participantCamera != null) participantCamera.enabled = false;
        if (driverCamera != null) driverCamera.enabled = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (backdrop != null) backdrop.gameObject.SetActive(false);
        if (copyRoot != null) copyRoot.gameObject.SetActive(false);
        foreach (var r in hiddenLive)
            if (r != null) r.enabled = true;
        hiddenLive.Clear();
    }

    // ---------------------------------------------------------------- per frame
    void ApplyFrame(float time, float delta, float impact, float start, float end)
    {
        // vehicles
        foreach (var track in recording.vehicles)
        {
            if (track.source == null || !vehicleCopies.TryGetValue(track.source, out var copy))
                continue;
            var pose = recording.Sample(track.poses, time);
            copy.gameObject.SetActive(pose.active);
            if (pose.active)
                copy.SetPositionAndRotation(pose.position, pose.rotation);
        }
        // participant: a figure of the menu height/weight, driven by the recorded head and body
        var head = recording.Sample(recording.head, time);
        var body = recording.Sample(recording.body, time);
        if (head.active)
        {
            var ground = body.active ? body.position.y : head.position.y - participantHeight * ReplayMannequin.EyeRatio;
            var earlier = recording.Sample(body.active ? recording.body : recording.head, time - 0.15f);
            var current = body.active ? body.position : head.position;
            var velocity = Vector3.ProjectOnPlane(current - earlier.position, Vector3.up) / 0.15f;
            var bikePose = recording.bicycle != null ? recording.Sample(recording.bicycle, time) : default;
            var seated = bikePose.active;
            var fall = time > impact ? Mathf.Clamp01((time - impact) / 0.7f) : 0f;
            // riding: the figure sits on the recorded bicycle (not just under the head) until the crash
            Vector3? seat = null;
            var seatForward = Vector3.zero;
            if (seated && fall <= 0f && recording.TrySeat(bikePose, out var seatPoint, out var forward))
            {
                seat = seatPoint;
                seatForward = forward;
            }
            participant.Pose(head, ground, velocity, seated, fall, fallDirection, Mathf.Max(delta, 0.0001f), seat, seatForward);
            UpdateGaze(head);
        }
        if (bicycleCopy != null && recording.bicycle != null)
        {
            var bike = recording.Sample(recording.bicycle, time);
            bicycleCopy.gameObject.SetActive(bike.active);
            if (bike.active) bicycleCopy.SetPositionAndRotation(bike.position, bike.rotation);
        }
        contactMarker.gameObject.SetActive(time >= impact);

        // cameras
        UpdateOverview(time, delta, head, impact);
        if (head.active)
        {
            var target = head.rotation;
            participantViewRotation = Quaternion.Slerp(participantViewRotation, target, 1f - Mathf.Exp(-14f * Mathf.Max(delta, 0.001f)));
            participantCamera.transform.SetPositionAndRotation(head.position, participantViewRotation);
        }
        var car = recording.ImpactVehicle;
        if (car != null)
        {
            var pose = recording.Sample(car.poses, time);
            if (pose.active)
            {
                // right-hand drive: driver's eyes above the steering wheel (+x), slightly behind it
                var eye = pose.position + pose.rotation * new Vector3(0.42f, 0.9f, 0.05f);
                driverCamera.transform.SetPositionAndRotation(eye, pose.rotation * Quaternion.Euler(3f, 0f, 0f));
            }
        }

        // ui
        var t = Mathf.InverseLerp(start, end, time);
        timelineFill.fillAmount = t;
        var toContact = impact - time;
        timeLabel.text = toContact > 0.05f ? $"接触まで {toContact:0.0} 秒"
            : toContact > -0.35f ? "<color=#FF5A4D>接触 / Contact</color>"
            : $"接触後 {-toContact:0.0} 秒";
        var flash = Mathf.Clamp01(1f - Mathf.Abs(toContact) / 0.35f);
        foreach (var frame in viewFrames)
            frame.color = Color.Lerp(FrameColor, ContactColor, flash);
    }

    void ResetOverviewFraming()
    {
        var head = recording.Sample(recording.head, recording.StartTime);
        var car = recording.ImpactVehicle;
        var impactPose = car != null ? recording.Sample(car.poses, recording.impactTime) : head;
        // camera looks across the vehicle's path, from the side the participant came from
        var vehicleDir = Vector3.forward;
        if (car != null)
        {
            var a = recording.Sample(car.poses, recording.impactTime - 1f);
            var d = Vector3.ProjectOnPlane(impactPose.position - a.position, Vector3.up);
            vehicleDir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.ProjectOnPlane(impactPose.rotation * Vector3.forward, Vector3.up).normalized;
        }
        fallDirection = vehicleDir;
        var forward = Vector3.Cross(vehicleDir, Vector3.up).normalized;
        var participantMove = Vector3.ProjectOnPlane(recording.Sample(recording.head, recording.impactTime).position - head.position, Vector3.up);
        if (Vector3.Dot(forward, participantMove) < 0f)
            forward = -forward;
        overviewForward = forward;
        overviewCenter = impactPose.position;
        overviewSpan = 30f;
        participantViewRotation = head.rotation;
        UpdateOverview(recording.StartTime, 1f, head, recording.impactTime);
    }

    void UpdateOverview(float time, float delta, AccidentReplayRecording.Pose head, float impact)
    {
        var car = recording.ImpactVehicle;
        var carPose = car != null ? recording.Sample(car.poses, time) : default;
        var impactPoint = car != null ? recording.Sample(car.poses, impact).position : head.position;
        var p = head.active ? head.position : impactPoint;
        var v = carPose.active ? carPose.position : impactPoint;
        var targetCenter = Vector3.Lerp((p + v) * 0.5f, impactPoint, 0.35f);
        targetCenter.y = impactPoint.y;
        var targetSpan = Mathf.Clamp(Vector3.Distance(p, v) * 1.25f + 8f, 14f, 46f);
        var k = 1f - Mathf.Exp(-3f * delta);
        overviewCenter = Vector3.Lerp(overviewCenter, targetCenter, k);
        overviewSpan = Mathf.Lerp(overviewSpan, targetSpan, k);
        var rotation = Quaternion.LookRotation(overviewForward, Vector3.up) * Quaternion.Euler(52f, 0f, 0f);
        var distance = overviewSpan * 0.5f / Mathf.Tan(overviewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
        overviewCamera.transform.SetPositionAndRotation(overviewCenter - rotation * Vector3.forward * distance, rotation);
    }

    void UpdateGaze(AccidentReplayRecording.Pose head)
    {
        if (gazeLeft == null)
            return;
        var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();
        var origin = participant.EyePosition;
        const float length = 6f;
        var left = Quaternion.AngleAxis(-AccidentReplayAnalysis.GazeHalfAngle, Vector3.up) * forward;
        var right = Quaternion.AngleAxis(AccidentReplayAnalysis.GazeHalfAngle, Vector3.up) * forward;
        gazeLeft.SetPosition(0, origin); gazeLeft.SetPosition(1, origin + left * length);
        gazeRight.SetPosition(0, origin); gazeRight.SetPosition(1, origin + right * length);
    }

    // ---------------------------------------------------------------- scene objects
    void BuildSceneOverlay()
    {
        if (copyRoot == null)
            copyRoot = new GameObject("AccidentReplayCopies").transform;
        EnsureMaterial();
        var proxyLayer = LayerMask.NameToLayer("Ignore Raycast");
        // rebuilt per run: height/weight may differ after a retry with new settings
        if (participant != null)
            Destroy(participant.gameObject);
        // the participant's own camera must not see its own body
        participant = ReplayMannequin.Build(copyRoot, participantHeight, participantWeight, proxyLayer);
        if (contactMarker == null)
        {
            contactMarker = Primitive(PrimitiveType.Cylinder, "ContactMarker", copyRoot, new Vector3(1.6f, 0.02f, 1.6f));
            contactMarker.GetComponent<Renderer>().material.SetColor("_BaseColor", ContactColor);
        }
        var car = recording.ImpactVehicle;
        var impactPoint = car != null ? recording.Sample(car.poses, recording.impactTime).position
            : recording.Sample(recording.head, recording.impactTime).position;
        contactMarker.position = impactPoint + Vector3.up * 0.03f;
        if (car != null && car.source != null && vehicleCopies.TryGetValue(car.source, out var impactCopy)
            && impactCopy.Find("ImpactVehiclePin") == null)
        {
            // marks "the car that hit you" in the overview; sits above the roof, so the
            // driver's camera inside the car does not see it
            var pin = Primitive(PrimitiveType.Sphere, "ImpactVehiclePin", impactCopy, new Vector3(0.7f, 0.7f, 0.7f));
            pin.localPosition = new Vector3(0f, 2.4f, 0.3f);
            pin.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", VehicleColor);
        }

        foreach (var p in paths)
            if (p != null) Destroy(p.gameObject);
        paths.Clear();
        // Unity's headless renderer crashes on dynamic LineRenderers; paths are visual only.
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return;
        var ownPath = Path("Path_Participant", recording.head, ParticipantColor, 0.14f);
        ownPath.gameObject.layer = proxyLayer;      // not drawn in the participant's own view
        paths.Add(ownPath);
        if (car != null)
            paths.Add(Path("Path_Vehicle", car.poses, VehicleColor, 0.14f));
        if (gazeLeft == null)
        {
            gazeLeft = Line("Gaze_Left", ParticipantColor, 0.06f);
            gazeRight = Line("Gaze_Right", ParticipantColor, 0.06f);
            gazeLeft.gameObject.layer = gazeRight.gameObject.layer = proxyLayer;
        }
    }

    LineRenderer Path(string name, AccidentReplayRecording.Pose[] track, Color color, float width)
    {
        var line = Line(name, color, width);
        var points = new List<Vector3>();
        var groundY = recording.Sample(recording.body, recording.impactTime).position.y;
        foreach (var pose in track)
            if (pose.active)
                points.Add(new Vector3(pose.position.x, groundY + 0.12f, pose.position.z));
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        return line;
    }

    LineRenderer Line(string name, Color color, float width)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(copyRoot, false);
        line.sharedMaterial = new Material(markerMaterial) { color = color };
        line.sharedMaterial.SetColor("_BaseColor", color);
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.positionCount = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = new Material(markerMaterial);
        r.sharedMaterial.SetColor("_BaseColor", ParticipantColor);
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    void EnsureMaterial()
    {
        if (markerMaterial != null)
            return;
        var asset = Resources.Load<Material>("AccidentReplayMarkers");
        markerMaterial = asset != null ? new Material(asset) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        markerMaterial.name = "AccidentReplayMarker";
    }

    Transform CopyRenderers(Transform source, string name)
    {
        var root = new GameObject(name).transform;
        root.SetParent(copyRoot, false);
        root.SetPositionAndRotation(source.position, source.rotation);
        var any = false;
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                continue;
            var copy = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
            copy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            copy.transform.localScale = filter.transform.lossyScale;
            copy.transform.SetParent(root, true);
            copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var copyRenderer = copy.GetComponent<MeshRenderer>();
            copyRenderer.sharedMaterials = renderer.sharedMaterials;
            copyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            any = true;
        }
        if (!any)
        {
            Destroy(root.gameObject);
            return null;
        }
        return root;
    }

    void HideLive(AvatarPresenter avatar)
    {
        hiddenLive.Clear();
        foreach (var source in vehicleCopies.Keys)
            HideRenderers(source);
        if (avatar != null)
            HideRenderers(avatar.transform);
        var context = GameplaySceneContext.Instance;
        if (context != null)
        {
            if (context.Bicycle != null) HideRenderers(context.Bicycle.transform);
            if (context.BicycleAfterAccident != null) HideRenderers(context.BicycleAfterAccident.transform);
        }
    }

    void HideRenderers(Transform root)
    {
        if (root == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            hiddenLive.Add(r);
            r.enabled = false;
        }
    }

    void ClearCopies()
    {
        vehicleCopies.Clear();
        bicycleCopy = null;
        participant = null;
        gazeLeft = gazeRight = null;
        paths.Clear();
        if (copyRoot != null)
            Destroy(copyRoot.gameObject);
        copyRoot = null;
    }

    // ---------------------------------------------------------------- cameras
    void EnsureCameras()
    {
        if (overviewCamera != null)
            return;
        var ui = LayerMask.NameToLayer("UI");
        var mask = displayCamera.cullingMask & ~(ui >= 0 ? 1 << ui : 0);
        overviewCamera = CreateCamera("Camera_ReplayOverview", 1280, 720, 50f, mask, out overviewTexture);
        var proxy = LayerMask.NameToLayer("Ignore Raycast");
        participantCamera = CreateCamera("Camera_ReplayParticipant", 768, 432, 72f, mask & ~(1 << proxy), out participantTexture);
        driverCamera = CreateCamera("Camera_ReplayDriver", 768, 432, 72f, mask, out driverTexture);
        driverCamera.nearClipPlane = 0.05f;
        participantCamera.nearClipPlane = 0.08f;
    }

    Camera CreateCamera(string name, int width, int height, float fov, int mask, out RenderTexture texture)
    {
        texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "RT_" + name, antiAliasing = 1 };
        texture.Create();
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.tag = "Untagged";
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.fieldOfView = fov;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 400f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.cullingMask = mask;
        camera.targetTexture = texture;
        camera.allowMSAA = false;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Base;
        data.allowXRRendering = false;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        return camera;
    }

    // ---------------------------------------------------------------- ui
    void EnsureCanvas()
    {
        if (canvas != null)
            return;
        var go = new GameObject("Canvas_AccidentReplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = LayerMask.NameToLayer("UI");
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rootRect = (RectTransform)go.transform;
        rootRect.sizeDelta = PanelSize;
        rootRect.localScale = Vector3.one * VrPanel.ScaleFor(PanelSize.x, PanelAngularWidth, PanelDistance);
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        go.AddComponent<VrPanel>();
        if (MetaEditorSimulationController.IsActive)
            MetaEditorSimulationController.ConfigureCanvas(canvas);
        else
        {
            var raycaster = go.AddComponent<OVRRaycaster>();
            raycaster.ignoreReversedGraphics = true;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            raycaster.blockingMask = 0;
        }
        AnchorCanvas();
        var root = (RectTransform)go.transform;

        EnsureBackdrop();
        var panel = Box(root, "ReplayPanel", Vector2.zero, PanelSize, UiKit.Panel, 1.4f);

        // header: REPLAY chip, title, scenario name; countdown pill on the right
        var chip = Box(panel, "Chip", new Vector2(-770f, 452f), new Vector2(150f, 50f), UiKit.Coral, 2f);
        var chipText = Text(chip, "Text", Vector2.zero, chip.sizeDelta, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        chipText.text = "REPLAY";
        chipText.color = UiKit.Ink;
        chipText.characterSpacing = 8f;
        var title = Text(panel, "Title", new Vector2(-250f, 452f), new Vector2(860f, 60f), 38f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.text = "事故のリプレイ / Accident replay";
        title.color = UiKit.Text;
        scenarioLabel = Text(panel, "Scenario", new Vector2(-250f, 412f), new Vector2(860f, 36f), 26f, FontStyles.Normal, TextAlignmentOptions.Left);
        scenarioLabel.color = UiKit.Muted;
        var timePill = Box(panel, "TimePill", new Vector2(640f, 440f), new Vector2(380f, 64f), UiKit.Card, 2f);
        timeLabel = Text(timePill, "Time", Vector2.zero, new Vector2(350f, 60f), 32f, FontStyles.Bold, TextAlignmentOptions.Center);

        // views: overview 1100x619 on the left; participant and driver 540x304 stacked on the right
        viewFrames = new Image[3];
        View(panel, "View_Overview", new Vector2(-305f, 70f), new Vector2(1100f, 619f), overviewTexture,
            "上空から / Overview", UiKit.Sky, 0);
        View(panel, "View_Participant", new Vector2(575f, 227.5f), new Vector2(540f, 304f), participantTexture,
            "あなたの視点 / Your view", ParticipantColor, 1);
        var driver = View(panel, "View_Driver", new Vector2(575f, -87.5f), new Vector2(540f, 304f), driverTexture,
            "運転者の視点 / Driver's view", VehicleColor, 2);
        driverMissing = Text(driver, "Missing", Vector2.zero, new Vector2(500f, 80f), 28f, FontStyles.Normal, TextAlignmentOptions.Center);
        driverMissing.text = "運転者の映像はありません";
        driverMissing.color = UiKit.Muted;

        // legend chips under the overview
        LegendChip(panel, new Vector2(-860f, -275f), 250f, ParticipantColor, "あなた（人物）");
        LegendChip(panel, new Vector2(-600f, -275f), 230f, ParticipantColor, "線：歩いた道");
        LegendChip(panel, new Vector2(-360f, -275f), 290f, ParticipantColor, "扇：見ていた方向");
        LegendChip(panel, new Vector2(-60f, -275f), 240f, VehicleColor, "ぶつかった車");
        LegendChip(panel, new Vector2(190f, -275f), 250f, ContactColor, "接触した場所");

        // timeline
        var track = Box(panel, "Timeline", new Vector2(0f, -330f), new Vector2(1660f, 14f), UiKit.CardRaised, 0.25f);
        var fill = Box(track, "Fill", Vector2.zero, Vector2.zero, UiKit.Sky);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
        timelineFill = fill.GetComponent<Image>();
        // Filled images only honour fillAmount when they have a sprite
        var white = Texture2D.whiteTexture;
        timelineFill.sprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f));
        timelineFill.type = Image.Type.Filled;
        timelineFill.fillMethod = Image.FillMethod.Horizontal;
        timelineFill.fillAmount = 0f;
        contactTick = UiKit.Dot(track, "ContactTick", ContactColor, Vector2.zero, 30f);
        var tickLabel = Text(contactTick, "Label", new Vector2(0f, 34f), new Vector2(120f, 30f), 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        tickLabel.text = "接触";
        tickLabel.color = ContactColor;

        // controls
        replayButton = ButtonBox(panel, "Button_Replay", new Vector2(-260f, -410f), new Vector2(480f, 90f),
            "もう一度再生 / Replay", UiKit.CardRaised, UiKit.Text, Replay);
        skipButton = ButtonBox(panel, "Button_Skip", new Vector2(260f, -410f), new Vector2(480f, 90f),
            "スキップ / Skip", UiKit.Amber, UiKit.Ink, Skip);
        skipLabel = skipButton.GetComponentInChildren<TMP_Text>(true);
        hintLabel = Text(panel, "Hint", new Vector2(0f, -470f), new Vector2(1600f, 34f), 24f, FontStyles.Normal, TextAlignmentOptions.Center);
        hintLabel.color = UiKit.Muted;
        SetWaitingAtEnd(false);
        canvas.gameObject.SetActive(false);
    }

    void LegendChip(RectTransform parent, Vector2 left, float width, Color color, string label)
    {
        var chip = Box(parent, "Legend_" + label, left + new Vector2(width * 0.5f, 0f), new Vector2(width - 12f, 44f), UiKit.Card, 2f);
        UiKit.Dot(chip, "Dot", color, new Vector2(-width * 0.5f + 30f, 0f), 18f);
        var text = Text(chip, "Text", new Vector2(14f, 0f), new Vector2(width - 60f, 40f), 26f, FontStyles.Normal, TextAlignmentOptions.Left);
        text.text = label;
        text.color = UiKit.Text;
    }

    /// <summary>Shown under the title, e.g. the scenario's short name.</summary>
    public void SetScenarioLabel(string text)
    {
        pendingScenarioLabel = text ?? string.Empty;
        if (scenarioLabel != null)
            scenarioLabel.text = pendingScenarioLabel;
    }

    /// <summary>World-locked in front of the viewer; sorting keeps it above the backdrop.</summary>
    void AnchorCanvas()
    {
        if (canvas == null || displayCamera == null)
            return;
        canvas.worldCamera = displayCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        canvas.GetComponent<VrPanel>().Attach(displayCamera.transform, PanelDistance);
    }

    Canvas backdrop;

    /// <summary>Dark, head-locked backdrop at the near plane: nothing of the scene shows around the panel.</summary>
    void EnsureBackdrop()
    {
        if (backdrop != null || displayCamera == null)
            return;
        var go = new GameObject("Canvas_ReplayBackdrop", typeof(RectTransform), typeof(Canvas));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(displayCamera.transform, false);
        backdrop = go.GetComponent<Canvas>();
        backdrop.renderMode = RenderMode.ScreenSpaceCamera;
        backdrop.worldCamera = displayCamera;
        backdrop.planeDistance = Mathf.Max(displayCamera.nearClipPlane + 0.01f, 0.04f);
        backdrop.overrideSorting = true;
        backdrop.sortingOrder = SortingOrder - 1;
        var image = Box((RectTransform)go.transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.06f, 1f));
        image.anchorMin = Vector2.zero; image.anchorMax = Vector2.one;
        image.offsetMin = image.offsetMax = Vector2.zero;
        go.SetActive(false);
    }

    RectTransform View(RectTransform parent, string name, Vector2 position, Vector2 size, RenderTexture texture, string label,
        Color key, int index)
    {
        var frame = Box(parent, name, position, size + new Vector2(12f, 12f), FrameColor, 0.5f);
        viewFrames[index] = frame.GetComponent<Image>();
        var imageGo = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageGo.layer = parent.gameObject.layer;
        imageGo.transform.SetParent(frame, false);
        var rect = (RectTransform)imageGo.transform;
        rect.sizeDelta = size;
        var raw = imageGo.GetComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        var width = Mathf.Min(size.x - 28f, 440f);
        var tag = Box(frame, "Label", Vector2.zero, new Vector2(width, 48f), new Color(0.02f, 0.03f, 0.06f, 0.8f), 2f);
        tag.anchorMin = tag.anchorMax = tag.pivot = new Vector2(0f, 0f);
        tag.anchoredPosition = new Vector2(16f, 16f);
        var dot = UiKit.Dot(tag, "Key", key, Vector2.zero, 16f);
        dot.anchorMin = dot.anchorMax = new Vector2(0f, 0.5f);
        dot.anchoredPosition = new Vector2(24f, 0f);
        var text = Text(tag, "Text", new Vector2(18f, 0f), tag.sizeDelta - new Vector2(56f, 0f), 28f, FontStyles.Bold, TextAlignmentOptions.Left);
        text.text = label;
        return frame;
    }

    void PositionContactTick(float start, float end, float impact)
    {
        var parent = (RectTransform)contactTick.parent;
        var x = Mathf.InverseLerp(start, end, impact);
        contactTick.anchorMin = contactTick.anchorMax = new Vector2(x, 0.5f);
        contactTick.anchoredPosition = Vector2.zero;
    }

    RectTransform Box(RectTransform parent, string name, Vector2 position, Vector2 size, Color color, float corner = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (corner > 0f)
        {
            image.sprite = UiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / corner;
        }
        return rect;
    }

    TMP_Text Text(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = align;
        text.color = UiKit.Text;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.extraPadding = true;
        text.raycastTarget = false;
        return text;
    }

    Button ButtonBox(RectTransform parent, string name, Vector2 position, Vector2 size, string label, Color color, Color textColor, UnityEngine.Events.UnityAction action)
    {
        var rect = Box(parent, name, position, size, Color.white, 2f);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.fadeDuration = 0.07f;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            TitleButtonFeedback.PlayClickFeedback();
            action();
        });
        var text = Text(rect, "Label", Vector2.zero, size - new Vector2(20f, 0f), 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.text = label;
        text.color = textColor;
        return button;
    }

    void OnDestroy()
    {
        Hide();
        foreach (var texture in new[] { overviewTexture, participantTexture, driverTexture })
            if (texture != null) { texture.Release(); Destroy(texture); }
        if (canvas != null) Destroy(canvas.gameObject);
        if (backdrop != null) Destroy(backdrop.gameObject);
        if (copyRoot != null) Destroy(copyRoot.gameObject);
        if (markerMaterial != null) Destroy(markerMaterial);
    }
}
