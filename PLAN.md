# Meta 交通安全模拟器正确运行实施方案

## 总结

- 目标：无需头显，仅在 Unity Editor 内通过键鼠完整运行 `TraficAcidentTitle_Meta → TraficAcident_Meta → 事故/到达终点 → 返回标题`。
- 覆盖场景 0–9，包含步行、自行车、车辆生成、事故展示、结果说明、正常到达和参数回传。
- 当前基线：Unity 6.3、Meta XR SDK 203、OpenXR；Android APK 已成功构建并在 Quest 2 启动过，未发现崩溃，但项目没有自动化测试，且编辑器模拟器未配置。

## 当前架构与涉及资产

### Scene

- `TraficAcidentTitle_Meta.unity`：当前启用的标题场景，使用 `OVRCameraRig`、`OVRInputModule`、世界空间标题 Canvas。
- `TraficAcident_Meta.unity`：当前启用的游戏场景，包含 Meta XR 玩家、10 个场景根节点、车辆工厂、道路、事故/结果 UI。
- `TraficAcidentTitle.unity`、`TraficAcident.unity`：旧 OpenXR/XRI 场景，仅作为键鼠移动和 UI 输入实现的参考，不纳入本轮运行入口。

### Prefab

- Meta SDK 的 `OVRCameraRig.prefab`。
- `Car_EndlessGo.prefab`、`Car_Left.prefab`、`Car_Right.prefab`、`Car_SideHit.prefab`。
- `RainParticle.prefab` 继续用于天气效果。
- 本轮不重做车辆、人物或道路资源，也不复制 Meta 场景。

### ScriptableObject

- `Scenario_00.asset` 至 `Scenario_09.asset`。
- 已包含唯一 ID、标题、事故说明、步行/自行车模式和交通流配置；保持现有数据结构，只增加完整性校验。

### 核心 Script

- 标题与路由：`GameDirector_Title`、`TitleCommandButton`、`TitleOptionToggle`、`SceneRoute`。
- Meta/XR：`MetaTitleSceneSetup`、`MetaGameplaySceneSetup`、`OpenXRScene`、`OpenXRInput`、`OpenXRPlayerController`。
- 场景生命周期：`GameDirector`、`ScenarioRuntime`、`GameplayFlowController`、`GameplaySceneContext`、`PlayerActor`。
- 交通：`CarFactory`、`AccidentCarFactory`、`CarController`、`VehiclePool`、`TrafficManager`、`WaypointPath`、`WaypointRouteRegistry`。
- 结果展示：`HybridAccidentPresentation`、`AvatarPresenter`、`AccidentResultPresenter`、`GoalController`、`BicycleController`。

## 实施变更

### 编辑器键鼠模拟层

- 新增 `MetaEditorSimulationController`，仅在 `Application.isEditor` 时创建和运行，Quest/Android 路径完全不启用。
- 标题场景使用普通鼠标点击世界空间 Canvas；禁用 `OVRInputModule/OVRRaycaster`，启用 `InputSystemUIInputModule/GraphicRaycaster`。
- 游戏场景使用：
  - `W/A/S/D`：移动或自行车行进。
  - 按住鼠标右键拖动：视角旋转，俯仰限制为 ±80°。
  - `Tab`：切换游戏 UI。
  - `Enter` 或 `Space`：结果页返回。
  - `R`：保留现有场景重置行为。
- 编辑器视角通过插入到 `Camera Offset` 与 `OVRCameraRig` 之间的运行时 Pivot 驱动，不直接写入被 Meta 跟踪系统管理的 CenterEye 相机。
- 鼠标保持可见，不采用锁定光标模式，保证游戏内世界空间按钮始终可点击。

### 输入与移动所有权

- 扩展 `OpenXRInput`：编辑器中读取键盘，设备构建继续读取 XR Controller。
- 编辑器使用现有 `OpenXRPlayerController`；设备构建继续使用 `OVRPlayerController`。
- 修改 `OpenXRScene`，统一提供启停移动、重力和最低加速度设置，确保任意时刻只有一个移动控制器生效。
- `GameDirector.LateRespawn` 移除对 `OVRPlayerController` 的直接启停和参数写入，统一调用 `OpenXRScene`，避免编辑器重新启用 OVR 控制器。
- 步行速度继承当前 OVR 参数；自行车场景继续使用现有较高加速度、IK 目标和自行车随 XR Origin 移动逻辑。

### 场景和流程稳定性

- `ScenarioRuntime.Awake` 在其他场景组件进入 `Start` 前关闭全部场景根和交通根；`GameDirector.Start` 根据传入 ID 只激活一个场景，消除 10 个场景同时初始化的顺序风险。
- 启动时校验场景 0–9：
  - ID 唯一且连续。
  - ScriptableObject、根节点、出生点、目标和事故区域均有效。
  - 玩家模式、交通流、车辆 Prefab 和路线完整。
- 保留现有车辆池、路线注册表、事故感应器和事故状态机，不重写 `CarController` 的既有行驶逻辑。
- `AccidentResultPresenter` 在编辑器模式生成标准 GraphicRaycaster 结果 Canvas，并在进入结果页时恢复鼠标操作。
- 保持现有流程：`Playing → AccidentTriggered → Impact → Replay → Results → Finished`；正常到达继续走 `GoalReached → 返回标题`。

### 接口影响

- 保持 `SceneRoute`、`ScenarioDefinitionAsset` 和标题参数传递格式不变。
- `OpenXRScene` 增加模式无关的移动参数接口；已有启停移动和重力接口保持兼容。
- 不新增配置 ScriptableObject，不改变 10 个场景资产的数据格式。
- 编辑器模拟层不会进入 Android/Quest 的运行分支。

## 风险控制

- Meta 与标准 UI 输入模块不能同时工作；切换必须集中在场景初始化脚本中完成。
- OVR 与键鼠移动控制器不能同时写 CharacterController；由 `OpenXRScene` 统一选择所有者。
- 不直接旋转 CenterEye，防止 Meta SDK 在 LateUpdate 覆盖编辑器视角。
- 车辆与玩家实体碰撞当前被忽略，事故由 `MetaVehicleImpactSensor` 触发；测试必须确认编辑器 CharacterController 仍能进入该触发器。
- 自行车在骑行、事故时会重新挂接父节点并切换刚体模式，必须覆盖场景 6、7、9 的重复进入和重置。
- 车辆池复用时必须复位路线、事故标记、速度、刚体和动态感应器状态。
- 项目当前没有 Git 仓库；修改大型 Scene 前先创建可恢复备份，且不得覆盖现有场景文件作为迁移手段。

## 验证与验收

### 自动检查

- 增加 EditMode 测试：
  - Build Settings 中 Meta 标题/游戏场景启用且顺序正确。
  - 10 个 Scenario 资产与场景条目一一对应。
  - 所有必需引用、车辆 Prefab、出生点、目标和路线非空。
  - Meta 场景无 Missing Script、重复 EventSystem 或重复 Main Camera。
- 增加 PlayMode 参数化测试，对场景 0–9 分别验证：
  - 进入 `Playing`，仅目标场景根被激活。
  - 玩家出生位置、身高/体重碰撞体和步行/自行车模式正确。
  - 对应车辆能够生成并取得有效路线。
  - 事故流程到达 `Results`，交通和移动被冻结，说明文字取自正确的 Scenario 资产。
  - 目标触发进入 `GoalReached` 并返回 Meta 标题。
  - 返回后事件编号、身高、体重、环境和调查参数保持。
  - 重载场景后静态事故状态、车辆池和事件订阅不会残留。

### 手工 Editor 验收

- 无头显启动 `TraficAcidentTitle_Meta`，验证全部选项、数值按钮、随机场景和声音预览可用。
- 对场景 0–9 各执行一次事故路径和一次正常到达路径。
- 验证 W/A/S/D、右键视角、Tab UI、结果页按钮与 Enter/Space 返回。
- 验证场景 6、7、9 的自行车位置、地面贴合、转向、事故脱离和倒地表现。
- 验证事故后车辆停止、视角过渡、人物倒地、结果文字和再次进入场景。
- Console 不得出现编译错误、空引用、MissingReference、缺少路线或 Meta 引用不完整错误。
- 连续完成至少三次“标题 → 游戏 → 返回”循环，确认输入模块、相机和静态状态不累积。

## 完成标准与默认假设

- 验收环境仅为 Unity Editor，不要求本轮完成 Quest 真机视觉验收。
- 必须覆盖全部 10 个场景及事故、目标两条结果路径。
- 使用项目内键鼠替代层，不安装 Meta XR Simulator 或 XR Device Simulator。
- Quest/Android 原有 OVR/OpenXR 路径必须保持可编译；最后执行一次 Android 构建作为非回归检查。
- 不改变现有玩法内容、场景编号、事故说明、车辆美术和道路布局。
