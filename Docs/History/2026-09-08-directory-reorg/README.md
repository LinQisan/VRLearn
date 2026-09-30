# Unity 目录整理记录（2026-09-08）

## 整理原则与最终目录

保留正式 Meta 场景、资源 GUID 和第三方包内部布局，以修正混放和重复目录为主。运行时脚本仍集中在 Scripts，不按每个类另建子目录；Resources、Plugins/Android、TextMesh Pro、XR、Oculus 等目录保留其加载或工具约定。

```text
Assets/
├── _Project/
│   ├── Animations/           # Animator Controller、姿态 FBX
│   ├── Audio/                # 音效与混音器
│   ├── Editor/               # Meta 字体工具及 Android 构建
│   ├── Input/                # Input Actions
│   ├── Materials/            # 合并后的材质与物理材质
│   ├── Models/               # 人物、交通信号模型
│   │   └── Meshes/           # Meta 场景生成网格
│   ├── Prefabs/              # 车辆与雨粒子
│   ├── ScenarioDefinitions/  # 0–9 情景资产
│   ├── Scenes/               # 两个正式 Meta 场景
│   ├── Scripts/              # 运行时代码
│   ├── Settings/             # 渲染设置
│   ├── Shaders/              # 项目着色器
│   ├── Tests/                # EditMode / PlayMode
│   ├── Textures/             # 贴图与 RenderTexture
│   ├── UI/                   # Fonts / Textures
│   ├── World/                # Terrain
│   └── XR/                   # 项目 XR 配置
├── ThirdParty/               # 包括从 Scenes 移出的 ModularBuildingsFramework
├── Plugins/
├── Resources/
├── TextMesh Pro/
├── Oculus/
├── XR/
└── StreamingAssets/
```

## 移动与合并

- `Art/Audio` → `_Project/Audio`；`Art/Models` → `_Project/Models`，其中 Animator Controller 与 `sitpose.fbx` 归入 Animations。
- `Art/Materials` 的材质及其嵌套 Materials 合入现有 `_Project/Materials`；PNG 与 RenderTexture 归入 Textures，shader 归入 Shaders。
- `Art/Images/Triangle2.png` → `UI/Textures`；Art/VFX 的雨贴图、材质、预制件分别归入 Textures、Materials、Prefabs。
- `Scenes/japanese_traffic_signals.fbx` → Models；`Meshes/TraficAcident_Meta` → `Models/Meshes`。
- `Scenes/ModularBuildingsFramework` 整包移入 `Assets/ThirdParty`，保留脚本命名空间、Editor 层级和包内部资源结构。
- 删除已合空的 Art、Images、VFX、Materials、Meshes 及旧场景资源目录和对应文件夹 meta。

逐文件移动路径和 SHA-256 见 [file-audit.json](file-audit.json)。356 个移动文件（含 meta）内容完全不变；保留的资产与目录 GUID 未改变。

## 删除与必要代码调整

删除以下资产及对应 `.meta`：

- `Assets/_Project/Scenes/TraficAcidentTitle.unity`
- `Assets/_Project/Scenes/TraficAcident.unity`
- `Assets/_Project/Scenes/TraficAcident/NavMesh-Ground_Road.asset`：引用扫描确认只有已删除游戏场景使用它。
- `Assets/_Project/Editor/TitleMenuMigration.cs`：只迁移已删除标题场景，且自动加载时会读取该场景文件。
- `Assets/_Project/Editor/XRUIProjectMigration.cs`：仅处理两个已删除场景，项目内没有其他调用。

从 `TitleTextSdfMigration.cs` 移除专属于旧标题场景的入口及私有辅助方法，保留 Meta 日文字体功能。Build Settings 移除已删除场景记录；SceneRoute 只路由至现存 Meta 场景，两个调用处同步去除旧标题分支。

本次提交同时纳入用户上一轮要求的 `PLAN.md` 删除和 `Agent.MD` 新增；Agent 文档已删除这两个旧场景的描述，并更新目录说明。保留用户此前对设备目标的编辑。

## 因用途或替代关系不确定而保留

- `SM_Building1.fbx` 与 `SM_Building2.fbx` 二进制内容相同，但 GUID、导入配置不同，且两个资源都被正式 Meta 游戏场景引用，因此不合并。
- 未单凭“无直接代码调用”删除运行时脚本、第三方 Examples、模型、字体或材质；它们可能通过场景、预制件、反射、运行时加载或编辑器工作流使用。
- `Human.blend`、既有字体变体、生成网格和地形资源保留，不根据文件名判定废弃。
- 两份 AndroidManifest 内容不完全相同，涉及 SDK 与插件构建约定，本轮不合并、不删除。
- Unity/SDK 的空 XR、StreamingAssets 目录及配置保留。仓库外的本地 Backups、Builds、Library、Logs 不作删除。

## 引用检查与已知问题

整理前后核对 Assets、Packages 和 ProjectSettings：无新增无法解析的 GUID、无重复 GUID、无孤立 meta、无缺失 meta；所有保留资产 GUID 一致。两个正式场景、现有四个车辆预制件、10 个情景配置、包清单与锁文件内容均未改变。现行代码、Build Settings 和 Agent 文档已无两个删除场景的路径或路由常量引用。

静态扫描发现 **整理前已存在** 的 17 个无法在 Assets 与当前 PackageCache meta 中解析的对象引用 GUID，共 53 处、涉及 39 个文件，详见 [pre-existing-unresolved-references.json](pre-existing-unresolved-references.json)。其中包括：

- Meta 游戏场景 `Canvas_MainButton` 上一个已禁用 Image 的材质引用。
- 第三方建筑、车库、卡车材质的贴图/立方体贴图引用，OfficeBuilding 的物理材质引用，以及两个 FBX 的外部材质映射。
- `DefaultVolumeProfile.asset` 中的四个组件脚本引用。

这些是历史问题，不是本次移动造成；未知原始资产和视觉意图时不猜测替代资源，也不清空引用。静态扫描仅校验可见 GUID，不覆盖 Unity 所有内置资源、子资源 fileID 和运行时路径，不能代替图形及设备验收。

## 验证

- editmode: 18/18 通过，失败 0、跳过 0，Unity 退出码 0。结果见 [editmode-results.xml](editmode-results.xml)。
- playmode: 23/23 通过，失败 0、跳过 0，Unity 退出码 0。结果见 [playmode-results.xml](playmode-results.xml)。
- 最终展开嵌套材质目录后，顺序重新运行了两个程序集，结果为以上记录。
- 验证使用 Unity 6000.3.19f1、macOS batchmode/nographics；覆盖正式场景引用与核心对象、全部 10 个情景、事故与目标状态、回放、实际车速、重试和车辆池复用。
- 无新增编译错误；日志仍包含本机 OpenXR/OVR 设备与初始化消息。没有执行 Android 构建、Quest 真机或图形/音频验收。
- Git 差异检查通过；除列出的 5 个已有代码/配置文件外，保留的 Assets、Packages、ProjectSettings 文件内容未变。移动由 Git 识别为 356 个内容完全一致的重命名；新旧文件夹 meta 的相似度配对仅为 Git 展示方式，实际 GUID 核对见资产审计。
- 完整本机日志位于 `Logs/Tests/Structure-20260908/`，未加入版本控制。
