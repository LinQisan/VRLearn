using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Generated sedan appearance; original experiment transforms and physics stay intact.</summary>
public static class HikoneVehicleVisuals
{
    const string SedanPath = "Assets/_Project/Hikone/Prefabs/HK_Sedan.prefab";
    const string VisualName = "HK_SedanVisual";

    public static void RebuildCarPrefabs()
    {
        var sedan = AssetDatabase.LoadAssetAtPath<GameObject>(SedanPath);
        if (sedan == null) throw new InvalidOperationException("Missing " + SedanPath);
        foreach (var path in Directory.GetFiles("Assets/_Project/Prefabs", "Car_*.prefab"))
        {
            var car = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Replace(car, sedan);
                PrefabUtility.SaveAsPrefabAsset(car, path);
                HikoneEnvironmentBuilder.NormalizeSerializedText(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(car); }
        }
    }

    public static void ReplaceParkedCars(Scene scene)
    {
        var sedan = AssetDatabase.LoadAssetAtPath<GameObject>(SedanPath);
        if (sedan == null) throw new InvalidOperationException("Missing " + SedanPath);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var car in root.GetComponentsInChildren<Transform>(true)
                     .Where(t => t.name.StartsWith("Car 5") && t.Find("Car_NO.1201") != null).ToArray())
            Replace(car.gameObject, sedan);
    }

    static void Replace(GameObject car, GameObject sedan)
    {
        var legacy = car.transform.Find("Car_NO.1201");
        if (legacy == null) throw new InvalidOperationException(car.name + " has no original sedan visual");
        // Always measure the preserved original, including on a second generation pass.
        var original = BoundsIn(car.transform, legacy.GetComponentsInChildren<MeshFilter>(true));
        var old = car.transform.Find(VisualName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var lights = car.transform.Find("LightContainer");
        foreach (var r in car.GetComponentsInChildren<Renderer>(true))
        {
            if (lights != null && r.transform.IsChildOf(lights)) continue;
            if (r.sharedMaterials.Any(m => m != null &&
                    (m.name.StartsWith("Material_Invisible") || m.name == "ColliderOnlyMaterial"))) continue;
            r.enabled = false;
        }
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(sedan, car.transform);
        visual.name = VisualName;
        var source = BoundsIn(visual.transform, visual.GetComponentsInChildren<MeshFilter>(true));
        var scale = new Vector3(original.size.x / source.size.x, original.size.y / source.size.y, original.size.z / source.size.z);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = scale;
        visual.transform.localPosition = original.center - Vector3.Scale(source.center, scale);
        foreach (var t in visual.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = legacy.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
    }

    static Bounds BoundsIn(Transform space, IEnumerable<MeshFilter> filters)
    {
        var bounds = new Bounds();
        var first = true;
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            var matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (var x in new[] { -1f, 1f })
            foreach (var y in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
            {
                var point = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
}
