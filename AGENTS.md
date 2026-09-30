# AGENTS.md — VRLearn

Guide for AI coding agents and developers working on this project. Read **Hard rules** before changing anything. If this file disagrees with the code, scenes or tests, the implementation wins — fix this file as part of your change.

Design intent and trade-offs: [DESIGN.md](DESIGN.md) (Chinese). Hikone scene parameters and generation details: [Docs/Hikone/README.md](Docs/Hikone/README.md) (Chinese).

**Language:** write code, comments, commit messages and this file in English. Talk to the project owner in Chinese. In-game UI text is bilingual Japanese / English.

## At a glance

- **What:** a traffic-safety VR experience and experiment for Meta Quest 2 aimed at **pedestrians** (not specifically children). The participant walks (a few scenarios are ridden on a bicycle) through an intersection, experiences a hazard (an accident), then sees a replay, a review and results. Data is written to CSV.
- **Unity** `6000.3.19f1` (see `ProjectSettings/ProjectVersion.txt`). Main packages: Meta XR Core `203.0.0`, OpenXR `1.17.1`, XR Interaction Toolkit `2.6.3`, Input System `1.19.0`, URP `17.3.0`. Do not upgrade packages as a side effect of feature work.
- **Build Settings** (order is fixed and checked by an EditMode test):
  1. `Assets/_Project/Scenes/Title.unity` — title
  2. `Assets/_Project/Scenes/Gameplay_Hikone.unity` — Hikone Kyobashi environment (the only gameplay scene; the old standard scene was removed in September 2026)
- **Scenarios:** 10 built-in (ids 0–9), -1 means random. Scenarios 6, 7 and 9 are cycling; the rest are walking. **Custom scenarios** are JSON files built at runtime (`EventNumber` 100, see below).
- **Repository:** `https://github.com/LinQisan/VRLearn`, default branch `main`.
- Accident replay and vehicle damage are educational presentation, never an accident-reconstruction-grade simulation.

## Layout

| Path | Contents |
| --- | --- |
| `Assets/_Project/Scripts/` | Runtime code (single assembly, Assembly-CSharp), by area: `Core/` (GameDirector, flow, scene routing, retry, CSV), `Scenario/` (scenario data, custom scenarios, accident trigger, goal), `Traffic/` (cars, factories, pool, waypoints, signals), `Player/` (player, avatar, bicycle, phone), `XR/` (rig set-up, input, Editor simulation), `Accident/` (impact, replay, feedback), `Title/` (title menu), `UI/` (UiKit, VrPanel), `Recording/` |
| `Assets/_Project/Editor/` | Editor tools: font preparation, Android build, title menu generator `TitleMenuLayout.cs`, `DocScreenshots.cs` (`Tools/VRLearn/Docs/Capture Screenshots` re-renders the pictures in `Docs/Hikone`) |
| `Assets/_Project/Hikone/` | Hikone environment: `Models/` (FBX), `Materials/`, `Textures/`, `Prefabs/`, `Layout/hikone_layout.json`, `Editor/HikoneEnvironmentBuilder.cs` |
| `Art/Hikone/` | **Source** of the Hikone assets: Blender/Python generators and constraint data exported from the gameplay scene's roads, waypoints and triggers |
| `Assets/_Project/ScenarioDefinitions/` | `Scenario_00`–`09` scenario assets |
| `ScenarioEditor/` | Web scenario editor (Node, no dependencies): `server.mjs`, `web/` (UI; `web/scenario.js` is the shared model/validation), `test/` |
| `Scenarios/` | Custom scenario files (JSON): `scenario.schema.json`, `templates/builtin-01…10.json` (exported built-ins), `maps/hikone-kyobashi/` (`map.png` + `map.json` for the web editor; generated) |
| `Assets/_Project/Prefabs/` | Vehicles (`Car_Left`, …) and rain particles |
| `Assets/_Project/Tests/EditMode`, `PlayMode` | Tests (the test assemblies reach game code through reflection) |
| `Assets/ThirdParty/` | Third-party content; do not modify unless unavoidable |
| `Docs/` | `Hikone/` (current scene notes and screenshots), `History/` (dated records, text only: evidence of past states, not current rules; raw logs and test XML are in git history) |

## Code map

- **Title & routing:** `GameDirector_Title` holds every selection (body data, scenario, unpleasant tone, …). `TitleOptionToggle` drives the tiled options, `TitleCommandButton` the command buttons (±5, Start, Reset, Play). `TitleButtonFeedback` / `TitleButtonStyle` own colours and click feedback. Scenario tiles are grouped by `ScenarioDefinitionAsset.setting` (crossing / mid-block / bicycle) and `TitleScenarioDetail` shows the selected scenario's name and learning goal; `TitleMenuSummary` shows the chosen conditions next to Start. Scenarios are shown **1-based** (01–10) everywhere in the UI; `EventNumber` and the CSV stay 0-based. `SceneRoute` owns scene names and scenario ids; every route (start, retry, back to title) goes to `SceneRoute.Gameplay`.
- **Scenario setup:** `GameDirector` → `ScenarioRuntime` (exactly 10 entries) / `GameplaySceneContext` / `ScenarioDefinitionAsset`; `PlayerActor` holds player state.
- **Scenario content is data:** each `ScenarioDefinitionAsset` holds the names (`displayName`, `shortTitle`, `shortTitleEn`), `setting`, `learningGoal`, `eventSummary` (`事故の状況：…\n安全確認のポイント：…`) and the **accident schedule** (`stopTraffic`, `accidentLaunches`: factory, delay, heading, offset). `AccidentCarFactory` only runs that schedule when the participant enters the accident area; `CarFactory.SpawnAccidentCar` launches one car. The canonical table lives in `Assets/_Project/Editor/ScenarioCatalog.cs` (`Tools/VRLearn/Scenarios/Write Scenario Catalog To Assets`); edit the table, not the assets. The schedules reproduce the old hard-coded timings exactly (DESIGN.md §3.1).
- **Custom scenarios (JSON):** `CustomScenario` (format, validation, `ToJson`), `CustomScenarioSession` (the chosen file; `EventNumber` **100**), `CustomScenarioLibrary` (folders: `persistentDataPath/Scenarios` on the headset, plus the repo's `Scenarios/` in the Editor). `GameDirector.Start` calls `CustomScenarioRunner.Setup` instead of `ScenarioRuntime.Activate` when `EventNumber` is 100: it builds spawn, goal (cloned from built-in 01's goal), trigger area, one `WaypointPath` per vehicle and the vehicle schedule, then `ScenarioRuntime.ActivateCustom`. Everything after that (placement, impact, replay, feedback, CSV) is the shared pipeline. Only the Hikone map is supported. Code that used to test scenario numbers for cycling must use `ScenarioMode.IsBicycle` (it reads the active scenario). `ScenarioDefinition` exposes `DisplayName`, `EventSummary`, `LearningGoal`, `NumberLabel` for both kinds — use those, not `asset`, in UI.
- **XR & input:** `MetaTitleSceneSetup` places the title canvas 1.6 m in front of the headset (level, once); `MetaGameplaySceneSetup`; `OpenXRScene` / `OpenXRInput` wrap input (left stick = move, A/X = primary); `MetaEditorSimulationController` provides keyboard/mouse simulation in the Editor. Pointer rays: `GameplayFlowController` shows the controllers (and their rays) only on `GoalReached`, `Replay` and `Results` and hides them while walking or riding; `OpenXRScene.SetControllerVisualsVisible` hides the controller models only, never the ray's `LineRenderer`.
- **Traffic:** `CarFactory` / `AccidentCarFactory` → `VehiclePool` → `CarController`, driving along `WaypointPath` (via `WaypointRouteRegistry`). **Vehicle height comes from a downward raycast onto the road colliders**; waypoint Y values are ignored.
- **Accident & results:** `MetaVehicleImpactSensor` → `GameplayFlowController` (`Preparing → Playing → AccidentTriggered → Impact → Replay → Results → Finished`, plus the `GoalReached` branch) → `HybridAccidentPresentation` (impact flash + body-bound view) → `AccidentReplayPresenter` (three-view replay) → `AccidentResultPresenter` (feedback: explanation + evaluation, Title / Try again). A safe arrival goes `GoalController` → `HybridAccidentPresentation.PresentGoal` (chime, short green pulse, haptic; no particles) → `GoalReached → Results` via `MarkGoalResults` → `AccidentResultPresenter.ShowSuccess` with a `CrossingAnalysis` (seconds looking left/right of the travel direction, closest car). `ScenarioRetry` reloads the **current** scene with the current settings. Do not add a competing state machine.
- **Replay data:** `AccidentReplayRecorder` (on the player object) samples head pose, body, bicycle and every `CarController` at 20 Hz into a 12 s ring buffer and keeps recording through the traffic freeze until the replay starts; `Build()` cuts the window (8 s before to 1.6 s after contact) and starts it after the last placement before contact (a jump over 1.5 m in one sample, or the bicycle appearing), so pre-placement frames never show the figure and the bicycle apart. `AccidentReplayAnalysis` derives the feedback figures (time the participant looked toward the car, last look before contact, speeds). Replay visuals are render-only copies made at contact time (before damage deformation); live objects are hidden, never rewound. The participant is a `ReplayMannequin`: a primitive figure proportioned from the menu **height and weight** (girth from BMI), walking with the recorded body speed, turning its head with the recorded head pose, seated on the recorded bicycle's saddle and facing along it when cycling (`HipTarget` / `HeadTarget` give the seat and riding direction), falling after contact, with a lime ground ring and the gaze fan. Shared UI look (palette, rounded sprites, pill buttons) is `UiKit`.
- **Data:** `CSVPrinter` writes `CarData_*`, `HumanData_*`, `HumanData_InExperiment_*`. The last `HumanData` column, `Scene`, records the environment. Only ever append new columns at the end. Numbers are written culture-invariant; each trial is saved exactly once, both on "Title" and on "Try again" (R on the feedback page). R during a run is an operator abort and saves nothing. Files go to `Application.persistentDataPath` (Editor on macOS: `~/Library/Application Support/DefaultCompany/VRLearn`; Quest: `/sdcard/Android/data/com.moxuanxuerain.vrlearn/files`). Tests that save a trial must delete their files.

### Names kept for data compatibility

Code identifiers use correct spelling; renamed serialized fields carry `[FormerlySerializedAs("<old name>")]` so scenes and prefabs still load. These legacy spellings are **data** and must stay as they are:

| Kept as is | Where | Meaning |
| --- | --- | --- |
| `OnAcident`, `AfterAcident`, `AcidentProgress`, `AcidentCar` | CSV column names | accident happened / after accident / legacy presentation step / car is the accident car |
| `AcidentAreas1…9`, `AcidentArea0`, `CarFactory_Acident[_Left/_Right]`, `WayPointContainer_Acident[_Left/_Right]`, `Bicycle_AfterAcident` | scene object names found by code | per-scenario accident area, its car factories and routes |
| `AcidentFactory` | tag | accident car factory |
| `DieFlash` (`DieFlashNumber`, CSV `DieFlash`) | option, CSV | 走馬灯 (flashback) effect |
| `ButtonOption`, `LegacyAccidentPresentation` (was `CenterEyeCamera`) | scripts | the pre-Meta title/result buttons and accident sequence; `LegacyAccidentPresentation` also holds the shared `Accident` / `AccidentProgress` state read by the CSV and goal checks |

Renamed in this cleanup (GUIDs unchanged): scenes `TraficAcidentTitle_Meta` → `Title`, `TraficAcident_Meta` (later removed), `TraficAcident_Hikone_Meta` → `Gameplay_Hikone` (the CSV `Scene` column now records the new names); classes `CenterEyeCamera` → `LegacyAccidentPresentation`, `TrafficLightUP` → `TrafficSignalPreset`, `MetaTitleMenuPages` → `TitleSoundReadout`, `ButtonTest` → `ButtonClickEffect`, `GoalTextController` → `BillboardText`, `ReverseCollider` → `InsideOutMeshCollider`, `CanvasController` → `GameplayCanvasAnchor`; Japanese-named audio/material/texture files → English names.

## Hard rules (breaking these breaks the experiment)

1. **Do not move or alter experiment objects:** player, waypoints, spawns, goals, accident areas, car factories, `DestroyArea`s, traffic signals, `InvisibleWallContainer`, `ScenarioHost` and the bindings of all 10 scenarios. The Hikone scene only replaces the environment visuals.
2. **The road colliders are ground truth.** `Environment/RoadContainerNeo` only has its renderers disabled; its colliders, lamp-pole colliders and night lights stay. Carriageway is **y 0.01**, sidewalk **y 0.41** (40 cm curbs, sloped ramps at junction corners). Any new road visual must match exactly, or wheels sink or float (this has happened).
3. **The black walls are occluders, not clutter.** `OnOffContainer/BlackWallContainer` has no code references and no colliders; it hides where vehicles spawn and despawn. Removing it lets participants see cars pop in — a confound. In the Hikone scene they sit inside gatehouses (the yagura gate at the west end of Route 25, the nagaya gate over the southern side street).
4. **Occlusion is an experimental variable.** After changing buildings, trees or walls, re-check spawn/despawn visibility and each scenario's accident-route visibility (method: DESIGN.md §4) and report any change.
5. **Never hand-edit generated output.** `Assets/_Project/Hikone/Models/*.fbx`, `Layout/hikone_layout.json` and everything under `HikoneEnvironment` in the scene are generated; change `Art/Hikone/*.py` or `HikoneEnvironmentBuilder.cs` and regenerate. Same for the title layout: change `TitleMenuLayout.cs`.
6. **Exactly 10 built-in scenarios.** Changing the count or data shape requires updating `SceneRoute`, the `ScenarioRuntime` validation and the tests. New scenarios are added as JSON files (custom scenarios), not as scene objects. The JSON format is versioned (`version`); a breaking change needs a new version, a loader for the old one, and updates to `Scenarios/scenario.schema.json` and the templates.
7. **Asset GUIDs:** move or delete assets together with their `.meta`. Before calling a component unused, check C# references, serialized references and UnityEvent bindings — code search alone is not enough.
8. **Serialized data:** renaming a field needs a migration (`FormerlySerializedAs`); only append to serialized enums (that is how `TitleOptionKind` grew).
9. **XR:** never let the OVR and Editor controllers drive the player at the same time; never overwrite the tracked head camera's rotation.
10. **Vehicle reuse:** on release or reload, reset route, speed, accident flags, rigidbody, mesh, coroutines, static state and event subscriptions. Read the accident speed before traffic is frozen.
11. **Replay integrity:** the replay must always be able to reach the feedback page — after one playback it stops on its last frame and waits for the participant ("結果へ進む" or A/X; it never advances by itself), Skip is always available, frame errors fall through to feedback, and `HybridAccidentPresentation` keeps a watchdog deadline (kept alive while the replay plays or waits). Keep all of these when changing it. The participant figure, its path and gaze lines live on the `Ignore Raycast` layer so the participant camera does not see them.
12. **VR legibility (Quest 2, ~20 px per degree):** body text ≥ ~1.1° tall, nothing below 0.9°; touch targets ≥ 2.5°. The title menu is **one page**: a 2.5 m wide world-space canvas at 1.6 m (150 × 88 units, 1 unit ≈ 0.6°, about 76° wide); `MetaGameplaySmokeTests` measures every text and tile and checks that the controller ray reaches every control (interactable, a raycaster on its canvas, nothing drawn over it). Never put a control in `Header_Content`: it has its own canvas and a non-interactive CanvasGroup (the v0.2.1 step tabs sat there and could not be pressed). The replay (1.7 m, 64°) and feedback (1.6 m, 60°) are **world-locked** panels (`VrPanel`: level, in front, re-centres after a 45° turn) over a head-locked black backdrop — never head-locked pages that fill the lens (edges are outside the sharp area and cannot be looked at). Do not enable TMP auto-sizing at runtime (it silently shrank the old menu); sizes are authored in `TitleMenuLayout`. Menu-only screens raise `XRSettings.eyeTextureResolutionScale` to 1.3; gameplay resets it to 1.
13. **Text:** the UI is Japanese/English; TMP fonts are static atlases, so new strings need their glyphs baked (`TitleMenuLayout` and `AccidentExperienceValidation.PrepareFont` do it; the latter also reads all scenario texts) — then review the font asset diff. After editing scenario texts, run `Tools/VRLearn/Prepare Accident Experience Font` and rebuild the title menu.

## Workflows

### Change the Hikone environment

```text
Art/Hikone/hk_terrain.py   height function: moat, masugata, castle hill (shared by Blender and the layout)
Art/Hikone/hk_assets.py    all modular assets (authored in Unity coordinates; the export X-mirror is compensated)
Art/Hikone/hk_textures.py  procedural textures
Art/Hikone/hk_layout.py    layout + conflict validation  ->  Assets/_Project/Hikone/Layout/hikone_layout.json
```

1. Edit the scripts, run `python3 Art/Hikone/hk_layout.py` and require `validation problems: 0`.
2. In Blender run `hk_textures.run(); hk_assets.build_all()` (through BlenderMCP or `Blender -b --python`).
3. In Unity run `Tools/VRLearn/Hikone/1. Import Models, Materials & Prefabs`, then `2. Build Hikone Scene`.
4. If roads, waypoints or triggers in the scene changed (these are experiment objects, see hard rule 1), first run `Tools/VRLearn/Hikone/0. Export Constraints From Scene` to refresh `Art/Hikone/scene_constraints.json` and `road_tiles_geometry.json`.

### Change the title menu

Edit `Assets/_Project/Editor/TitleMenuLayout.cs`, then run `Tools/VRLearn/Rebuild Title Menu (VR tiles)`. It is idempotent and reuses existing controls and bindings. Layout, all on one page: header (title), a top row of cards (participant / conditions / survey / tone), then the scenario card (built-in groups or custom tiles, the built-in / custom tabs, Random and the detail panel) and the start column (selection summary, Reset, Start). Design rules: DESIGN.md §6 and hard rule 12.

### Custom scenarios

- Run one: `Tools/VRLearn/Custom Scenarios/Play Scenario File…` (opens the Hikone scene and plays the file; the scene is not modified).
- Check all files: `Tools/VRLearn/Custom Scenarios/Validate Scenario Files`.
- Refresh `Scenarios/templates/` after changing a built-in scenario: `Tools/VRLearn/Custom Scenarios/Export Built-in Scenarios As Templates`. The templates reproduce spawn, goal, trigger, routes, headings, timings and car models; behaviour that only exists as code for a built-in number (06 speeding/not stopping, 07/08 side-impact crash) is not in the file.
- The CSV writes `EventNumber` 100 and the file's `id` in the last column, `CustomScenario`.
- Map for the web editor: `Tools/VRLearn/Custom Scenarios/Export Map For Scenario Editor` writes `Scenarios/maps/hikone-kyobashi/map.png` (orthographic top-down, 10 px/m, trees hidden, fog off) and `map.json`: the world rectangle (`px = (x - xMin) * pixelsPerMeter`, `py from top = (zMax - z) * pixelsPerMeter`), road surfaces as triangles from the road colliders (`carriageway` y 0.01, `sidewalk` y 0.41, `ramp`, `ground`), the invisible walls that bound the participant, the built-in lanes (route polylines, driving direction = point order), signal positions and the allowed vehicle models. Re-export after any change to roads, walls, routes or the environment look; generated, never hand-edit. Vehicles may cross sidewalks at driveways (07/08 start in the parking exit), so "not on the carriageway" is a warning, "not on any road surface" an error.

### Web scenario editor

- Run: `cd ScenarioEditor && node server.mjs`, open http://localhost:8765/ (binds 127.0.0.1). Tests: `npm test` (node:test; no packages).
- `web/scenario.js` mirrors `CustomScenario.cs`: fields, defaults, JSON field order, and the **same Japanese validation messages** (a test checks the C# file contains them). Change both together. It adds map checks from `map.json` (vehicle points must be on carriageway/ramp/sidewalk; sidewalk-only is a warning; spawn/goal must be on road tiles — the "ground" plane also lies under the moat; walls between spawn and goal and a trigger off the walking line are warnings) and a timing model matching `CarController` (8 m/s² to cruise speed; repeating cars use the middle of their interval).
- Saving writes `Scenarios/<id>.json`; ids starting with `builtin-` are refused (templates are never overwritten). Delete moves files to `Scenarios/.trash/` (git-ignored).
- "▶ Unityで試す" writes `Scenarios/.play-request.json`; `ScenarioEditorBridge` (Editor, `InitializeOnLoad`) polls it once a second when not playing/compiling, deletes it and calls `CustomScenarioTools.Play`. `CustomScenarioLibrary.Scan` skips `maps/`, hidden files/folders and the schema.

### Custom scenarios on the headset

- **Title:** the scenario card has two tabs. "組み込み" is the built-in grid; "カスタム" (`TitleCustomScenarios`) fills 12 prebuilt slot tiles per page from `CustomScenarioLibrary.ScanForTitle()` (the headset folder `persistentDataPath/Scenarios`, plus the repo's `Scenarios/` in the Editor; templates excluded; invalid files and duplicate ids are counted, not shown). Slots use `TitleOptionKind.CustomScenario` (appended to the enum) with the scenario id; `GameDirector_Title.SelectCustomScenario` sets `EventNumber` 100 and `CustomScenarioSession.Current`. "読み直す" rescans without restarting. `SceneRoute.ClampScenarioSelection` keeps 100 only while a custom scenario is selected, so returning from a custom run reopens its tab. Built-in tile counts and sizes are unchanged (the title test still sees 11 scenario tiles).
- **PC → Quest:** the web editor's "⇪ Questに送る" runs adb (found via `$ADB`, PATH, the Android SDK or the adb bundled with Unity) and mirrors every error-free non-template scenario into `/sdcard/Android/data/<package>/files/Scenarios` (package read from `ProjectSettings.asset`), deleting headset files no longer on the PC. USB or wireless adb; the app must be installed. No networking code runs in the app. `ScenarioEditor/test/fake-adb.mjs` emulates a headset for the tests.
- **Fonts:** user-written text contains characters the static TMP atlases were never baked with. `NotoSansJP Dynamic Fallback SDF` (dynamic, from `NotoSansJP-SemiBold.ttf`, cleared on build) is a fallback of every static Japanese font asset (`Tools/VRLearn/Setup Dynamic Font Fallback`). Its atlas fills up while the Editor renders text; run that menu again before committing so the asset stays empty (`FontFallbackTests` checks the fallback wiring).

## Verification

- Docs-only change: check paths and content.
- Logic or asset change: run both test assemblies. Current baseline: **EditMode 44/44, PlayMode 42/42, ScenarioEditor `npm test` 11/11** (about 2 minutes). `MetaGameplaySmokeTests` covers title tiles, every scenario, the accident schedule of scenarios 01–09 (trigger → first accident car), replay → feedback, goal → success page, and one trial → three parsable CSV files; `ScenarioAndFeedbackTests` (EditMode) checks the scenario catalog, the success analysis and the replay figure's size; `HikoneSceneTests` checks vehicles on the road, a grounded player and title routing; `CustomScenarioFormatTests` (EditMode) and `CustomScenarioTests` (PlayMode) cover the JSON format, the templates, `ScenarioMapTests` (EditMode) checks the exported map against its image, the lanes and the templates, `ScenarioEditor/test` (node) covers the web model, validation parity, timing and the server, a template of 01 running like the built-in, the custom goal/feedback/CSV, a cycling file, and a file on the device listed on the title's custom tab and started from there.
- Batch mode (close any Editor that has this project open first):

```sh
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity"
RESULT_DIR="$PWD/Logs/Tests/$(date +%Y%m%d-%H%M%S)"; mkdir -p "$RESULT_DIR"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -assemblyNames VRLearn.EditModeTests -testResults "$RESULT_DIR/editmode.xml" -logFile "$RESULT_DIR/editmode.log"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -assemblyNames VRLearn.PlayModeTests -testResults "$RESULT_DIR/playmode.xml" -logFile "$RESULT_DIR/playmode.log"
```

- Trust the XML results, not the process exit code.
- Headless runs cannot prove UI, audio or headset interaction. For experience changes, walk through title → accident → replay/results → retry → back manually, and run cycling scenarios 6, 7 and 9 three times in a row to catch leftover state.
- Input, head tracking, rendering and comfort changes need on-device acceptance on a Quest. Report "build succeeded" and "verified on device" separately.

### Known pitfalls

- **Two timing-sensitive tests fail occasionally:** `VehicleLifecycleTests.FirstUseAndReuseObserveIdenticalSpawnConfig` and `WalkingPhoneModePresentsPhoneInView`. They pass on rerun; treat them as regressions only if they fail repeatedly.
- **Test runs modify project settings:** `ProjectSettings/EditorSettings.asset` `m_EnterPlayModeOptions` becomes 1, `UnityConnectSettings.asset` `m_Enabled` becomes 0, and `InitTestScene*.unity` files may be left in `Assets/`. Revert or delete them before committing.
- **The Editor throttles in the background:** test jobs started through MCP may not begin until Unity is brought to the front.
- **Blender → FBX → Unity mirrors the X axis.** `hk_lib.U()` compensates; never write vertices around it.

## MCP tools (optional)

`.mcp.json` registers two servers:
- `blender` — `uvx blender-mcp`. Click Connect in Blender's BlenderMCP sidebar panel (port 9876).
- `unityMCP` — MCP for Unity over HTTP at `http://127.0.0.1:8080/mcp`. Start a session in Unity via `Window → MCP for Unity`.

`.codex/config.toml` uses Unity's official `unity mcp`; it is independent of the servers above.

MCP for Unity's `execute_code` can fail silently in Roslyn mode; pass `compiler: "codedom"`.

## Android build

`AccidentExperienceValidation.BuildAndroid` prepares the font, then builds the enabled scenes to `Builds/VRLearn-<bundleVersion>-<versionCode>.apk` (e.g. `VRLearn-0.2.0-2.apk`). Bump `bundleVersion` and the Android `bundleVersionCode` in Player Settings for every APK handed out:

```sh
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -buildTarget Android \
  -executeMethod AccidentExperienceValidation.BuildAndroid -logFile "$PWD/Logs/android-build.log"
```

## Git and delivery

- Commit source, required assets with their `.meta`, package manifests, project settings and docs. Never commit `Library/`, `Logs/`, `Temp/`, `Builds/`, `Backups/`, APKs, `*.csproj` or credentials. Keep only the APKs still being tested or handed out in `Builds/`; released APKs live on the GitHub release. Version control is git only (the Plastic / Unity Version Control workspace and package were removed).
- Run `git diff --check` and drop unrelated Unity-generated changes (see Known pitfalls) before committing.
- Commit or push only when asked; never force-push over remote history.
- Every hand-off states what changed, what was verified, and what was not.
