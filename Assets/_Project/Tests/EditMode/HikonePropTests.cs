using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VRLearn.Tests.EditMode
{
    public sealed class HikonePropTests
    {
        [TestCase("Car_Left")]
        [TestCase("Car_Right")]
        [TestCase("Car_EndlessGo")]
        [TestCase("Car_SideHit")]
        public void PortedSedanPreservesTheOriginalFootprintAndHasReadablePaintedBodyAndFourWheels(string name)
        {
            var car = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{name}.prefab");
            var legacy = car.transform.Find("Car_NO.1201");
            var visual = car.transform.Find("HK_SedanVisual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(legacy.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            var oldBounds = LocalBounds(car.transform, legacy);
            var newBounds = LocalBounds(car.transform, visual);
            Assert.That(Vector3.Distance(oldBounds.center, newBounds.center), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(oldBounds.size, newBounds.size), Is.LessThan(0.001f));
            var body = visual.GetComponentsInChildren<MeshFilter>().First(f => f.name == "BodyMain");
            Assert.That(body.sharedMesh.isReadable, Is.True, "cosmetic impact deformation");
            Assert.That(body.sharedMesh.colors.Length, Is.EqualTo(body.sharedMesh.vertexCount), "web pigments survived FBX import");
            Assert.That(body.GetComponent<Renderer>().sharedMaterials.All(m => m.GetFloat("_VertexPigment") == 1f), Is.True);
            Assert.That(visual.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Wheel_")), Is.EqualTo(4));
            foreach (var wheel in visual.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Wheel_")))
            {
                var bounds = LocalBounds(car.transform, wheel);
                Assert.That(bounds.min.y, Is.EqualTo(newBounds.min.y).Within(0.001f), wheel.name + " touches the ground");
                Assert.That(Mathf.Sign(bounds.center.z - newBounds.center.z),
                    Is.EqualTo(wheel.name.StartsWith("Wheel_F") ? 1f : -1f), wheel.name + " is at its axle");
            }
        }

        static Bounds LocalBounds(Transform space, Transform root)
        {
            var result = new Bounds();
            var first = true;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var b = f.sharedMesh.bounds;
                var m = space.worldToLocalMatrix * f.transform.localToWorldMatrix;
                foreach (var x in new[] { -1f, 1f })
                foreach (var y in new[] { -1f, 1f })
                foreach (var z in new[] { -1f, 1f })
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }
    }
}
