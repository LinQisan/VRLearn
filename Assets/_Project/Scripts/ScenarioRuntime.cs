using System;
using System.Collections.Generic;
using UnityEngine;

public enum ScenarioTrafficFlow
{
    None,
    Beside,
    Vertical
}

public enum ScenarioPlayerMode
{
    Walking,
    Bicycle
}

/// <summary>Player-mode questions that legacy code used to answer from the scenario number.</summary>
public static class ScenarioMode
{
    public static bool IsBicycle(int eventNumber)
    {
        var active = ScenarioRuntime.Current != null ? ScenarioRuntime.Current.Active : null;
        if (active != null)
            return active.PlayerMode == ScenarioPlayerMode.Bicycle;
        return eventNumber == 6 || eventNumber == 7 || eventNumber == 9;
    }

    /// <summary>Riding without the walking animation/footsteps (built-in 6, 7 and every custom ride).</summary>
    public static bool RidesWithoutWalkCycle(int eventNumber) =>
        eventNumber == 6 || eventNumber == 7 || (CustomScenarioSession.IsCustom(eventNumber) && IsBicycle(eventNumber));
}

[Serializable]
public sealed class ScenarioDefinition
{
    public ScenarioDefinitionAsset asset;
    public int id;
    public string displayName;
    public ScenarioPlayerMode playerMode;
    public GameObject root;
    public Transform playerSpawn;
    public GameObject goal;
    public GameObject accidentArea;
    public ScenarioTrafficFlow trafficFlow;
    /// <summary>Set for a scenario built at runtime from a JSON file (not serialized).</summary>
    [NonSerialized] public CustomScenario custom;

    public int Id => asset != null ? asset.id : id;
    public ScenarioPlayerMode PlayerMode => asset != null ? asset.playerMode : custom != null ? custom.PlayerMode : playerMode;
    public ScenarioTrafficFlow TrafficFlow => asset != null ? asset.trafficFlow : trafficFlow;
    public bool IsCustom => custom != null;

    // ---- description shared by the title, replay and feedback pages
    public string DisplayName => asset != null ? asset.displayName : custom != null ? custom.name : displayName;
    public string ShortTitle => asset != null ? asset.shortTitle : custom != null ? custom.name : displayName;
    public string ShortTitleEn => asset != null ? asset.shortTitleEn : custom != null ? custom.nameEn : string.Empty;
    public string LearningGoal => asset != null ? asset.learningGoal : custom != null ? custom.learningGoal : string.Empty;
    public string EventSummary => asset != null ? asset.eventSummary : custom != null ? custom.EventSummary : string.Empty;
    public ScenarioSetting Setting => asset != null ? asset.setting : custom != null ? custom.Setting : ScenarioSetting.Crossing;
    /// <summary>"01".."10" for built-ins, "カスタム" for custom scenarios.</summary>
    public string NumberLabel => IsCustom ? "カスタム" : (Id + 1).ToString("00");

    public void SetActive(bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
            return;
        }
        if (goal != null)
            goal.SetActive(active);
        if (playerSpawn != null)
            playerSpawn.gameObject.SetActive(active);
        if (accidentArea != null)
            accidentArea.SetActive(active);
    }
}

/// <summary>
/// Owns the scene objects that vary by accident scenario. The legacy scene used
/// sibling indices in three unrelated containers; this component stores the
/// relationship explicitly and activates exactly one entry.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1190)]
public sealed class ScenarioRuntime : MonoBehaviour
{
    [SerializeField] ScenarioDefinition[] entries;
    [SerializeField] GameObject besideTraffic;
    [SerializeField] GameObject verticalTraffic;
    [SerializeField] TrafficManager trafficManager;

    public static ScenarioRuntime Current { get; private set; }
    public ScenarioDefinition Active { get; private set; }
    public bool IsConfigurationValid { get; private set; }
    public int EntryCount => entries != null ? entries.Length : 0;

    private void Awake()
    {
        Current = this;
        IsConfigurationValid = ValidateConfiguration(true);
        Active = null;
        if (entries != null)
        {
            foreach (var entry in entries)
                entry?.SetActive(false);
        }

        if (trafficManager != null)
            trafficManager.Apply(ScenarioTrafficFlow.None);
        else
        {
            if (besideTraffic != null)
                besideTraffic.SetActive(false);
            if (verticalTraffic != null)
                verticalTraffic.SetActive(false);
        }
    }

    public ScenarioDefinition GetById(int id)
    {
        if (entries == null)
            return null;
        foreach (var entry in entries)
            if (entry != null && entry.Id == id)
                return entry;
        return null;
    }

    public bool ValidateConfiguration(bool logErrors = false)
    {
        var valid = true;
        if (entries == null || entries.Length != 10)
        {
            ReportValidationError(
                $"ScenarioRuntime requires exactly 10 entries; found {EntryCount}.", logErrors);
            valid = false;
        }

        var ids = new HashSet<int>();
        if (entries != null)
        {
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    ReportValidationError($"Scenario entry {index} is null.", logErrors);
                    valid = false;
                    continue;
                }

                var id = entry.Id;
                if (id < 0 || id > 9 || !ids.Add(id))
                {
                    ReportValidationError($"Scenario ID {id} is outside 0-9 or duplicated.", logErrors);
                    valid = false;
                }
                if (entry.asset == null || entry.root == null || entry.playerSpawn == null ||
                    entry.goal == null || entry.accidentArea == null)
                {
                    ReportValidationError(
                        $"Scenario {id} is missing its asset, root, spawn, goal, or accident area.",
                        logErrors);
                    valid = false;
                }
            }
        }

        for (var id = 0; id < 10; id++)
        {
            if (ids.Contains(id))
                continue;
            ReportValidationError($"Scenario ID {id} is missing.", logErrors);
            valid = false;
        }
        return valid;
    }

    void ReportValidationError(string message, bool logErrors)
    {
        if (logErrors)
            Debug.LogError(message, this);
    }

    public bool Activate(int id)
    {
        if (entries == null)
            return false;
        Active = null;
        foreach (var entry in entries)
        {
            if (entry == null)
                continue;
            var selected = entry.Id == id;
            entry.SetActive(selected);
            if (selected)
                Active = entry;
        }

        if (Active == null)
            return false;

        if (trafficManager != null)
        {
            trafficManager.Apply(Active.TrafficFlow);
        }
        else
        {
            if (besideTraffic != null)
                besideTraffic.SetActive(Active.TrafficFlow == ScenarioTrafficFlow.Beside);
            if (verticalTraffic != null)
                verticalTraffic.SetActive(Active.TrafficFlow == ScenarioTrafficFlow.Vertical);
        }
        return true;
    }

    /// <summary>A goal object to clone for runtime-built scenarios (visuals, GoalController, audio).</summary>
    public GameObject GoalTemplate => GetById(0)?.goal;

    /// <summary>Activates a scenario built at runtime: every built-in entry and all regular traffic stay off.</summary>
    public void ActivateCustom(ScenarioDefinition custom)
    {
        if (entries != null)
            foreach (var entry in entries)
                entry?.SetActive(false);
        Active = custom;
        custom.SetActive(true);
        if (trafficManager != null)
            trafficManager.Apply(ScenarioTrafficFlow.None);
        else
        {
            if (besideTraffic != null) besideTraffic.SetActive(false);
            if (verticalTraffic != null) verticalTraffic.SetActive(false);
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        ScenarioDefinition[] configuredEntries,
        GameObject configuredBesideTraffic,
        GameObject configuredVerticalTraffic)
    {
        entries = configuredEntries;
        besideTraffic = configuredBesideTraffic;
        verticalTraffic = configuredVerticalTraffic;
    }
#endif
}
