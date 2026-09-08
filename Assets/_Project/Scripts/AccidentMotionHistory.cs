using UnityEngine;

/// <summary>Six seconds of real poses, bounded memory and no per-frame allocations.</summary>
[DisallowMultipleComponent]
public sealed class AccidentMotionHistory : MonoBehaviour
{
    public const int Capacity = 121;
    public struct Sample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    readonly Sample[] samples = new Sample[Capacity];
    int next;
    public int Count { get; private set; }
    float nextSampleAt;

    void OnEnable() => ResetHistory();
    public void ResetHistory()
    {
        Count = next = 0;
        nextSampleAt = 0f;
    }

    void LateUpdate()
    {
        if (TrafficAccidentState.IsFrozen || Time.timeScale == 0f || Time.time < nextSampleAt)
            return;
        Capture(Time.time);
        nextSampleAt = Time.time + 0.05f;
    }

    public void Capture(float time)
    {
        // Respawning/teleporting starts a new continuous segment.
        if (Count > 0 && (transform.position - samples[(next + Capacity - 1) % Capacity].position).sqrMagnitude > 400f)
            ResetHistory();
        samples[next] = new Sample { time = time, position = transform.position, rotation = transform.rotation };
        next = (next + 1) % Capacity;
        Count = Mathf.Min(Count + 1, Capacity);
    }

    public Sample[] Snapshot()
    {
        var result = new Sample[Count];
        int first = (next - Count + Capacity) % Capacity;
        for (int i = 0; i < Count; i++)
            result[i] = samples[(first + i) % Capacity];
        return result;
    }

    public static Sample Evaluate(Sample[] track, float time)
    {
        if (track == null || track.Length == 0)
            return new Sample { rotation = Quaternion.identity };
        if (time <= track[0].time)
            return track[0];
        for (int i = 1; i < track.Length; i++)
        {
            if (track[i].time < time)
                continue;
            float t = Mathf.InverseLerp(track[i - 1].time, track[i].time, time);
            return new Sample {
                time = time,
                position = Vector3.Lerp(track[i - 1].position, track[i].position, t),
                rotation = Quaternion.Slerp(track[i - 1].rotation, track[i].rotation, t)
            };
        }
        return track[track.Length - 1];
    }
}
