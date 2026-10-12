# AGENTS.md

VRLearn: a traffic-safety VR experience **and experiment** for Meta Quest 2, aimed at pedestrians (not specifically children). The participant walks (cycles in scenarios 6, 7, 9) through a Hikone Kyobashi intersection, meets a hazard, then sees a replay, feedback and results; every trial is written to CSV. Replay and damage are educational presentation, not accident reconstruction.

Unity `6000.3.19f1`; Meta XR Core 203.0.0, OpenXR 1.17.1, XR Interaction Toolkit 2.6.3, Input System 1.19.0, URP 17.3.0 — never upgrade packages as a side effect. Scenes (Build Settings order, checked by a test): `Title`, `Gameplay_Hikone` (the only gameplay scene). Scenarios: 10 built-in (ids 0–9, shown 01–10 in the UI; -1 = random) and custom JSON files (`EventNumber` 100). Repository `LinQisan/VRLearn`, branch `main`.

Talk to the owner in Chinese. Code, comments, commits and this file in English; RATIONALE.md and `Docs/Hikone` in Chinese; README in Japanese; UI Japanese / English.

## Commands

```sh
D="$PWD/Logs/Tests/$(date +%Y%m%d-%H%M%S)"                                     # close the Editor first
unity test "$PWD" --mode EditMode --output "$D/editmode.xml" --timeout 1200    # ~1.5 min (mostly Editor start-up)
unity test "$PWD" --mode PlayMode --output "$D/playmode.xml" --timeout 1800    # ~3.5 min
(cd ScenarioModel && npm test)                                                 # shared scenario model, no packages
"/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath "$PWD" -buildTarget Android \
  -executeMethod AccidentExperienceValidation.BuildAndroid -logFile "$PWD/Logs/android-build.log"   # → Builds/VRLearn-<version>-<code>.apk
```

`unity test` (Unity's CLI, see Tools) runs only this project's test assembly for the mode and exits 0 (passed), 8 (tests failed) or 6 (the run itself failed: compile error, licence, crash, timeout). Read the counts from the XML. Bump `bundleVersion` and `bundleVersionCode` for every APK handed out.

Editor menus (`Tools/VRLearn/…`) for generated content — change the source, then run the menu:

| Change | Edit | Then run |
| --- | --- | --- |
| Hikone environment | `Art/Hikone/hk_*.py` (`python3 Art/Hikone/hk_layout.py` must report `validation problems: 0`) | Blender `hk_textures.run(); hk_assets.build_all()` (export only the changed models: FBX carry timestamps) → `Hikone/1. Import…` → `Hikone/2. Build Hikone Scene` → `Hikone/3. Check Occlusion` → map export → `Docs/Capture Screenshots`. If scene roads, waypoints or triggers changed, first `Hikone/0. Export Constraints From Scene`. New materials need entries in `hk_lib.MAT_COLORS` and the builder's `MatSpec` |
| Title menu | `Assets/_Project/Editor/TitleMenuLayout.cs` | `Rebuild Title Menu (VR tiles)` (idempotent) |
| Built-in scenario content | the table in `Assets/_Project/Editor/ScenarioCatalog.cs`, not the assets | `Scenarios/Write Scenario Catalog To Assets`, then `Custom Scenarios/Export Built-in Scenarios As Templates` |
| Scenario or UI text | the source above | `Prepare Accident Experience Font` + rebuild the title menu; review the font asset diff |
| Roads, walls, routes, environment look | — | `Custom Scenarios/Export Map For Scenario Editor` (`Scenarios/maps/`) |
| Anything the browser port shows | — | `Web/Export Scene For Web`, then in `../hikone-traffic-sim` its `tools/export_models.py` and `node tools/sync.mjs` |

Also: `Custom Scenarios/Play Scenario File…`, `Validate Scenario Files`; `Setup Dynamic Font Fallback` (re-run before committing so the dynamic fallback atlas is empty).

## Hard rules (breaking these breaks the experiment)

1. **Experiment objects stay put:** player, waypoints, spawns, goals, accident areas, car factories, `DestroyArea`s, traffic signals, `InvisibleWallContainer`, `ScenarioHost` and the bindings of all 10 scenarios. The Hikone scene only replaces visuals.
2. **Road colliders are ground truth.** `Environment/RoadContainerNeo` has only its renderers off; colliders, lamp-pole colliders and night lights stay. Carriageway y 0.01, sidewalk y 0.41 (40 cm curbs, ramps at junction corners); any new road visual must match exactly. Vehicle height comes from a downward raycast onto these colliders; waypoint Y is ignored.
3. **Black walls are occluders** (`OnOffContainer/BlackWallContainer`, no code references, no colliders): they hide where cars spawn and despawn. Never remove them.
4. **Occlusion is an experimental variable.** After changing buildings, trees, walls, parked vehicles or terrain, run `Hikone/3. Check Occlusion` (`Logs/occlusion-report.txt`) before and after (e.g. with `git stash`) and report any difference.
5. **Never hand-edit generated output:** `Hikone/Models/*.fbx`, `Layout/hikone_layout.json`, everything under `HikoneEnvironment` in the scene, the title menu, the scenario assets, `Scenarios/templates/`, `Scenarios/maps/`. Blender → FBX mirrors X; `hk_lib.U()` compensates — never write vertices around it.
6. **Exactly 10 built-in scenarios.** Changing count or shape means updating `SceneRoute`, the `ScenarioRuntime` validation and the tests. New scenarios are JSON files. The JSON format is versioned: a breaking change needs a new `version`, a loader for the old one, `Scenarios/scenario.schema.json` and the templates.
7. **`ScenarioModel/scenario.js` mirrors `CustomScenario.cs`** (fields, defaults, field order, the same Japanese validation messages; a test compares them). Change both together; the browser port's copy is synced, never edited.
8. **Assets and serialized data:** move or delete assets with their `.meta`; check C#, serialized and UnityEvent references before calling anything unused; renamed fields get `[FormerlySerializedAs]`; serialized enums only grow at the end.
9. **CSV is append-only:** new columns go at the end; numbers culture-invariant; each trial saved exactly once (on "Title" and on "Try again"; R during a run is an operator abort and saves nothing). Tests that save a trial delete their files.
10. **Legacy spellings are data and stay:** CSV columns `OnAcident`, `AfterAcident`, `AcidentProgress`, `AcidentCar`, `DieFlash`; scene objects `AcidentAreas1…9`, `AcidentArea0`, `CarFactory_Acident[_Left/_Right]`, `WayPointContainer_Acident[_Left/_Right]`, `Bicycle_AfterAcident`; tag `AcidentFactory`; scripts `ButtonOption`, `LegacyAccidentPresentation` (holds the shared `Accident` / `AccidentProgress` state).
11. **One flow state machine:** `GameplayFlowController` (`Preparing → Playing → AccidentTriggered → Impact → Replay → Results → Finished`, plus `GoalReached`). Do not add a competing one.
12. **Replay integrity:** after one playback the replay stops on its last frame and waits for "結果へ進む" / A/X (never advances by itself); Skip is always available; frame errors fall through to feedback; `HybridAccidentPresentation` keeps a watchdog alive while the replay plays or waits. Replay figure, path and gaze lines stay on `Ignore Raycast`. Replay visuals are render-only copies; live objects are hidden, never rewound.
13. **Vehicle reuse:** on release or reload reset route, speed, accident flags, rigidbody, mesh, body (back to sedan), coroutines, static state and subscriptions. Read the accident speed before traffic freezes. The kei-car traffic mix uses its own `System.Random` — never touch the `UnityEngine.Random` sequence of the traffic timing.
14. **XR:** never let the OVR and Editor controllers drive the player together; never overwrite the tracked head camera's rotation. Controllers and rays show only on `GoalReached`, `Replay`, `Results`; hiding controller models never hides the ray's `LineRenderer`.
15. **VR legibility (Quest 2, ~20 px/°):** body text ≥ ~1.1°, nothing below 0.9°, targets ≥ 2.5°; the title menu is one page (2.5 m canvas at 1.6 m) and `MetaGameplaySmokeTests` measures it. Never put a control in `Header_Content` (non-interactive CanvasGroup). Replay and feedback are world-locked `VrPanel`s, never head-locked full-lens pages. No TMP auto-sizing at runtime. Menus raise `eyeTextureResolutionScale` to 1.3; gameplay resets it to 1.
16. **Text:** TMP fonts are static atlases — new strings need their glyphs baked (see the menus above).
17. **Cycling checks** use `ScenarioMode.IsBicycle`, never scenario numbers; UI reads `ScenarioDefinition` (`DisplayName`, `EventSummary`, `LearningGoal`, `NumberLabel`), which covers built-in and custom scenarios.

## Where things are

- `Assets/_Project/Scripts/` (one assembly): `Core/` (`GameDirector`, `GameplayFlowController`, `SceneRoute`, `ScenarioRetry`, `CSVPrinter`), `Scenario/` (`ScenarioRuntime`, `ScenarioDefinitionAsset`, `CustomScenario*`, `AccidentCarFactory`, goal), `Traffic/` (`CarFactory` → `VehiclePool` → `CarController` on `WaypointPath`; `VehicleBody` + `Resources/VehicleBodies.asset`), `Player/`, `XR/` (`OpenXRScene`, `OpenXRInput`, `MetaEditorSimulationController`), `Accident/` (`MetaVehicleImpactSensor`, `HybridAccidentPresentation`, `AccidentReplayRecorder` / `Presenter` / `Analysis`, `ReplayMannequin`, `AccidentResultPresenter`), `Title/` (`GameDirector_Title` holds every selection), `UI/` (`UiKit`, `VrPanel`).
- `Assets/_Project/Editor/` menus and generators; `Assets/_Project/Hikone/` generated environment; `Art/Hikone/` its Python sources and scene constraints.
- `Scenarios/` custom JSON, schema, `templates/`, `maps/` (generated); `ScenarioModel/` shared JS model. The web editor and browser port are `../hikone-traffic-sim` (separate repository).
- Custom scenarios: `persistentDataPath/Scenarios` on the headset (plus `Scenarios/` in the Editor); `CustomScenarioRunner.Setup` builds them at runtime, then the shared pipeline runs. The web editor's "▶ Unityで試す" writes `Scenarios/.play-request.json`, which `ScenarioEditorBridge` picks up.
- CSV files (`CarData_*`, `HumanData_*`, `HumanData_InExperiment_*`) go to `persistentDataPath`: macOS Editor `~/Library/Application Support/DefaultCompany/VRLearn`, Quest `/sdcard/Android/data/com.moxuanxuerain.vrlearn/files`.
- `Assets/ThirdParty/` — do not modify unless unavoidable.

## Before handing off

- Docs-only: check paths and content. Logic or assets: both test assemblies pass — baseline **EditMode 46/46, PlayMode 43/43, ScenarioModel 8/8**. `VehicleLifecycleTests.FirstUseAndReuseObserveIdenticalSpawnConfig` and `WalkingPhoneModePresentsPhoneInView` fail occasionally; rerun before calling it a regression.
- Test runs may leave changes to revert before committing: `EditorSettings.asset` `m_EnterPlayModeOptions`, `UnityConnectSettings.asset` `m_Enabled`, stray `Assets/InitTestScene*.unity`. Run `git diff --check`.
- Headless tests do not prove UI, audio or headset behaviour: for experience changes walk title → accident → replay/results → retry → back, and run 6, 7, 9 three times in a row. Report "build succeeded" and "verified on device" separately.
- Commit only source, assets with `.meta`, manifests, settings and docs — never `Library/`, `Logs/`, `Temp/`, `Builds/`, APKs, `*.csproj`, credentials. Commit or push only when asked; never force-push.
- Say what changed, what was verified, and what was not.

## Tools

Unity's official CLI (`unity`, in `~/.unity/bin`; `unity self-update`) drives the Editor through the `com.unity.pipeline` package in this project: `unity status`, `unity list`, `unity command …` (`unity command eval` takes C# statements: `return …;`), and its MCP server `unity mcp` (the `unity` entry in `.mcp.json`; Codex: `.codex/config.toml`). Its agent skill is installed with `unity skill install claude-code`. `blender` in `.mcp.json` is the community `uvx blender-mcp` (Connect in Blender's BlenderMCP panel, port 9876). It stays on purpose: the Blender Foundation's official MCP server (Blender Lab) needs Blender 5.1+, and Blender 5.x has no Intel Mac build — this Mac (Intel) runs Blender 4.5 LTS, the last one with Intel support. The asset pipeline does not need any MCP (`Blender -b --python`); either MCP runs generated code unguarded.

## Docs

[RATIONALE.md](RATIONALE.md) why things are as they are, and the decision log · [Docs/Hikone/README.md](Docs/Hikone/README.md) Hikone scene parameters · [Scenarios/README.md](Scenarios/README.md) custom scenario files · `Docs/History/` dated records (evidence of past states, not rules) · [README.md](README.md) overview (Japanese). Each fact lives in one place; if a doc disagrees with the code, the code wins — fix the doc in the same change.
