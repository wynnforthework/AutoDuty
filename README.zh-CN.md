# AutoDuty — 个人维护版

[English](README.md) · [简体中文](README.zh-CN.md)

<img align="right" width="150" height="150" src="logo.png">

AutoDuty 是用于编排和执行《最终幻想14》副本路线的 Dalamud 插件，支持亲信、任务辅助器、冒险者分队等重复副本流程。本仓库基于 [erdelf/AutoDuty](https://github.com/erdelf/AutoDuty)，在 **`dev_01`** 分支维护包含 AI 辅助修改的实验版本，独立维护，不是上游正式版。

当前测试版本：**0.0.0.399.2**，目标 Dalamud API 15。最近代码 Release 编译零错误、33 组离线测试通过，**尚未进行本版本游戏实测**。这些结果不代表所有副本、区域转换或 16～100 级全程无人干预练级已经验证成功。

## 本维护版的主要功能

| 范围 | 行为 |
| --- | --- |
| 路线编辑 | 同副本多个版本、原始脚本保护、创建可编辑副本、按职能匹配路线、步骤高亮、编辑窗口删除和保存修改。 |
| 执行控制 | 暂停/恢复、重试、跳过、从指定步骤执行；独立步骤卡片显示详情、快捷操作和进度。手动跳步可能使后续步骤缺少前置条件。 |
| 录制与共享 | 录制移动及支持的事件，其余动作手动补充；通过导入/导出共享配置。录制无法自动识别全部游戏机制。 |
| 练级编排 | 指定使用/跳过副本、选择职能路线、查看已编排副本，并在升级后重新选择符合条件的副本。 |
| 数据概览 | 本次挂机时间、副本次数、经验与升级数；路线完成度显示历史最高值而非平均值。完整且无人工干预的通关计为 100%，自动重试后恢复本身不算人工干预。 |
| 运行可靠性 | 游戏就绪检查、挂起动作计时、导航诊断、会话生命周期修正，以及按当前步骤分配导航与战斗控制权。 |

## 最近的可靠性修改

区域转换分为“疑似发生转换、过程结束、确认目的区域、确认当前步骤完成”四个阶段。脚本可配置目的地图、水平范围和高度范围；附近地面与异步限频的 vnavmesh 路径查询提供辅助证据。未知位移先停止旧导航，缺少完成证据时暂停。旧路线可能需要显式补充目的区域配置，并非所有脚本都会自动修好。

战斗控制以当前步骤为准：MoveTo 关闭托管的 BossMod 移动与自动输出；StopForCombat false 关闭 BossMod 移动、开启输出；true 停止路线导航，开启战斗移动与输出。

Boss 发现使用 NPC Rank、BossMod 模块及已跟踪对象身份，已移除此前补丁引入的脚本 Note/名称查找链。遭遇完成需要证据，不能仅凭对象消失或脱战判定击杀。缺少有效识别证据的 Boss 仍有适配限制。

详见[改动分类与维护记录](docs/上游PR拆分与提交建议.md)、[区域转换判定与录制](docs/区域转换判定与录制.md)、[Boss 识别本地审查](docs/Boss识别本地审查.md)、[功能使用与验收](docs/挂机功能使用与验收.md)。

[上游 Issue #654](https://github.com/erdelf/AutoDuty/issues/654) 中提出的官方脚本库下载进度、清空再下载方案，**当前维护版尚未作为完整功能实现**。

## 编译与加载本维护版

版本格式为“上游四段版本号.本地修订”，当前 `0.0.0.399.2`，本地更新仅递增第五段。.NET/Dalamud 的版本字段最多支持四段，因此 AssemblyVersion 与 manifest 保持 `0.0.0.399`，完整五段版本写入 DLL ProductVersion，并显示于插件窗口和诊断中。

上游插件源安装的是上游版本。测试本维护版请编译 `dev_01` 源码，通过 Dalamud 开发插件加载。

```powershell
git clone --recurse-submodules --branch dev_01 https://github.com/wynnforthework/AutoDuty.git
cd AutoDuty
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -PlanOnly
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

当前工作流使用 Windows x64、.NET SDK 10、Dalamud SDK/API 15。先阅读[开发 skill](skills/dalamud-plugin-dev/SKILL.md)，按自己电脑修改[本机配置](skills/dalamud-plugin-dev/references/local-environment.json)中的 Dalamud 依赖和部署路径。构建入口优先使用 `%USERPROFILE%/.codex/skills/dalamud-plugin-dev` 中已安装的 skill，缺失时使用仓库副本。`-PlanOnly` 检查计划，`-BuildOnly` 只编译不部署。

维护者本机固定热加载入口为 `D:/UGit/AutoDuty/artifacts/AutoDuty-session-stop-fix/AutoDuty.dll`。在 Dalamud 开发插件路径中添加配置好的 DLL 一次，后续构建覆盖同一位置，不再按版本号或提交创建新加载入口。使用 `/ad` 打开设置。不要同时加载上游 AutoDuty 与本维护版。

## 配套插件

- [vnavmesh](https://github.com/awgil/ffxiv_navmesh)：路线导航与网格查询。
- [BossMod](https://github.com/awgil/ffxiv_bossmod) 或 BossModReborn：战斗机制与战斗移动。
- [Rotation Solver Reborn](https://github.com/FFXIV-CombatReborn/RotationSolverReborn) 或 [Wrath Combo](https://github.com/PunishXIV/WrathCombo)：自动输出，实际可用性与兼容性取决于客户端和插件版本。
- 可选集成包括 Gearsetter、AutoRetainer；保留原有修理、战利品处理及副本间自动化等功能。

## 问题反馈与贡献规则

本维护版的问题请提交至[本仓库](https://github.com/wynnforthework/AutoDuty/issues)，附上插件版本、客户端版本与国服/国际服、副本、脚本文件名/版本、**从 0 开始**的步骤序号、相关日志，以及手动重试或干预后是否恢复。不要要求上游维护者支持本 fork 的修改。

本仓库包含 AI 辅助代码。根据上游不接受 AI 代码的反馈及仓库所有者的明确要求，停止为 `erdelf/AutoDuty` 准备和提交 AI 参与的代码 PR，包括从个人 fork 创建 PR。本地推送钩子会拦截直接推送上游，但不能阻止网页/API 创建 PR，仍须遵守 [AGENTS.md](AGENTS.md)。个人仓库继续维护；历史 PR 拆分材料仅作档案，不是待提交计划。

## 来源与版权

项目来源为 [erdelf/AutoDuty](https://github.com/erdelf/AutoDuty)，保留原作者归属与声明。本维护版不为原项目代码授予新许可。

原项目未提供授权许可，适用[默认版权规则](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository#choosing-the-right-license)。
