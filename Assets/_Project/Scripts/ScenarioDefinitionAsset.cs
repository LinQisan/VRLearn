using System;
using UnityEngine;

/// <summary>Where a scenario takes place; used to group the choices on the title screen.</summary>
public enum ScenarioSetting
{
    Crossing,      // crosswalk at the Kyobashi intersection
    MidBlock,      // crossing Route 25 between intersections (parked trucks)
    Bicycle        // riding along Route 25
}

/// <summary>Which car factory an accident launch or traffic stop refers to.</summary>
public enum ScenarioFactory
{
    Left,               // shared traffic factories (GameplaySceneContext)
    Right,
    Left2,
    Right2,
    AreaAccident,       // CarFactory_Acident under the scenario's accident area
    AreaAccidentLeft,   // CarFactory_Acident_Left
    AreaAccidentRight   // CarFactory_Acident_Right
}

/// <summary>One accident car: which factory, how long after the trigger, initial heading and offset.</summary>
[Serializable]
public struct AccidentLaunch
{
    public ScenarioFactory factory;
    [Min(0f)] public float delaySeconds;
    public bool overrideYaw;
    public float yaw;
    public Vector3 offset;
}

/// <summary>
/// Stable scenario metadata shared by the title, gameplay bootstrap and tooling, plus the
/// accident schedule that runs when the participant enters the accident area.
/// Scene object bindings remain in ScenarioRuntime.
/// </summary>
[CreateAssetMenu(menuName = "VRLearn/Scenario Definition", fileName = "Scenario_00")]
public sealed class ScenarioDefinitionAsset : ScriptableObject
{
    public int id;
    public string displayName;
    [TextArea(2, 4)] public string eventSummary;
    public ScenarioPlayerMode playerMode;
    public ScenarioTrafficFlow trafficFlow;

    [Header("Title screen")]
    [Tooltip("Short Japanese name shown on the scenario tile.")]
    public string shortTitle;
    [Tooltip("Short English name shown under the Japanese one.")]
    public string shortTitleEn;
    public ScenarioSetting setting;
    [Tooltip("One sentence: what the participant should learn.")]
    [TextArea(1, 3)] public string learningGoal;

    [Header("Accident schedule (starts when the participant enters the accident area)")]
    [Tooltip("Factories whose regular traffic stops when the accident area is entered.")]
    public ScenarioFactory[] stopTraffic = Array.Empty<ScenarioFactory>();
    public AccidentLaunch[] accidentLaunches = Array.Empty<AccidentLaunch>();
}
