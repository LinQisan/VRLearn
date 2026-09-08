# VRLearn 项目开发指南

本文供参与本项目的开发者与 AI 编程助手使用。修改前应核对实际代码、场景配置与工作区差异；本文与实现不一致时，以实现为依据并更新文档。

## 项目定位与环境

VRLearn 是面向 Meta Quest 的 Unity 交通安全体验项目，包含步行、自行车、交通生成、事故演出、轨迹回放和结果复盘，也支持无头显的编辑器键鼠体验。

- Unity：`6000.3.19f1`，版本依据 `ProjectSettings/ProjectVersion.txt`。
- 主要依赖：Meta XR Core `203.0.0`、OpenXR `1.17.1`、XR Interaction Toolkit `2.6.3`、Input System `1.19.0`、URP `17.3.0`。
- 依赖声明与解析版本分别见 `Packages/manifest.json`、`Packages/packages-lock.json`；不要在普通功能修改中顺带升级。
- 设备构建目标为 Quest2；
- 仓库：`https://github.com/LinQisan/VRLearn`，主分支为 `main`。

事故回放和车损用于教学展示，不应描述为事故鉴定级物理重建。

## 目录与入口

| 路径 | 用途 |
| --- | --- |
| `Assets/_Project/Scripts/` | 项目运行时代码 |
| `Assets/_Project/Scenes/` | 正式 Meta 标题与体验场景 |
| `Assets/_Project/ScenarioDefinitions/` | 10 个情景配置资产 |
| `Assets/_Project/Prefabs/` | 车辆等预制件 |
| `Assets/_Project/UI/` | 界面字体与 UI 贴图 |
| `Assets/_Project/Materials/`、`Textures/`、`Shaders/` | 项目材质、贴图、渲染纹理与着色器 |
| `Assets/_Project/Models/`、`Animations/` | 项目模型、生成网格、动画与控制器 |
| `Assets/_Project/Audio/` | 音效与混音器 |
| `Assets/_Project/World/` | 地形等世界资源 |
| `Assets/_Project/Editor/` | Meta 字体准备及构建工具 |
| `Assets/_Project/Tests/EditMode/` | 场景配置、记录逻辑测试 |
| `Assets/_Project/Tests/PlayMode/` | 游戏流程、事故体验及车辆生命周期测试 |
| `Assets/ThirdParty/` | 第三方模型、插件与配套脚本 |
| `Assets/Resources/`、`Assets/XR/`、`Assets/Oculus/` | 运行时加载资源与 XR 配置 |
| `ProjectSettings/`、`Packages/` | Unity 项目与包配置 |
| `Docs/` | 体验说明、整理记录与历史验证证据 |

当前 Build Settings 启用的场景依次为：

1. `Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity`
2. `Assets/_Project/Scenes/TraficAcident_Meta.unity`


## 代码职责与流程

- **标题与路由**：`GameDirector_Title` 管理选择和参数；`TitleCommandButton`、`TitleOptionToggle` 等处理界面；`SceneRoute` 管理场景名和情景编号。合法情景为 0–9，随机选择标记为 -1。
- **情景初始化**：`GameDirector`、`ScenarioRuntime`、`GameplaySceneContext`、`ScenarioDefinitionAsset` 负责情景加载、引用与配置；`PlayerActor` 负责玩家相关状态。情景 6、7、9 为自行车，其余为步行。
- **输入与 XR**：`MetaTitleSceneSetup`、`MetaGameplaySceneSetup` 初始化 Meta 场景；`OpenXRScene`、`OpenXRInput` 封装玩家和输入访问；`MetaEditorSimulationController` 提供编辑器模拟；`XRUIRuntimePolicy` 管理 UI 运行时策略。
- **交通**：`CarFactory`、`AccidentCarFactory` 生成车辆；`VehiclePool` 管理复用；`CarController` 驱动车辆；`WaypointPath`、`WaypointRouteRegistry` 与 `TrafficManager` 管理路线和交通。
- **事故与结果**：`MetaVehicleImpactSensor` 检测接触，`GameplayFlowController` 管理阶段，`TrafficAccidentState` 冻结交通；`HybridAccidentPresentation`、`AccidentImpactPhysics`、`AvatarPresenter` 处理演出与身体表现。
- **复盘与重试**：`AccidentMotionHistory` 记录轨迹，`AccidentTrajectoryReplay` 回放，`AccidentOverheadView` 提供俯视，`AccidentResultPresenter` 展示结果；`ScenarioRetry` 按当前参数重载情景。`AccidentVehicleDamage`、`VehicleWheelVisuals` 负责车辆视觉效果。

事故主流程为 `Preparing → Playing → AccidentTriggered → Impact → Replay → Results → Finished`。正常到达通过 `GoalReached` 分支处理；修改时核对 `GameplayFlowController` 和既有表现脚本之间的衔接，不要另建互相竞争的状态机。

## 修改约定

1. 先查看 Git 差异及目标代码，保留已有未提交修改。优先在项目脚本中作小范围修改，遵循所在文件的命名、缩进和生命周期风格。
2. 修改资产时保留 `.meta` 和 GUID。移动或删除资产须同步处理其 `.meta`，并检查场景、预制件、ScriptableObject 的引用。不能仅凭 C# 无调用就判定组件无用，还需检查序列化引用与 UnityEvent 绑定。
3. 重命名序列化字段要考虑已有资产数据迁移，必要时使用 `FormerlySerializedAs`。不要无关地重写大型场景、预制件或批量重新序列化资源。
4. 编辑器工具放在 `Editor` 目录或使用适当的编译条件，避免将 `UnityEditor` 依赖带入设备代码。
5. 不直接改 `Library/PackageCache`。涉及第三方插件时先检查项目侧是否能完成修复；必须修改时说明原因与升级影响。
6. 保持 10 个情景的编号、配置与场景引用一致。改变数量或数据结构时同步更新路由、初始化校验及测试。
7. 通过现有 XR 封装切换移动与重力，避免 OVR 和编辑器控制器同时驱动玩家；避免直接覆盖头部跟踪相机的旋转。UI 输入模块的选择应遵循场景初始化策略。
8. 车辆回收和场景重载时，复位路线、速度、事故标记、刚体、网格、协程、静态状态及事件订阅。事故速度应在冻结车流前读取实际速度。
9. 提前结束演出或进入结果页时，应清理回放、恢复隐藏模型并停止震动。重试须保留情景、身体参数、天气、时间和手机等现有配置。
10. 当前界面包含日文文案，新增文字须检查 TMP 字形覆盖。已有字体准备工具会修改字体资产，运行后检查相关差异。

## 本地运行与验证

使用指定版本 Unity 打开项目，等待包解析与资源导入完成，然后打开 Meta 标题场景并进入 Play Mode。

- 编辑器：W/A/S/D 移动，按住鼠标右键观察，鼠标操作界面；Tab 对应辅助按钮输入，Enter/Space 对应主按钮输入；结果界面支持返回和同条件重试，R 使用重试逻辑。
- Quest：使用现有手柄移动与射线交互；涉及设备输入、头部追踪、渲染或舒适性的修改需要真机验收。

按修改范围选择验证：纯文档检查内容、路径及差异即可；逻辑与资产修改运行相关测试；涉及场景初始化、状态流转或车辆复用时覆盖两个项目测试程序集。可在 Unity Test Runner 中运行，也可在关闭同一项目的编辑器实例后执行批处理。

以下为 macOS 示例，在项目根目录运行，两个测试集顺序执行，使用新的结果目录保存每轮证据：

```sh
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity"
PROJECT_DIR="$PWD"
RESULT_DIR="$PROJECT_DIR/Logs/Tests/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$RESULT_DIR"

"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PROJECT_DIR" \
  -runTests -testPlatform EditMode -assemblyNames VRLearn.EditModeTests \
  -testResults "$RESULT_DIR/editmode.xml" -logFile "$RESULT_DIR/editmode.log"

"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PROJECT_DIR" \
  -runTests -testPlatform PlayMode -assemblyNames VRLearn.PlayModeTests \
  -testResults "$RESULT_DIR/playmode.xml" -logFile "$RESULT_DIR/playmode.log"
```

检查测试 XML 的结果与日志，不仅依赖进程退出码。测试使用项目现有程序集结构，部分运行时代码通过反射访问；移动类型或重命名成员时检查测试是否同步受影响。

涉及体验流程时，手工覆盖标题进入、事故、正常到达、重试和返回；特别检查自行车情景 6、7、9，并连续运行至少三次以观察残留状态。无图形批处理不能证明界面、声音、后处理或头显交互正确。

`Docs/Audit-20260908/verification.md` 记录了历史 EditMode 18/18、PlayMode 23/23 通过；该记录不是后续修改的验证结果，也不包含 Android 构建或 Quest 真机通过结论。

## Android 构建

已有构建入口为 `AccidentExperienceValidation.BuildAndroid`，会先执行 `PrepareFont`，再按启用场景构建至 `Builds/VRLearn-AccidentExperience.apk`。需要 Android 验证时可使用上述 Unity 路径变量执行：

```sh
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PROJECT_DIR" \
  -buildTarget Android -executeMethod AccidentExperienceValidation.BuildAndroid \
  -logFile "$PROJECT_DIR/Logs/android-build.log"
```

字体也可通过菜单 `Tools/VRLearn/Prepare Accident Experience Font` 准备。构建成功与设备运行验收应分别报告。

## Git 与交付

- 遵循 `.gitignore`，提交源码、必要资源及其 `.meta`、包配置、项目设置和文档。
- 不提交 Unity 缓存、日志、用户设置、生成的工程文件、APK、本地备份或凭据。`Builds/`、`Backups/`、`Library/`、`Logs/` 等已排除。
- 提交前检查 `git diff --check` 和差异范围，避免混入 Unity 自动产生的无关变化。按用户要求提交或推送，不强制覆盖远程历史。
- 交付说明写清实际修改、已执行验证及尚未验证的部分。历史文档中的“没有 Git 仓库”等描述已过时，不应作为当前状态依据。
