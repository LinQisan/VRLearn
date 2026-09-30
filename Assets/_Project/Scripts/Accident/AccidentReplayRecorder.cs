using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable slice of recorded motion used by the accident replay and its evaluation.
/// Times are in <see cref="Time.time"/> seconds; poses are world space.
/// </summary>
public sealed class AccidentReplayRecording
{
    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool active;
    }

    public sealed class Track
    {
        public string name;
        public Transform source;
        public Pose[] poses;
    }

    /// <summary>Jumps larger than this between two frames are shown as a cut, not a slide.</summary>
    public const float TeleportDistance = 8f;

    public float[] times;
    public Pose[] head;
    public Pose[] body;
    public Pose[] bicycle;
    public readonly List<Track> vehicles = new List<Track>();
    public int impactVehicleIndex = -1;
    public float impactTime;

    public int FrameCount => times != null ? times.Length : 0;
    public float StartTime => FrameCount > 0 ? times[0] : 0f;
    public float EndTime => FrameCount > 0 ? times[FrameCount - 1] : 0f;
    public float Duration => Mathf.Max(0f, EndTime - StartTime);
    public bool IsValid => FrameCount > 1 && Duration > 0.1f;
    public Track ImpactVehicle => impactVehicleIndex >= 0 && impactVehicleIndex < vehicles.Count
        ? vehicles[impactVehicleIndex]
        : null;

    /// <summary>Interpolated pose at <paramref name="time"/>; inactive frames and teleports are not blended.</summary>
    public Pose Sample(Pose[] track, float time)
    {
        if (track == null || track.Length == 0 || FrameCount == 0)
            return new Pose { rotation = Quaternion.identity };
        if (time <= times[0])
            return track[0];
        var last = FrameCount - 1;
        if (time >= times[last])
            return track[last];

        // binary search for the frame pair around time
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (times[mid] <= time) lo = mid; else hi = mid;
        }
        var a = track[lo];
        var b = track[hi];
        if (!a.active || !b.active)
            return time - times[lo] < times[hi] - time ? a : b;
        if ((a.position - b.position).sqrMagnitude > TeleportDistance * TeleportDistance)
            return time - times[lo] < times[hi] - time ? a : b;
        var t = Mathf.InverseLerp(times[lo], times[hi], time);
        return new Pose
        {
            position = Vector3.Lerp(a.position, b.position, t),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
            active = true
        };
    }
}

/// <summary>
/// Records the participant (head and body), the bicycle and every vehicle at a fixed rate
/// into a bounded ring buffer. Keeps recording after the traffic freeze so the replay can
/// show the first moments after contact. No per-frame allocations once the ring is warm.
/// </summary>
[DisallowMultipleComponent]
public sealed class AccidentReplayRecorder : MonoBehaviour
{
    public const float SampleInterval = 0.05f;
    public const float BufferSeconds = 12f;
    public static readonly int Capacity = Mathf.CeilToInt(BufferSeconds / SampleInterval) + 1;

    sealed class VehicleRing
    {
        public Transform source;
        public AccidentReplayRecording.Pose[] poses;
        public int lastSeenFrame = -1;
    }

    Transform head;
    Transform body;
    Transform bicycle;

    readonly float[] times = new float[Capacity];
    readonly AccidentReplayRecording.Pose[] headRing = new AccidentReplayRecording.Pose[Capacity];
    readonly AccidentReplayRecording.Pose[] bodyRing = new AccidentReplayRecording.Pose[Capacity];
    readonly AccidentReplayRecording.Pose[] bicycleRing = new AccidentReplayRecording.Pose[Capacity];
    readonly Dictionary<Transform, VehicleRing> vehicles = new Dictionary<Transform, VehicleRing>();
    readonly List<Transform> scratchVehicles = new List<Transform>();

    int frameCounter;     // total frames captured since reset
    float nextSampleAt;
    bool recording = true;

    public int Count => Mathf.Min(frameCounter, Capacity);
    public bool IsRecording => recording;

    public void Configure(Transform headTransform, Transform bodyTransform, Transform bicycleTransform)
    {
        head = headTransform;
        body = bodyTransform;
        bicycle = bicycleTransform;
        ResetRecording();
    }

    public void ResetRecording()
    {
        frameCounter = 0;
        nextSampleAt = 0f;
        vehicles.Clear();
        recording = true;
    }

    /// <summary>Stops sampling; the buffer stays readable for <see cref="Build"/>.</summary>
    public void StopRecording() => recording = false;

    void LateUpdate()
    {
        if (!recording || Time.time < nextSampleAt)
            return;
        nextSampleAt = Time.time + SampleInterval;
        scratchVehicles.Clear();
        foreach (var car in FindObjectsByType<CarController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            scratchVehicles.Add(car.transform);
        CaptureFrame(Time.time, scratchVehicles);
    }

    public void CaptureFrame(float time, IReadOnlyList<Transform> activeVehicles)
    {
        var slot = frameCounter % Capacity;
        times[slot] = time;
        headRing[slot] = PoseOf(head);
        bodyRing[slot] = PoseOf(body);
        bicycleRing[slot] = PoseOf(bicycle != null && bicycle.gameObject.activeInHierarchy ? bicycle : null);

        if (activeVehicles != null)
        {
            for (var i = 0; i < activeVehicles.Count; i++)
            {
                var source = activeVehicles[i];
                if (source == null)
                    continue;
                if (!vehicles.TryGetValue(source, out var ring))
                {
                    ring = new VehicleRing { source = source, poses = new AccidentReplayRecording.Pose[Capacity] };
                    vehicles.Add(source, ring);
                }
                ring.poses[slot] = PoseOf(source);
                ring.lastSeenFrame = frameCounter;
            }
        }
        // vehicles not seen this frame are inactive in this slot
        foreach (var ring in vehicles.Values)
            if (ring.lastSeenFrame != frameCounter)
                ring.poses[slot] = default;

        frameCounter++;
    }

    static AccidentReplayRecording.Pose PoseOf(Transform t) => t == null
        ? default
        : new AccidentReplayRecording.Pose { position = t.position, rotation = t.rotation, active = true };

    /// <summary>
    /// Builds the replay window [impactTime - secondsBefore, impactTime + secondsAfter], clipped to
    /// what was recorded. Vehicles that never appear in the window are dropped.
    /// </summary>
    public AccidentReplayRecording Build(Transform impactVehicle, float impactTime, float secondsBefore, float secondsAfter)
    {
        var recording = new AccidentReplayRecording { impactTime = impactTime };
        var count = Count;
        if (count == 0)
        {
            recording.times = new float[0];
            return recording;
        }
        var oldest = frameCounter - count;
        var from = impactTime - secondsBefore;
        var to = impactTime + secondsAfter;

        var frames = new List<int>(count);
        for (var f = oldest; f < frameCounter; f++)
        {
            var t = times[f % Capacity];
            if (t >= from - 1e-4f && t <= to + 1e-4f)
                frames.Add(f % Capacity);
        }
        recording.times = new float[frames.Count];
        recording.head = new AccidentReplayRecording.Pose[frames.Count];
        recording.body = new AccidentReplayRecording.Pose[frames.Count];
        recording.bicycle = new AccidentReplayRecording.Pose[frames.Count];
        var anyBicycle = false;
        for (var i = 0; i < frames.Count; i++)
        {
            var s = frames[i];
            recording.times[i] = times[s];
            recording.head[i] = headRing[s];
            recording.body[i] = bodyRing[s];
            recording.bicycle[i] = bicycleRing[s];
            anyBicycle |= bicycleRing[s].active;
        }
        if (!anyBicycle)
            recording.bicycle = null;

        foreach (var ring in vehicles.Values)
        {
            var poses = new AccidentReplayRecording.Pose[frames.Count];
            var seen = false;
            for (var i = 0; i < frames.Count; i++)
            {
                poses[i] = ring.poses[frames[i]];
                seen |= poses[i].active;
            }
            if (!seen)
                continue;
            if (impactVehicle != null && ring.source == impactVehicle)
                recording.impactVehicleIndex = recording.vehicles.Count;
            recording.vehicles.Add(new AccidentReplayRecording.Track
            {
                name = ring.source != null ? ring.source.name : "Vehicle",
                source = ring.source,
                poses = poses
            });
        }
        return recording;
    }
}
