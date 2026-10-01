# 彦根・京桥路口（夢京橋キャッスルロード）实验场景

场景文件：`Assets/_Project/Scenes/Gameplay_Hikone.unity`（Build Settings 中唯一的体验场景）。

**标准场景已删除**（2026-09）：彦根场景最初复制自标准场景，实验对象（道路碰撞体、路点、触发区等）与之完全相同。标题画面没有环境选择，开始、重试和返回标题都进入彦根场景。

## 截图

用 `Tools/VRLearn/Docs/Capture Screenshots`（`Assets/_Project/Editor/DocScreenshots.cs`）从固定视点重新拍 01–07 和 10（每张只显示一个情景的对象，与体验时相同）；08、09 来自 PlayMode 测试 `AccidentExperienceTests.RecordedReplayCompletesAndRetryRetainsParticipantSettings`（不加 `-nographics` 运行时会存到 `Logs/AccidentExperience-Visuals/`，裁掉黑边后转为 JPG）。环境或菜单变化后请重拍。

| 图 | 内容 |
| --- | --- |
| [01](01_kyobashi_intersection.jpg) | 从京桥路口西南角望向彦根城 |
| [02](02_castle_road.jpg) | 夢京橋キャッスルロード（向南） |
| [03](03_aerial.jpg) | 航拍：城下町、中堀、京桥和彦根城 |
| [04](04_bridge.jpg) | 京桥上望向枡形 |
| [05](05_route25_west_gate.jpg) | 县道 25 号向西，尽头是櫓门（遮挡车辆生成点） |
| [06](06_junctions_from_above.jpg) | 俯视：本町 T 字路口（左）和京桥路口（右） |
| [07](07_title_menu.jpg) | 标题菜单（一页） |
| [08](08_accident_replay.jpg) | 事故回放（三视角） |
| [09](09_feedback.jpg) | 反馈页 |
| [10](10_parked_trucks.jpg) | 情景 03 的停放卡车（日式厢式货车） |

标题菜单的设计见 [DESIGN.md](../../DESIGN.md) §6，生成方法见 AGENTS.md「Change the title menu」。

## 与真实地图的对应

参照 Google 地图和街景：县道 25 号（中堀通り）沿中堀南岸延伸，夢京橋キャッスルロード从南侧接入，京桥从北侧跨过中堀，到达京桥口御门遗址（枡形石垣，道路在此转弯）。映射到原有道路网：

| 真实 | 场景（Unity 世界坐标） |
| --- | --- |
| 县道 25 号（中堀通り） | 东西主路 z 16–36，北侧直接临护城河 |
| 京桥口十字路口 | 主路口中心 (32, 26) |
| 京桥 | 北口 x 25–39，z 39.6–52 |
| 京桥口枡形 | 桥北的石垣围合区；正面是高石垣（z 76），道路在此右转，尽头有城墙 |
| 夢京橋キャッスルロード | 南口向南延伸至 z −150。两侧是町家和榉树行道树，设蓝色方向指示牌，限速 30 |
| 本町通（江户町家街） | 与 Castle Road 在 (32, −6) 形成 T 字路口，向西延伸 |
| 本町 T 字路口 | x 0–20，z 16–36，在京桥路口西侧 2 m 处。它朝东的一侧不画斑马线：那条街已由京桥路口西侧的斑马线横穿，两条并排是错误的（`road_cross(east_crosswalk=False)`） |
| 路口西侧停车场「P」 | x −80…−60（场景 6/7 车辆由此驶出） |
| 中堀 | 南岸为低石垣加石柱锁链栏；北岸为高石垣，上方是松、樱和阔叶林 |
| 彦根城 | 本丸平台中心 (−40, 205)，高 50 m，位于京桥的西北方向，从路口可以看到 |

## 实验逻辑保持不变

- 玩家、相机、WayPoint、10 个 Scenario、危险事件、信号灯、InvisibleWall、数据记录与 UI 的对象和坐标均未修改。
- 原道路瓦片只关闭了 Renderer；碰撞体继续用于车辆地面检测和玩家落地判定。路灯位置与原灯杆一致，桥上的路灯落在桥面人行道上。
- **路面高度与碰撞体一致**：原碰撞体的车道在 y 0.01，人行道在 y 0.41（路缘高 40 cm，路口转角为坡道）。新的沥青和标线放在 y 0.01。人行道、路缘和转角坡道直接取自原道路网格（`Art/Hikone/road_tiles_geometry.json` → `HK_RoadRaised`），只替换为彦根的石材，因此车轮和脚下都与碰撞面完全重合。车辆模型本身无需修改：轮胎底部就是碰撞体底部。
- **黑墙（BlackWallContainer）**：没有任何代码引用，也没有碰撞体。它们是遮挡车辆生成点和消失点的黑色面片。直接删除会让车辆凭空出现或消失，影响实验，因此改为嵌入建筑：
  - `BlackWall`（x −103）原样保留，现在位于县道 25 号西端的櫓门之内，看起来是城门通道的暗处，车辆从门洞驶出。
  - `BlackWall (1)` 缩小并移到南侧小街的長屋門通道内（位置 (10, 2.4, 6.63)，缩放 9.4×5）。
  - `BlackWall (2)` 已停用，由本町通北侧的土塀和路口西南角的土蔵代替。
- 核查结果：在所有场景中，车辆生成点和消失点的遮挡程度均不低于原场景（`HikoneSceneTests` 和下方的视线核查）。
- **停放的卡车**（情景 03/04/06/09 的遮挡物）：场景生成器把原来的欧洲式半挂车外观换成日式厢式货车 `HK_Truck_Large`（10 吨级，11.8 m）和 `HK_Truck_Medium`（4 吨级，8.6 m），高度都是 3.62 m，放在原外观的位置上。原模型的碰撞体和 `TruckCollider` 不变。
- **隐形辅助物体**（`Material_Invisible`、`ColliderOnlyMaterial`）改为真正不可见、不投影。
- **护城河岸边**：地形在石垣墙线外 5 cm 就降到水底（`hk_terrain.WALL_GAP`），石垣前面不再露出草台和土壁；原道路网格中跨过岸线的人行道三角面在岸线处切掉（`road_raised(clip_moat=True)`）。
- **行道树**：榉树按修剪后的大小（约 8.5 m 高、5.5 m 宽）。布局生成时，树干离路灯杆、标志杆和信号灯至少 1.5 m，灯笼不在树冠里，树冠不压到房屋和土塀；行道树沿树列挪到空位，散种的树有冲突则删除（`hk_layout.clear_town_trees`，校验中也会检查）。

## 危险事件视线

当前的核查结果和方法见 [DESIGN.md](../../DESIGN.md) §4（`Tools/VRLearn/Hikone/3. Check Occlusion`）。以下是最初换成彦根场景时的记录（各 Scenario 自身事故车路线，35 m 内可见率，原场景 → 新场景）：

S1 90→96%，S2 83→89%，S3 100→96%，S4 82→97%，S5 100→98%，S6/7 97→99%，S8 90→92%，S9 100→100%。
S4 的事故车从桥上驶来。实际的京桥路口北侧就是开阔的护城河，所以这一项的可见率明显提高，这是按真实地形还原的结果。S2/S3/S5/S8 的卡车遮挡保持不变。

## 数据记录

`HumanData_*.csv` 末尾新增一列 `Scene`（当前固定为 `Gameplay_Hikone`；旧数据中可能是 `TraficAcident_Hikone_Meta` 或 `TraficAcident_Meta`），用于区分环境。原有列的顺序不变。

## 重新生成

```bash
python3 Art/Hikone/hk_layout.py      # 布局 + 冲突校验
```

在 Blender 中执行：`hk_textures.run(); hk_assets.build_all()`（BlenderMCP 或 headless 均可）。只改了一个模型时，可以只导出那一个（`hk_lib.export(hk_assets.<函数>(...), -1)`），避免所有 FBX 因时间戳而产生差异。然后在 Unity 菜单中依次执行：

0. `Tools/VRLearn/Hikone/0. Export Constraints From Scene`：仅在场景的道路、路点或触发区有变化时执行。它从彦根场景重新导出 `Art/Hikone/scene_constraints.json` 和 `road_tiles_geometry.json`，结果与现有文件一致即说明这些实验对象没有变化。
1. `Tools/VRLearn/Hikone/1. Import Models, Materials & Prefabs`
2. `Tools/VRLearn/Hikone/2. Build Hikone Scene`（同时处理黑墙）
3. `Tools/VRLearn/Rebuild Title Menu (VR tiles)`（标题菜单）

设计意图与决策记录见根目录的 [DESIGN.md](../../DESIGN.md)。

## 性能

新环境约 19.2 万三角面，1272 个 Renderer（全部 Static），共享 34 个 URP/Lit 材质（已开启 GPU Instancing），贴图 512px。原场景环境约 5.3 万面、71 个材质。
