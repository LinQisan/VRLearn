using UnityEngine;

/// <summary>Activates only the traffic network required by the current scenario.</summary>
[DisallowMultipleComponent]
public sealed class TrafficManager : MonoBehaviour
{
    [SerializeField] GameObject besideTraffic;
    [SerializeField] GameObject verticalTraffic;

    public void Apply(ScenarioTrafficFlow flow)
    {
        if (besideTraffic != null)
            besideTraffic.SetActive(flow == ScenarioTrafficFlow.Beside);
        if (verticalTraffic != null)
            verticalTraffic.SetActive(flow == ScenarioTrafficFlow.Vertical);
    }

#if UNITY_EDITOR
    public void EditorConfigure(GameObject beside, GameObject vertical)
    {
        besideTraffic = beside;
        verticalTraffic = vertical;
    }
#endif
}
