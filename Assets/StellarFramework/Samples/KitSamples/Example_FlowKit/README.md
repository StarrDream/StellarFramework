# FlowKit Sample

FlowKit Sample 分成两个层级：

- `FlowKitSample.cs + FlowKitSample.json`：最小 API 示例，只演示 `Entry -> Delay -> Complete`。
- `FireDrillWorkflow4P.flow.json + FlowKit_Playable.unity`：正式教学示例，演示 4 人消防演练、双向通讯、并行、汇合、失败分支和超时。

## 推荐从这里开始

打开：

`Assets/StellarFramework/Samples/KitSamples/Scenes/FlowKit_Playable.unity`

进入 Play Mode 后，场景会自动加载 `FireDrillWorkflow4P.flow.json`。界面可手动模拟 4 名玩家就绪、任务完成、安全复核，也可勾选某个 `Operation.failed` 观察失败路径。

Graph Editor 可通过：

`StellarFramework -> FlowKit -> 示例 -> 消防演练流程（4人）`

直接打开并 Frame All。
## 双向通讯约定

| 方向 | FlowKit 机制 | 含义 |
| :--- | :--- | :--- |
| FlowKit -> Gameplay | `Operation` | 下发“现在该做什么”的命令 |
| Gameplay -> FlowKit | `Signal` | 上报一次性事件，例如“玩家已就绪” |
| Gameplay -> FlowKit | `State` | 上报当前事实，例如“灭火已完成=true” |
| Gameplay -> FlowKit | `OperationResult` | 回报命令是否成功启动/执行 |

Sample 的关键链路：

`Operation.start -> Gameplay 开启任务 -> Signal/State 回报 -> FlowKit 推进 -> 下一 Operation.start`

FlowKit 不实现灭火、疏散、抓取、VR 输入或网络同步；这些都属于外部玩法系统。Sample 中的按钮只是用来模拟这些外部系统回报事实。

## 失败路径

`flow.operation` 有 `成功 / 失败 / 已取消` 三个显式输出。消防演练示例将关键失败分支接到角色失败处理，再进入 `flow.fail`，因此最终状态是 `FlowRunStatus.Failed`，不会把业务失败伪装成成功完成。

完整说明见：`FireDrillWorkflow4P-Guide.md`。
