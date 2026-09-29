using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace VRLearn.Tests.EditMode
{
    /// <summary>The exported map (Scenarios/maps/hikone-kyobashi) matches the image and the scenarios.</summary>
    public sealed class ScenarioMapTests
    {
        [Serializable] sealed class MapFile
        {
            public string format; public string image; public int imageWidth, imageHeight; public float pixelsPerMeter;
            public Rect4 world; public Surface[] surfaces; public Lane[] lanes; public string[] vehicleModels;
        }
        [Serializable] sealed class Rect4 { public float xMin, zMin, xMax, zMax; }
        [Serializable] sealed class Surface { public string kind; public float[] triangles; }
        [Serializable] sealed class Lane { public string id; public P[] points; }
        [Serializable] sealed class P { public float x, z; }
        [Serializable] sealed class Scenario { public string id; public P spawn; public Area goal; public Vehicle[] vehicles; }
        [Serializable] sealed class Area { public float x, z; }
        [Serializable] sealed class Vehicle { public string model; public P[] route; }

        static readonly string[] RoadKinds = { "carriageway", "ramp", "sidewalk" };

        static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Scenarios"));
        static MapFile Map => JsonUtility.FromJson<MapFile>(File.ReadAllText(Path.Combine(Folder, "maps", "hikone-kyobashi", "map.json")));

        static float DistanceToSurface(MapFile map, float x, float z, params string[] kinds)
        {
            var best = float.MaxValue;
            var p = new Vector2(x, z);
            foreach (var s in map.surfaces.Where(s => kinds.Contains(s.kind)))
                for (var i = 0; i < s.triangles.Length; i += 6)
                {
                    var a = new Vector2(s.triangles[i], s.triangles[i + 1]);
                    var b = new Vector2(s.triangles[i + 2], s.triangles[i + 3]);
                    var c = new Vector2(s.triangles[i + 4], s.triangles[i + 5]);
                    if (Inside(p, a, b, c)) return 0f;
                    best = Mathf.Min(best, Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a))));
                }
            return best;
        }

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 o, Vector2 u, Vector2 v) => (u.x - o.x) * (v.y - o.y) - (u.y - o.y) * (v.x - o.x);
            var d1 = Cross(p, a, b); var d2 = Cross(p, b, c); var d3 = Cross(p, c, a);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
            return Vector2.Distance(p, a + t * (b - a));
        }

        [Test]
        public void ImageAndWorldRectangleAgree()
        {
            var map = Map;
            Assert.That(map.format, Is.EqualTo("vrlearn-map"));
            var png = File.ReadAllBytes(Path.Combine(Folder, "maps", "hikone-kyobashi", map.image));
            int BigEndian(int offset) => (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            Assert.That(BigEndian(16), Is.EqualTo(map.imageWidth));
            Assert.That(BigEndian(20), Is.EqualTo(map.imageHeight));
            Assert.That((map.world.xMax - map.world.xMin) * map.pixelsPerMeter, Is.EqualTo(map.imageWidth).Within(1f));
            Assert.That((map.world.zMax - map.world.zMin) * map.pixelsPerMeter, Is.EqualTo(map.imageHeight).Within(1f));
            Assert.That(map.surfaces.Select(s => s.kind), Is.SupersetOf(new[] { "carriageway", "sidewalk" }));
            Assert.That(map.lanes.Length, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void BuiltInLanesRunOnTheCarriageway()
        {
            var map = Map;
            // cars may cross the sidewalk at driveways (the parking exit of 07/08); never the open ground
            foreach (var lane in map.lanes)
                foreach (var p in lane.points)
                    Assert.That(DistanceToSurface(map, p.x, p.z, RoadKinds), Is.LessThan(1.5f), $"{lane.id} ({p.x}, {p.z})");
        }

        [Test]
        public void TemplatesFitTheMap()
        {
            var map = Map;
            foreach (var file in Directory.GetFiles(Path.Combine(Folder, "templates"), "*.json"))
            {
                var s = JsonUtility.FromJson<Scenario>(File.ReadAllText(file));
                foreach (var p in new[] { s.spawn, new P { x = s.goal.x, z = s.goal.z } })
                {
                    Assert.That(p.x, Is.InRange(map.world.xMin, map.world.xMax), s.id);
                    Assert.That(p.z, Is.InRange(map.world.zMin, map.world.zMax), s.id);
                    // participants stay on the road tiles; the "ground" plane also lies under the moat
                    Assert.That(DistanceToSurface(map, p.x, p.z, RoadKinds), Is.LessThan(0.5f),
                        $"{s.id}: ({p.x}, {p.z}) is not on a road or sidewalk");
                }
                foreach (var v in s.vehicles)
                {
                    Assert.That(map.vehicleModels, Does.Contain(v.model), s.id);
                    // route[0] is where the built-in factory stands; the rest is the lane
                    foreach (var p in v.route.Skip(1))
                        Assert.That(DistanceToSurface(map, p.x, p.z, RoadKinds), Is.LessThan(1.5f), $"{s.id} ({p.x}, {p.z})");
                }
            }
        }
    }
}
