using UnityEngine;

/// <summary>
/// Stable scenario metadata shared by the title, gameplay bootstrap and tooling.
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
}
