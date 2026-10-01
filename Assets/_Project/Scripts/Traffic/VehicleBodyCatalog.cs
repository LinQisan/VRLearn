using System;
using UnityEngine;

/// <summary>
/// Car bodies besides the prefabs' own sedan (Resources/VehicleBodies.asset, written by
/// Tools/VRLearn/Hikone/1. Import Models, Materials & Prefabs). See <see cref="VehicleBody"/>.
/// </summary>
[CreateAssetMenu(menuName = "VRLearn/Vehicle Body Catalog")]
public sealed class VehicleBodyCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        /// <summary>Scenario files (vehicles[].body) and the CarData CSV (Body) use this id.</summary>
        public string id;
        public string label;
        public GameObject prefab;
        /// <summary>Outer size in metres (width, height, length): collision box and light placement.</summary>
        public Vector3 size;
        public float massKg;
        /// <summary>Share of the ordinary traffic of the built-in scenarios (the rest are sedans).</summary>
        [Range(0f, 1f)] public float trafficShare;
        /// <summary>Material tinted per car with one of the colours.</summary>
        public string paintMaterial = "HK_KeiPaint";
        public Color[] colors = Array.Empty<Color>();
    }

    public Entry[] bodies = Array.Empty<Entry>();

    public Entry Find(string id) => Array.Find(bodies, b => b.id == id);
}
