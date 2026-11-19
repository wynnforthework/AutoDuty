# 万象森国第 2 步移动控制审查（0.0.0.399.9）

## 实机证据与判断边界

用户报告前方无墙或障碍、角色原地不走。`.399.8` 的 `navigation-20261119-173032-324.jsonl` 显示：第 2 步是前往 `(-330.68, 41, -146.37)` 后等待 10000ms 的 `Wait`，不是区域转换步骤。寻路请求成功后曾正常行走，接战后在 `(-337.5, 41.2, -116.7)` 附近停滞，后续多次重寻路仍无位移。日志中的水平距离使用 X/Z，约 30.4m；X/Y 两项接近不代表已到目标。

`finding=false, running=true` 表示路径计算结束、跟随器仍在运行；`movementAllowed=true` 只说明 vnavmesh 自身允许移动。这些字段不能证明游戏接收到了有效移动输入，也不能证明前方有墙。停滞时采样的 `casting=false`，不能据此确定是读条锁移动。此次未出现区域转换证据，不套用区域转换完成规则。

## 本次修改

- 原先只有 `StopForCombat` 接近目标时请求 BossMod `Cast=DropInstants`。现在所有仍启用输出的坐标接近阶段使用同一策略，包括 `Wait`、`WaitFor`、交互；进入动作或战斗阶段后恢复原策略。`MoveTo` 仍关闭输出。没有把等待动作变成“遇战即停”，也没有延长卡住超时或自动跳步。
- 增加只读移动控制诊断：同时记录已加载 BossMod/BossModReborn 的输入阻止状态、用户/实际输入、控制器期望方向、即时读条状态和移动提示，以及角色职业、是否正在移动。仅作为排障证据，不参与完成判定。
- 诊断通过可选反射读取已知内部成员，版本变化或不可用时返回 `null`/`available=false`；不修改其它插件字段、不缓存热加载实例、不扫描内存。它不是稳定 IPC，缺失数据不能解释为“没有阻止移动”。

## 接口核对

本机 `BossModReborn/7.5.8.6/BossModReborn.dll` 的 PE 元数据已核对：`BossMod.Plugin` 的 `_movementOverride`、`_amex`、`_hints`；`MovementOverride.MovementBlocked`、`UserMove`、`ActualMove`、`DesiredDirection`；`ActionManagerEx.PreventMovingWhileCasting`、`MoveMightInterruptCast`、`CastTimeRemaining`；`AIHints` 的 `ForcedMovement`、`HoldCasts`、`MaxCastTime`、`ForceCancelCastOther/Mechanic`。证据为本机 `artifacts/bmr-metadata.log`。

实现参考：[BossModReborn 源码 8d5ff8e](https://github.com/FFXIV-CombatReborn/BossModReborn/tree/8d5ff8e2195ebbd519334852eab478414cdea67b)。其中读条锁移动会考虑即将开始的读条，且新实现会在 RSR 自动输出时让 RSR 管理；所以不能只凭 AutoDuty 启用了配置就认定 BossMod 正在锁移动。RSR 本地 `RotationSolver.json` 的 `PoslockCasting.Value=false`，本次不改用户配置或 RSR。

## 验证与待实测

Release 编译零错误，36 组离线测试通过。补充坐标 Wait 等动作的接近/动作/暂停/停止阶段回归，确保限制仅作用于接近过程。

**已修复策略遗漏，但尚未实机确认本次停滞的根因或消失。** 重新运行该路线：第 2 步应继续前往目标，抵达后才开始等待。若再次停住，自动保存的 JSONL 将包含 `movementControllers`：检查输入是否被阻止、期望输入与实际输入是否一致，再决定修复输入控制还是导航跟随。单次采样不能代替完整过程证据。
