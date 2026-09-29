using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRLearn.Tests.PlayMode
{
    /// <summary>
    /// The Hikone scene only swaps the environment visuals; vehicles still drive on the
    /// original (now invisible) road colliders. These checks catch a rebuild that breaks that.
    /// </summary>
    public sealed class HikoneSceneTests
    {
        const string HikoneScene = "Assets/_Project/Scenes/TraficAcident_Hikone_Meta.unity";
        static readonly int[] TrafficScenarioIds = { 0, 1, 6 };

        static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp");

        [UnityTest]
        public IEnumerator TitleStartsHikoneSceneAndStandardIsBlocked()
        {
            const string TitleScene = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
            var route = GameType("SceneRoute");
            var selected = route.GetProperty("SelectedEnvironment");
            try
            {
                var load = SceneManager.LoadSceneAsync(TitleScene, LoadSceneMode.Single);
                while (!load.isDone)
                    yield return null;
                yield return null;

                var director = UnityEngine.Object.FindFirstObjectByType(GameType("GameDirector_Title"));
                Assert.That(director, Is.Not.Null);
                // The standard environment is blocked: the title defaults to Hikone and even an
                // explicit request for environment 0 is clamped to Hikone.
                Assert.That(director.GetType().GetField("Environment").GetValue(director), Is.EqualTo(1));
                var kind = Enum.Parse(GameType("TitleOptionKind"), "Environment");
                director.GetType().GetMethod("ApplyOption").Invoke(director, new object[] { kind, 0, 0f });
                Assert.That(director.GetType().GetField("Environment").GetValue(director), Is.EqualTo(1));

                director.GetType().GetField("EventNumber").SetValue(director, 0);
                director.GetType().GetMethod("StartGame").Invoke(director, null);
                var deadline = Time.realtimeSinceStartup + 20f;
                while (SceneManager.GetActiveScene().path != HikoneScene && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HikoneScene));
                Assert.That(route.GetProperty("GameplayForCurrentScene").GetValue(null), Is.EqualTo("TraficAcident_Hikone_Meta"),
                    "retry / restart must stay in the chosen environment");
            }
            finally
            {
                selected.SetValue(null, 1);
            }
        }

        [UnityTest]
        public IEnumerator VehiclesStayOnTheRoadAndPlayerStaysGrounded(
            [ValueSource(nameof(TrafficScenarioIds))] int scenarioId)
        {
            void ConfigureBeforeStart(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != HikoneScene)
                    return;
                var director = UnityEngine.Object.FindFirstObjectByType(GameType("GameDirector"));
                director.GetType().GetField("EventNumber").SetValue(director, scenarioId);
            }

            SceneManager.sceneLoaded += ConfigureBeforeStart;
            var load = SceneManager.LoadSceneAsync(HikoneScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            SceneManager.sceneLoaded -= ConfigureBeforeStart;

            var environment = GameObject.Find("Environment/HikoneEnvironment");
            Assert.That(environment, Is.Not.Null, "Hikone environment root missing");
            Assert.That(environment.transform.childCount, Is.GreaterThan(5));

            var carType = GameType("CarController");
            var seenCars = 0;
            var end = Time.time + 8f;
            while (Time.time < end)
            {
                foreach (var car in UnityEngine.Object.FindObjectsByType(carType, FindObjectsSortMode.None))
                {
                    var t = ((Component)car).transform;
                    if (!t.gameObject.activeInHierarchy)
                        continue;
                    seenCars++;
                    Assert.That(t.position.y, Is.InRange(-0.5f, 3f),
                        $"{t.name} left the road surface at {t.position}");
                }
                yield return null;
            }
            Assert.That(seenCars, Is.GreaterThan(0), "no vehicle ran during the observation window");

            var player = UnityEngine.Object.FindFirstObjectByType(GameType("PlayerActor")) as Component;
            Assert.That(player, Is.Not.Null);
            Assert.That(player.transform.position.y, Is.InRange(-0.2f, 2.5f),
                $"player not grounded: {player.transform.position}");
        }
    }
}
