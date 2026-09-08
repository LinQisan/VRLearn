using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Replays recorded participant poses using render-only copies; never rewinds live physics.</summary>
[DisallowMultipleComponent]
public sealed class AccidentTrajectoryReplay : MonoBehaviour
{
    AccidentMotionHistory.Sample[] playerTrack;
    AccidentMotionHistory.Sample[] vehicleTrack;
    Transform vehicle;
    GameObject visualRoot;
    Transform carCopy;
    Transform playerMarker;
    TMP_Text caption;
    readonly List<Renderer> hidden = new List<Renderer>();
    Material pathMaterial;
    float firstTime;
    float lastTime;
    public float Duration => HasRecording ? Mathf.Max(0f, lastTime - firstTime) : 0f;
    public bool HasRecording => playerTrack != null && vehicleTrack != null
        && playerTrack.Length > 1 && vehicleTrack.Length > 1;
    public Bounds ReplayBounds { get; private set; }

    public void Capture(AccidentMotionHistory playerHistory, Transform impactVehicle)
    {
        vehicle = impactVehicle;
        var vehicleHistory = vehicle != null ? vehicle.GetComponent<AccidentMotionHistory>() : null;
        if (playerHistory == null || vehicleHistory == null)
            return;
        playerHistory.Capture(Time.time);
        vehicleHistory.Capture(Time.time);
        playerTrack = playerHistory.Snapshot();
        vehicleTrack = vehicleHistory.Snapshot();
        if (!HasRecording)
            return;
        lastTime = Mathf.Min(playerTrack[playerTrack.Length - 1].time, vehicleTrack[vehicleTrack.Length - 1].time);
        firstTime = Mathf.Max(lastTime - 6f, playerTrack[0].time, vehicleTrack[0].time);
        var bounds = new Bounds(AccidentMotionHistory.Evaluate(playerTrack, firstTime).position, Vector3.one * 4f);
        IncludeTrack(playerTrack, ref bounds);
        IncludeTrack(vehicleTrack, ref bounds);
        bounds.Expand(5f);
        ReplayBounds = bounds;
        // Copy the intact model now, before impact deformation is applied.
        BuildVisuals();
        visualRoot.SetActive(false);
    }

    void IncludeTrack(AccidentMotionHistory.Sample[] track, ref Bounds bounds)
    {
        foreach (var sample in track)
            if (sample.time >= firstTime && sample.time <= lastTime)
                bounds.Encapsulate(sample.position);
    }

    public IEnumerator Play(AccidentOverheadView view, AvatarPresenter avatar, TMP_FontAsset font)
    {
        if (!HasRecording || visualRoot == null || Duration < 0.1f)
            yield break;
        HideRenderers(vehicle);
        if (avatar != null)
            HideRenderers(avatar.transform);
        visualRoot.SetActive(true);
        view.SetReplayBounds(ReplayBounds);
        CreateCaption(view.OverlayCanvas.transform, font);
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            float time = firstTime + elapsed;
            ApplyPose(carCopy, AccidentMotionHistory.Evaluate(vehicleTrack, time));
            var playerPose = AccidentMotionHistory.Evaluate(playerTrack, time);
            playerMarker.position = playerPose.position + Vector3.up * 0.9f;
            caption.text = $"記録した軌跡の再生  •  接触まで {lastTime - time:0.0} 秒\n水色：体験者　／　黄：衝突車両　　他の交通は接触後の位置";
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        ApplyPose(carCopy, vehicleTrack[vehicleTrack.Length - 1]);
        playerMarker.position = playerTrack[playerTrack.Length - 1].position + Vector3.up * 0.9f;
        caption.text = "接触地点　／　この後、事故後の状態を表示します";
        yield return new WaitForSecondsRealtime(0.6f);
        StopReplay();
        view.ClearReplayBounds();
    }

    void HideRenderers(Transform root)
    {
        if (root == null)
            return;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;
            hidden.Add(renderer);
            renderer.enabled = false;
        }
    }

    void BuildVisuals()
    {
        visualRoot = new GameObject("RecordedAccidentReplay");
        carCopy = new GameObject("RecordedVehicle").transform;
        carCopy.SetParent(visualRoot.transform, false);
        carCopy.SetPositionAndRotation(vehicle.position, vehicle.rotation);
        carCopy.localScale = vehicle.lossyScale;
        foreach (var filter in vehicle.GetComponentsInChildren<MeshFilter>())
        {
            var source = filter.GetComponent<MeshRenderer>();
            if (source == null || !source.enabled || filter.sharedMesh == null)
                continue;
            var copy = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
            copy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            copy.transform.localScale = filter.transform.lossyScale;
            copy.transform.SetParent(carCopy, true);
            copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = copy.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var markerAsset = Resources.Load<Material>("AccidentReplayMarkers");
        pathMaterial = markerAsset != null ? new Material(markerAsset) : new Material(shader);
        pathMaterial.name = "ReplayMarkerMaterial";
        pathMaterial.SetColor("_BaseColor", new Color(0.15f, 0.9f, 1f));
        var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        marker.name = "RecordedParticipantMarker";
        marker.GetComponent<Collider>().enabled = false;
        Destroy(marker.GetComponent<Collider>());
        marker.GetComponent<Renderer>().sharedMaterial = pathMaterial;
        marker.transform.SetParent(visualRoot.transform, false);
        marker.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
        playerMarker = marker.transform;
        // Unity's headless macOS renderer has a native LineRenderer crash when
        // drawing dynamic paths. Quest/standalone builds keep the paths; the
        // no-graphics test runner still exercises poses, bounds and captions.
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            CreatePath("ParticipantPath", playerTrack, new Color(0.15f, 0.9f, 1f));
            CreatePath("VehiclePath", vehicleTrack, new Color(1f, 0.72f, 0.15f));
        }
    }

    void CreatePath(string name, AccidentMotionHistory.Sample[] track, Color color)
    {
        var path = new GameObject(name).AddComponent<LineRenderer>();
        path.transform.SetParent(visualRoot.transform, false);
        path.sharedMaterial = pathMaterial;
        path.startColor = path.endColor = color;
        path.startWidth = path.endWidth = 0.12f;
        path.shadowCastingMode = ShadowCastingMode.Off;
        path.receiveShadows = false;
        var points = new List<Vector3>();
        foreach (var sample in track)
            if (sample.time >= firstTime && sample.time <= lastTime)
                points.Add(sample.position + Vector3.up * 0.12f);
        path.positionCount = points.Count;
        path.SetPositions(points.ToArray());
    }

    void CreateCaption(Transform canvas, TMP_FontAsset font)
    {
        var label = new GameObject("ReplayCaption", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = canvas.gameObject.layer;
        label.transform.SetParent(canvas, false);
        var rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, 0.78f);
        rect.anchorMax = new Vector2(0.96f, 0.94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        caption = label.GetComponent<TextMeshProUGUI>();
        if (font != null) caption.font = font;
        caption.fontSize = 28f;
        caption.alignment = TextAlignmentOptions.Center;
        caption.color = Color.white;
        caption.outlineWidth = 0.2f;
        caption.raycastTarget = false;
    }

    static void ApplyPose(Transform target, AccidentMotionHistory.Sample pose) =>
        target.SetPositionAndRotation(pose.position, pose.rotation);

    public void StopReplay()
    {
        foreach (var renderer in hidden)
            if (renderer != null) renderer.enabled = true;
        hidden.Clear();
        if (visualRoot != null) visualRoot.SetActive(false);
        if (caption != null) Destroy(caption.gameObject);
        caption = null;
    }

    void OnDisable() => StopReplay();
    void OnDestroy()
    {
        StopReplay();
        if (visualRoot != null) Destroy(visualRoot);
        if (pathMaterial != null) Destroy(pathMaterial);
    }
}
