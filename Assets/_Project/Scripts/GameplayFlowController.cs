using System;
using UnityEngine;

/// <summary>
/// Scene-wide hard lock for traffic. Accident presentation is not the only
/// possible accident entry point, so traffic safety belongs to gameplay state.
/// </summary>
public static class TrafficAccidentState
{
    public static bool IsFrozen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ResetAfterSceneLoad()
    {
        IsFrozen = false;
    }

    public static void ResetForScenario()
    {
        IsFrozen = false;
    }

    public static void FreezeAll()
    {
        IsFrozen = true;

        foreach (var vehicle in UnityEngine.Object.FindObjectsByType<CarController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            vehicle.FreezeForAccident();
        }

        foreach (var factory in UnityEngine.Object.FindObjectsByType<CarFactory>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            factory.StopAllCoroutines();
            factory.enabled = false;
        }

        foreach (var factory in UnityEngine.Object.FindObjectsByType<AccidentCarFactory>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            factory.StopAllCoroutines();
            factory.enabled = false;
        }
    }
}

public enum GameplayPhase
{
    Preparing,
    Playing,
    GoalReached,
    AccidentTriggered,
    Impact,
    Replay,
    Results,
    Finished
}

/// <summary>
/// Owns gameplay phase transitions. Legacy presentation scripts can still render
/// the accident, but no longer decide independently whether a trigger is valid.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayFlowController : MonoBehaviour
{
    [SerializeField] GameplayPhase phase = GameplayPhase.Preparing;
    [SerializeField] int scenarioId = -1;
    [SerializeField] CenterEyeCamera presentation;

    public GameplayPhase Phase => phase;
    public int ScenarioId => scenarioId;
    public event Action<GameplayPhase> PhaseChanged;

    public void BeginScenario(int id)
    {
        TrafficAccidentState.ResetForScenario();
        scenarioId = id;
        if (presentation == null)
            presentation = OpenXRScene.MainCamera != null
                ? OpenXRScene.MainCamera.GetComponent<CenterEyeCamera>()
                : null;
        SetPhase(GameplayPhase.Playing);
    }

    private void LateUpdate()
    {
        if (TrafficAccidentState.IsFrozen)
            OpenXRScene.SetPlayerLocomotionEnabled(false);

        if (presentation == null)
            return;

        if (phase == GameplayPhase.AccidentTriggered && presentation.Acident != 0)
            MarkImpact();
        if (phase == GameplayPhase.Impact && presentation.AcidentProgress >= 1)
            MarkReplay();
        if (phase == GameplayPhase.GoalReached && presentation.AcidentProgress >= 3)
            MarkReplay();
        if (phase == GameplayPhase.Replay && presentation.AcidentProgress >= 5)
            MarkResults();
    }

    public bool TryReachGoal()
    {
        if (phase != GameplayPhase.Playing)
            return false;
        SetPhase(GameplayPhase.GoalReached);
        return true;
    }

    public bool TryTriggerAccident()
    {
        if (phase != GameplayPhase.Playing)
            return false;
        SetPhase(GameplayPhase.AccidentTriggered);
        return true;
    }

    public void MarkImpact()
    {
        if (phase == GameplayPhase.AccidentTriggered)
            SetPhase(GameplayPhase.Impact);
    }

    public void MarkReplay()
    {
        if (phase == GameplayPhase.Impact || phase == GameplayPhase.GoalReached)
            SetPhase(GameplayPhase.Replay);
    }

    public void MarkResults()
    {
        if (phase == GameplayPhase.Replay)
            SetPhase(GameplayPhase.Results);
    }

    public void Finish()
    {
        SetPhase(GameplayPhase.Finished);
    }

    private void SetPhase(GameplayPhase next)
    {
        if (phase == next)
            return;
        phase = next;
        if (phase == GameplayPhase.AccidentTriggered)
            TrafficAccidentState.FreezeAll();
        PhaseChanged?.Invoke(phase);
    }
}
