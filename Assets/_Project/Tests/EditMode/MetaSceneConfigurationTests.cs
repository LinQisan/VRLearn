using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace VRLearn.Tests.EditMode
{
    public sealed class MetaSceneConfigurationTests
    {
        const string TitleScene = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
        const string GameplayScene = "Assets/_Project/Scenes/TraficAcident_Meta.unity";

        [Test]
        public void VehiclePersonImpulseRespectsMassAndMomentum()
        {
            var physicsType = System.Type.GetType("AccidentImpactPhysics, Assembly-CSharp");
            Assert.That(physicsType, Is.Not.Null);
            var calculate = physicsType.GetMethod("Calculate");
            Assert.That(calculate, Is.Not.Null);
            var lightPerson = calculate.Invoke(null, new object[]
                { Vector3.forward, 1200f, 45f, 10f });
            var heavyPerson = calculate.Invoke(null, new object[]
                { Vector3.forward, 1200f, 110f, 10f });
            float Read(object value, string propertyName) => (float)physicsType
                .GetProperty(propertyName)
                .GetValue(value);

            Assert.That(
                Read(lightPerson, "PersonLaunchSpeedMetersPerSecond"),
                Is.GreaterThan(Read(heavyPerson, "PersonLaunchSpeedMetersPerSecond")),
                "A lighter body must gain more collision velocity than a heavier body.");
            Assert.That(Read(lightPerson, "VehiclePostImpactSpeedMetersPerSecond"), Is.LessThan(10f));
            Assert.That(Read(heavyPerson, "VehiclePostImpactSpeedMetersPerSecond"), Is.LessThan(10f));
            Assert.That(Read(lightPerson, "PersonImpulseNewtonSeconds"), Is.GreaterThan(0f));
            Assert.That(Read(heavyPerson, "PersonImpulseNewtonSeconds"), Is.GreaterThan(0f));
        }

        [Test]
        public void BuildSettingsUseMetaTitleThenGameplay()
        {
            var enabledScenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    enabledScenes.Add(scene.path);

            CollectionAssert.AreEqual(
                new[] { TitleScene, GameplayScene },
                enabledScenes,
                "Only the Meta title and gameplay scenes should be enabled, in that order.");
        }

        [Test]
        public void ScenarioAssetsAreCompleteUniqueAndContinuous()
        {
            var ids = new HashSet<int>();
            for (var id = 0; id < 10; id++)
            {
                var path = $"Assets/_Project/ScenarioDefinitions/Scenario_{id:00}.asset";
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                Assert.That(asset, Is.Not.Null, $"Missing {path}");

                var serialized = new SerializedObject(asset);
                var storedId = serialized.FindProperty("id").intValue;
                Assert.That(storedId, Is.EqualTo(id), $"Unexpected ID in {path}");
                Assert.That(ids.Add(storedId), Is.True, $"Duplicate scenario ID {storedId}");
                Assert.That(serialized.FindProperty("displayName").stringValue, Is.Not.Empty);
                Assert.That(serialized.FindProperty("eventSummary").stringValue, Is.Not.Empty);
            }
            Assert.That(ids.Count, Is.EqualTo(10));
        }

        [TestCase(TitleScene)]
        [TestCase(GameplayScene)]
        public void MetaScenesHaveNoMissingScriptsOrDuplicateCoreObjects(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);

            var missingScripts = 0;
            var eventSystems = 0;
            var mainCameras = 0;
            var mainCameraNames = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject);
                eventSystems += root.GetComponentsInChildren<EventSystem>(true).Length;
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.enabled && camera.gameObject.activeInHierarchy && camera.CompareTag("MainCamera"))
                    {
                        mainCameras++;
                        mainCameraNames.Add(camera.name);
                    }
            }

            Assert.That(missingScripts, Is.Zero, $"{scenePath} contains Missing Script components.");
            Assert.That(eventSystems, Is.EqualTo(1), $"{scenePath} must contain one EventSystem.");
            Assert.That(
                mainCameras,
                Is.EqualTo(1),
                $"{scenePath} must contain one Main Camera. Active: {string.Join(", ", mainCameraNames)}");
        }

        [Test]
        public void GameplaySceneHasCompleteScenarioVehicleAndRouteBindings()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var components = GetAllComponents(scene);

            Component scenarioRuntime = null;
            Component routeRegistry = null;
            var carFactories = new List<Component>();
            foreach (var component in components)
            {
                if (component == null)
                    continue;
                switch (component.GetType().Name)
                {
                    case "ScenarioRuntime": scenarioRuntime = component; break;
                    case "WaypointRouteRegistry": routeRegistry = component; break;
                    case "CarFactory": carFactories.Add(component); break;
                }
            }

            Assert.That(scenarioRuntime, Is.Not.Null);
            var runtimeData = new SerializedObject(scenarioRuntime);
            var entries = runtimeData.FindProperty("entries");
            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.arraySize, Is.EqualTo(10));
            var ids = new HashSet<int>();
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                var asset = entry.FindPropertyRelative("asset").objectReferenceValue;
                Assert.That(asset, Is.Not.Null, $"Scenario entry {index} has no asset.");
                var assetData = new SerializedObject(asset);
                ids.Add(assetData.FindProperty("id").intValue);
                AssertReference(entry, "root", index);
                AssertReference(entry, "playerSpawn", index);
                AssertReference(entry, "goal", index);
                AssertReference(entry, "accidentArea", index);
            }
            Assert.That(ids.Count, Is.EqualTo(10));

            Assert.That(routeRegistry, Is.Not.Null);
            var routes = new SerializedObject(routeRegistry).FindProperty("routes");
            Assert.That(routes, Is.Not.Null);
            Assert.That(routes.arraySize, Is.EqualTo(15));
            var routeIds = new HashSet<int>();
            for (var index = 0; index < routes.arraySize; index++)
            {
                var route = routes.GetArrayElementAtIndex(index);
                routeIds.Add(route.FindPropertyRelative("id").enumValueIndex);
                Assert.That(
                    route.FindPropertyRelative("path").objectReferenceValue,
                    Is.Not.Null,
                    $"Route {index} has no path.");
            }
            Assert.That(routeIds.Count, Is.EqualTo(15));

            Assert.That(carFactories.Count, Is.GreaterThan(0));
            foreach (var factory in carFactories)
            {
                var prefab = new SerializedObject(factory).FindProperty("CarPrefab");
                Assert.That(prefab, Is.Not.Null, $"{factory.name} has no CarPrefab field.");
                Assert.That(prefab.objectReferenceValue, Is.Not.Null, $"{factory.name} has no vehicle prefab.");
            }
        }

        [TestCase("Car_EndlessGo")]
        [TestCase("Car_Left")]
        [TestCase("Car_Right")]
        [TestCase("Car_SideHit")]
        public void VehiclePrefabsHaveControllerAndNoMissingScripts(string prefabName)
        {
            var path = $"Assets/_Project/Prefabs/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Missing {path}");
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab),
                Is.Zero,
                $"{prefabName} contains a Missing Script.");

            var foundController = false;
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (component != null && component.GetType().Name == "CarController")
                    foundController = true;
            Assert.That(foundController, Is.True, $"{prefabName} requires CarController.");
        }

        static void AssertReference(SerializedProperty entry, string propertyName, int index)
        {
            Assert.That(
                entry.FindPropertyRelative(propertyName).objectReferenceValue,
                Is.Not.Null,
                $"Scenario entry {index} has no {propertyName}.");
        }

        static List<Component> GetAllComponents(Scene scene)
        {
            var result = new List<Component>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Component>(true));
            return result;
        }
    }
}
