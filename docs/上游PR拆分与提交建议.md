# AutoDuty 上游 PR 拆分与提交建议（待选择）

日期：2026-11-17；本次更新覆盖到 `1bcd1482`。本文是提交范围的提案，尚未创建上游 PR 功能分支、推送或打开 Issue/PR。P01 已有本地 Issue 草稿，仍待选择与审核。

## 1. 核对范围与结论

- 上游：`https://github.com/erdelf/AutoDuty`；个人 fork：`https://github.com/wynnforthework/AutoDuty`。
- 初版整理时执行过 `git fetch upstream --prune`；本次更新没有重新获取远端。比较本地已获取的 `upstream/master = ee4eced8` 与集成快照 `dev_01 = 1bcd1482`，不声称这是远端当前最新状态。
- 快照比基线多 **51 个提交**，其中 49 个为原始改造/使用文档提交，另 2 个为 PR 拆分建议和 P01 Issue 草稿。完整差异为 **90 个文件，7438 行新增、409 行删除**；排除这两份 PR 筹备文档后为 **88 个文件，6982 行新增、409 行删除**。数字以 `1bcd1482` 为固定快照，不包括本次文档更新提交。
- `.gitmodules` 改过 ECommons 来源；副本 `Paths` 和 `Resources/md5s.json` 没有净改动。本轮运行问题修复主要在执行器，不能包装成“更新副本脚本”PR。
- `upstream/dev = aedf055e`，目前包含在 `master` 中。上游近期有从 `dev` 合并至 `master` 的记录；实际发 PR 前仍需确认目标分支约定，不能仅凭默认分支断定。
- `feat/mathematics-ore-shopping`、`feat/trials-roulette-loop` 是另外两条功能分支，不属于本次 `dev_01` 差异，不混入。

建议按**最终行为**拆分，不按开发时间线提交。小修复优先，大功能后置；补丁中只带对应英文说明、相关翻译和必要测试，不带整份中文开发流水账、测试 ZIP 或个人配置。

## 2. 可选择的 PR 清单

“优先”表示建议先准备，**不代表已经获得你的发布选择**。“拆补丁”表示原提交不能原样照搬。各组内部仍可能需要再拆成几个小提交。

| 编号 | 独立 PR 主题 | 建议 | 上游依赖 | 整理方式 |
|---|---|---|---|---|
| P01 | 步骤编辑窗口不再不断变宽 | 优先 | 无产品功能依赖 | 以 `00b99e12` 为源，剥离新编辑器上下文 |
| P02 | 角色就绪变化时保持路径选择行稳定 | 优先 | 无产品功能依赖 | 提取 `a52563c4`，补入 `12a64429` 相关界面片段 |
| P03 | 野营地钥匙交互结束、对话结束证据不被重复消费 | 优先 | 无执行器改造依赖 | 以 `fc7f15c4` 为源；单独提取测试 |
| P04 | 路径拐点容差与终点到达范围分离 | 优先 | 无新窗口或统计依赖；需适配原执行器 | 仅取 `ac4095ba` 容差部分，结合到达判定片段 |
| P05 | 通过完整名称识别 Boss，支持英文脚本与中文对象名 | 优先 | 无统计依赖 | 拆取 `67fdd313` 的名称识别部分 |
| P06 | 每步有限重试、暂停恢复、导航就绪等待与实际位移监测 | 重点，但后于小修复 | P04 可先合并 | 多提交整合，作为执行器基础 PR |
| P07 | 相邻战斗检查点提前接战后正常推进 | 重点 | 建议在 P06 后 | 整合 `490b16b9`、`6ff69f47` 战斗部分 |
| P08 | 交互切区生命周期与确认返回入口后的恢复 | 重点，需游戏验证 | P06；建议 P03 已合并 | 合并切区相关片段，避免与 P06 相互依赖成环 |
| P09 | 可导出的有界导航诊断 | 建议 | P06；P08 信息为可选增量 | 拆出日志与导出，不携带统计和新动作 |
| P10 | 向后兼容的路线 JSON 及参数验证 | 建议作为功能基础 | 无新 UI 依赖 | `7a989e5e` + `54d5efd2` 的格式/验证部分 |
| P11 | 路线编辑、可靠保存、草稿版本、原始脚本只读 | 建议，范围较大 | P10；热编辑恢复部分依赖 P06 | 多提交整合，可再拆“磁盘保存”和“热编辑/版本” |
| P12 | 按职业/职能分配同副本不同脚本 | 建议 | P10；复制/草稿 UI 依赖 P11 | 从旧练级/编辑提交中提取角色匹配逻辑 |
| P13 | 可见练级编排、使用/跳过、排队前按新等级重选 | 建议 | P12；默认不要求统计模块 | 基础编排独立发，统计栏后续追加 |
| P14 | 手动路线录制与动作补录 | 可选功能 | P10、P11 | 自动识别范围和手工补录能力须写清楚 |
| P15 | 压缩路线分享和导入预览 | 可选功能 | P10、P11 | 独立于录制；不与性能报告分享混在一起 |
| P16 | 会话经验统计、最高完成度和运行报告 | 可选大功能 | P06、P10；编排展示扩展依赖 P13 | 必须包含最终“自动重试不算干预”的修正 |
| P17 | 独立执行步骤卡片窗口 | 可选 UI 功能 | P06；编辑入口依赖 P11 | 独立窗口、选项和本地化一组，不捆绑传送 |
| P18 | 每次插件会话首次进入副本时重建网格 | 待讨论默认策略 | 可独立；与 P06 就绪等待衔接 | 从 `048fe4ed` 提取，建议默认关闭供上游评估 |
| P19 | 通用 Boss 完成证据、提前击杀与入口重走保留击败状态 | 重点，需游戏验证 | P05、P06；与 P08 联合验收 | 拆取 `60f6d5ca`，P09 诊断钩子可后接 |
| P20 | 未 Stop 退本后的执行状态清理与新轮重置 | 优先准备，接执行器基础后 | P06；P08/P19 的状态清理按已合并功能接入 | 拆取 `1bcd1482`，不能为清理钩子捆绑其它功能 |
| H01 | 新增 TeleportTo 动作、步骤菜单直接传送 | 建议先留 fork | P06/P11 的部分接口 | 与所有稳定性 PR 分离，需上游明确接受此功能范围 |
| H02 | UI、命令和日志全部统一零基编号 | 待讨论兼容性 | 涉及各窗口、命令与日志 | 独立决定；不能借 P04 混入命令语义变更 |
| H03 | ECommons 子模块 URL、中文计划与验收流水账 | 本地保留；依赖修复可另议 | 无 | 仅有可复现的干净克隆失败时单独提依赖修复 |

建议第一轮选择 **P01～P05**；第二轮以 **P06** 为基础，准备 **P07～P09、P20、P19**。P20 属于执行生命周期修复，P19 属于 Boss 完成判断，两者独立发 PR；P19 与 P08 必须联合验收死亡回入口场景。P10～P18 根据上游对功能方向的反馈逐组提交，而不是同时发十几个互相依赖的 PR。既有编号保持不变，新增修复续编为 P19/P20。

## 3. 每组最终范围与英文 PR 草案

下列标题/正文可作为上游 PR 初稿。Validation 是**提交前必须补齐的条件**，不是宣称拆分后的分支已经验证通过。

### P01：稳定编辑窗口宽度

分支建议：`fix/action-editor-width`。主要文件：`Windows/BuildTab.cs`。

只处理自动尺寸与内容宽度相互放大，打开时合理初始化尺寸，允许手动调整和内容滚动。不带新步骤菜单、版本选择和自动保存。

对应 Issue 草稿见 [P01-Issue草稿.md](P01-Issue草稿.md)。后续每个 Issue 按 `[type] title` 命名，保留上游模板字段，并说明受影响版本/源码基线、修改范围、实际修改、原因和收益、已执行测试的方法与结果、尚未验证的项目。不能将上游基线版本写成已复现版本，不能将集成分支构建结果写成独立 PR 分支验证结果。

**Title:** Fix action editor width growth while open

**Body:** The action editor can grow wider each frame because its content width feeds back into automatic window sizing. Initialize a bounded window size when opened and let long content scroll while preserving manual resizing.

**Validation:** 在上游原编辑窗口中打开长参数步骤，停留、缩放、关闭重开；至少确认大字号与缩放下不会重新增宽。原集成分支仅编译验证，不能以此代替独立分支验收。

### P02：稳定主页路径行与目录缓存

分支建议：`fix/stable-path-selector`。文件：`Windows/MainTab.cs`，必要时 `Updater/FileHelper.cs`。

角色短暂不可操作时保留路径行并禁用操作；不要每帧重载路径目录导致选项列表和滚动位置重置。`12a64429` 的“排队前重选副本”归 P13，不放这里。

**Title:** Keep route selection stable across player readiness changes

**Body:** Keep the route selector visible while the player is temporarily unavailable and disable actions until they become valid. Avoid replacing unchanged route lists during browsing so that the layout and scroll position stay stable.

**Validation:** 副本外打开构建窗口、进出加载与长列表滚动；确认目录变更仍能显式刷新。

### P03：结束钥匙交互

分支建议：`fix/brayflox-key-interaction`。文件：`Helpers/InteractionCompletion.cs`、`Managers/ActionsManager.cs`。

限定 territory `1041` / NPC `1004346` / key `2000521` 的持有证据；每次交互独立保留对话出现/关闭状态，避免共用边沿被重复消费。普通交互、门和其它副本不借用钥匙证据。

**Title:** Finish Brayflox key interactions after receiving the key

**Body:** The key NPC remains targetable after handing over the key, which can keep the interaction loop running. Recognize the duty-specific key reward and retain dialogue completion evidence for the current interaction without consuming shared UI edges twice.

**Validation:** 钥匙前/后、重新执行、其它 NPC/门、Talk/YesNo 关闭及施法等待；现有模型测试覆盖这些条件，仍需游戏内拿钥匙复现。

### P04：绕障容差与地面到达

分支建议：`fix/navigation-waypoint-tolerance`。主要文件：`AutoDuty.cs`，可提取纯判定 helper。

途中 vnavmesh `Path.Tolerance` 固定为 `0.25m`，因为其作用于每个路径拐点，不能传交互终点 `2m` 范围。Interactable 的地面接近使用水平距离与高差保护；MoveTo 的精确到达不放宽。若上游尚无相关到达改动，先核对原实现，避免创造无意义的改动。可将地面到达单独拆为 P04b。

**Title:** Separate waypoint tolerance from interaction arrival range

**Body:** vnavmesh applies Path.Tolerance to intermediate corners as well as the destination. Keep a tight waypoint tolerance and evaluate interaction arrival separately so that a larger interaction radius does not discard obstacle corners early.

**Validation:** 普通 MoveTo、绕柱、坡地交互、跨楼层拒绝；上游 FollowPath 源码支持容差语义，但没有真实失败轨迹证明每次撞墙都由此导致，不在 PR 中作这种保证。`ac4095ba` 中编号、统计等片段全部排除。

### P05：Boss 身份匹配

分支建议：`fix/localized-boss-identification`。文件：`Helpers/BossIdentity.cs`、`Helpers/ObjectHelper.cs`、`Managers/ActionsManager.cs`、`AutoDuty.cs` 相关调用。

本组只提取 `67fdd313` 的完整路线备注名与本地名称/英文 BNpcName 匹配，保留该阶段的等级标记/BossMod 兜底。只返回附近、存活、可选中的适当对象；不因“已在战斗”就猜测首领成功。当前集成版本后续在 P19 中进一步限制已知名称的兜底、加入死亡与副本目标证据；这些不是 P05 的隐含范围。

**Title:** Recognize named bosses across client languages

**Body:** A route naming a boss in English can fail to identify that boss on another client language when rank or module detection does not match. Add exact name matching against the localized name and the English BNpcName entry while keeping the existing fallback detection.

**Validation:** 完整英文/中文名、空名、部分名称、小怪拒绝，以及樵鸣洞目标首领；识别调用应在接近/动作/遇战阶段一致。诊断日志依赖 P09 时后加，不为日志拖入整套执行器。

### P06：执行器失败与就绪管理

分支建议：`feat/bounded-step-recovery`。文件：`AutoDuty.Execution.cs`、`AutoDuty.cs`、attempt/pause/readiness/progress helpers、配置与最小控制 UI。

最终行为：每步独立有限重试；耗尽后暂停原步骤；Pause/Resume 不重复启动仍在队列中的动作；停止旧导航、保留步骤前置条件。未就绪、开场封锁、网格未完成时不计入移动卡住；卡住依据实际位移，最低有效窗口 `5s`；计算路径/施法与实际堵墙分开。保留移动/动作总超时约束。

**Title:** Add bounded step recovery without silently skipping failed movement

**Body:** Track navigation and action attempts explicitly, retry failed work within the same step, and pause when recovery is exhausted. Wait for player, duty, and mesh readiness before navigation, and measure actual movement progress rather than treating path state changes as movement.

**Validation:** 路径拒绝、无路径、真实卡住、慢加载、施法、战斗中断、暂停恢复、重试次数、旧配置迁移。最小 UI 和本地化必需，统计 hooks、录制、传送、卡片窗口不属于本 PR。原先跨功能测试入口要拆成仅依赖本 PR 的测试。

### P07：相邻战斗检查点

分支建议：`fix/early-combat-checkpoints`。文件：`CombatCheckpointProgress.cs`、执行器相关调用。

前一步已在附近观察到该段战斗并结束时，后续 StopForCombat 正常应用开关，不要求重新等一场不存在的战斗；不能因区域暂时没有怪直接跳过。保留新波次、Boss、远处检查点和死亡重置的边界。

**Title:** Handle combat completed before an adjacent checkpoint

**Body:** A nearby encounter may start before the character reaches its StopForCombat checkpoint. Retain scoped evidence of that encounter so that the checkpoint can apply its switch after combat ends instead of waiting for another fight.

**Validation:** 相邻提前接战、无战斗证据、新波次、坡地、远点/Boss 不复用、死亡重置。

### P08：切区与入口恢复

分支建议：`fix/interaction-transfer-lifecycle`。文件：`InteractionTransitionProgress.cs`、`AutoDuty.Recovery.cs`、`EntranceRecovery.cs`、动作与条件处理。

切区依赖实际交互证据；开始加载立即停止旧导航，框架帧清理旧任务；加载结束、角色稳定且位置变化后推进一次。流沙对象不能因消失就提前结束。耗时有界，无法确认则暂停，不能向旧区域坐标自动重试。死亡、Return 与正常内部传送分开，确认回到已记录入口再从第 0 步重启。

**Title:** Complete interaction transfers after loading settles

**Body:** Interaction objects can disappear before loading flags arrive, or remain available long enough for the old interaction loop to restart navigation. Keep ownership of the transfer until loading settles, cancel work tied to the old location, and distinguish normal transfers from returns to the route entrance.

**Validation:** 第 3→5、第 7→8 步、重复加载标志、同 territory 坐标跳转、超时、死亡/返回与暂停恢复。核心诊断可以晚于 P09 接入；不依赖导出窗口才能工作。P08 负责副本内回入口重走，不负责真正退本的清理（P20）或确认已击败 Boss（P19）。实际石卫塔诊断已表明，仅从第 0 步重走会再次寻找消失的第一 Boss，因此不能把 P08 单独描述成已完整解决死亡恢复。

### P09：导航诊断

分支建议：`feat/navigation-trace-export`。文件：`NavigationDiagnostics.cs`、IPC 包装、导出按钮及执行器事件钩子。

有界内存缓冲、失败/手动导出、磁盘保留数限制；记录路线指纹、目标/角色位置、导航状态、容差、移动许可、切区和调用来源。不能宣称获得实际路径点坐标或其它插件内部调用栈。

**Title:** Export bounded navigation traces for failed route steps

**Body:** Capture a bounded trace of AutoDuty navigation requests and step state, and export it on failure or explicit request. Include enough position and navigation context to compare automatic failures with successful manual navigation without continuous disk logging.

**Validation:** 缓冲上限、导出异常、保留数量、停止状态、帧开销；核对文件路径/位置与诊断内容，保留自动化调用来源但不混入角色身份统计导出。

### P10：路线格式与参数验证

分支建议：`feat/compatible-route-json`。文件：`Data/JsonConverters.cs`、`Classes.cs`、`PathValidator.cs`、`WaitForArguments.cs`、`OptionalWaitConfiguration.cs`。

**Title:** Add compatible route serialization and explicit argument validation

**Body:** Preserve legacy routes while adding explicit condition discriminators and schema checks. Reject malformed action arguments before execution and keep optional waits from being reported as hard step failures.

**Validation:** 当前 315 条内置路线往返、旧别名、null/损坏对象、整数溢出、未来 schema 拒绝、可选等待超时。格式变更是上游接口决策，先确认是否接受 versioned envelope；不能只为其它功能顺带改所有保存格式。

### P11：保存、热编辑与路线版本

分支建议：先 `fix/route-edit-persistence`，后 `feat/live-route-variants`。

**Title:** Persist route edits and keep independent editable variants

**Body:** Save confirmed edits to the selected route file and preserve metadata on reload. Keep original routes as read-only fallbacks, edit copies independently, and relocate the active step by identity when the running route changes.

**Validation:** 增删改/重排、删最后一步、磁盘失败、重载、编辑另一版本、执行中修改、原始路线保护、右键不误删、编辑步骤高亮、其它窗口编辑与多开协议。P11a 只负责磁盘持久化，P11b 再加入版本及热编辑。原始只读政策需上游讨论；不强行把它作为修复保存失败的条件。

### P12：职能脚本分配

分支建议：`feat/role-route-assignment`。

**Title:** Manage route assignments for tank, healer, and DPS roles

**Body:** Assign different route files to roles within the same duty while preserving assignments for other roles. Keep draft routes out of automatic selection until enabled and expose assignment controls where routes are managed.

**Validation:** 分配、覆盖、移除、切职业、草稿/不存在文件、共享兜底路径；确认同一副本无需重复维护一套练级编排。

### P13：练级编排与升级后排队重选

分支建议：`feat/visible-leveling-plan`；可先独立 `fix/reselect-duty-before-queue`。

**Title:** Show and edit the leveling duty plan before queueing

**Body:** Build the catalog from game duty data and expose explicit use/skip controls, including duties without local routes. Re-evaluate the next eligible duty using the current job level before queueing and show the planned duties on the main page.

**Validation:** 升级后的下一次排队、禁用/缺脚本/未解锁、空方案、物等不足、Support/Trust 差异。使用最终“副本进入等级”设计，不提交已经废弃的手填职业等级区间/优先级设计，也不声称当前自动选择已实现按经验效率最大化。P16 成绩栏后接。

### P14：录制与补录

分支建议：`feat/route-recording`。

**Title:** Record route movement with reviewable action markers

**Body:** Record movement samples and observable interaction or combat markers into an editable draft route. Allow manual insertion from the full action catalog for actions that cannot be inferred from game events.

**Validation:** 抖动、拐角、终点、动作边界、保存/取消、开始运行时拒绝未保存录制；明确不承诺自动识别所有构建动作，不能称为无须校验的“一遍录制全自动通关”。

### P15：路线分享与导入

分支建议：`feat/route-share-preview`。

**Title:** Share compressed routes with validated import previews

**Body:** Encode routes as shareable compressed text and preview the imported duty, actions, and metadata before saving. Validate corrupted or oversized data and save imports as independent drafts.

**Validation:** 往返、分段、损坏/超大输入、未来版本、覆盖冲突、导入不打断运行路线。不能夹带运行报告/经验数据，后者属于 P16。

### P16：自动化统计与最高完成度

分支建议：`feat/automation-metrics`，报告交换可作为后续 `feat/share-run-reports`。

**Title:** Track autonomous route completion and leveling session metrics

**Body:** Track session time, experience, levels gained, and per-route execution evidence. Show the best historical completion for the selected script and job; successful automatic retries remain autonomous, while exhausted recovery and manual intervention interrupt completion.

**Validation:** 自动重试后 100%、终止/手动干预冻结、步骤去重、最后 ExitDuty、跨等级/职业 XP、脚本版本隔离、历史 45/46 兼容。报告隐藏角色 ID、参与者去重和异常数据拒绝。最高完成度不等于成功率，界面与英文描述须区分；不要提交旧“首次重试冻结”“平均完成度作为编排成绩”语义。

### P17：独立步骤卡片

分支建议：`feat/execution-step-deck`。

**Title:** Add an independent execution step window

**Body:** Show the current action and neighboring steps in a separate compact window with complete tags, arguments, coordinates, and progress. Support automatic centering and manual browsing with the existing pause, resume, and step controls.

**Validation:** 主窗口关闭、缩放/字体、长内容、首末步居中、手动浏览不抢回、窗口生命周期。已有原生 ImGui 离线模拟验证，不能写成真实游戏验收。移除传送按钮后仍可独立使用。

### P19：通用 Boss 完成证据

分支建议：`fix/boss-completion-evidence`。来源：`60f6d5ca`。主要文件：`AutoDuty.BossProgress.cs`、`Helpers/BossProgress.cs`、`Helpers/ObjectHelper.cs`、`Managers/ActionsManager.cs` 和执行器调用。

实际触发：石卫塔（1042）第一 Boss 已击败，第二 Boss 战死亡回入口后从头重走，第 5 步仍等待已经消失的 Chudo-Yudo，发生 `Boss-GetBossObject` 超时。这里的副本编号和名称是复现证据，不是实现中的特判条件。

最终行为：在动作队列之外观察对应 Boss 的死亡，或读取包含完整首领名称且计数达标的 ContentDirector 目标。按区域、备注和战斗位置保存本轮证据；保留到死亡回入口重走，真正新轮才清空。英文备注通过名称表解析本地语言。存活首领优先于旧死亡记录；已知名称不回退到其它首领；无可解析名称时，只能将当前 Boss 动作已绑定对象的死亡作为证据，不能分配附近任意尸体。重新接近对象时从对象表获取，避免沿用已经消失的对象。

已确认击败的步骤不再等待该首领出现或重新开战，保留拾取流程并等待队伍脱战。脱战、暂时消失、不可选中或附近没有怪本身不是胜利；缺少完成证据时有界等待后保护暂停。若既没有观察到死亡，也没有可匹配名称的已完成游戏目标，仍可能无法自动确认，不能宣称适用于所有特殊阶段或未命名脚本。

**Title:** Preserve verified boss completion across entrance recovery

**Body:** Returning to the entrance after a later encounter can replay Boss steps for enemies that have already despawned. Retain per-duty evidence from a matching boss death or completed named duty objective, recognize early kills, and clear that evidence for a new run. Do not treat disappearance or leaving combat alone as victory, or substitute a different nearby boss for a known name.

**Validation:** 已在集成分支编译并运行模型回归，覆盖提前击杀、尸体消失、第二 Boss 团灭后保留第一 Boss 记录、新轮重置、区域/位置隔离、阶段复活、未完成/零数量目标及中英文名称边界。模型不初始化游戏服务，不能证明真实 ContentDirector 文本或对象生命周期已验收。独立分支还需验证石卫塔重走、其它副本正常/提前击杀、阶段转换，以及未命名路线。

**拆分要求：** 基于 P05/P06 适配；与 P08 联合验收但不反向捆绑 P08 的交互传送。`boss-candidates` 的目标文本/计数、名称和死亡状态，以及 `boss-defeated` 来源记录归 P09 的可选增量；不为日志导出引入统计、录制或传送功能。P19 自身须在 DutyStarted/离开副本时清空证据，不能强制要求完整 P20 才不会跨轮复用。

### P20：退本后的步骤状态清理

分支建议：`fix/reset-execution-after-duty-exit`。来源：`1bcd1482`。主要文件：`AutoDuty.DutyLifecycle.cs`、`Helpers/DutyExecutionBoundary.cs`、`Helpers/DeathHelper.cs`、`AutoDuty.cs`。

实际触发：执行到中间步骤，没有点 Stop 就退出副本；退出加载可能先触发保护暂停，区域切换处理随后在 Paused 分支提前返回，旧任务、步骤索引和手动选择遗留到下一轮。再次开始时可能从退出前的步骤执行。

最终行为：真正离开副本的清理先于 Stopped/Paused 提前返回，清除旧任务、导航/跟随、死亡恢复回调、失败重试、交互状态、入口和步骤选择。新轮 DutyStarted 再次重置，但不打断正在等待开场的启动队列。暂停后退本结束旧运行；正常自动循环保留排队/轮间流程。区域 0、内部地图切换及副本内 Pause/Resume 不作为退本；DutyRecommenced 与新一轮 DutyStarted 分开。野外后续传送不反复清空新的选择。

**Title:** Clear retained route execution when leaving a duty

**Body:** Leaving a duty without pressing Stop can retain the old step and queued work, especially when loading first pauses execution. Invalidate that state before the paused/stopped territory-change return and reset it for a new duty, while preserving normal loop queueing and progress during transfers within the duty.

**Validation:** 集成分支 Release 构建通过，44 个已有警告、0 错误；模型回归覆盖未 Stop 退出、加载区域 0、内部地图、同副本连续两轮及野外选择保留。尚未游戏验证，也未验证独立 PR 分支。提交前必须分别实测中途直接退本、暂停/失败后退本、正常自动循环、内部传送、死亡回入口、重新选中步骤后有意从该步启动。当前“副本内/外”以项目内容目录判断，未知副本地图不在已验证范围。

**拆分要求：** P06 是执行状态基础；P08 的交互/入口清理、P07 的战斗检查点和 P19 的首领清理，只对目标分支已存在的状态保留钩子。若这些功能尚未合并，移除相应引用，而不是把整个功能带进 P20。P09 的诊断、P16 的成绩结束和 P11/P17 的选择显示同理按已存在接口适配。不能把“真正退本后清理”扩展成“每次加载或普通暂停都归零”。

### P18 / H01 / H02 / H03 的范围决定

- P18 每会话重建：不是检测网格有变化才重建，会增加首次进入等待；当前开关及拒绝/超时回退语义要让上游评估。英文标题可用 `Optionally rebuild navmesh on the first duty visit of a session`。
- H01 直接坐标传送：与“卡墙不自动返回/不自动跳步”的稳定性改造独立。脚本动作和手动菜单合为单独提案；不借可靠性 PR 隐含引入。
- H02 编号：保留用户 fork 的零基选择，但 `/ad step` 从一基变零基是可见兼容性变化。若上游拒绝，所有其它 PR 应继续使用上游约定；`ac4095ba` 的编号片段不得漏入 P04。
- H03 依赖与文档：`.gitmodules` 的修改来自最初缺 commit 的 ECommons 问题，但源 URL 变更是否适合上游要在全新 checkout 上重现；不在其它 PR 顺手改。中文研发日志保留本地，提供针对单个 PR 的简短英文说明。

## 4. 依赖顺序与提交边界

推荐顺序：

1. P01/P02/P03/P04/P05 可各自以选定上游基线为起点，不互相携带。
2. P06 建执行基础；P07/P08/P09/P20 在其后，但彼此不要形成循环依赖。P19 在 P05/P06 后准备，并与 P08 联合验收。P20 不因调用某个功能的清理接口而硬依赖该整组功能；P19 也必须自行保证证据不跨轮复用。
3. P10 是格式基础；P11 保存/热编辑、P12 职能在其后；P13 基础编排不依赖 P16。
4. P14/P15 分别依赖路线保存与格式；两者之间无需依赖。
5. P16 最后接统计与编排成绩；P17 最后接展示层。P18 默认策略另议。

`AutoDuty.cs`、`BuildTab.cs`、`ActionsManager.cs`、共享本地化文件和 `tests/Program.cs` 是主要冲突点。原提交中的 Metrics/Recorder/Teleport 调用若目标 PR 不包含对应功能，必须移除或用上游现有接口改写；不能复制完整文件来“解决冲突”，否则会把未选择功能带进 PR。

分支有依赖时，优先等基础 PR 合并，再从更新后的上游建分支。若决定同时开堆叠 PR，必须明确其 base 是前一个功能分支；依赖合并后重定位到上游目标分支，避免 reviewer 看到重复变更。

## 5. 选择后如何 cherry-pick

不是直接从 `dev_01` 开分支再删除不想要的功能。每个候选分支从上游目标分支或已确认的依赖基线创建，优先使用独立 worktree，保持正在测试的 `dev_01` 不动。

下面只是操作模板，本轮没有执行：

```powershell
git fetch upstream --prune
git worktree add -b pr/action-editor-width ../AutoDuty-pr-action-editor-width upstream/master
git -C ../AutoDuty-pr-action-editor-width cherry-pick -n 00b99e12
# 在该 worktree 中核对并保留本主题补丁，适配上游原有编辑器。
# 混合提交用 cherry-pick -n 后按 hunk 整理；新测试/文档只取本功能部分。
git -C ../AutoDuty-pr-action-editor-width diff --check
git -C ../AutoDuty-pr-action-editor-width diff --cached
# 在该分支编译与针对性验收后，创建范围清晰的新提交。
```

- 对纯净可应用提交可以 `cherry-pick -x` 保留来源；**所有“可直接应用”的判断目前都未在独立分支验证**。
- 混合提交使用 `cherry-pick -n`/手工提取后新建提交，提交正文注明来源 SHA；不伪称重组补丁仍是原来的提交哈希。
- 某个提交引用了之前新增的类，不等于应该把之前所有提交都带进来；先提取最小依赖或重写为上游接口。
- PR 内可有几个主题一致的提交，例如 helper、集成、测试；不要求机械“一原提交一 PR”。
- 通过 `git diff upstream/<target>...HEAD` 确认不含其它候选组、二进制、个人数据或无关子模块修改。
- 每个分支独立编译/测试；集成分支通过不能证明拆分后通过。每个 PR 说明行为、必要兼容性和尚未完成的游戏验证。
- 选定后再 push 到个人 fork，并显式指定上游仓库、base、head。发布范围以你选择为准。

## 6. 已有验证与缺口

- 当前集成版本 Release/x64 编译通过：44 个已有警告、0 错误；测试依赖本机 Dalamud API 15 环境。
- 离线测试涵盖 315 条路线格式、等待参数、录制取样、保存重载、职能分配、战斗检查点、导航监测、交互切区、完成度、经验累计和报告验证；新增 Boss 完成证据及退本边界场景。最新集成验证日志为 `artifacts/build-duty-lifecycle.log`、`artifacts/tests-duty-lifecycle.log`。
- 已有集成测试包：`AutoDuty-test-60f6d5ca.zip`（Boss 修复）、`AutoDuty-test-1bcd1482.zip`（含前者及退本重置）。它们是本地测试产物，不提交上游；没有收到游戏内验收结果，不能写成已验证正常。
- 本次仅更新拆分文档并核对 Git/源码，未重新运行代码测试；**仍未为候选分支执行 cherry-pick、独立构建或游戏验收**。实际卡墙、首领识别、切区、加载速度与 ImGui 字体效果仍需真实客户端检查。
- 独立 PR 不携带整个庞大的测试入口；按功能抽出有意义的回归用例与最小测试设施。UI 小修复采用针对性人工验收，不增加只镜像实现的测试。

## 7. 待你选择

可以直接指定编号，例如：“先做 P01、P03、P04、P05；P06 后续；H01/H02 不发”。未选择项仍留在 `dev_01`，不影响你的测试版。

最终发布前，每个选中项会得到：独立分支、范围整理后的提交、验证结果、英文 PR 正文，以及明确的上游依赖。本文只是范围提案，不意味着选中内容已经能够无冲突 cherry-pick。

## 附录：原始提交去向

下面按时间顺序列出固定快照中的全部 **51 个提交**（含 2 个本地 PR 筹备文档提交）。一个提交可能拆入多个 PR；编号代表来源映射，不代表可以整提交照搬。本次文档更新提交不计入该固定快照。

| 提交 | 主题摘要 | 去向 |
| `93b18410` | docs: record duty reliability implementation plan and fix ECommons source | H03 |
| `50c1ab78` | feat: add per-step failure state and unified retry pause recovery | P06 |
| `7991e301` | fix: detect rejected movement and action timeouts without silently advancing | P06 |
| `c76d5d78` | feat: support live route editing and explicit step recovery controls | P11、P06 |
| `7a989e5e` | feat: add compatible versioned path JSON and explicit condition discriminators | P10 |
| `048fe4ed` | feat: rebuild navmesh once per duty per plugin session | P18 |
| `2035fd20` | feat: select routes by job metadata and configure leveling duty filters | P12、P13 |
| `46c340ee` | feat: record manual duty routes with event markers and editable samples | P14 |
| `6cb14ca2` | feat: share compressed routes and review imports before saving | P15 |
| `54d5efd2` | fix: preserve optional wait semantics and harden recording recovery edges | P10、P14、P06 |
| `76b0d830` | docs: document duty tools usage and offline verification results | H03（按主题重写） |
| `d9df6496` | feat: track uninterrupted route completion and session experience metrics | P16 |
| `12507509` | feat: add automation dashboard and shared route performance reports | P16 |
| `23fa2910` | docs: explain completion metrics and leveling evidence validation | P16 文档 |
| `62385b1b` | fix: restore corrupted Chinese duty tools translations | 各功能相关翻译；MainTab 修正可随 P02 |
| `b1139480` | feat: rank leveling plans by level intervals priority and measured efficiency | P13、P16（剔除已废弃的区间/排名设计） |
| `217a7fbe` | feat: add visible leveling plan editor backed by the game dungeon catalog | P13 |
| `a2671a5a` | docs: explain visible leveling plans and measured efficiency selection | P13 文档（只保留最终行为） |
| `0713563b` | fix: restore active step highlighting and explicit automatic follow control | P06 最小步骤显示、P11 |
| `baa60f55` | fix: simplify shared leveling plans and show planned duties on main page | P13 |
| `22a989b3` | feat: manage per-role route assignments and edit routes outside duties | P12、P11 |
| `9bd65841` | feat: expose full manual action catalog in recorder and document detection limits | P14 |
| `2a99eebc` | fix: persist confirmed route edits and reload steps from disk | P11 |
| `7b1572b2` | feat: keep independent draft route variants without replacing role assignments | P11、P12 |
| `bda502a5` | fix: delete edited steps immediately by identity from any tab | P11 |
| `3a71092f` | fix: restart paused navigation and preserve paused action queues | P06 |
| `490b16b9` | fix: handle early combat at adjacent stop-for-combat checkpoints | P07（附带界面/文案归 P11/P13） |
| `cd7b3094` | feat: add TeleportTo coordinate action without pathfinding | H01 |
| `00b99e12` | fix: prevent action editor auto-size width feedback | P01 |
| `a52563c4` | fix: keep path selector visible when player readiness changes | P02 |
| `46365350` | fix: remove build right-click deletion and highlight edited step | P11 |
| `707773c7` | feat: protect original routes and select independent editing versions | P11 |
| `6ff69f47` | fix: recognize combat switch arrival on slopes and diagnose stopped navigation | P07、P04、P06（导航停止诊断） |
| `12a64429` | fix: stabilize route catalog UI and refresh leveling selection before queueing | P02、P13 |
| `fc7f15c4` | fix: finish Brayflox key interaction and retain dialogue completion evidence | P03 |
| `f59a21fb` | fix: pause failed navigation without Return or transition step skipping | P06、P08 |
| `38f1db90` | feat: capture bounded navigation traces with movement call sources | P09 |
| `9ace8e22` | fix: isolate route movement and recover confirmed entrance returns from step one | P06、P08、P09 |
| `1450ca90` | feat: add manual teleport to step context menus | H01 |
| `e2b09f08` | feat: add independent execution step card window | P17（移除 H01 按钮） |
| `67fdd313` | fix: recognize named bosses and accept ground interaction endpoints | P05、P04 |
| `2a53fe71` | fix: display zero-based step indices across route windows | H02 |
| `ac4095ba` | Fix route corner tolerance and use zero-based step diagnostics | P04、P09、H02 |
| `0549abc6` | Track interaction transfers until loading settles before advancing route | P08、P09 |
| `7f7b2a7b` | Wait for navigation readiness and report each bounded step attempt | P06 |
| `707ba5e1` | Show best historical route completion in leveling plan | P16、P13 展示扩展 |
| `0a934f5b` | Count successful automatic retries as autonomous route completion | P16（最终重试语义） |
| `ccd5f49f` | docs: propose scoped upstream PRs and map source commits | 本地 PR 筹备文档，不单独发产品 PR |
| `e552c2af` | docs: draft P01 issue with upstream fields and validation evidence | P01 Issue 草稿；不随代码 PR 加入上游仓库 |
| `60f6d5ca` | fix: retain generic boss defeat evidence through entrance recovery | P19；诊断增量归 P09 |
| `1bcd1482` | fix: reset retained route execution when leaving a duty | P20；按已合并功能适配清理钩子 |
