using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRLearn.Tests.PlayMode
{
    public sealed class TrafficSignalTests
    {
        const string Scene = "Assets/_Project/Scenes/Gameplay_Hikone.unity";
        static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");
        static Component Find(string name) => UnityEngine.Object.FindFirstObjectByType(T(name)) as Component;
        static object Call(Component c, string name, params object[] args) => c.GetType().GetMethod(name).Invoke(c, args);
        static float Elapsed(Component c) => (float)c.GetType().GetProperty("ElapsedSeconds").GetValue(c);

        static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Scene);
            var deadline = Time.realtimeSinceStartup + 15f;
            while (Find("GameplayFlowController")?.GetType().GetProperty("Phase")?.GetValue(Find("GameplayFlowController"))?.ToString() != "Playing"
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Find("TrafficSignalCycle"), Is.Not.Null);
            Assert.That(Find("GameplayFlowController").GetType().GetProperty("Phase").GetValue(Find("GameplayFlowController")).ToString(), Is.EqualTo("Playing"));
        }

        [UnityTest]
        public IEnumerator LightsBlinkSwitchTogetherAndFreezeThroughAccidentReplayAndResults()
        {
            yield return Load();
            var clock = Find("TrafficSignalCycle");
            Call(clock, "ResetForScenario", 1);
            Call(clock, "Advance", 16.5f);
            foreach (Component head in UnityEngine.Object.FindObjectsByType(T("TrafficSignalPreset"), FindObjectsSortMode.None))
            {
                if (head.CompareTag("Light_Walk"))
                {
                    var green = head.transform.Find("GreenLight").GetComponent(T("TrafficLightController"));
                    Assert.That(green.GetType().GetProperty("IsLit").GetValue(green), Is.False, "pedestrian green blinks off");
                }
            }
            Call(clock, "Advance", 3.5f);
            foreach (Component head in UnityEngine.Object.FindObjectsByType(T("TrafficSignalPreset"), FindObjectsSortMode.None))
            {
                var expected = head.transform.parent.name == "Vertical" && head.CompareTag("Light_Car") ? "Yellow" : "Red";
                Assert.That(head.GetType().GetProperty("Aspect").GetValue(head).ToString(), Is.EqualTo(expected));
            }
            var flow = Find("GameplayFlowController");
            Assert.That(Call(flow, "TryTriggerAccident"), Is.True);
            var stopped = Elapsed(clock);
            foreach (var next in new[] { "MarkImpact", "MarkReplay", "MarkResults" })
            {
                Call(flow, next);
                Call(clock, "Advance", 10f);
                Assert.That(Elapsed(clock), Is.EqualTo(stopped), next);
            }
        }

        [UnityTest]
        public IEnumerator ResetAndSceneReloadRestoreAValidInitialPhaseAndGoalStopsTheClock()
        {
            yield return Load();
            var clock = Find("TrafficSignalCycle");
            foreach (var id in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 100 })
            {
                Call(clock, "ResetForScenario", id);
                Assert.That(Elapsed(clock), Is.Zero);
                foreach (Component head in UnityEngine.Object.FindObjectsByType(T("TrafficSignalPreset"), FindObjectsSortMode.None))
                {
                    var lit = 0;
                    foreach (var lens in head.GetComponentsInChildren(T("TrafficLightController")))
                        if ((bool)lens.GetType().GetProperty("IsLit").GetValue(lens)) lit++;
                    Assert.That(lit, Is.EqualTo(1), "exactly one initial aspect, including bicycle/custom scenarios");
                }
            }
            Call(clock, "Advance", 27f);
            Assert.That(Elapsed(clock), Is.EqualTo(27f));
            Call(clock, "ResetForScenario", 1);
            Assert.That(Elapsed(clock), Is.Zero);
            var oldId = clock.GetInstanceID();
            yield return Load();
            clock = Find("TrafficSignalCycle");
            Assert.That(clock.GetInstanceID(), Is.Not.EqualTo(oldId));
            Assert.That(Elapsed(clock), Is.LessThan(1f));
            Assert.That(Call(Find("GameplayFlowController"), "TryReachGoal"), Is.True);
            var stopped = Elapsed(clock);
            Call(clock, "Advance", 10f);
            Assert.That(Elapsed(clock), Is.EqualTo(stopped));
        }
    }
}
