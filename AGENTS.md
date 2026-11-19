# AutoDuty 本机开发

## fork 版本号

- 对外显示格式为 `<上游四段版本>.<本地修订>`，例如 `0.0.0.399.1`；当前值以 AutoDuty.csproj 为准。本地修改只递增 LocalRevision；UpstreamVersion 仅在实际同步上游版本时调整。
- .NET/Dalamud 的 AssemblyVersion 和 manifest 仅支持四段，保持 UpstreamVersion；完整五段版本使用 InformationalVersion（DLL ProductVersion），用于窗口、诊断和交付记录。不要将本地修订写进上游第四段，也不要给引用项目传全局 Version。

## 上游 AI 代码 PR 禁止规则（用户于 2026-11-19 明确要求）

- 用户明确告知上游 `erdelf/AutoDuty` 禁止 AI 代码，并要求助手在其尝试向上游发送 PR 时主动阻止。此限制包括通过个人 fork 向上游创建 PR，不以推送目标是 origin 为例外。
- 不得为该上游准备或发布包含 AI 生成/修改代码的 PR，包括创建提交用分支、cherry-pick 拆分、draft PR、GitHub CLI/API/网页提交，或借助其它账号/仓库绕过。遇到相关请求，应先指出本规则及上游政策原因，停止上游代码贡献流程；不要把一句“确认继续”当作解除限制。
- 本地开发、测试、固定目录 DLL 部署和用户 fork 的备份可以继续。不得把 AI 代码伪装为人工代码，或建议通过改写提交信息隐藏来源。
- Bug/Feature Issue 不在此代码 PR 禁令内，但每次发布仍须有用户明确授权，并遵守上游当时的文档/AI 内容政策。
- 政策来源：用户本次明确指示。仓库 `.github/copilot-instructions.md` 另有禁止 AI code review 的要求；该文件不能单独作为“禁止所有 AI 代码”的证据。
- 本地 pre-push 钩子拦截直接向上游推送；它无法拦截网页/API 从 fork 创建 PR，因此所有助手仍须执行上述规则。

开发、修改、编译或部署本插件时，先读取并遵循 `C:/Users/Administrator/.codex/skills/dalamud-plugin-dev/SKILL.md`。本机依赖、API 查阅入口和唯一热加载目录由该 skill 管理。

skill 的版本化副本在 `skills/dalamud-plugin-dev/`。维护 skill 时同步该副本与上述已安装目录，保持本机路径配置一致；安装目录缺失时可直接读取仓库副本。

统一入口：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`。只检查路径用 `-PlanOnly`，只编译待测试用 `-BuildOnly`。最终部署始终覆盖 `D:/UGit/AutoDuty/artifacts/AutoDuty-session-stop-fix/AutoDuty.dll`，不得再按版本、commit 或任务名创建测试 DLL 入口。版本 ZIP 仅作按需归档。
