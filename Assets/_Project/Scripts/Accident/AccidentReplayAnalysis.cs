using UnityEngine;

/// <summary>
/// Feedback figures derived from the recorded replay: whether and when the participant looked
/// toward the vehicle that hit them, and how fast both were moving. Pure computation.
/// </summary>
public readonly struct AccidentReplayAnalysis
{
    /// <summary>Half-angle of the "looking toward the car" cone (horizontal, degrees).</summary>
    public const float GazeHalfAngle = 35f;
    /// <summary>Beyond this distance a car in the gaze cone does not count as noticed.</summary>
    public const float GazeMaxDistance = 60f;

    public readonly bool HasVehicle;
    /// <summary>Seconds the impact vehicle was inside the gaze cone before contact.</summary>
    public readonly float SecondsLookingAtVehicle;
    /// <summary>Seconds between the last look at the vehicle and contact; negative if never.</summary>
    public readonly float LastLookBeforeImpact;
    /// <summary>Vehicle speed just before contact (km/h), from the recorded track.</summary>
    public readonly float VehicleSpeedKmh;
    /// <summary>Participant speed over the last second before contact (km/h).</summary>
    public readonly float ParticipantSpeedKmh;

    public bool LookedAtVehicle => LastLookBeforeImpact >= 0f;

    AccidentReplayAnalysis(bool hasVehicle, float seconds, float lastLook, float vehicleKmh, float participantKmh)
    {
        HasVehicle = hasVehicle;
        SecondsLookingAtVehicle = seconds;
        LastLookBeforeImpact = lastLook;
        VehicleSpeedKmh = vehicleKmh;
        ParticipantSpeedKmh = participantKmh;
    }

    public static AccidentReplayAnalysis From(AccidentReplayRecording recording)
    {
        if (recording == null || !recording.IsValid)
            return new AccidentReplayAnalysis(false, 0f, -1f, 0f, 0f);

        var participantKmh = SpeedKmh(recording, recording.body, recording.impactTime);
        var vehicle = recording.ImpactVehicle;
        if (vehicle == null)
            return new AccidentReplayAnalysis(false, 0f, -1f, 0f, participantKmh);

        var seen = 0f;
        var lastLook = -1f;
        for (var i = 1; i < recording.FrameCount; i++)
        {
            var time = recording.times[i];
            // the moment of contact itself says nothing about having noticed the car
            if (time > recording.impactTime - 0.1f)
                break;
            var headPose = recording.head[i];
            var car = vehicle.poses[i];
            if (!headPose.active || !car.active)
                continue;
            if (IsLookingAt(headPose, car.position))
            {
                seen += time - recording.times[i - 1];
                lastLook = recording.impactTime - time;
            }
        }
        return new AccidentReplayAnalysis(
            true, seen, lastLook, SpeedKmh(recording, vehicle.poses, recording.impactTime), participantKmh);
    }

    public static bool IsLookingAt(AccidentReplayRecording.Pose head, Vector3 target)
    {
        var toTarget = Vector3.ProjectOnPlane(target - head.position, Vector3.up);
        var distance = toTarget.magnitude;
        if (distance > GazeMaxDistance || distance < 0.3f)
            return false;
        var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            return false;
        return Vector3.Angle(forward, toTarget) <= GazeHalfAngle;
    }

    static float SpeedKmh(AccidentReplayRecording recording, AccidentReplayRecording.Pose[] track, float atTime)
    {
        if (track == null)
            return 0f;
        var a = recording.Sample(track, atTime - 1f);
        var b = recording.Sample(track, atTime - 0.05f);
        if (!a.active || !b.active)
            return 0f;
        var distance = Vector3.ProjectOnPlane(b.position - a.position, Vector3.up).magnitude;
        return distance / 0.95f * 3.6f;
    }
}

/// <summary>
/// Feedback for a successful run (goal reached): how much the participant looked to each side
/// of their direction of travel and how close the nearest car came. Pure computation.
/// </summary>
public readonly struct CrossingAnalysis
{
    /// <summary>Head turned at least this far from the travel direction counts as a side check.</summary>
    public const float SideCheckMinAngle = 35f;
    public const float SideCheckMaxAngle = 150f;

    public readonly bool HasData;
    public readonly float SecondsLookingLeft;
    public readonly float SecondsLookingRight;
    /// <summary>Closest horizontal distance to any vehicle during the window (m); negative if none.</summary>
    public readonly float ClosestVehicleMeters;
    public readonly float DurationSeconds;

    public bool CheckedLeft => SecondsLookingLeft >= 0.3f;
    public bool CheckedRight => SecondsLookingRight >= 0.3f;

    CrossingAnalysis(bool hasData, float left, float right, float closest, float duration)
    {
        HasData = hasData;
        SecondsLookingLeft = left;
        SecondsLookingRight = right;
        ClosestVehicleMeters = closest;
        DurationSeconds = duration;
    }

    public static CrossingAnalysis From(AccidentReplayRecording recording)
    {
        if (recording == null || !recording.IsValid)
            return new CrossingAnalysis(false, 0f, 0f, -1f, 0f);

        // travel direction from where the participant started in the window to where they ended
        var first = FirstActive(recording.body) ?? FirstActive(recording.head);
        var last = LastActive(recording.body) ?? LastActive(recording.head);
        var travel = first.HasValue && last.HasValue
            ? Vector3.ProjectOnPlane(last.Value - first.Value, Vector3.up)
            : Vector3.zero;
        var left = 0f;
        var right = 0f;
        var closest = float.MaxValue;
        for (var i = 1; i < recording.FrameCount; i++)
        {
            var dt = recording.times[i] - recording.times[i - 1];
            var head = recording.head[i];
            if (head.active && travel.sqrMagnitude > 0.25f)
            {
                var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
                if (forward.sqrMagnitude > 1e-4f)
                {
                    var angle = Vector3.SignedAngle(travel, forward, Vector3.up);   // + = right
                    var magnitude = Mathf.Abs(angle);
                    if (magnitude >= SideCheckMinAngle && magnitude <= SideCheckMaxAngle)
                    {
                        if (angle > 0f) right += dt; else left += dt;
                    }
                }
            }
            var reference = recording.body[i].active ? recording.body[i].position : head.position;
            foreach (var vehicle in recording.vehicles)
            {
                var pose = vehicle.poses[i];
                if (!pose.active)
                    continue;
                var d = Vector3.ProjectOnPlane(pose.position - reference, Vector3.up).magnitude;
                if (d < closest)
                    closest = d;
            }
        }
        return new CrossingAnalysis(true, left, right, closest == float.MaxValue ? -1f : closest, recording.Duration);
    }

    static Vector3? FirstActive(AccidentReplayRecording.Pose[] track)
    {
        if (track == null) return null;
        foreach (var pose in track)
            if (pose.active) return pose.position;
        return null;
    }

    static Vector3? LastActive(AccidentReplayRecording.Pose[] track)
    {
        if (track == null) return null;
        for (var i = track.Length - 1; i >= 0; i--)
            if (track[i].active) return track[i].position;
        return null;
    }
}
