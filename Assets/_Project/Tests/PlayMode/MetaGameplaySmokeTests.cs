using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace VRLearn.Tests.PlayMode
{
    [TestFixture("Assets/_Project/Scenes/Gameplay_Hikone.unity")]
    public sealed class MetaGameplaySmokeTests
    {
        readonly string GameplayScene;

        public MetaGameplaySmokeTests(string gameplayScene)
        {
            GameplayScene = gameplayScene;
        }
        const string TitleScene = "Assets/_Project/Scenes/Title.unity";
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
            var menuDistance = Vector3.Distance(camera.transform.position, canvas.transform.position);
            Assert.That(menuDistance, Is.InRange(1.5f, 1.75f),
                "The title menu sits 1.6 m away so its text is legible on Quest 2.");
            Assert.That(Vector3.Angle(canvas.transform.up, Vector3.up), Is.LessThan(1f), "The menu stands upright.");

            var director = FindSceneComponent("GameDirector_Title", scene);
            Assert.That(director, Is.Not.Null);
            Assert.That(director.GetType().GetField("EventNumber")?.GetValue(director), Is.EqualTo(-1));

            // Every scenario and every unpleasant tone is a tile: one trigger pull, no stepping.
            var eventTiles = FindOptionTiles(scene, "EventNumber");
            var toneTiles = FindOptionTiles(scene, "Hz");
            Assert.That(eventTiles.Count, Is.EqualTo(11), "RANDOM + scenarios 0-9 must all be tiled.");
            Assert.That(toneTiles.Count, Is.EqualTo(16), "None + 3000-17000 Hz must all be tiled.");
            // everything is on one page: every tile is visible, at least 2.5° (Quest 2: ~20 px per
            // degree), all text is at least 0.9° tall, and the controller ray reaches every control
            Assert.That(FindSceneComponent("TitleMenuSummary", scene, true), Is.Not.Null);
            float Degrees(float units) => Mathf.Atan(units * canvas.transform.lossyScale.x / menuDistance) * Mathf.Rad2Deg;
            foreach (var tile in eventTiles.Concat(toneTiles))
            {
                Assert.That(tile.gameObject.activeInHierarchy, Is.True, tile.name + " must be visible without paging.");
                Assert.That(tile.interactable, Is.True, tile.name);
                var size = ((RectTransform)tile.transform).rect.size;
                Assert.That(Degrees(Mathf.Min(size.x, size.y)), Is.GreaterThanOrEqualTo(2.5f),
                    $"{tile.name} is too small to hit with a Touch ray.");
            }
            foreach (var text in canvas.GetComponentsInChildren<Component>(false).Where(c => c.GetType().Name == "TextMeshProUGUI"))
            {
                var fontSize = (float)text.GetType().GetProperty("fontSize").GetValue(text);
                if (string.IsNullOrWhiteSpace(GetText(text)))
                    continue;
                Assert.That(Degrees(fontSize), Is.GreaterThanOrEqualTo(0.9f),
                    $"'{GetText(text)}' ({text.name}) is too small to read on Quest 2.");
            }
            var menuSize = ((RectTransform)canvas.transform).rect.size * canvas.transform.lossyScale.x;
            Assert.That(2f * Mathf.Atan(menuSize.x / 2f / menuDistance) * Mathf.Rad2Deg, Is.LessThanOrEqualTo(80f),
                "The one-page menu stays within a comfortable width.");
            AssertEveryControlIsHitByThePointer(canvas);
            var nine = eventTiles.First(t => (int)GetField(t.GetComponent("TitleOptionToggle"), "intValue") == 9);
            nine.isOn = true;
            Assert.That(director.GetType().GetField("EventNumber")?.GetValue(director), Is.EqualTo(9),
                "Selecting the scenario 9 tile must select scenario 9 directly.");
            var tone = toneTiles.First(t => Mathf.Approximately((float)GetField(t.GetComponent("TitleOptionToggle"), "floatValue"), 12000f));
            tone.isOn = true;
            Assert.That(director.GetType().GetField("Hz")?.GetValue(director), Is.EqualTo(12000f));
            Assert.That(FindCommandButton(scene, "EventDown"), Is.Null, "The scenario stepper was replaced by tiles.");
        }

        /// <summary>
        /// The controller ray must reach every visible control: it is interactable (no CanvasGroup
        /// switches it off), its canvas has a raycaster, and at its centre it is the top-most
        /// raycast target (no label, heading or panel drawn over it).
        /// </summary>
        static void AssertEveryControlIsHitByThePointer(Canvas canvas)
        {
            bool Blocks(Graphic g)
            {
                if (!g.raycastTarget || !g.isActiveAndEnabled)
                    return false;
                for (var t = g.transform; t != null; t = t.parent)
                {
                    var group = t.GetComponent<CanvasGroup>();
                    if (group != null && group.enabled)
                    {
                        if (!group.blocksRaycasts)
                            return false;
                        if (group.ignoreParentGroups)
                            break;
                    }
                }
                return true;
            }
            // depth-first order is the draw order: later graphics are on top
            var targets = canvas.GetComponentsInChildren<Graphic>(false).Where(Blocks).ToList();
            var problems = new List<string>();
            foreach (var control in canvas.GetComponentsInChildren<Selectable>(false))
            {
                if (!control.interactable)
                    continue;  // deliberately disabled (e.g. a page arrow on the first page)
                if (!control.IsInteractable())
                {
                    problems.Add(control.name + " (switched off by a CanvasGroup)");
                    continue;
                }
                var owner = control.GetComponentInParent<Canvas>();
                if (owner.GetComponent<BaseRaycaster>() == null)
                {
                    problems.Add($"{control.name} (canvas {owner.name} has no raycaster)");
                    continue;
                }
                var center = control.transform.TransformPoint(((RectTransform)control.transform).rect.center);
                var top = targets.LastOrDefault(g => g.rectTransform.rect.Contains(g.rectTransform.InverseTransformPoint(center)));
                if (top == null || top.GetComponentInParent<Selectable>() != control)
                    problems.Add($"{control.name} (covered by {(top != null ? top.name : "nothing")})");
            }
            Assert.That(problems, Is.Empty, "The controller ray cannot reach: " + string.Join(", ", problems));
        }

        static List<Toggle> FindOptionTiles(Scene scene, string kind)
        {
            var result = new List<Toggle>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "TitleOptionToggle")
                    continue;
                if (GetField(component, "kind")?.ToString() == kind)
                    result.Add(component.GetComponent<Toggle>());
            }
            return result;
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
            var rays = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true))
                .Where(c => c != null && c.GetType().Name == "XRRayInteractor").ToList();
            Assert.That(rays, Is.Not.Empty);
            Assert.That(rays.Any(r => r.gameObject.activeInHierarchy), Is.False,
                "No pointer ray while walking or riding.");

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
            Assert.That(rays.All(r => r.gameObject.activeInHierarchy && r.GetComponent<LineRenderer>().enabled), Is.True,
                "The feedback page shows the pointer rays.");
            var readableCanvas = GetField(results, "readableCanvas") as Canvas;
            Assert.That(readableCanvas, Is.Not.Null);
            Assert.That(readableCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace),
                "The feedback page is a world-locked panel (head-locked full-view pages were unreadable on Quest 2).");
            Assert.That(readableCanvas.worldCamera, Is.EqualTo(Camera.main));
            Assert.That(readableCanvas.sortingOrder, Is.GreaterThan(30000));
            AssertComfortablePanel(readableCanvas, 1.4f, 1.8f, 50f, 62f);

            Invoke(flow, "BeginScenario", scenarioId);
            Assert.That(InvokeBool(flow, "TryReachGoal"), Is.True);
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(),
                Is.EqualTo("GoalReached"));
        }

        [UnityTest]
        public IEnumerator AccidentReplayShowsThreeViewsThenFeedback()
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
                "The tracked rig must visibly recoil, drop or tilt before the replay.");

            // after the body-bound impact view, the three-view replay takes over
            var replay = hybrid.GetType().GetProperty("Replay")?.GetValue(hybrid);
            Assert.That(replay, Is.Not.Null);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!(bool)replay.GetType().GetProperty("IsVisible").GetValue(replay) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(replay.GetType().GetProperty("IsVisible").GetValue(replay), Is.True);
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(), Is.EqualTo("Replay"));

            var overview = replay.GetType().GetProperty("OverviewCamera").GetValue(replay) as Camera;
            var participant = replay.GetType().GetProperty("ParticipantCamera").GetValue(replay) as Camera;
            var driver = replay.GetType().GetProperty("DriverCamera").GetValue(replay) as Camera;
            foreach (var view in new[] { overview, participant, driver })
            {
                Assert.That(view, Is.Not.Null);
                Assert.That(view.targetTexture, Is.Not.Null);
                Assert.That(view, Is.Not.EqualTo(Camera.main));
                Assert.That(view.stereoTargetEye, Is.EqualTo(StereoTargetEyeMask.None));
            }
            Assert.That(overview.enabled && participant.enabled, Is.True);
            Assert.That(driver.enabled, Is.False, "No vehicle hit the participant in this synthetic impact.");

            var canvas = replay.GetType().GetProperty("Canvas").GetValue(replay) as Canvas;
            Assert.That(canvas.gameObject.activeSelf, Is.True);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvas.worldCamera, Is.EqualTo(Camera.main));
            AssertComfortablePanel(canvas, 1.5f, 1.9f, 55f, 66f);
            Assert.That(Camera.main.cullingMask, Is.EqualTo(1 << LayerMask.NameToLayer("UI")),
                "Ragdoll and live cars must not be drawn over the replay.");
            // overview is the largest view (left); the two side views together are half its area
            float Area(string name)
            {
                var rect = (RectTransform)canvas.transform.Find("ReplayPanel/" + name + "/Image");
                return rect.rect.width * rect.rect.height;
            }
            var big = Area("View_Overview");
            var side = Area("View_Participant") + Area("View_Driver");
            Assert.That(side / big, Is.InRange(0.45f, 0.55f));
            Assert.That(canvas.transform.Find("ReplayPanel/View_Overview").localPosition.x,
                Is.LessThan(canvas.transform.Find("ReplayPanel/View_Participant").localPosition.x));
            Assert.That(canvas.transform.Find("ReplayPanel/Button_Replay"), Is.Not.Null);
            Assert.That(canvas.transform.Find("ReplayPanel/Button_Skip"), Is.Not.Null);
            var replayRays = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true))
                .Where(c => c != null && c.GetType().Name == "XRRayInteractor").ToList();
            Assert.That(replayRays.All(r => r.gameObject.activeInHierarchy && r.GetComponent<LineRenderer>().enabled), Is.True,
                "The replay shows the pointer rays for Replay / Next.");

            // the participant is shown as a figure of the menu height, on a layer their own view skips
            var figure = replay.GetType().GetProperty("Participant").GetValue(replay) as Component;
            Assert.That(figure, Is.Not.Null);
            var menuHeight = (int)FindSceneComponent("GameDirector", scene).GetType().GetField("Height")
                .GetValue(FindSceneComponent("GameDirector", scene));
            if (menuHeight > 0)
                Assert.That((float)figure.GetType().GetProperty("HeightMeters").GetValue(figure),
                    Is.EqualTo(Mathf.Clamp(menuHeight / 100f, 0.9f, 2.1f)).Within(0.001f));
            Assert.That(participant.cullingMask & (1 << figure.gameObject.layer), Is.Zero);
            Assert.That(figure.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(10));

            // at the end the replay stays on its last frame until the participant presses Next
            bool WaitingAtEnd() => (bool)replay.GetType().GetProperty("IsWaitingAtEnd").GetValue(replay);
            deadline = Time.realtimeSinceStartup + 20f;
            while (!WaitingAtEnd() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(WaitingAtEnd(), Is.True, "The replay reaches its end.");
            yield return new WaitForSecondsRealtime(3f);
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(), Is.EqualTo("Replay"),
                "The replay must not advance to feedback by itself.");
            Assert.That(replay.GetType().GetProperty("IsVisible").GetValue(replay), Is.True);
            Assert.That(GetText(canvas.transform.Find("ReplayPanel/Button_Skip").GetComponentsInChildren<Component>(true)
                .First(c => c.GetType().Name == "TextMeshProUGUI")), Does.Contain("結果へ"));

            // Next -> feedback
            Invoke(replay, "Skip");
            deadline = Time.realtimeSinceStartup + 3f;
            while (flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString() != "Results" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(flow.GetType().GetProperty("Phase")?.GetValue(flow)?.ToString(), Is.EqualTo("Results"));
            Assert.That(overview.enabled || participant.enabled || driver.enabled, Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.False);
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

        [UnityTest]
        public IEnumerator GoalShowsSuccessFeedback()
        {
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);    // a little recorded movement

            var scene = SceneManager.GetActiveScene();
            var flow = FindSceneComponent("GameplayFlowController", scene);
            var player = FindSceneComponent("PlayerActor", scene);
            var goal = FindSceneComponent("GoalController", scene);
            Assert.That(goal, Is.Not.Null, "The active scenario has a goal.");
            // the real goal trigger path: chime, flow change, success page (no smoke particles)
            Invoke(goal, "OnTriggerEnter", player.GetComponent<Collider>());
            Assert.That(flow.GetType().GetProperty("Phase").GetValue(flow).ToString(), Is.EqualTo("GoalReached"));
            var particles = goal.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(particles.Any(p => p.isPlaying), Is.False, "The goal no longer plays the smoke effect.");

            var deadline = Time.realtimeSinceStartup + 6f;
            while (flow.GetType().GetProperty("Phase").GetValue(flow).ToString() != "Results" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(flow.GetType().GetProperty("Phase").GetValue(flow).ToString(), Is.EqualTo("Results"));
            var results = FindSceneComponent("AccidentResultPresenter", scene, true);
            Assert.That(results.GetType().GetProperty("IsSuccess").GetValue(results), Is.True);
            Assert.That(GetText((Component)GetField(results, "runtimeHeading")), Does.Contain("ゴール"));
            Assert.That(GetText((Component)GetField(results, "runtimeMetrics")), Does.Contain("左の確認").And.Contain("右の確認"));
            Assert.That(GetText((Component)GetField(results, "runtimeVerdict")), Is.Not.Empty);
            Assert.That(Camera.main.cullingMask, Is.EqualTo(1 << LayerMask.NameToLayer("UI")));
            Assert.That(((Behaviour)FindSceneComponent("OpenXRPlayerController", scene)).enabled, Is.False,
                "Movement stays frozen on the success page.");
        }

        [UnityTest]
        public IEnumerator TrialWritesThreeParsableCsvFiles()
        {
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);    // some sampled rows

            var scene = SceneManager.GetActiveScene();
            var director = FindSceneComponent("GameDirector", scene);
            var csv = director.GetComponent("CSVPrinter");
            var flow = FindSceneComponent("GameplayFlowController", scene);
            var hybrid = FindSceneComponent("HybridAccidentPresentation", scene, true);
            Assert.That(InvokeBool(flow, "TryReachGoal"), Is.True);
            director.GetType().GetField("GoalFlag").SetValue(director, 1);
            Assert.That(InvokeBool(hybrid, "PresentGoal"), Is.True);
            var deadline = Time.realtimeSinceStartup + 6f;
            while (flow.GetType().GetProperty("Phase").GetValue(flow).ToString() != "Results" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Invoke(csv, "CSVPrint");
            Invoke(csv, "CSVPrint");    // a second call in the same trial must not write again
            var files = (string[])csv.GetType().GetProperty("LastWrittenFiles").GetValue(csv);
            try
            {
                Assert.That(files.Length, Is.EqualTo(3));
                var car = System.IO.File.ReadAllLines(files[0]);
                var human = System.IO.File.ReadAllLines(files[1]);
                var track = System.IO.File.ReadAllLines(files[2]);

                Assert.That(human.Length, Is.EqualTo(2));
                var head = human[0].Split(',');
                var row = human[1].Split(',');
                Assert.That(head, Is.EqualTo(new[] { "EventNumber", "Height", "Weight", "DieFlash", "Gender", "Age",
                    "License", "OnAcident", "OnGoal", "Hz", "SmartPhone", "Incident", "Weather", "SkyTime", "Scene", "CustomScenario" }),
                    "Existing analysis scripts read these columns in this order; only append.");
                Assert.That(row.Length, Is.EqualTo(head.Length));
                Assert.That(row.Last(), Is.Empty, "Built-in scenarios leave CustomScenario empty.");
                Assert.That(row[0], Is.EqualTo(director.GetType().GetField("EventNumber").GetValue(director).ToString()));
                Assert.That(row[Array.IndexOf(head, "OnGoal")], Is.EqualTo("1"));
                Assert.That(row[Array.IndexOf(head, "OnAcident")], Is.EqualTo("0"));
                Assert.That(row[Array.IndexOf(head, "Scene")], Is.EqualTo(scene.name));

                Assert.That(track[0], Is.EqualTo("Time,PlayerPositionX,PlayerPositionY,PlayerPositionZ,PlayerRotationX,PlayerRotationY,PlayerRotationZ,AfterAcident,AcidentProgress"));
                Assert.That(track.Length, Is.GreaterThan(5), "Head samples are recorded during the trial.");
                // Body (VehicleBody) was appended last; the older columns keep their order
                Assert.That(car[0], Is.EqualTo("Time,CarID,CarPositionX,CarPositionY,CarPositionZ,AcidentCar,Body"));
                var previous = -1f;
                foreach (var line in track.Skip(1))
                {
                    var cells = line.Split(',');
                    Assert.That(cells.Length, Is.EqualTo(9), line);
                    foreach (var cell in cells.Take(7))
                        Assert.That(float.TryParse(cell, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out _), Is.True, line);
                    var time = float.Parse(cells[0], System.Globalization.CultureInfo.InvariantCulture);
                    Assert.That(time, Is.GreaterThanOrEqualTo(previous), "Time must not go backwards.");
                    previous = time;
                }
                foreach (var line in car.Skip(1))
                {
                    var cells = line.Split(',');
                    Assert.That(cells.Length, Is.EqualTo(7), line);
                    Assert.That(new[] { "sedan", "kei-tall", "kei-hatch" }, Does.Contain(cells[6]), line);
                }
            }
            finally
            {
                // never leave test trials among real experiment data
                foreach (var file in files)
                    if (System.IO.File.Exists(file))
                        System.IO.File.Delete(file);
            }
        }

        static readonly int[] ScheduledScenarioIds = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        [UnityTest]
        public IEnumerator AccidentAreaLaunchesTheScheduledCar(
            [ValueSource(nameof(ScheduledScenarioIds))] int scenarioId)
        {
            void ConfigureBeforeStart(Scene loaded, LoadSceneMode mode)
            {
                if (loaded.path != GameplayScene)
                    return;
                var director = FindSceneComponent("GameDirector", loaded);
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
            var area = FindSceneComponent("AccidentCarFactory", scene);
            Assert.That(area, Is.Not.Null, "The active scenario has an accident area.");
            var definition = area.GetType().GetProperty("Definition").GetValue(area);
            Assert.That(definition, Is.Not.Null);
            Assert.That((int)GetField(definition, "id"), Is.EqualTo(scenarioId));
            var launches = (Array)GetField(definition, "accidentLaunches");
            var firstDelay = launches.Cast<object>().Min(l => (float)GetField(l, "delaySeconds"));

            bool AccidentCarActive() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(c => c.GetType().Name == "CarController" && (bool)GetField(c, "AccidentCar"));
            Assert.That(AccidentCarActive(), Is.False);

            var player = FindSceneComponent("PlayerActor", scene);
            Invoke(area, "OnTriggerEnter", player.GetComponent<Collider>());
            var deadline = Time.realtimeSinceStartup + firstDelay + 1.5f;
            while (!AccidentCarActive() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(AccidentCarActive(), Is.True,
                $"Scenario {scenarioId}: the first accident car must appear {firstDelay:0.0} s after the trigger.");
        }

        /// <summary>World-locked, in front of the eyes, at a comfortable distance and angular width.</summary>
        static void AssertComfortablePanel(Canvas canvas, float minDistance, float maxDistance, float minDegrees, float maxDegrees)
        {
            var head = Camera.main.transform;
            Assert.That(canvas.transform.IsChildOf(head), Is.False, "Not head-locked.");
            var toPanel = canvas.transform.position - head.position;
            Assert.That(toPanel.magnitude, Is.InRange(minDistance, maxDistance));
            Assert.That(Vector3.Angle(Vector3.ProjectOnPlane(head.forward, Vector3.up), Vector3.ProjectOnPlane(toPanel, Vector3.up)),
                Is.LessThan(10f), "In front of the viewer.");
            var rect = (RectTransform)canvas.transform;
            var width = rect.rect.width * rect.lossyScale.x;
            var degrees = 2f * Mathf.Atan(width * 0.5f / toPanel.magnitude) * Mathf.Rad2Deg;
            Assert.That(degrees, Is.InRange(minDegrees, maxDegrees), "The whole panel fits in the sharp part of the lens.");
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
