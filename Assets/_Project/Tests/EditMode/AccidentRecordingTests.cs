using System;
using NUnit.Framework;
using UnityEngine;

namespace VRLearn.Tests.EditMode
{
    public sealed class AccidentRecordingTests
    {
        [TestCase(0f)]
        [TestCase(-5f)]
        public void StationaryContactDoesNotInventImpactEnergy(float speed)
        {
            var type = Type.GetType("AccidentImpactPhysics, Assembly-CSharp");
            var impact = type.GetMethod("Calculate").Invoke(null, new object[] { Vector3.forward, 1200f, 70f, speed });
            Assert.That(type.GetProperty("PersonImpulseNewtonSeconds").GetValue(impact), Is.EqualTo(0f));
            Assert.That(type.GetProperty("PersonLaunchSpeedMetersPerSecond").GetValue(impact), Is.EqualTo(0f));
        }

        [Test]
        public void HistoryKeepsNewestSamplesAndInterpolatesWithoutChangingSource()
        {
            var go = new GameObject("RecordingTest");
            try
            {
                var type = Type.GetType("AccidentMotionHistory, Assembly-CSharp");
                var history = go.AddComponent(type);
                for (int i = 0; i < 200; i++)
                {
                    go.transform.position = new Vector3(i, 0f, 0f);
                    type.GetMethod("Capture").Invoke(history, new object[] { i * 0.05f });
                }
                var snapshot = (Array)type.GetMethod("Snapshot").Invoke(history, null);
                Assert.That(snapshot.Length, Is.EqualTo(121));
                var sampleType = snapshot.GetType().GetElementType();
                Assert.That(sampleType.GetField("position").GetValue(snapshot.GetValue(0)), Is.EqualTo(new Vector3(79f, 0f, 0f)));
                var pose = type.GetMethod("Evaluate").Invoke(null, new object[] { snapshot, 5.025f });
                Assert.That(((Vector3)sampleType.GetField("position").GetValue(pose)).x, Is.EqualTo(100.5f).Within(0.001f));
                type.GetMethod("ResetHistory").Invoke(history, null);
                Assert.That(type.GetProperty("Count").GetValue(history), Is.EqualTo(0));
                Assert.That(snapshot.Length, Is.EqualTo(121));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase("Car_EndlessGo")]
        [TestCase("Car_Left")]
        [TestCase("Car_Right")]
        [TestCase("Car_SideHit")]
        public void InvisibleVehicleHelpersDoNotRenderOrReflectLight(string name)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/" + name + ".prefab");
            int helpers = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.name != "Material_Invisible") continue;
                helpers++;
                Assert.That(renderer.enabled, Is.False, renderer.name + " should be collision geometry only.");
                Assert.That(renderer.GetComponent<Collider>(), Is.Not.Null);
            }
            Assert.That(helpers, Is.GreaterThan(0));
        }

        [Test]
        public void TeleportDiscardsDiscontinuousHistory()
        {
            var go = new GameObject("TeleportTest");
            try
            {
                var type = Type.GetType("AccidentMotionHistory, Assembly-CSharp");
                var history = go.AddComponent(type);
                type.GetMethod("Capture").Invoke(history, new object[] { 1f });
                go.transform.position = Vector3.right * 100f;
                type.GetMethod("Capture").Invoke(history, new object[] { 2f });
                Assert.That(type.GetProperty("Count").GetValue(history), Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
