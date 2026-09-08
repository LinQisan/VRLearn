# 整理验证记录（2026-09-08）

- Unity：6000.3.19f1，macOS batchmode、nographics。
- EditMode：18/18 通过，失败/跳过均0，退出码0。
- PlayMode：23/23 通过，失败/跳过均0，退出码0。
- 未新增测试；运行现有两个项目测试程序集。覆盖场景配置、参数传递、事故回放/结果、实际速度取值、车辆池复用与网格复位等。
- 无 Android 构建、Quest 真机或参与者试验；无图形模式不验证路径线、后处理、音频实际输出和头显交互。
- 日志存在本机缺 OpenXR runtime/设备初始化及SDK消息；测试通过不意味着日志无错误消息，不据此推断Quest设备可用。
- 对3份脚本做修改前后token比对（忽略注释、空白和3个确认未用import）一致。测试完成后只进一步收紧删除注释留下的空白和孤立注释，再次比对有效token一致，未引入运行语句变化。
- 最终哈希核对：只改变CSVPrinter、CenterEyeCamera、ButtonTest三份脚本；归档两份历史InitTestScene及meta；Assets/Packages/ProjectSettings无其他新增、缺失或改变。正式场景、prefab、模型、情景asset、包锁完全一致。
- 本轮测试运行器生成的临时场景已自行清理，未保留在Assets。

可复查：[文件差异核对](/Users/xinyu/VRLearn/Docs/Audit-20260908/final-file-check.json)、[清理diff](/Users/xinyu/VRLearn/Docs/Audit-20260908/cleanup.diff)、[EditMode XML](/Users/xinyu/VRLearn/Docs/Audit-20260908/editmode-results.xml)、[PlayMode XML](/Users/xinyu/VRLearn/Docs/Audit-20260908/playmode-results.xml)。

复跑参数（使用项目锁定的Unity编辑器，一次运行一个测试集）：

```text
-batchmode -nographics -projectPath /Users/xinyu/VRLearn -runTests -testPlatform EditMode -assemblyNames VRLearn.EditModeTests -testResults <新的绝对XML路径> -logFile <新的绝对日志路径>
-batchmode -nographics -projectPath /Users/xinyu/VRLearn -runTests -testPlatform PlayMode -assemblyNames VRLearn.PlayModeTests -testResults <新的绝对XML路径> -logFile <新的绝对日志路径>
```

不要覆盖本轮XML作为后续版本的测试结果；新测试应单独标识版本与日期。
