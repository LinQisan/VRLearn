using System;
using UnityEngine;

public enum WaypointRouteId
{
    Left,
    Right,
    Left2,
    Right2,
    Accident1,
    Accident2,
    Accident3,
    Accident4,
    Accident5,
    Accident6,
    Accident7,
    AccidentLeft,
    AccidentRight,
    Accident8Left,
    Accident8Right
}

[Serializable]
public struct WaypointRouteBinding
{
    public WaypointRouteId id;
    public Transform path;
}

/// <summary>Typed replacement for runtime GameObject.Find route lookup.</summary>
[DefaultExecutionOrder(-1200)]
[DisallowMultipleComponent]
public sealed class WaypointRouteRegistry : MonoBehaviour
{
    public static WaypointRouteRegistry Instance { get; private set; }
    [SerializeField] WaypointRouteBinding[] routes;

    private void Awake()
    {
        Instance = this;
    }

    public Transform Get(WaypointRouteId id)
    {
        foreach (var route in routes)
            if (route.id == id)
                return route.path;
        Debug.LogError($"Waypoint route {id} is not configured.", this);
        return null;
    }

    public static Transform Resolve(WaypointRouteId id)
    {
        if (Instance != null)
            return Instance.Get(id);

        var legacyPath = id switch
        {
            WaypointRouteId.Left => "WayPointContainer/Left",
            WaypointRouteId.Right => "WayPointContainer/Right",
            WaypointRouteId.Left2 => "WayPointContainer/Left2",
            WaypointRouteId.Right2 => "WayPointContainer/Right2",
            WaypointRouteId.Accident1 => "AcidentAreas1/WayPointContainer_Acident",
            WaypointRouteId.Accident2 => "AcidentAreas2/WayPointContainer_Acident",
            WaypointRouteId.Accident3 => "AcidentAreas3/WayPointContainer_Acident",
            WaypointRouteId.Accident4 => "AcidentAreas4/WayPointContainer_Acident",
            WaypointRouteId.Accident5 => "AcidentAreas5/WayPointContainer_Acident",
            WaypointRouteId.Accident6 => "AcidentAreas6/WayPointContainer_Acident",
            WaypointRouteId.Accident7 => "AcidentAreas7/WayPointContainer_Acident",
            WaypointRouteId.AccidentLeft => "WayPointContainer_Acident_Left/Left",
            WaypointRouteId.AccidentRight => "WayPointContainer_Acident_Right/Right",
            WaypointRouteId.Accident8Left => "AcidentAreas8/WayPointContainer_Acident_Left",
            WaypointRouteId.Accident8Right => "AcidentAreas8/WayPointContainer_Acident_Right",
            _ => string.Empty
        };
        var legacy = GameObject.Find(legacyPath);
        return legacy != null ? legacy.transform : null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(WaypointRouteBinding[] configuredRoutes)
    {
        routes = configuredRoutes;
    }
#endif
}
