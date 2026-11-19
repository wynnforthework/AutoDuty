# Boss 对象获取与无名称步骤完成判定（0.0.0.403）

## 本次证据

本机 `navigation-20261119-111422-999.jsonl` 对应创造环境第 30 步，记录运行版本为 0.0.0.400。19:12:16 至 19:14:22，Boss-GetBossObject 持续等待；对象表能看到拉冬之王（BaseId=13349、NameId=10398、rank=6、module=true、IsTargetable=true），最后一次候选记录 HP=730698。该记录说明获取阶段已有可识别对象，并非只有击杀后尸体消失这一种问题；它不包含最终击杀时刻，也没有记录 DutyData 是否为空。

## 代码缺陷与修改

区域通知处理原先无条件将 DutyData 清空，包括 territory=0 加载占位通知。Boss 获取回调使用 `(Plugin.DutyData?.bossObject = GetBossForStep(action)) != null`：DutyData 为空时短路整个赋值，连对象查询也不执行，但独立的候选诊断仍能打印 Boss。这条代码路径会将状态丢失误报成找不到 Boss。此次移除区域通知的无条件清空，保留实际离本、停止和新开路线的生命周期清理；步骤开始和 Boss 获取增加状态恢复。查询、写入和任务成功返回改为明确的独立操作。

另一个独立缺口是脚本无名称 Boss 步骤：之前尸体判定依赖当前 DutyData.bossObject，且无法在尸体失去 Boss rank/module 后重新匹配。现在按本次副本、步骤名称与坐标保留观察过的 EntityId、BaseId、本地名称；只用这些已确认身份认领死亡对象，并用观察到的名称匹配已完成目标。记录与临时对象引用分离，离本或新一轮重置时清理。仍然不把单纯脱战、对象消失、其它房间目标或队友当成胜利。

诊断新增 dutyContextPresent、storedBoss、selectedBoss，以及获取成功时的 boss-acquired 记录，方便区别对象筛选、临时状态和任务推进问题。

## 验证

Release 编译及离线测试覆盖无名称步骤身份保留、rank/module 丢失后的死亡身份匹配、尸体消失后保留完成证据、本地名称目标、跨步骤与新副本隔离、假 EntityId 及相位消失不误判。API 沿用现有 IBattleChara/IGameObject；本机 Hooks/dev/Dalamud.xml 已核对 EntityId、CurrentHp、MaxHp、IsDead 的契约。本轮未新增游戏事件或原生接口。

游戏验收需确认：第 30 步出现 boss-acquired 并进入 Boss-MoveCheck/Boss-BossCheck；击杀后出现 boss-defeated，完成步骤；再验证无名称的其它副本 Boss、死亡回起点和区域加载。离线通过不能替代游戏验收。若再次失败，新诊断会直接显示上下文和实际选中对象，避免继续只看到 boss-timeout。
