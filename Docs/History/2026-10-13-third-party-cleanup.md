# 第三方依赖清理

2026-10-13，按所有者要求检查并删除未使用的第三方依赖。以路灯、车辆移植后的工作区为基线。

## 已删除

| 依赖 | 依据 |
| --- | --- |
| `com.unity.ai.navigation` 2.0.13 | 当前资产没有引用包内 GUID，代码没有使用 `Unity.AI.Navigation` 或 NavMeshSurface/Link/Modifier；锁文件没有其他包依赖它。旧清理记录提到的导航场景已不在项目中。 |
| `com.unity.multiplayer.center` 1.0.1 | 没有多人游戏功能、代码或包内资产引用；锁文件没有其他包依赖它。 |
| `com.unity.timeline` 1.8.12 | 没有 Timeline 资产、类型或包内 GUID 引用；锁文件没有其他包依赖它。RootMotion 的 PlayableDirector 使用属于引擎的 Director 模块，该模块保留。 |
| `com.unity.visualscripting` 1.9.11 | 没有可视化脚本图、对应类型或包内 GUID 引用；锁文件没有其他包依赖它。 |
| `Assets/ThirdParty/SingleDetailedTruck/Scripts/CameraFreeflight.cs` | 素材附带的自由相机示例；整个 Assets、ProjectSettings 和源码没有脚本 GUID、类型或字符串加载引用。脚本、`.meta` 及空 Scripts 目录的 `.meta` 一起删除。 |

由 Unity 重新解析 `packages-lock.json`，只减少上述四个包，没有新增包或改变其余包的版本。它们清理前的本地包缓存合计约 73 MiB；这不是 APK 大小的减少量，也不代表 Git 仓库减少了同等体积。

## 保留

- **Final IK / RootMotion**：GameDirector、HumanController、AvatarPresenter 等仍调用 VRIK，场景还引用 FullBodyBipedIK 和 GrounderFBBIK。保留完整插件及编辑器工具，不逐个裁掉依赖报告中未被场景直接引用的类。
- **DOTween**：交通灯、车辆与事故表现仍有直接调用，保留插件和模块。
- **所有第三方模型包及贴图**：Unity 依赖报告确认 Azerilo、Basic Motions、ModularLowpolyStreetsFree、SingleDetailedTruck（示例脚本除外）、SkySeriesFreebie、Free Bike Pack 的资产均可从构建场景、Resources 或设置到达。已隐藏的原道路、灯杆和车辆仍参与碰撞或外观重建，不能按渲染器关闭来删除。
- **MetaInteraction 的 shader include 与许可证**：RoundedBoxUI.shader 用相对路径包含 Box2DSignedDistance.cginc，Unity 的序列化依赖报告没有列出这种文本依赖。
- **AndroidManifest.xml、XR/URP/Input System、测试和 IDE 工具**：分别用于 Quest 构建、运行和开发。RootMotion 还使用引擎的 AI / Director API，保留对应内置模块。

## 核查方式

运行 `UnusedAssetReport.Run`，再独立检查 Assets、ProjectSettings、Art 中的 GUID、代码与路径依赖，以及包锁文件的反向依赖。报告菜单仍保留，并增加说明：不可直接将“unused”列表作为删除列表；代码、shader include、Android 清单、许可证和编辑器生成器需要另行检查。

本地证据保存在忽略目录 `Logs/thirdparty-*`，包括清理前依赖报告、静态引用清单、原锁文件及被删除示例脚本的恢复副本。

## 验证结果

- Unity 重新解析与编译成功，清理后依赖报告的根数和可达资产数保持不变；删除脚本的 GUID、类型与路径没有残留引用。
- EditMode **50/50**、PlayMode **44/44**、ScenarioModel **8/8** 全部通过。Unity 测试结果在 `Logs/Tests/thirdparty-cleanup/`。
- 四个包的本项目 PackageCache 目录已由 Unity 自动移除；其余锁文件依赖条目逐项相同。
- 本轮清理未改动场景或模型，无需重新进行遮挡比较；未打包 APK，未验证 Quest 真机。
