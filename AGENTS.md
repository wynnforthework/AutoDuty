# AutoDuty 本机开发

开发、修改、编译或部署本插件时，先读取并遵循 `C:/Users/Administrator/.codex/skills/dalamud-plugin-dev/SKILL.md`。本机依赖、API 查阅入口和唯一热加载目录由该 skill 管理。

skill 的版本化副本在 `skills/dalamud-plugin-dev/`。维护 skill 时同步该副本与上述已安装目录，保持本机路径配置一致；安装目录缺失时可直接读取仓库副本。

统一入口：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`。只检查路径用 `-PlanOnly`，只编译待测试用 `-BuildOnly`。最终部署始终覆盖 `D:/UGit/AutoDuty/artifacts/AutoDuty-session-stop-fix/AutoDuty.dll`，不得再按版本、commit 或任务名创建测试 DLL 入口。版本 ZIP 仅作按需归档。
