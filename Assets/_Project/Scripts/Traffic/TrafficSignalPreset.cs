using UnityEngine;

public enum TrafficSignalAspect { Red, Yellow, Green }

/// <summary>The preserved experiment head and its red/yellow/green channel bindings.</summary>
public class TrafficSignalPreset : MonoBehaviour
{
    TrafficLightController red, yellow, green;
    bool cached, vertical, pedestrian;
    public bool IsVertical { get { Cache(); return vertical; } }
    public bool IsPedestrian { get { Cache(); return pedestrian; } }
    public TrafficSignalAspect Aspect { get; private set; }

    void Cache()
    {
        if (cached) return;
        cached = true;
        vertical = transform.parent != null && transform.parent.name == "Vertical";
        pedestrian = CompareTag("Light_Walk");
        red = transform.Find("RedLight")?.GetComponent<TrafficLightController>();
        yellow = transform.Find("YellowLight")?.GetComponent<TrafficLightController>();
        green = transform.Find("GreenLight")?.GetComponent<TrafficLightController>();
    }

    public void Apply(TrafficSignalAspect aspect, bool greenVisible = true)
    {
        Cache();
        Aspect = aspect;
        red?.SetLit(aspect == TrafficSignalAspect.Red);
        yellow?.SetLit(aspect == TrafficSignalAspect.Yellow && !IsPedestrian);
        green?.SetLit(aspect == TrafficSignalAspect.Green && greenVisible);
    }
}
