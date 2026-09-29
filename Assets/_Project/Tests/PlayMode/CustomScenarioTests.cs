using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRLearn.Tests.PlayMode
{
    /// <summary>JSON scenarios built at runtime in the Hikone scene.</summary>
    public sealed class CustomScenarioTests
    {
        const string HikoneScene = "Assets/_Project/Scenes/TraficAcident_Hikone_Meta.unity";
        const int CustomEventNumber = 100;

        static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");
        static Component Find(string type) =>
            UnityEngine.Object.FindFirstObjectByType(T(type), FindObjectsInactive.Include) as Component;
        static object Get(object target, string member)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = target.GetType().GetField(member, flags);
            return field != null ? field.GetValue(target) : target.GetType().GetProperty(member, flags).GetValue(target);
        }
        static object Call(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        static string Phase() => Get(Find("GameplayFlowController"), "Phase").ToString();

        static object LoadTemplate(string id)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Scenarios", "templates", id + ".json"));
            var errors = new List<string>();
            var scenario = T("CustomScenarioLibrary").GetMethod("Load").Invoke(null, new object[] { path, errors });
            Assert.That(scenario, Is.Not.Null, string.Join(" / ", errors));
            return scenario;
        }

        static IEnumerator LoadCustom(object scenario)
        {
            T("CustomScenarioSession").GetMethod("Select").Invoke(null, new[] { scenario });
            void Configure(Scene scene, LoadSceneMode mode)
            {
                var director = UnityEngine.Object.FindFirstObjectByType(T("GameDirector"));
                director.GetType().GetField("EventNumber").SetValue(director, CustomEventNumber);
            }
            SceneManager.sceneLoaded += Configure;
            var load = SceneManager.LoadSceneAsync(HikoneScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            SceneManager.sceneLoaded -= Configure;
            yield return new WaitForSeconds(0.3f);    // LateRespawn places the player
        }

        [TearDown]
        public void ClearSelection() => T("CustomScenarioSession").GetMethod("Clear").Invoke(null, null);

        [UnityTest]
        public IEnumerator TemplateOfScenario01RunsLikeTheBuiltIn()
        {
            var scenario = LoadTemplate("builtin-01");
            yield return LoadCustom(scenario);

            var runtime = Find("ScenarioRuntime");
            var active = Get(runtime, "Active");
            Assert.That((bool)Get(active, "IsCustom"), Is.True);
            Assert.That(Get(active, "PlayerMode").ToString(), Is.EqualTo("Walking"));
            foreach (Array entries in new[] { (Array)Get(runtime, "entries") })
                foreach (var entry in entries)
                    Assert.That(((GameObject)Get(entry, "root")).activeSelf, Is.False, "Built-in scenario objects stay off.");
            Assert.That(Phase(), Is.EqualTo("Playing"));

            var player = Find("PlayerActor");
            var spawn = new Vector2(40f, 14f);
            Assert.That(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), spawn),
                Is.LessThan(1f), "The participant starts at the file's spawn point.");

            // trigger → the accident car of the file appears 6 s later on the right-hand route
            var runner = Find("CustomScenarioRunner");
            var trigger = Find("CustomScenarioTrigger");
            Assert.That(trigger, Is.Not.Null);
            Call(trigger, "OnTriggerEnter", player.GetComponent<Collider>());
            Assert.That((bool)Get(runner, "Triggered"), Is.True);
            bool AccidentCar(out Component car)
            {
                car = UnityEngine.Object.FindObjectsByType(T("CarController"), FindObjectsSortMode.None)
                    .Cast<Component>().FirstOrDefault(c => (bool)Get(c, "AcidentCar"));
                return car != null;
            }
            yield return new WaitForSeconds(5.5f);
            Assert.That(AccidentCar(out _), Is.False, "Not before its 6 s delay.");
            var deadline = Time.time + 1.5f;
            Component accidentCar = null;
            while (!AccidentCar(out accidentCar) && Time.time < deadline)
                yield return null;
            Assert.That(accidentCar, Is.Not.Null, "The accident car appears 6 s after the trigger.");
            var route = (Transform)Get(accidentCar, "pointsParent");
            Assert.That(route.name, Does.StartWith("Route_2"));
            Assert.That(Vector3.Distance(route.GetChild(0).position, new Vector3(90f, route.GetChild(0).position.y, 6.16f)),
                Is.LessThan(0.05f));
            Assert.That((float)Get(accidentCar, "Speed"), Is.EqualTo(10f).Within(0.01f), "36 km/h");

            // the right-hand background flow stopped at the trigger; the left keeps running
            var vehicles = (Array)Get(scenario, "vehicles");
            Assert.That((bool)Get(vehicles.GetValue(1), "stopOnTrigger"), Is.True);
            Assert.That((bool)Get(vehicles.GetValue(0), "stopOnTrigger"), Is.False);
        }

        [UnityTest]
        public IEnumerator CustomGoalShowsTheFilesTextAndCsvNamesTheFile()
        {
            var scenario = LoadTemplate("builtin-01");
            yield return LoadCustom(scenario);
            var goal = UnityEngine.Object.FindObjectsByType(T("GoalController"), FindObjectsSortMode.None)
                .Cast<Component>().First(c => c.transform.parent != null && c.transform.parent.name.StartsWith("CustomScenario_"));
            Assert.That(goal.GetComponentsInChildren<ParticleSystem>(true), Is.Empty, "No goal smoke.");
            var player = Find("PlayerActor");
            Call(goal, "OnTriggerEnter", player.GetComponent<Collider>());
            var deadline = Time.realtimeSinceStartup + 6f;
            while (Phase() != "Results" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Phase(), Is.EqualTo("Results"));
            var results = Find("AccidentResultPresenter");
            Assert.That((string)Get(Get(results, "runtimeTitle"), "text"), Is.EqualTo((string)Get(scenario, "name")));
            Assert.That((string)Get(Get(results, "runtimeChip"), "text"), Is.EqualTo("カスタム"));

            var csv = Find("CSVPrinter");
            Call(csv, "CSVPrint");
            var files = (string[])Get(csv, "LastWrittenFiles");
            try
            {
                var human = File.ReadAllLines(files[1]);
                var head = human[0].Split(',');
                var row = human[1].Split(',');
                Assert.That(head.Last(), Is.EqualTo("CustomScenario"));
                Assert.That(row.Last(), Is.EqualTo("builtin-01"));
                Assert.That(row[0], Is.EqualTo(CustomEventNumber.ToString()));
            }
            finally
            {
                foreach (var file in files)
                    File.Delete(file);
            }
        }

        [UnityTest]
        public IEnumerator TitleListsScenariosOnTheDeviceAndStartsTheChosenOne()
        {
            const string TitleScene = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
            const string Id = "zz-title-test";
            const string Name = "テスト用の場面（タイトル）";
            var folder = Path.Combine(Application.persistentDataPath, "Scenarios");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, Id + ".json");
            var scenario = LoadTemplate("builtin-03");
            scenario.GetType().GetField("id").SetValue(scenario, Id);
            scenario.GetType().GetField("name").SetValue(scenario, Name);
            File.WriteAllText(file, (string)scenario.GetType().GetMethod("ToJson").Invoke(scenario, null));
            try
            {
                var load = SceneManager.LoadSceneAsync(TitleScene, LoadSceneMode.Single);
                while (!load.isDone)
                    yield return null;
                yield return null;
                yield return null;

                var list = Find("TitleCustomScenarios");
                Assert.That(list, Is.Not.Null);
                Call(list, "Show", true);
                Assert.That((bool)Get(list, "ShowingCustom"), Is.True);
                var tile = list.GetComponentsInChildren<UnityEngine.UI.Toggle>(false)
                    .FirstOrDefault(t => t.name.StartsWith("Custom_")
                        && ((string)Get(t.transform.Find("Label").GetComponent(T("TMPro.TMP_Text") ?? Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro")), "text")).Contains(Name));
                Assert.That(tile, Is.Not.Null, "The file on the device is offered on the custom tab.");
                var size = ((RectTransform)tile.transform).rect.size;
                Assert.That(Mathf.Min(size.x, size.y), Is.GreaterThanOrEqualTo(4.5f));
                tile.isOn = true;

                var director = Find("GameDirector_Title");
                Assert.That(director.GetType().GetField("EventNumber").GetValue(director), Is.EqualTo(CustomEventNumber));
                var current = T("CustomScenarioSession").GetProperty("Current").GetValue(null);
                Assert.That(Get(current, "id"), Is.EqualTo(Id));
                var detail = Find("TitleScenarioDetail");
                Assert.That((string)Get(Get(detail, "titleText"), "text"), Is.EqualTo(Name));

                // built-in tiles stay usable: choosing one clears the custom choice
                Call(list, "Show", false);
                Assert.That((bool)Get(list, "ShowingCustom"), Is.False);
                Call(list, "Show", true);

                Call(director, "StartGame");
                var deadline = Time.realtimeSinceStartup + 10f;
                while (SceneManager.GetActiveScene().path != HikoneScene && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return new WaitForSeconds(0.3f);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HikoneScene));
                var runner = Find("CustomScenarioRunner");
                Assert.That(runner, Is.Not.Null, "The chosen file is built in the gameplay scene.");
                Assert.That(Get(Get(runner, "Scenario"), "id"), Is.EqualTo(Id));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [UnityTest]
        public IEnumerator BicycleFilePutsTheParticipantOnTheBicycle()
        {
            var scenario = LoadTemplate("builtin-07");
            yield return LoadCustom(scenario);
            var active = Get(Find("ScenarioRuntime"), "Active");
            Assert.That(Get(active, "PlayerMode").ToString(), Is.EqualTo("Bicycle"));
            var bicycle = (GameObject)Get(Find("GameplaySceneContext"), "Bicycle");
            Assert.That(bicycle.activeInHierarchy, Is.True);
            var spawn = Get(scenario, "spawn");
            var expected = new Vector2((float)Get(spawn, "x"), (float)Get(spawn, "z"));
            Assert.That(Vector2.Distance(new Vector2(bicycle.transform.position.x, bicycle.transform.position.z), expected),
                Is.LessThan(2f));
            // background traffic of the ride starts almost at once
            var runner = Find("CustomScenarioRunner");
            var deadline = Time.time + 3f;
            while ((int)Get(runner, "SpawnCount") == 0 && Time.time < deadline)
                yield return null;
            Assert.That((int)Get(runner, "SpawnCount"), Is.GreaterThan(0));
        }
    }
}
