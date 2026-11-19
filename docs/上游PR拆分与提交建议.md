# AutoDuty 改动分类与 fork 维护记录（原上游 PR 拆分建议）

## 当前维护决定（2026-11-19）

版本规则更新：此前 `0.0.0.400`～`0.0.0.407` 为历史本地编号，现统一重新标记为 `0.0.0.399.1`。前四段跟随上游，后续本地更新仅递增第五段。程序集/manifest 因四段限制保持 `0.0.0.399`，完整版本写入 DLL 产品版本并用于 UI/诊断；下文旧版本与测试记录保留历史含义。

后续在个人仓库 [wynnforthework/AutoDuty](https://github.com/wynnforthework/AutoDuty/tree/dev_01) 的 `dev_01` 集成分支维护。上游不接受 AI 代码，停止向 `erdelf/AutoDuty` 准备、拆分和提交 AI 参与的代码 PR，包括通过 fork 创建 PR。本文保留 P 编号作为改动分类，**不再作为待发布的上游 PR 清单**。

限制已写入 [AGENTS.md](../AGENTS.md)，并用本地 pre-push 钩子阻止直接推送上游。钩子不能阻止网页/API 从 fork 创建 PR，仍须遵守维护规则。个人 fork 的备份和维护可以继续；不得隐藏 AI 参与，也不改变原项目版权和未授权许可状态。

## 最近修改与分类

2026-11-20 补充：`0.0.0.399.11` [修复创造环境极北造物院第 18 步多段加载提前完成](Ktisis-step18-multiphase-loading.md)（P08、P09）。Loading 仅作为转换开始证据；短暂恢复 Ready 但仍在源位置时不再提前推进步骤，第二段加载继续归属于原移动步骤。Release 零错误、36 组离线测试通过，尚需在窗口确认加载 `.399.11` 后游戏实测。仅维护个人 fork。

2026-11-20 补充：`0.0.0.399.10` [修复创造环境极北造物院第 3 步跳跃落地误判](Ktisis-step3-jump-landing-ownership.md)（P08、P09）。未知位移采样改为按步骤归属；普通 Jump/JumpTo 和无活动路径的零坐标等待不再被误判为区域转换，显式转换与真实旧导航保护保留。Release 零错误、36 组离线测试通过，尚需游戏实测。仅维护个人 fork。

2026-11-20 补充：`0.0.0.399.9` [万象森国第 2 步移动控制审查](Vanaspati-step2-movement-control.md)（P07、P09）。补齐坐标 Wait/交互等动作接近途中遗漏的瞬发策略，增加可选只读 BossMod 输入仲裁诊断。Release 零错误、36 组离线测试通过；已发现的策略遗漏已处理，但本次停滞根因及实机修复效果仍待验证。仅维护个人 fork。

2026-11-20 补充：`0.0.0.399.8` [接入游戏区域信号并审查判断边界](Transfer-game-state-audit.md)。移除加载对旧目标距离和弹射对固定层差/位移量的依赖；接入 ZoneInit，MapIdChanged 只记录及失效旧验证，纯坐标异常不能让旧脚本自动完成。明确保留阈值的用途及未知机制暂停限制。Release 零错误、36组离线测试通过，尚需游戏实测。仅维护个人 fork。

2026-11-20 更正：`0.0.0.399.7` [分离区域转换完成与后续寻路](Transfer-completion-before-continuation.md)。第 34 步目标不是第 32 步的落点，不能据此推断坡道或扩大高度范围。旧脚本的后续坐标只用于异步路径连通性验证；转换状态与地面稳定后仅完成当前步骤，后续仍独立导航。显式区域保持原约束。Release 零错误，36 组离线测试通过，尚需游戏实测。

2026-11-20 历史记录：`0.0.0.399.6` 的[第 32 步坡面落地验证](Ktisis-step32-sloped-landing.md)以 12 米范围兼容落点高差，但“坡道”推论证据不足，且仍错误地把后续点作为落地区域。该方案已由 `.399.7` 替代。

2026-11-20 补充：`0.0.0.399.5` 修正 StopForCombat true 途中遇战即停的本地回归，改为抵达目标后再移交战斗控制，并区分尚未开战、战斗中和已结束证据。详见 [StopForCombat 到点战斗修复](StopForCombat-arrival-combat.md)。Release 零错误，35 组离线测试通过，尚需游戏实测。仅维护个人 fork。

2026-11-20 补充：`0.0.0.399.4` 修复[创造环境第 18 步转换目的地推断](创造环境第18步转换目的地推断修复.md)。真实 974 路线在就绪检查后还有零坐标 StopForCombat false，旧两步前瞻误报 destination-unknown。现在受限地穿过无条件就绪检查/关闭战斗/注释，解析新平台位置，并补充真实脚本回归；仍逐步执行后续动作。Release 零错误，35 组离线测试通过，尚需游戏实测。

2026-11-20 补充：`0.0.0.399.3` 完成[动作战斗策略逐项审查](动作战斗策略逐项审查.md)。移除 P07 中“其它动作默认开启战斗”的本地回归，仅 Boss / KillInRange / StopForCombat true 明确中断路线战斗；同步修正交互末尾重新开移动、通用 Wait 读取全局状态隐式等脱战。Release 零错误、34 组离线测试通过，尚无游戏实测。

以下记录补充历史拆分快照之后的改动；提交号指个人集成分支，不表示可独立 cherry-pick 或已有游戏验证。

| 分类 | 当前行为及修正 | 代表提交 |
| --- | --- | --- |
| P06 / P08：执行就绪与时间统计 | 任务执行、步骤切换和失败重试检查持续游戏就绪；加载期间挂起动作计时；恢复后继续同一步。重试次数与首次执行区分。 | `08dff929`、`07b54b29`、`877e015d` |
| P07：步骤控制权 | MoveTo 关闭托管 BossMod 移动和输出；StopForCombat false 关闭 BossMod 移动、开启输出；true 停止路线导航并启用战斗移动和输出。修正战斗动作锁误伤导航就绪检查。 | `d6c6ae6e`、`79c8b961`、`7f6d81f6` |
| P08：无加载转换 | 补充穿门、弹射平台等没有完整加载标志的转换证据，避免继续追逐旧区域坐标。 | `542662a9`、`0f9af249` |
| P08 / P10 / P14：统一区域转换 | 分离疑似转换、过程结束、目的区域确认与步骤完成；加入目的地图/水平范围/高度配置、地面检查、异步限频路径验证及转换录制。未知位移先停止导航，证据不足暂停。 | `cf6a0bc6`、`ab44b58e` |
| P05：Boss 遭遇与上下文 | 恢复副本上下文，跟踪观察到的 Boss 身份和死亡证据；不再将对象消失直接当作死亡。 | `c73d326e` |
| P05：移除名称查找回归 | 删除脚本 Note → BNpcName → 名称筛选链，使用 Rank / BossMod 模块 / 已跟踪 EntityId + BaseId；先过滤尸体，再选择活体，并约束遭遇归属。 | `e59ff769` |
| 会话生命周期 | 手动进本保持被动；窗口开关不改变自动化会话；Stop 不再依赖当前路线/窗口状态显示。 | `4358399d`、`b9e645b1` |
| 构建交付与维护规则 | 固定热加载目录，核验部署 DLL 版本与交付凭据；禁止上游 AI 代码 PR 并拦截直接推送。 | `6074f2c4`、`3b404f43`、`c3eb9ced` |

### P05 审查结论修正

[PR #652 的反馈](https://github.com/erdelf/AutoDuty/pull/652#issuecomment-6956697828)指出了补丁自身引入的问题。对比上游 0.0.0.399（`ee4eced8`）与提交 `9f3f13a5`：原版没有按脚本名称寻找 Boss，补丁加入的名称过滤在匹配不到时也没有回退。因此，不能把这个新增失败路径描述成上游原有的本地化缺陷。

个人版 0.0.0.407 已删除该查找链。名称仍可用于显示、日志，以及把已识别对象的名称关联到目标文字，不用于发现 Boss。测试直接调用生产对象选择器，但模拟 Rank/模块输入不能证明实机服务返回正确；此前所有 boss-timeout 的根因也并未全部查明。详见 [Boss 识别本地审查](Boss识别本地审查.md)。下方历史 P05 的名称方案已经废弃。

### 当前验证与待验收

- 最近代码版本：`0.0.0.407`；代码快照：`e59ff769`。
- Release 编译零错误，33 组离线测试通过；**尚未进行本版本游戏实测**。
- 部署仍覆盖同一个本机目录 `D:/UGit/AutoDuty/artifacts/AutoDuty-session-stop-fix/`，没有新增按版本命名的加载目录。
- 区域转换需用普通换图、穿门、弹射、掉落、移动平台、死亡返回和未知位移做实机回归；旧脚本推断不足时需要在副本中显式配置目的区域。
- Boss 需验证提前击杀、阶段切换、同区域多个 Boss、Rank/模块缺失及上下文恢复。不能据离线测试宣称全副本或 16～100 级无人干预已实现。

实现与验收说明：[区域转换判定与录制](区域转换判定与录制.md)、[步骤执行机制统一审查](步骤执行机制统一审查.md)、[挂机功能使用与验收](挂机功能使用与验收.md)。仓库入口同步更新为 [English README](../README.md) / [中文 README](../README.zh-CN.md)。

### 已发布 Issue 与未实施建议

- [#653：区域切换就绪与步骤失败](https://github.com/erdelf/AutoDuty/issues/653)：报告国服 0.0.0.399 魔塔神门巴别塔第 10→11 步问题，包含用户实测复现与分阶段判定建议。该报告不能替代新版实测。
- [#654：官方脚本库下载状态与刷新](https://github.com/erdelf/AutoDuty/issues/654)：建议显示真实下载进度、保持忙碌状态，并提出清空本地脚本再下载。**这是建议，当前 fork 尚未实现此完整方案**；README 不把它列为已交付功能。

---

## 历史档案：2026-11-17 上游拆分提案（已停止执行）

以下保留当时的快照、计划和验证记录，描述的是当时状态，不代表当前 PR 状态或后续提交安排。独立性构建结论也仅适用于其中列出的旧提交。

日期：2026-11-17。第一轮 P01～P05 已从上游 `ee4eced8` 分别提取、测试、提交并推送，已创建五个上游 Draft PR；此前误建的 Bug Issue 已关闭，草稿格式用于 PR 正文。独立性、120 种合并顺序、31 种组合构建、测试结果与待完成的游戏内验收见 [第一轮提交与验证记录](pr-first-round/README.md)。P06 及以后仍按本提案后续处理。

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
| P05 | Boss 身份识别、通用完成证据与重走保留击败状态 | 第一轮，需游戏验证 | 适配上游执行器；不硬依赖完整 P06 | `67fdd313` 名称部分 + `60f6d5ca`（原 P19） |
| P06 | 有限重试、暂停恢复、导航就绪、位移监测及退本/新轮重置 | 重点，第一轮后续 | P04 可先合并；复用已合并 P05 的 Boss 清理入口 | 执行器提交 + `1bcd1482`（原 P20） |
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
| H01 | 新增 TeleportTo 动作、步骤菜单直接传送 | 建议先留 fork | P06/P11 的部分接口 | 与所有稳定性 PR 分离，需上游明确接受此功能范围 |
| H02 | UI、命令和日志全部统一零基编号 | 待讨论兼容性 | 涉及各窗口、命令与日志 | 独立决定；不能借 P04 混入命令语义变更 |
| H03 | ECommons 子模块 URL、中文计划与验收流水账 | 本地保留；依赖修复可另议 | 无 | 仅有可复现的干净克隆失败时单独提依赖修复 |

当前第一轮范围为 **P01～P05**，其中 P05 已包含原 P19；**P06 已包含原 P20，仍作为较大的执行器 PR 后续准备**。若第一轮也需要退本清理，应明确把合并后的完整 P06 纳入，而不是仍按一个小型 P20 估算范围。P07～P09 在执行器基础之后，P10～P18 根据上游反馈逐组推进。

编号合并记录：**P19 → P05，P20 → P06**。P19/P20 不再作为独立候选或重复发 PR，其来源提交仍在附录保留。此调整仅更新提交范围，不表示已经发布或完成独立分支验收。

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

### P05：Boss 身份识别与完成证据（已合并原 P19）

> 历史方案，已被 0.0.0.407 的审查和修正取代。下文名称查找方案不再使用，也不再准备上游 PR；以本文开头的 P05 审查结论为准。

分支建议：`fix/boss-identification-and-completion`。来源：`67fdd313` 的名称识别部分与 `60f6d5ca`。主要文件：`Helpers/BossIdentity.cs`、`Helpers/ObjectHelper.cs`、`AutoDuty.BossProgress.cs`、`Helpers/BossProgress.cs`、`Managers/ActionsManager.cs`、`AutoDuty.cs` 相关调用。

完整路线备注名匹配本地名称或英文 BNpcName；只返回附近、存活、可选中的对应首领。已知准确名称时不回退到其它首领；没有可解析名称的旧路线保留等级标记/BossMod 兜底。接近过程中从对象表重新获取对象，避免沿用已经消失的对象。身份识别与是否完成分开判断，不将正在战斗等同于识别成功。

实际触发：石卫塔（1042）第一 Boss 已击败，第二 Boss 战死亡回入口后从头重走，第 5 步仍等待已经消失的 Chudo-Yudo，发生 `Boss-GetBossObject` 超时。这里的副本编号和名称是复现证据，不是实现中的特判条件。

最终行为：在动作队列之外观察对应 Boss 的死亡，或读取包含完整首领名称且计数达标的 ContentDirector 目标。按区域、备注和战斗位置保存本轮证据；保留到死亡回入口重走，真正新轮才清空。英文备注通过名称表解析本地语言。存活首领优先于旧死亡记录；已知名称不回退到其它首领；无可解析名称时，只能将当前 Boss 动作已绑定对象的死亡作为证据，不能分配附近任意尸体。重新接近对象时从对象表获取，避免沿用已经消失的对象。

已确认击败的步骤不再等待该首领出现或重新开战，保留拾取流程并等待队伍脱战。脱战、暂时消失、不可选中或附近没有怪本身不是胜利；缺少完成证据时有界等待后保护暂停。若既没有观察到死亡，也没有可匹配名称的已完成游戏目标，仍可能无法自动确认，不能宣称适用于所有特殊阶段或未命名脚本。

**Title:** Fix boss identification and completion across client languages and route replays

**Body:** Identify route bosses by their exact localized or English name and retain per-duty completion evidence from a matching death or completed named duty objective. Handle early kills and replayed steps after entrance recovery without waiting for a defeated boss to respawn. Do not treat disappearance or leaving combat alone as victory, or substitute another nearby boss for a known name.

**Validation:** 完整英文/中文名、空名、部分名称、小怪拒绝，接近/动作/遇战阶段识别一致；提前击杀、尸体消失、第二 Boss 团灭后保留第一 Boss、新轮重置、区域/位置隔离、阶段复活、未完成/零数量目标及名称边界。已有集成编译和模型回归，但模型不初始化游戏服务，不能代替真实名称表、ContentDirector 文本和对象生命周期验收。独立分支须测试石卫塔重走、樵鸣洞识别、其它副本正常/提前击杀及未命名路线。

**提交边界：** 一个 PR 内可分“名称识别”“完成证据与生命周期”“回归测试”几个提交，统一说明最终行为。先适配上游现有任务队列和停止/暂停入口，不为当前 `FailCurrentStep` 等调用引入整套 P06；是否能独立应用须在 worktree 编译确认。若上游没有合适的安全停止接口，只提取最小保护，不静默跳过步骤。P05 自身必须在新轮/真正退本时清空 Boss 证据，不能等 P06 合并才保证正确性；该清理接口供后续 P06 复用。P08 的入口重走与本组联合验收，但交互传送仍归 P08。诊断扩展归 P09，不拖入统计、录制和传送。

### P06：执行器失败、就绪与副本生命周期管理（已合并原 P20）

分支建议：`feat/bounded-step-recovery`。文件：`AutoDuty.Execution.cs`、`AutoDuty.cs`、attempt/pause/readiness/progress helpers、配置与最小控制 UI；新增 `AutoDuty.DutyLifecycle.cs`、`Helpers/DutyExecutionBoundary.cs`、`Helpers/DeathHelper.cs` 的退本清理部分（来源 `1bcd1482`）。

最终行为：每步独立有限重试；耗尽后暂停原步骤；Pause/Resume 不重复启动仍在队列中的动作；停止旧导航、保留步骤前置条件。未就绪、开场封锁、网格未完成时不计入移动卡住；卡住依据实际位移，最低有效窗口 `5s`；计算路径/施法与实际堵墙分开。保留移动/动作总超时约束。

实际触发：执行到中间步骤，没有点 Stop 就退出副本；退出加载可能先触发保护暂停，区域切换处理随后在 Paused 分支提前返回，旧任务、步骤索引和手动选择遗留到下一轮。再次开始时可能从退出前的步骤执行。

最终行为：真正离开副本的清理先于 Stopped/Paused 提前返回，清除旧任务、导航/跟随、死亡恢复回调、失败重试、交互状态、入口和步骤选择。新轮 DutyStarted 再次重置，但不打断正在等待开场的启动队列。暂停后退本结束旧运行；正常自动循环保留排队/轮间流程。区域 0、内部地图切换及副本内 Pause/Resume 不作为退本；DutyRecommenced 与新一轮 DutyStarted 分开。野外后续传送不反复清空新的选择。

**Title:** Manage bounded step recovery and reset execution between duty runs

**Body:** Track navigation and action attempts explicitly, retry failed work within the same step, and pause when recovery is exhausted. Wait for player, duty, and mesh readiness before navigation, and measure actual movement progress rather than treating path state changes as movement. Clear stale step work when leaving a duty, including while paused, and reset new runs without interrupting the normal queueing pipeline.

**Validation:** 路径拒绝、无路径、真实卡住、慢加载、施法、战斗中断、暂停恢复、重试次数、旧配置迁移。最小 UI 和本地化必需，统计 hooks、录制、传送、卡片窗口不属于本 PR。原先跨功能测试入口要拆成仅依赖本 PR 的测试。退本新增验证：未 Stop 直接退出、暂停/失败后退出、区域 0、内部地图、同本连续两轮、野外选择保留及正常自动循环。当前集成版本 Release 编译通过（44 个已有警告、0 错误），模型回归通过；独立 PR 和游戏内验证均待执行。“副本内/外”依赖项目内容目录，未知副本地图尚未验证。

**提交边界：** 一个 PR 内按“重试与暂停恢复”“就绪与位移监测”“退本/新轮清理”“测试与最小 UI”组织提交。退本清理与普通暂停/内部传送明确分开，不因每次加载就归零。复用 P05 已存在的 Boss 证据清理入口，不重复实现；P07/P08 的检查点和交互状态仅在目标分支已存在时接入，不反向捆绑整个功能。P09 诊断、P16 成绩结束以及 P11/P17 选择显示同样按已存在接口适配。

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

**Validation:** 第 3→5、第 7→8 步、重复加载标志、同 territory 坐标跳转、超时、死亡/返回与暂停恢复。核心诊断可以晚于 P09 接入；不依赖导出窗口才能工作。P08 负责副本内回入口重走，不负责真正退本的清理（P06）或确认已击败 Boss（P05）。实际石卫塔诊断已表明，仅从第 0 步重走会再次寻找消失的第一 Boss，因此不能把 P08 单独描述成已完整解决死亡恢复。

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

### P18 / H01 / H02 / H03 的范围决定

- P18 每会话重建：不是检测网格有变化才重建，会增加首次进入等待；当前开关及拒绝/超时回退语义要让上游评估。英文标题可用 `Optionally rebuild navmesh on the first duty visit of a session`。
- H01 直接坐标传送：与“卡墙不自动返回/不自动跳步”的稳定性改造独立。脚本动作和手动菜单合为单独提案；不借可靠性 PR 隐含引入。
- H02 编号：保留用户 fork 的零基选择，但 `/ad step` 从一基变零基是可见兼容性变化。若上游拒绝，所有其它 PR 应继续使用上游约定；`ac4095ba` 的编号片段不得漏入 P04。
- H03 依赖与文档：`.gitmodules` 的修改来自最初缺 commit 的 ECommons 问题，但源 URL 变更是否适合上游要在全新 checkout 上重现；不在其它 PR 顺手改。中文研发日志保留本地，提供针对单个 PR 的简短英文说明。

## 4. 依赖顺序与提交边界

推荐顺序：

1. P01/P02/P03/P04 各自从选定上游基线准备。合并后的 P05 同时提取名称识别与完成证据，优先适配上游现有停止/暂停机制；不能整提交搬入 P06，独立可用性须实际编译验证。
2. P06 合并退本/新轮重置，建立执行基础，复用已合并 P05 的证据清理接口；P07/P08/P09 后接，不形成循环依赖。P05 的最小 Boss 生命周期清理与 P06 的全面执行清理边界分明；P05 与 P08 联合验收死亡回入口场景。
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

## 7. 当前选择与后续准备

按当前范围先准备 P01～P05，P05 含原 P19；P06 含原 P20，是否同批准备另行明确。其它项仍留在 `dev_01`，不影响测试版。分组调整不等于授权发布 Issue/PR；独立分支与说明整理完成后按选定范围推进。

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
| `60f6d5ca` | fix: retain generic boss defeat evidence through entrance recovery | P05（已合并原 P19）；诊断增量归 P09 |
| `1bcd1482` | fix: reset retained route execution when leaving a duty | P06（已合并原 P20）；按已合并功能适配清理钩子 |
