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

    public int Id => asset != null ? asset.id : id;
    public ScenarioPlayerMode PlayerMode => asset != null ? asset.playerMode : playerMode;
    public ScenarioTrafficFlow TrafficFlow => asset != null ? asset.trafficFlow : trafficFlow;

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

    public ScenarioDefinition Active { get; private set; }
    public bool IsConfigurationValid { get; private set; }
    public int EntryCount => entries != null ? entries.Length : 0;

    private void Awake()
    {
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
