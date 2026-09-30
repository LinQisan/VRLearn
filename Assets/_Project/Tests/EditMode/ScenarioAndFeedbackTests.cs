using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VRLearn.Tests.EditMode
{
    /// <summary>Scenario content, success feedback analysis and the replay figure.</summary>
    public sealed class ScenarioAndFeedbackTests
    {
        static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");
        static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
        static object Prop(object target, string property) => target.GetType().GetProperty(property).GetValue(target);

        static List<ScriptableObject> Scenarios() => AssetDatabase
            .FindAssets("t:ScenarioDefinitionAsset", new[] { "Assets/_Project/ScenarioDefinitions" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(s => (int)Get(s, "id"))
            .ToList();

        [Test]
        public void TenScenariosAreNamedGroupedAndExplained()
        {
            var scenarios = Scenarios();
            Assert.That(scenarios.Select(s => (int)Get(s, "id")), Is.EqualTo(Enumerable.Range(0, 10)));
            Assert.That(scenarios.Select(s => (string)Get(s, "shortTitle")).Distinct().Count(), Is.EqualTo(10),
                "Every scenario needs its own short name on the title tiles.");
            foreach (var s in scenarios)
            {
                Assert.That((string)Get(s, "shortTitle"), Is.Not.Empty.And.Length.LessThanOrEqualTo(12), s.name);
                Assert.That((string)Get(s, "shortTitleEn"), Is.Not.Empty, s.name);
                Assert.That((string)Get(s, "learningGoal"), Is.Not.Empty, s.name);
                var summary = (string)Get(s, "eventSummary");
                Assert.That(summary, Does.StartWith("事故の状況：").And.Contain("\n安全確認のポイント："), s.name);
            }
            // each group on the title screen has entries
            var settings = scenarios.Select(s => Get(s, "setting").ToString()).ToList();
            Assert.That(settings.Distinct(), Is.EquivalentTo(new[] { "Crossing", "MidBlock", "Bicycle" }));
            Assert.That(scenarios.Where(s => Get(s, "playerMode").ToString() == "Bicycle").Select(s => Get(s, "setting").ToString()),
                Is.All.EqualTo("Bicycle"), "Bicycle scenarios are grouped under 自転車.");
        }

        [Test]
        public void AccidentSchedulesAreData()
        {
            foreach (var s in Scenarios())
            {
                var id = (int)Get(s, "id");
                var launches = (Array)Get(s, "accidentLaunches");
                if (id == 9)
                {
                    Assert.That(launches.Length, Is.Zero, "Scenario 10 (wrong-way riding) uses regular oncoming traffic.");
                    continue;
                }
                Assert.That(launches.Length, Is.GreaterThanOrEqualTo(1), s.name);
                foreach (var launch in launches)
                    Assert.That((float)Get(launch, "delaySeconds"), Is.InRange(0f, 15f), s.name);
            }
        }

        [Test]
        public void SuccessAnalysisSeparatesLeftAndRightChecks()
        {
            var recorderType = T("AccidentReplayRecorder");
            var root = new GameObject("CrossingTest");
            var head = new GameObject("Head").transform;
            try
            {
                var recorder = root.AddComponent(recorderType);
                recorderType.GetMethod("Configure").Invoke(recorder, new object[] { head, root.transform, null });
                var capture = recorderType.GetMethod("CaptureFrame");
                // walk +z for 4 s: 1 s looking left, 0.5 s looking right, the rest ahead
                for (var i = 0; i <= 80; i++)
                {
                    var t = i * 0.05f;
                    root.transform.position = new Vector3(0f, 0f, t * 1.2f);
                    head.position = root.transform.position + Vector3.up * 1.6f;
                    var yaw = t >= 1f && t < 2f ? -90f : t >= 2.5f && t < 3f ? 90f : 0f;
                    head.rotation = Quaternion.Euler(0f, yaw, 0f);
                    capture.Invoke(recorder, new object[] { t, new List<Transform>() });
                }
                var recording = recorderType.GetMethod("Build").Invoke(recorder, new object[] { null, 4f, 12f, 0f });
                var analysis = T("CrossingAnalysis").GetMethod("From").Invoke(null, new[] { recording });
                Assert.That((bool)Get(analysis, "HasData"), Is.True);
                Assert.That((float)Get(analysis, "SecondsLookingLeft"), Is.EqualTo(1f).Within(0.06f));
                Assert.That((float)Get(analysis, "SecondsLookingRight"), Is.EqualTo(0.5f).Within(0.06f));
                Assert.That((bool)Prop(analysis, "CheckedLeft") && (bool)Prop(analysis, "CheckedRight"), Is.True);
                Assert.That((float)Get(analysis, "ClosestVehicleMeters"), Is.LessThan(0f), "No vehicles were recorded.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(head.gameObject);
            }
        }

        [TestCase(1.2f, 30f)]
        [TestCase(1.7f, 65f)]
        [TestCase(1.85f, 110f)]
        public void ReplayFigureMatchesMenuHeight(float height, float weight)
        {
            var type = T("ReplayMannequin");
            var poseType = T("AccidentReplayRecording").GetNestedType("Pose");
            var figure = (Component)type.GetMethod("Build").Invoke(null, new object[] { null, height, weight, 0 });
            try
            {
                var pose = Activator.CreateInstance(poseType);
                poseType.GetField("position").SetValue(pose, new Vector3(0f, height * 0.936f, 0f));
                poseType.GetField("rotation").SetValue(pose, Quaternion.identity);
                poseType.GetField("active").SetValue(pose, true);
                type.GetMethod("Pose").Invoke(figure, new object[] { pose, 0f, Vector3.zero, false, 0f, Vector3.zero, 0.02f, null, Vector3.zero });
                Assert.That((float)Prop(figure, "TopOfHeadY"), Is.EqualTo(height).Within(0.04f * height));
                var bounds = new Bounds(figure.transform.position, Vector3.zero);
                foreach (var r in figure.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);
                Assert.That(bounds.size.y, Is.EqualTo(height).Within(0.05f * height));
                Assert.That(figure.GetComponentsInChildren<Collider>(), Is.Empty, "The replay figure is render-only.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(figure.gameObject);
            }
        }

        [Test]
        public void CyclingReplayStartsAtPlacementWithTheRiderOnTheSaddle()
        {
            var recorderType = T("AccidentReplayRecorder");
            var recordingType = T("AccidentReplayRecording");
            var root = new GameObject("Player");
            var head = new GameObject("Head").transform;
            var bike = new GameObject("Bicycle");
            // the model's forward is -x; the seat is behind the rider's eyes
            var hip = new GameObject("HipTarget").transform;
            hip.SetParent(bike.transform, false);
            hip.localPosition = new Vector3(0.3f, 0.9f, 0f);
            var eyes = new GameObject("HeadTarget").transform;
            eyes.SetParent(bike.transform, false);
            eyes.localPosition = new Vector3(-0.1f, 1.5f, 0f);
            Component figure = null;
            try
            {
                var recorder = root.AddComponent(recorderType);
                recorderType.GetMethod("Configure").Invoke(recorder, new object[] { head, root.transform, bike.transform });
                var capture = recorderType.GetMethod("CaptureFrame");
                // 1 s somewhere else without the bicycle (before placement), then 3 s riding +z at 5 m/s
                bike.SetActive(false);
                root.transform.position = new Vector3(40f, 0f, 30f);
                head.position = root.transform.position + Vector3.up * 1.6f;
                for (var i = 0; i < 20; i++)
                    capture.Invoke(recorder, new object[] { i * 0.05f, new List<Transform>() });
                bike.SetActive(true);
                bike.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // -x (forward) -> +z
                for (var i = 20; i <= 80; i++)
                {
                    var t = i * 0.05f;
                    bike.transform.position = new Vector3(0f, 0f, (t - 1f) * 5f);
                    head.position = eyes.position;
                    head.rotation = Quaternion.Euler(0f, t < 2f ? -70f : 0f, 0f);   // looks left, then ahead
                    root.transform.position = new Vector3(head.position.x, 0f, head.position.z);
                    capture.Invoke(recorder, new object[] { t, new List<Transform>() });
                }
                var recording = recorderType.GetMethod("Build").Invoke(recorder, new object[] { null, 4f, 8f, 0f });
                Assert.That((float)Prop(recording, "StartTime"), Is.EqualTo(1f).Within(0.001f),
                    "The replay starts where the participant was placed, not before.");
                var bicycle = (Array)Get(recording, "bicycle");
                var poseType = recordingType.GetNestedType("Pose");
                foreach (var pose in bicycle)
                    Assert.That((bool)poseType.GetField("active").GetValue(pose), Is.True, "The bicycle is there from the first frame.");

                var first = bicycle.GetValue(0);
                var args = new[] { first, null, null };
                Assert.That((bool)recordingType.GetMethod("TrySeat").Invoke(recording, args), Is.True);
                var seat = (Vector3)args[1];
                // the first frame has the bicycle at the origin, so the seat is the saddle's offset
                Assert.That(Vector3.Distance(seat, hip.position - bike.transform.position), Is.LessThan(0.01f));
                Assert.That(Vector3.Angle((Vector3)args[2], Vector3.forward), Is.LessThan(1f));

                // the figure sits on the saddle and points along the bicycle, even while looking left
                const float height = 1.6f;
                figure = (Component)T("ReplayMannequin").GetMethod("Build").Invoke(null, new object[] { null, height, 60f, 0 });
                var headPose = ((Array)Get(recording, "head")).GetValue(0);
                T("ReplayMannequin").GetMethod("Pose").Invoke(figure, new object[]
                    { headPose, 0f, Vector3.zero, true, 0f, Vector3.zero, 0.02f, (Vector3?)seat, (Vector3)args[2] });
                var hips = figure.transform.position + Vector3.up * (0.53f * height);
                Assert.That(Vector3.Distance(hips, seat), Is.LessThan(0.02f), "The rider's hips are on the saddle.");
                Assert.That(Vector3.Angle(figure.transform.forward, Vector3.forward), Is.LessThan(1f),
                    "The rider faces the way the bicycle points, not where the head looks.");
            }
            finally
            {
                if (figure != null) UnityEngine.Object.DestroyImmediate(figure.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(head.gameObject);
                UnityEngine.Object.DestroyImmediate(bike);
            }
        }

        [Test]
        public void HeavierBuildIsWiderAtTheSameHeight()
        {
            var girth = T("ReplayMannequin").GetMethod("GirthFor");
            var slim = (float)girth.Invoke(null, new object[] { 1.7f, 55f });
            var average = (float)girth.Invoke(null, new object[] { 1.7f, 63.6f });
            var heavy = (float)girth.Invoke(null, new object[] { 1.7f, 95f });
            Assert.That(average, Is.EqualTo(1f).Within(0.01f), "BMI 22 is the neutral build.");
            Assert.That(slim, Is.LessThan(average));
            Assert.That(heavy, Is.GreaterThan(average));
        }
    }
}
