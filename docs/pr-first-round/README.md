# 第一轮 P01～P05 提交与验证记录（历史档案）

> 2026-11-19：停止向上游准备或提交 AI 参与的代码 PR。以下分支、PR 状态与测试结果仅为当时记录，不代表当前维护计划。后续改动维护于个人 fork 的 `dev_01`；参见[当前维护记录](../上游PR拆分与提交建议.md)。P05 的按名称查找方案已废弃，参见[本地审查](../Boss识别本地审查.md)。

日期：2026-11-17。五组补丁均已分别提交并推送至 fork，已为每组创建 Draft PR。此前误建的五个 Bug Issue 已关闭；确认过的草稿结构现用于 PR 正文。未合并，也未标为 Ready for review；各独立分支的游戏内回归尚未完成。

| 编号 | 分支 | 提交 | Issue | PR |
|---|---|---|---|---|
| P01 | `fix/action-editor-width` | `52ffea59` | [#643](https://github.com/erdelf/AutoDuty/issues/643)（误建，已关闭） | [#644](https://github.com/erdelf/AutoDuty/pull/644)（Draft） |
| P02 | `fix/stable-path-selector` | `b131082c` | [#645](https://github.com/erdelf/AutoDuty/issues/645)（误建，已关闭） | [#646](https://github.com/erdelf/AutoDuty/pull/646)（Draft） |
| P03 | `fix/brayflox-key-interaction` | `1031bc41` | [#647](https://github.com/erdelf/AutoDuty/issues/647)（误建，已关闭） | [#648](https://github.com/erdelf/AutoDuty/pull/648)（Draft） |
| P04 | `fix/navigation-waypoint-tolerance` | `524d90ac` | [#649](https://github.com/erdelf/AutoDuty/issues/649)（误建，已关闭） | [#650](https://github.com/erdelf/AutoDuty/pull/650)（Draft） |
| P05 | `fix/boss-identification-and-completion` | `9f3f13a5` | [#651](https://github.com/erdelf/AutoDuty/issues/651)（误建，已关闭） | [#652](https://github.com/erdelf/AutoDuty/pull/652)（Draft） |

## 独立性与合并顺序

- 公共基线：上游 `master` 的 `ee4eced8`（标签 `0.0.0.399`），本轮已重新 fetch；上游近期代码 PR 目标也为 `master`。
- 每个分支仅增加一条自己的提交，没有把 `dev_01`、其它 PR 分支或完整 P06 执行器作为父分支。
- ECommons、ECommons.IPC、NightmareUI 使用上游指定的原始 gitlink；没有修改 `.gitmodules`、子模块地址或固定版本。
- 已验证全部 **120 种合并顺序**：无冲突，最终代码树一致。
- **31 种非空组合全部编译通过**：5 个单独分支分别构建，26 个含多个 PR 的子集逐组构建。任意一个 PR 都可单独合并，也可按任意顺序组合。
- P05 包含原 P19；P06（包含原 P20）未在本轮提交。没有混入传送动作、统计、编排、新版本管理或统一重试框架。

## 实际执行的验证

构建环境：Windows x64、.NET 10、Dalamud API 15；DalamudLibPath 指向本机 `XIVLauncherCN/addon/Hooks/dev/`。构建命令：

```powershell
dotnet build AutoDuty/AutoDuty.csproj -c Release -t:Rebuild -p:Platform=x64 -p:DalamudLibPath=<Dalamud Hooks dev directory>/
```

未修改的上游基线与五个独立分支完整重建均为 **70 条警告、0 错误**。规范化比较警告内容后，五个分支均无新增警告。组合构建也全部为 0 错误。此处没有把“仅编译通过”表述为“游戏内通过”。

| 编号 | 方法 | 结果与边界 |
|---|---|---|
| P01 | 原生 cimgui，提取实际 ActionEditWindow 构造与 Draw 代码，模拟游戏服务；3 种缩放 × 3 种宽度，每种运行 120 帧 | 9 个场景宽度稳定；未初始化实际 Dalamud WindowSystem，不代替游戏内打开/保存/重开验证 |
| P02 | 实际 FileHelper 源码、真实 Windows FileSystemWatcher、模拟 UI/目录容器 | 8 项通过：读取/MD5 不刷新、后台不发布、框架线程发布、连续写入合并、改名、新增、删除 |
| P03 | 链接实际 InteractionCompletion 源码 | 10 项通过：钥匙前后、错误副本/NPC、对话出现关闭、施法与每次交互状态隔离 |
| P04 | 链接实际 RouteArrival 源码 | 6 项通过：坡面、跨楼层保护、交互半径、MoveTo 精度；真实地图仍待验收 |
| P05 | 链接实际 BossIdentity、BossProgress 和 AutoDuty.BossProgress 源码，模拟对象表/服务 | 所有断言通过：未出现不算胜利、提前击杀、消失不算击杀、复活优先、遭遇隔离、目标完成度、重进重置、名称及暂停；真实 Lumina、BossMod 与任务队列时序待游戏验证 |

合并后的代码又运行了一遍 P02～P05 的四组测试，均通过。测试项目使用不同目录名，不会互相覆盖。

```powershell
dotnet run --project tests/RouteCatalog.Tests/RouteCatalog.Tests.csproj -c Release
dotnet run --project tests/Interaction.Tests/Interaction.Tests.csproj -c Release
dotnet run --project tests/RouteArrival.Tests/RouteArrival.Tests.csproj -c Release
dotnet run --project tests/BossProgress.Tests/BossProgress.Tests.csproj -c Release
```

本机原始记录位于 `artifacts/`：`build-upstream-baseline.log`、`build-p01-final.log`～`build-p05-final.log`、`build-p05-final-insertion.log`、`build-subset-*.log`、`tests-p*-*.log`、`tests-combined-*.log`、`pr-first-round-merge-matrix.json`、`pr-first-round-subset-builds.json`。P01 原生布局测试工程位于 `artifacts/p01-native-layout/`。这些构建产物不会进入上游 PR。

## 游戏内验收与测试包

本机测试包位于 `artifacts/pr-first-round/`：`P01-AutoDuty.zip`～`P05-AutoDuty.zip` 是各自独立补丁，`P01-P05-Combined-AutoDuty.zip` 是仅合并这五组补丁的测试版。包内包含 DLL 和相关资源，ZIP 完整性已检查；不会自动替换已安装插件。

这些包基于上游，**不包含现有 dev_01 的其它增强功能**。请在单独的测试插件目录解压，在 Dalamud 开发插件中指向其中的 `AutoDuty.dll`；测试时只加载一个 AutoDuty 实例。回到原使用版本时切回原 DLL 路径。

| 编号 | 游戏内待验收 |
|---|---|
| P01 | 长参数/备注，停留、缩放、手动调整、关闭重开、保存/删除 |
| P02 | 副本外打开构建、加载状态变化、长列表滚动、真实脚本增删改与导入 |
| P03 | 野营地首次拿钥匙、已持钥匙重放、普通门与 NPC、Talk/YesNo 与施法 |
| P04 | 绕柱、坡面、桥/上下层、交互终点、精确 MoveTo；重点验证 4m 高差保护 |
| P05 | 中英文客户端、无名称回退、提前击杀、多阶段、死亡重走、新副本状态清理、超时暂停及同一步恢复 |

上游 PR 保持 Draft 的原因就是这些真实游戏验证尚未完成。源码测试和编译结果已如实写入每个 PR；不把之前集成分支的游戏经历当作这五个独立分支的验证记录。

## 文档与后续

本目录的 `P01-pr.md`～`P05-pr.md` 保存当前 PR 正文。`P01-issue.md`～`P05-issue.md` 为误建 Issue 的历史记录，对应 Issue 已关闭。PR 标题采用 `[Fix] title`；正文按已确认草稿的结构说明版本、复现、范围、改动、原因、收益、测试方法、结果与缺口。原 `*-issue.md` 仅保留误发记录，不作为后续提交入口。

游戏验收结果应补到对应 PR；满足验收后再转 Ready。后续根据上游反馈分别修改相应分支，继续保持独立性，暂不提交第二轮。
