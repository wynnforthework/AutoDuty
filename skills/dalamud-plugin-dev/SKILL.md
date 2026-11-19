---
name: dalamud-plugin-dev
description: 开发、修改、编译和部署 FFXIV Dalamud 插件时使用。核对官方 API 与本机版本，复用已记录的 .NET/Dalamud 路径，始终覆盖同一热加载 DLL 目录。适用于 AutoDuty 和独立新插件。
---

# Dalamud 插件开发

## 固定约定

- 先读 [本机配置](references/local-environment.json)，验证已记录路径。仅路径不存在或版本不兼容时定向寻找并更新配置；不要每轮全盘搜索 .NET、Dalamud 或另装 SDK。
- 每个插件只用一个固定部署目录、DLL 路径和 InternalName。不得用 commit、版本、日期、分支、任务标题创建新的测试入口。编译中间文件可在项目 bin/obj，最终测试文件始终覆盖配置中的 DeployDirectory。
- 已注册的历史目录即使名字含版本/功能名也冻结为字面常量，不再随版本推导。AutoDuty 固定入口为 `D:/UGit/AutoDuty/artifacts/AutoDuty-session-stop-fix/AutoDuty.dll`，复用现有游戏注册记录。worktree 共用该入口，串行部署。
- 新插件先读当前官方 SamplePlugin，匹配本机 API/TFM，再创建项目；分配 `D:/UGit/_dalamud-dev/<InternalName>/` 并在配置 Plugins 中记录一次。项目另放普通源码目录。不要为每次修改创建新插件身份。
- 首次注册固定 DLL 并启用开发插件 AutomaticReloading；之后只覆盖原 DLL，不反复禁用、删除、添加插件。不要通过改写运行中的 dalamudConfig.json 替代 UI 设置。

## 开发前必须核对接口

创建新插件、调用新 API、修改服务/事件/窗口/IPC 生命周期前，按 [API 查阅说明](references/api-and-lifecycle.md) 查官方文档，并用本机 DLL/XML 或对应源码验证签名。在工作上下文记录实际类型、成员、版本及证据路径/链接，不必每次新增文档。无法联网时明确使用本机 XML 和匹配源码替代，不凭记忆猜 API。

既有插件沿用其 SDK、TFM、依赖锁文件；不要因在线模板更新而无条件升级。原生 UI 视觉设计可结合已安装的 dalamud-ui-style skill。

## 构建和覆盖部署

使用 [build-plugin.ps1](scripts/build-plugin.ps1)，不临时拼接另一套输出规则：

```powershell
# 显示唯一部署路径，不构建、不覆盖
& 'C:/Users/Administrator/.codex/skills/dalamud-plugin-dev/scripts/build-plugin.ps1' -Plugin AutoDuty -PlanOnly
# Release 编译成功后覆盖同一路径，依赖先写、主 DLL 最后替换
& 'C:/Users/Administrator/.codex/skills/dalamud-plugin-dev/scripts/build-plugin.ps1' -Plugin AutoDuty
# worktree 构建仍部署到同一入口
& 'C:/Users/Administrator/.codex/skills/dalamud-plugin-dev/scripts/build-plugin.ps1' -Plugin AutoDuty -Project 'D:/UGit/AutoDuty-pr-p01/AutoDuty/AutoDuty.csproj'
```

有行为测试时先 `-BuildOnly`，运行项目适用的测试，通过后再常规调用部署。脚本不把编译成功当成游戏验证成功。文件占用/替换失败报告原路径和失败文件，不另换目录绕过。

AutoDuty 测试：设置 `AUTODUTY_TEST_DALAMUD` 为配置 DalamudLibPath，用配置 Dotnet 运行 `dotnet run --project tests/AutoDuty.Tests/AutoDuty.Tests.csproj -c Release -p:DalamudLibPath=<配置路径>`。不要用全局 `-p:Version=...` 改版号，它会传播到 ECommons 等引用项目；修改 AutoDuty.csproj 和现有开发显示版本。

交付首先提供固定 DLL 路径、版本和构建/测试结果。ZIP 仅用于用户要求的分享或归档，不作为新的开发入口，不要求用户每次重新注册。

## 热加载开发要求

事件、窗口、IPC、hook、任务队列在 Dispose 中释放，避免重载后旧实例继续控制角色。游戏对象访问和 UI 操作遵循对应 API 的线程约束。清理和失败恢复可重复执行。仅在游戏日志/UI 有证据时才称热加载成功；覆盖成功只代表文件更新。
