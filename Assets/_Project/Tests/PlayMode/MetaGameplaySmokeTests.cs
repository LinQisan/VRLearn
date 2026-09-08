using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace VRLearn.Tests.PlayMode
{
    public sealed class MetaGameplaySmokeTests
    {
        const string GameplayScene = "Assets/_Project/Scenes/TraficAcident_Meta.unity";
        const string TitleScene = "Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity";
        static readonly int[] ScenarioIds = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        [UnityTest]
        public IEnumerator MetaTitleUsesOneStandardMouseInputPathInEditor()
        {
            var load = SceneManager.LoadSceneAsync(TitleScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            for (var frame = 0; frame < 14; frame++)
                yield return null;

            var scene = SceneManager.GetActiveScene();
            var setup = FindSceneComponent("MetaTitleSceneSetup", scene);
            Assert.That(setup, Is.Not.Null);
            var canvas = GetField(setup, "titleCanvas") as Canvas;
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.enabled, Is.True);

            EventSystem eventSystem = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                eventSystem = root.GetComponentInChildren<EventSystem>(true);
                if (eventSystem != null)
                    break;
            }
            Assert.That(eventSystem, Is.Not.Null);

            var enabledModules = 0;
            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (module.enabled)
                {
                    enabledModules++;
                    Assert.That(module.GetType().Name, Is.EqualTo("InputSystemUIInputModule"));
                }
            }
            Assert.That(enabledModules, Is.EqualTo(1));

            var standardRaycaster = false;
            foreach (var component in canvas.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "GraphicRaycaster")
                    continue;
                standardRaycaster = ((Behaviour)component).enabled;
            }
            Assert.That(standardRaycaster, Is.True);

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(
                Vector3.Distance(camera.transform.position, canvas.transform.position),
                Is.InRange(1.9f, 2.1f),
                "The title menu should be readable at the intended two-metre VR distance.");

            var director = FindSceneComponent("GameDirector_Title", scene);
            var eventDown = FindCommandButton(scene, "EventDown");
            Assert.That(director, Is.Not.Null);
            Assert.That(eventDown, Is.Not.Null);
            Assert.That(eventDown.targetGraphic.raycastPadding.x, Is.LessThan(0f));
            Assert.That(director.GetType().GetField("EventNumber")?.GetValue(director), Is.EqualTo(-1));
            eventDown.onClick.Invoke();
            Assert.That(director.GetType().GetField("EventNumber")?.GetValue(director), Is.EqualTo(9),
                "Minus from RANDOM must wrap to scenario 9 instead of doing nothing.");
        }

        [UnityTest]
        public IEnumerator EveryScenarioActivatesAndSupportsBothOutcomeStates(
            [ValueSource(nameof(ScenarioIds))] int scenarioId)
        {
            void ConfigureBeforeStart(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != GameplayScene)
                    return;
                var director = FindSceneComponent("GameDirector", scene);
                Assert.That(director, Is.Not.Null);
                director.GetType().GetField("EventNumber").SetValue(director, scenarioId);
            }

            SceneManager.sceneLoaded += ConfigureBeforeStart;
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            SceneManager.sceneLoaded -= ConfigureBeforeStart;
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var runtime = FindSceneComponent("ScenarioRuntime", scene);
            var director = FindSceneComponent("GameDirector", scene);
            var flow = FindSceneComponent("GameplayFlowController", scene);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(director, Is.Not.Null);
            Assert.That(flow, Is.Not.Null);
            Assert.That(runtime.GetType().GetProperty("IsConfigurationValid")?.GetValue(runtime), Is.True);
            Assert.That(runtime.GetType().GetProperty("EntryCount")?.GetValue(runtime), Is.EqualTo(10));

            var active = runtime.GetType().GetProperty("Active")?.GetValue(runtime);
            Assert.That(active, Is.Not.Null);
            Assert.That(active.GetType().GetProperty("Id")?.GetValue(active), Is.EqualTo(scenarioId));
            Assert.That(CountActiveScenarioRoots(runtime), Is.EqualTo(1));
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(), Is.EqualTo("Playing"));

            var expectedBicycle = scenarioId == 6 || scenarioId == 7 || scenarioId == 9;
            Assert.That(active.GetType().GetProperty("PlayerMode")?.GetValue(active)?.ToString(),
                Is.EqualTo(expectedBicycle ? "Bicycle" : "Walking"));

            var player = FindSceneComponent("PlayerActor", scene);
            var editorController = FindSceneComponent("OpenXRPlayerController", scene);
            var metaController = FindSceneComponent("OVRPlayerController", scene);
            Assert.That(player, Is.Not.Null);
            Assert.That(editorController, Is.Not.Null);
            Assert.That(((Behaviour)editorController).enabled, Is.True);
            Assert.That(metaController, Is.Not.Null);
            Assert.That(((Behaviour)metaController).enabled, Is.False,
                "OVR and Editor locomotion must not run together.");
            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.name, Is.EqualTo("CenterEyeAnchor"));

            Assert.That(InvokeBool(flow, "TryTriggerAccident"), Is.True);
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(),
                Is.EqualTo("AccidentTriggered"));
            Invoke(flow, "MarkImpact");
            Invoke(flow, "MarkReplay");
            Invoke(flow, "MarkResults");
            Invoke(flow, "LateUpdate");
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(), Is.EqualTo("Results"));
            Assert.That(((Behaviour)editorController).enabled, Is.False,
                "Movement must be frozen on results.");

            var results = FindSceneComponent("AccidentResultPresenter", scene, true);
            Assert.That(results, Is.Not.Null);
            Invoke(results, "Show", scenarioId);
            var title = GetField(results, "runtimeTitle") as Component;
            var summary = GetField(results, "runtimeSummary") as Component;
            Assert.That(title, Is.Not.Null);
            Assert.That(summary, Is.Not.Null);
            Assert.That(GetText(title), Does.Contain((scenarioId + 1).ToString()));
            Assert.That(GetText(summary), Is.Not.Empty);
            var readableCanvas = GetField(results, "readableCanvas") as Canvas;
            Assert.That(readableCanvas, Is.Not.Null);
            Assert.That(readableCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera),
                "Result UI must render in camera space so accident geometry cannot occlude it.");
            Assert.That(readableCanvas.worldCamera, Is.EqualTo(Camera.main));
            Assert.That(readableCanvas.sortingOrder, Is.GreaterThan(30000));
            Assert.That(readableCanvas.planeDistance, Is.InRange(1.1f, 1.4f));
            var readablePanel = GetField(results, "readablePanel") as RectTransform;
            Assert.That(readablePanel, Is.Not.Null);
            Assert.That(readablePanel.localScale.x, Is.InRange(0.79f, 0.81f));

            Invoke(flow, "BeginScenario", scenarioId);
            Assert.That(InvokeBool(flow, "TryReachGoal"), Is.True);
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(),
                Is.EqualTo("GoalReached"));
        }

        [UnityTest]
        public IEnumerator AccidentReplayUsesIndependentOverheadCamera()
        {
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var flow = FindSceneComponent("GameplayFlowController", scene);
            var hybrid = FindSceneComponent("HybridAccidentPresentation", scene, true);
            var player = FindSceneComponent("PlayerActor", scene);
            Assert.That(flow, Is.Not.Null);
            Assert.That(hybrid, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(InvokeBool(flow, "TryTriggerAccident"), Is.True);

            var playerTransform = player.transform;
            var originPositionBeforeImpact = playerTransform.position;
            var originRotationBeforeImpact = playerTransform.rotation;
            Assert.That(
                Invoke(
                    hybrid,
                    "BeginImpact",
                    player,
                    playerTransform.position,
                    Vector3.forward,
                    8f,
                    null),
                Is.True);

            yield return new WaitForSecondsRealtime(0.32f);
            Assert.That(
                hybrid.GetType().GetProperty("IsBodyViewActive")?.GetValue(hybrid),
                Is.True);
            Assert.That(
                Vector3.Distance(originPositionBeforeImpact, playerTransform.position) > 0.04f
                || Quaternion.Angle(originRotationBeforeImpact, playerTransform.rotation) > 3f,
                Is.True,
                "The tracked rig must visibly recoil, drop or tilt before the overhead replay.");

            yield return new WaitForSecondsRealtime(1.3f);

            var overhead = FindSceneComponent("AccidentOverheadView", scene, true);
            Assert.That(overhead, Is.Not.Null);
            Assert.That(overhead.GetType().GetProperty("IsVisible")?.GetValue(overhead), Is.True);
            var viewCamera = overhead.GetType().GetProperty("ViewCamera")?.GetValue(overhead) as Camera;
            var overlayCanvas = overhead.GetType().GetProperty("OverlayCanvas")?.GetValue(overhead) as Canvas;
            Assert.That(viewCamera, Is.Not.Null);
            Assert.That(viewCamera.enabled, Is.True);
            Assert.That(viewCamera.targetTexture, Is.Not.Null);
            Assert.That(viewCamera, Is.Not.EqualTo(Camera.main));
            Assert.That(viewCamera.stereoTargetEye, Is.EqualTo(StereoTargetEyeMask.None));
            Assert.That(overlayCanvas, Is.Not.Null);
            Assert.That(overlayCanvas.gameObject.activeSelf, Is.True);
            Assert.That(overlayCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(overlayCanvas.worldCamera, Is.EqualTo(Camera.main));

            Invoke(hybrid, "ForceResults");
            yield return null;
            Assert.That(viewCamera.enabled, Is.False);
            Assert.That(overlayCanvas.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator WalkingPhoneModePresentsPhoneInView()
        {
            void ConfigureBeforeStart(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != GameplayScene)
                    return;
                var director = FindSceneComponent("GameDirector", scene);
                Assert.That(director, Is.Not.Null);
                director.GetType().GetField("SmartPhone")?.SetValue(director, 1);
            }

            SceneManager.sceneLoaded += ConfigureBeforeStart;
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            SceneManager.sceneLoaded -= ConfigureBeforeStart;
            for (var frame = 0; frame < 4; frame++)
                yield return null;

            var scene = SceneManager.GetActiveScene();
            var presenter = FindSceneComponent("SmartPhoneDistractionPresenter", scene, true);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.GetType().GetProperty("IsPresenting")?.GetValue(presenter), Is.True);

            var phoneVisual = presenter.GetType().GetProperty("PhoneVisual")?.GetValue(presenter) as Transform;
            var distractionScreen = presenter.GetType().GetProperty("DistractionScreen")?.GetValue(presenter) as GameObject;
            Assert.That(phoneVisual, Is.Not.Null);
            Assert.That(distractionScreen, Is.Not.Null);
            Assert.That(distractionScreen.activeInHierarchy, Is.True);
            Assert.That(phoneVisual.name, Is.EqualTo("RuntimePhoneModel"));
            Assert.That(phoneVisual.Find("PhoneBody"), Is.Not.Null);
            Assert.That(phoneVisual.Find("PhoneScreen"), Is.Not.Null);
            var phoneBodyRenderer = phoneVisual.Find("PhoneBody").GetComponent<Renderer>();
            Assert.That(phoneBodyRenderer, Is.Not.Null);
            Assert.That(
                phoneBodyRenderer.bounds.extents.magnitude,
                Is.InRange(0.085f, 0.105f),
                "The handset should use real-world dimensions, not the oversized Canvas scale.");
            Assert.That(
                presenter.GetType().GetProperty("HasEnabledPhoneLight")?.GetValue(presenter),
                Is.False,
                "The obsolete spotlight must not turn the phone into a glowing sphere.");

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var localPhonePosition = camera.transform.InverseTransformPoint(phoneVisual.position);
            Assert.That(localPhonePosition.z, Is.InRange(0.4f, 0.7f));
            Assert.That(localPhonePosition.x, Is.InRange(0.05f, 0.3f));
            Assert.That(localPhonePosition.y, Is.InRange(-0.4f, -0.08f));
        }

        [UnityTest]
        public IEnumerator ResultParameterTransferPreservesAllTitleValues()
        {
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var results = FindSceneComponent("AccidentResultPresenter", scene, true);
            Assert.That(results, Is.Not.Null);
            var titleType = Type.GetType("GameDirector_Title, Assembly-CSharp");
            Assert.That(titleType, Is.Not.Null);
            var titleObject = new GameObject("ParameterTransferProbe");
            var title = titleObject.AddComponent(titleType);

            var expected = new (string field, object value)[]
            {
                ("eventNumber", 8), ("dieFlashNumber", 1), ("height", 181),
                ("weight", 77), ("gender", 1), ("age", 42), ("license", 1),
                ("hz", 61.5f), ("smartPhone", 1), ("incident", 2),
                ("weather", 1), ("skyTime", 1)
            };
            foreach (var item in expected)
                SetField(results, item.field, item.value);

            Invoke(results, "TransferValuesToTitle", scene, LoadSceneMode.Additive);
            foreach (var item in expected)
            {
                var destinationName = char.ToUpperInvariant(item.field[0]) + item.field.Substring(1);
                var actual = titleType.GetField(destinationName)?.GetValue(title);
                Assert.That(actual, Is.EqualTo(item.value), destinationName);
            }
            UnityEngine.Object.Destroy(titleObject);
        }

        static readonly int[] BirthRaceScenarioIds = { 2, 6 };

        [UnityTest]
        public IEnumerator AvatarObservesTransferredScenario(
            [ValueSource(nameof(BirthRaceScenarioIds))] int scenarioId)
        {
            void ConfigureBeforeStart(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != GameplayScene)
                    return;
                var director = FindSceneComponent("GameDirector", scene);
                Assert.That(director, Is.Not.Null);
                director.GetType().GetField("EventNumber").SetValue(director, scenarioId);
            }

            SceneManager.sceneLoaded += ConfigureBeforeStart;
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            SceneManager.sceneLoaded -= ConfigureBeforeStart;
            // HumanController.Start runs after the transfer and must refresh
            // the scenario that MetaGameplaySceneSetup.Awake could only guess.
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var avatar = FindSceneComponent("HumanController", scene);
            Assert.That(avatar, Is.Not.Null, "Active avatar must exist after scenario start.");
            Assert.That(GetField(avatar, "EventNumber"), Is.EqualTo(scenarioId),
                "Avatar must observe the transferred scenario, not the scene-authored default.");
        }

        static Component FindSceneComponent(string typeName, Scene scene, bool includeInactive = false)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(includeInactive))
                if (component != null && component.GetType().Name == typeName)
                    return component;
            return null;
        }

        static Button FindCommandButton(Scene scene, string commandName)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "TitleCommandButton")
                    continue;
                var command = component.GetType().GetProperty("Command")?.GetValue(component);
                if (command?.ToString() == commandName)
                    return component.GetComponent<Button>();
            }
            return null;
        }

        static int CountActiveScenarioRoots(Component runtime)
        {
            var entries = GetField(runtime, "entries") as Array;
            var count = 0;
            if (entries == null)
                return count;
            foreach (var entry in entries)
            {
                var root = entry?.GetType().GetField("root")?.GetValue(entry) as GameObject;
                if (root != null && root.activeSelf)
                    count++;
            }
            return count;
        }

        static bool InvokeBool(object target, string methodName)
        {
            return (bool)Invoke(target, methodName);
        }

        static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing {target.GetType().Name}.{methodName}");
            return method.Invoke(target, arguments);
        }

        static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }

        static string GetText(Component textComponent)
        {
            return textComponent.GetType().GetProperty("text")?.GetValue(textComponent) as string;
        }
    }
}
