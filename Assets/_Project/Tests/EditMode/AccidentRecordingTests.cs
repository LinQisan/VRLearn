using System;
using System.Collections.Generic;
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

        static Type Recorder => Type.GetType("AccidentReplayRecorder, Assembly-CSharp");
        static Type Recording => Type.GetType("AccidentReplayRecording, Assembly-CSharp");
        static Type Analysis => Type.GetType("AccidentReplayAnalysis, Assembly-CSharp");

        static object Build(object recorder, Transform impactVehicle, float impactTime, float before, float after) =>
            Recorder.GetMethod("Build").Invoke(recorder, new object[] { impactVehicle, impactTime, before, after });

        static Vector3 SamplePosition(object recording, string track, float time)
        {
            var poses = Recording.GetField(track).GetValue(recording);
            var pose = Recording.GetMethod("Sample").Invoke(recording, new[] { poses, (object)time });
            return (Vector3)pose.GetType().GetField("position").GetValue(pose);
        }

        [Test]
        public void RecorderKeepsNewestTwelveSecondsAndInterpolates()
        {
            var root = new GameObject("RecorderTest");
            var head = new GameObject("Head").transform;
            try
            {
                var recorder = root.AddComponent(Recorder);
                Recorder.GetMethod("Configure").Invoke(recorder, new object[] { head, root.transform, null });
                var capture = Recorder.GetMethod("CaptureFrame");
                for (var i = 0; i < 400; i++)
                {
                    head.position = new Vector3(i, 1.5f, 0f);
                    capture.Invoke(recorder, new object[] { i * 0.05f, new List<Transform>() });
                }
                var capacity = (int)Recorder.GetField("Capacity").GetValue(null);
                Assert.That(Recorder.GetProperty("Count").GetValue(recorder), Is.EqualTo(capacity));
                Assert.That(capacity * 0.05f, Is.GreaterThanOrEqualTo(12f), "10 s replay + margin must fit.");

                var recording = Build(recorder, null, 19.95f, 8f, 0f);
                Assert.That((bool)Recording.GetProperty("IsValid").GetValue(recording), Is.True);
                Assert.That((float)Recording.GetProperty("Duration").GetValue(recording), Is.EqualTo(8f).Within(0.06f));
                Assert.That(SamplePosition(recording, "head", 19.025f).x, Is.EqualTo(380.5f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(head.gameObject);
            }
        }

        [Test]
        public void TeleportIsShownAsACutAndVanishedVehiclesAreInactive()
        {
            var root = new GameObject("TeleportTest");
            var head = new GameObject("Head").transform;
            var car = new GameObject("Car").transform;
            try
            {
                var recorder = root.AddComponent(Recorder);
                Recorder.GetMethod("Configure").Invoke(recorder, new object[] { head, root.transform, null });
                var capture = Recorder.GetMethod("CaptureFrame");
                capture.Invoke(recorder, new object[] { 0f, new List<Transform> { car } });
                head.position = Vector3.right * 100f;          // respawn-like jump
                capture.Invoke(recorder, new object[] { 0.05f, new List<Transform>() });   // car gone
                // a jump after contact is shown as a cut
                var recording = Build(recorder, car, 0f, 1f, 1f);
                Assert.That(SamplePosition(recording, "head", 0.02f).x, Is.EqualTo(0f), "a jump must not slide");
                // a jump before contact is a placement: the replay starts after it
                var placed = Build(recorder, car, 0.05f, 1f, 0f);
                Assert.That(Recording.GetProperty("StartTime").GetValue(placed), Is.EqualTo(0.05f));
                var vehicles = (System.Collections.IList)Recording.GetField("vehicles").GetValue(recording);
                Assert.That(vehicles.Count, Is.EqualTo(1));
                Assert.That(Recording.GetField("impactVehicleIndex").GetValue(recording), Is.EqualTo(0));
                var poses = (Array)vehicles[0].GetType().GetField("poses").GetValue(vehicles[0]);
                Assert.That(poses.GetValue(1).GetType().GetField("active").GetValue(poses.GetValue(1)), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(head.gameObject);
                UnityEngine.Object.DestroyImmediate(car.gameObject);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AnalysisMeasuresWhetherTheParticipantLookedAtTheCar(bool lookAtCar)
        {
            var root = new GameObject("AnalysisTest");
            var head = new GameObject("Head").transform;
            var car = new GameObject("Car").transform;
            try
            {
                var recorder = root.AddComponent(Recorder);
                Recorder.GetMethod("Configure").Invoke(recorder, new object[] { head, root.transform, null });
                var capture = Recorder.GetMethod("CaptureFrame");
                // car drives along +x towards the participant at 10 m/s; head faces the car
                // for the first second only, or never
                head.position = new Vector3(0f, 1.5f, 0f);
                for (var i = 0; i <= 60; i++)
                {
                    var t = i * 0.05f;
                    car.position = new Vector3(-30f + 10f * t, 0f, 0f);
                    var facingCar = lookAtCar && t < 1f;
                    head.rotation = Quaternion.LookRotation(facingCar ? Vector3.left : Vector3.forward);
                    capture.Invoke(recorder, new object[] { t, new List<Transform> { car } });
                }
                var recording = Build(recorder, car, 3f, 8f, 0f);
                var analysis = Analysis.GetMethod("From").Invoke(null, new[] { recording });
                var looked = (bool)Analysis.GetProperty("LookedAtVehicle").GetValue(analysis);
                var seconds = (float)Analysis.GetField("SecondsLookingAtVehicle").GetValue(analysis);
                var last = (float)Analysis.GetField("LastLookBeforeImpact").GetValue(analysis);
                var carKmh = (float)Analysis.GetField("VehicleSpeedKmh").GetValue(analysis);
                Assert.That(looked, Is.EqualTo(lookAtCar));
                Assert.That(carKmh, Is.EqualTo(36f).Within(0.5f));
                if (lookAtCar)
                {
                    Assert.That(seconds, Is.EqualTo(0.95f).Within(0.06f));
                    Assert.That(last, Is.EqualTo(2.05f).Within(0.06f));
                }
                else
                {
                    Assert.That(seconds, Is.EqualTo(0f));
                    Assert.That(last, Is.LessThan(0f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(head.gameObject);
                UnityEngine.Object.DestroyImmediate(car.gameObject);
            }
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
    }
}
