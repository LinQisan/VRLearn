using System.Linq;
using UnityEngine;

/// <summary>A synchronized signal clock; GameplayFlowController remains the gameplay owner.</summary>
[ExecuteAlways, DefaultExecutionOrder(50), DisallowMultipleComponent]
public sealed class TrafficSignalCycle : MonoBehaviour
{
    public const float GreenSeconds = 20f, YellowSeconds = 3f, ClearanceSeconds = 1f;
    public const float BlinkSeconds = 4f, HalfCycleSeconds = GreenSeconds + YellowSeconds + ClearanceSeconds;
    [SerializeField] TrafficSignalPreset[] signals = System.Array.Empty<TrafficSignalPreset>();
    GameplayFlowController flow;
    bool initialVerticalGreen;
    public float ElapsedSeconds { get; private set; }

    public void Configure(TrafficSignalPreset[] heads)
    {
        signals = heads;
        ResetForScenario(FindInScene<GameDirector>()?.EventNumber ?? 0);
    }

    T FindInScene<T>() where T : Component =>
        FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.gameObject.scene == gameObject.scene);

    void OnEnable()
    {
        if (!Application.isPlaying) ResetForScenario(FindInScene<GameDirector>()?.EventNumber ?? 0);
    }

    void Start()
    {
        if (Application.isPlaying) ResetForScenario(FindInScene<GameDirector>()?.EventNumber ?? 0);
    }

    void Update()
    {
        if (Application.isPlaying) Advance(Time.deltaTime);
    }

    // Preserves the old presets; 7, 9 and custom files previously lit every lens.
    // Those cases now use the same deterministic, valid default as scenario 0.
    public static bool StartsVerticalGreen(int scenarioId) =>
        scenarioId == 1 || scenarioId == 2 || scenarioId == 4 || scenarioId == 8;

    public void ResetForScenario(int scenarioId)
    {
        flow = FindInScene<GameplayFlowController>();
        initialVerticalGreen = StartsVerticalGreen(scenarioId);
        ElapsedSeconds = 0f;
        Apply();
    }

    public void Advance(float seconds)
    {
        if (flow == null || flow.Phase != GameplayPhase.Playing || TrafficAccidentState.IsFrozen
            || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        ElapsedSeconds = (ElapsedSeconds + seconds) % (HalfCycleSeconds * 2f);
        Apply();
    }

    public static TrafficSignalAspect Evaluate(float elapsed, bool startsVertical, bool vertical,
        bool pedestrian, out bool greenVisible)
    {
        var time = Mathf.Repeat(elapsed, HalfCycleSeconds * 2f);
        var activeVertical = time < HalfCycleSeconds ? startsVertical : !startsVertical;
        var local = time % HalfCycleSeconds;
        var active = vertical == activeVertical;
        greenVisible = true;
        if (pedestrian)
        {
            if (active || local >= GreenSeconds) return TrafficSignalAspect.Red;
            greenVisible = local < GreenSeconds - BlinkSeconds
                || Mathf.FloorToInt((local - (GreenSeconds - BlinkSeconds)) * 2f) % 2 == 0;
            return TrafficSignalAspect.Green;
        }
        return !active || local >= GreenSeconds + YellowSeconds ? TrafficSignalAspect.Red
            : local < GreenSeconds ? TrafficSignalAspect.Green : TrafficSignalAspect.Yellow;
    }

    void Apply()
    {
        foreach (var head in signals)
        {
            if (head == null) continue;
            var aspect = Evaluate(ElapsedSeconds, initialVerticalGreen, head.IsVertical,
                head.IsPedestrian, out var greenVisible);
            head.Apply(aspect, greenVisible);
        }
    }
}
