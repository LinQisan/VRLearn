# AGENTS.md

VRLearn: a traffic-safety VR experience **and experiment** for Meta Quest 2, aimed at pedestrians (not specifically children). The participant walks (cycles in scenarios 6, 7, 9) through a Hikone Kyobashi intersection, meets a hazard, then sees a replay, feedback and results; every trial is written to CSV. Replay and damage are educational presentation, not accident reconstruction.

Unity `6000.3.19f1`; Meta XR Core 203.0.0, OpenXR 1.17.1, XR Interaction Toolkit 2.6.3, Input System 1.19.0, URP 17.3.0 — never upgrade packages as a side effect. Scenes (Build Settings order, checked by a test): `Title`, `Gameplay_Hikone` (the only gameplay scene). Scenarios: 10 built-in (ids 0–9, shown 01–10 in the UI; -1 = random) and custom JSON files (`EventNumber` 100). Repository `LinQisan/VRLearn`, branch `main`.

Talk to the owner in Chinese. Code, comments, commits and this file in English; RATIONALE.md and `Docs/Hikone` in Chinese; README in Japanese; UI Japanese / English.

## Commands

```sh
D="$PWD/Logs/Tests/$(date +%Y%m%d-%H%M%S)"                                     # close the Editor first
# Targeted examples: choose the affected fixture or test; do not run both automatically.
unity test "$PWD" --mode EditMode --filter HikonePropTests --output "$D/props.xml" --timeout 1200
unity test "$PWD" --mode PlayMode --filter PortedWheelsRollAfterBodySwapsAndReturnToRestOnReuse --output "$D/wheels.xml" --timeout 1800
# Full regression: only when required by Verification below.
unity test "$PWD" --mode EditMode --output "$D/editmode.xml" --timeout 1200    # ~1.5 min (mostly Editor start-up)
unity test "$PWD" --mode PlayMode --output "$D/playmode.xml" --timeout 1800    # ~3.5 min
(cd ScenarioModel && npm test)                                                 # when shared format/data/timing changes; no packages
"/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath "$PWD" -buildTarget Android \
  -executeMethod AccidentExperienceValidation.BuildAndroid -logFile "$PWD/Logs/android-build.log"   # → Builds/VRLearn-<version>-<code>.apk
```

`unity test` (Unity's CLI, see Tools) runs only this project's test assembly for the mode and exits 0 (passed), 8 (tests failed) or 6 (the run itself failed: compile error, licence, crash, timeout). Read the counts from the XML. Bump `bundleVersion` and `bundleVersionCode` for every APK handed out.

Editor menus (`Tools/VRLearn/…`) for generated content — change the source, then run the menu:

| Change | Edit | Then run |
| --- | --- | --- |
| Hikone environment | `Art/Hikone/hk_*.py` (`python3 Art/Hikone/hk_layout.py` must report `validation problems: 0`) | Blender `hk_assets.build_all()` (export only the changed models: FBX carry timestamps) → `Hikone/1. Import…` → `Hikone/2. Build Hikone Scene` → `Hikone/3. Check Occlusion` → map export → `Docs/Capture Screenshots`. If scene roads, waypoints or triggers changed, first `Hikone/0. Export Constraints From Scene`. New materials need entries in `hk_lib.MAT_COLORS` and the pigment tables of `HikonePaintAssets` |
| Title menu | `Assets/_Project/Editor/TitleMenuLayout.cs` | `Rebuild Title Menu (VR tiles)` (idempotent) |
| Built-in scenario content | the table in `Assets/_Project/Editor/ScenarioCatalog.cs`, not the assets | `Scenarios/Write Scenario Catalog To Assets`, then `Custom Scenarios/Export Built-in Scenarios As Templates` |
| Scenario or UI text | the source above | `Prepare Accident Experience Font` + rebuild the title menu; review the font asset diff |
| Roads, walls, routes, environment look | — | `Custom Scenarios/Export Map For Scenario Editor` (`Scenarios/maps/`) |
| Anything the browser port shows | — | `Web/Export Scene For Web`, then in `../hikone-traffic-sim` its `tools/export_models.py` and `node tools/sync.mjs` |

Also: `Custom Scenarios/Play Scenario File…`, `Validate Scenario Files`; `Maintenance/Report Unused Assets` (`Logs/unused-assets.txt`: what nothing in the build reaches); `Setup Dynamic Font Fallback` (re-run before committing so the dynamic fallback atlas is empty).

## Hard rules (breaking these breaks the experiment)

1. **Experiment objects stay put:** player, waypoints, spawns, goals, accident areas, car factories, `DestroyArea`s, traffic signals, `InvisibleWallContainer`, `ScenarioHost` and the bindings of all 10 scenarios. The Hikone scene replaces visuals; its synchronized signal cycle follows `GameplayFlowController` (D41), while vehicle scheduling stays unchanged.
2. **Road colliders are ground truth.** `Environment/RoadContainerNeo` has only its renderers off; colliders, lamp-pole colliders and night lights stay. Carriageway y 0.01, sidewalk y 0.16 (15 cm kerbs, ramps at junction corners; was 40 cm until `HikoneKerbMigration`, RATIONALE D37 — its height-mapped copies of the road meshes are in `Assets/_Project/Hikone/Kerb15`, the third-party originals are untouched; the tool refuses to run twice). Any new road visual must match exactly. Vehicle height comes from a downward raycast onto these colliders; waypoint Y is ignored.
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
- `Assets/_Project/Editor/` menus and generators; `Assets/_Project/Hikone/` generated environment, painted by the `VRLearn/HikonePaint` shader with the browser port's pigments, brush and sky (`HikonePaintAssets`; RATIONALE D38 — clear day only, rain and night keep their own sky and light in `GameDirector`); `Art/Hikone/` its Python sources and scene constraints.
- `Scenarios/` custom JSON, schema, `templates/`, `maps/` (generated); `ScenarioModel/` shared JS model. The web editor and browser port are `../hikone-traffic-sim` (separate repository).
- Custom scenarios: `persistentDataPath/Scenarios` on the headset (plus `Scenarios/` in the Editor); `CustomScenarioRunner.Setup` builds them at runtime, then the shared pipeline runs. The web editor's "▶ Unityで試す" writes `Scenarios/.play-request.json`, which `ScenarioEditorBridge` picks up.
- CSV files (`CarData_*`, `HumanData_*`, `HumanData_InExperiment_*`) go to `persistentDataPath`: macOS Editor `~/Library/Application Support/DefaultCompany/VRLearn`, Quest `/sdcard/Android/data/com.moxuanxuerain.vrlearn/files`.
- `Assets/ThirdParty/` — do not modify unless unavoidable.

## Verification

Choose checks by the behavior affected, not the number of changed lines. Use the smallest existing fixture or test that covers the change; keep the full suites available. Do not add tests for prose, pure formatting or cosmetic constants, or tests that only repeat the implementation. Add a focused regression test when fixing a behavior that existing tests do not cover.

When several rows apply, combine their focused checks; use full regression if the cross-cutting or APK row applies.

| Change | Required checks |
| --- | --- |
| Docs, comments, pure formatting (no literal/serialized value changes) | Review content/paths and `git diff --check`; no Unity launch. |
| Editor-only report/tool, with no runtime or generated asset changes | Compile/run the affected menu or method and inspect its output; no PlayMode suite. If it writes assets, also use the appropriate asset row below. |
| Material/texture/colour only | Regenerate from source and inspect the affected view; no full automated suites. Shader changes also need a render check with the affected shader. |
| Model/prefab visuals | Related EditMode checks (e.g. `HikonePropTests` for sedan prefabs) and a visual check. Run the wheel/body PlayMode case only if wheel hierarchy, damage or body switching is affected. Silhouette/placement changes still require the before/after occlusion check in rule 4. |
| Text/fonts or title layout | Rebuild the affected content/fonts and inspect it. Font changes: `FontFallbackTests`; title layout/control changes: `MetaTitleUsesOneStandardMouseInputPathInEditor`. No unrelated gameplay tests. |
| Isolated runtime fix | Affected EditMode test or PlayMode test/fixture, plus a focused reproduction. E.g. wheel reset: `PortedWheelsRollAfterBodySwapsAndReturnToRestOnReuse`; vehicle reuse: `VehicleLifecycleTests`. |
| Scenario text/content or custom format/loader | Select `ScenarioAndFeedbackTests`, `CustomScenarioFormatTests`, `ScenarioMapTests` and/or the affected `CustomScenarioTests` case as applicable. Run `ScenarioModel` tests when shared format, templates, map or timing inputs change. Count/shape, scheduling or binding changes use full regression below. |
| Cross-cutting runtime changes, flow/replay ownership, CSV contract/save paths, pooling lifecycle, XR/input, experiment bindings/routes/spawns/colliders, package/assembly changes, or uncertain impact | Full EditMode + PlayMode; shared JS tests if affected. |
| APK delivery | Full EditMode + PlayMode + ScenarioModel; build validation and the experience checks below. |

Filtered Unity runs still pay the Editor start-up cost. Prefer one appropriate fixture over multiple launches for its individual cases. Read the XML counts and names: a zero-test run is not a pass. Retry failures with `--rerun-failed` using the same mode and existing output report, or use an explicit `--filter`. Expand coverage after failures or newly discovered dependencies; once relevant checks pass, do not repeat or broaden them merely for hand-off, commit or push. Reuse results while the tested code, assets and dependencies remain unchanged.

Full-suite inventory: **EditMode 55 cases, PlayMode 46 cases, ScenarioModel 8 cases**. Report the actual selected run's results, not these inventory counts. `VehicleLifecycleTests.FirstUseAndReuseObserveIdenticalSpawnConfig` and `WalkingPhoneModePresentsPhoneInView` fail occasionally; rerun the failing case before calling it a regression.

## Before handing off

- Complete the applicable checks above and run `git diff --check`.
- Test runs may leave changes to revert before committing: `EditorSettings.asset` `m_EnterPlayModeOptions`, `UnityConnectSettings.asset` `m_Enabled`, stray `Assets/InitTestScene*.unity`.
- Headless tests do not prove UI, audio or headset behaviour. For local visual/audio changes inspect the affected view or interaction. For flow, replay, movement, pooling or XR changes and APK delivery, walk title → accident → replay/results → retry → back, and run 6, 7, 9 three times in a row. Report "build succeeded" and "verified on device" separately.
- Commit only source, assets with `.meta`, manifests, settings and docs — never `Library/`, `Logs/`, `Temp/`, `Builds/`, APKs, `*.csproj`, credentials. Commit or push only when asked; never force-push.
- Say what changed, what was verified, and what was not.

## Tools

Unity's official CLI (`unity`, in `~/.unity/bin`; `unity self-update`) drives the Editor through the `com.unity.pipeline` package in this project: `unity status`, `unity list`, `unity command …` (`unity command eval` takes C# statements: `return …;`), and its MCP server `unity mcp` (the `unity` entry in `.mcp.json`; Codex: `.codex/config.toml`). Its agent skill is installed with `unity skill install claude-code`. `blender` in `.mcp.json` is the community `uvx blender-mcp` (Connect in Blender's BlenderMCP panel, port 9876). It stays on purpose: the Blender Foundation's official MCP server (Blender Lab) needs Blender 5.1+, and Blender 5.x has no Intel Mac build — this Mac (Intel) runs Blender 4.5 LTS, the last one with Intel support. The asset pipeline does not need any MCP (`Blender -b --python`); either MCP runs generated code unguarded.

Long Editor jobs (steps 1–2, occlusion, map export) from the command line: `unity run "$PWD" -- -executeMethod <Class.Method> -logFile <file>` with the Editor closed — never add `-batchmode`/`-quit` (it fails with exit 6). An open Editor in the background throttles: `unity command eval` hits its 5 s main-thread limit and queued work may never start. Anything that renders for a file (`DocScreenshots`, the scenario map) turns off asynchronous shader compilation first; otherwise a shader still compiling draws a placeholder (a magenta or two-tone image).

## Docs

[RATIONALE.md](RATIONALE.md) why things are as they are, and the decision log · [Docs/Hikone/README.md](Docs/Hikone/README.md) Hikone scene parameters · [Scenarios/README.md](Scenarios/README.md) custom scenario files · `Docs/History/` dated records (evidence of past states, not rules) · [README.md](README.md) overview (Japanese). Each fact lives in one place; if a doc disagrees with the code, the code wins — fix the doc in the same change.
