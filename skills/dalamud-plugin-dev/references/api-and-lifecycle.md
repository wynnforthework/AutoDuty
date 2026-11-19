# API 与依赖查阅

## 可信入口

- 官方开发文档：<https://dalamud.dev/category/plugin-development>
- 官方 API：<https://dalamud.dev/api/>
- 官方模板：<https://github.com/goatcorp/SamplePlugin>
- Dalamud 实现：<https://github.com/goatcorp/Dalamud>
- 原生客户端接口：<https://github.com/aers/FFXIVClientStructs>；本机源码路径见 local-environment.json。

先选具体服务/类型页面，再核对成员签名、线程要求和生命周期。在线文档可能比国服本机新，本机 Hooks/dev 程序集和 XML 才是实际编译目标。本机 ClientStructs 源码也可能不同步，需核对当前 DLL/XML，不能把本地 checkout 当成匹配证明。客户端结构不等于 Dalamud 稳定托管 API。

## 快速定位

```powershell
$hooks = 'C:/Users/Administrator/AppData/Roaming/XIVLauncherCN/addon/Hooks/dev'
rg -n -A 12 'IDalamudPluginInterface|IFramework|ICommandManager|IClientState|IPartyFinderGui|UiBuilder' "$hooks/Dalamud.xml"
rg -n -A 12 'AgentLookingForGroup|AgentModule' "$hooks/FFXIVClientStructs.xml"
```

按任务缩小搜索词，不输出整个 XML。签名细节不足时查源码或反射当前 DLL 的参数，不执行游戏函数。不为查询接口重复下载 Dalamud。

## 新插件落地

1. 读取官方 SamplePlugin 的 csproj、入口类和 manifest；对照本机 API 等级选兼容版本。保持 AssemblyName、InternalName 和配置一致。
2. 查服务接口后实现注入、命令注册、UiBuilder 事件和 WindowSystem；Dispose 对应解除。使用本机 ImGui bindings，不混用不同 API 代程序集。
3. 优先托管 API；需要 Agent/hook 时记录缺少何种托管能力，核对本机结构布局、函数签名和失败条件。窗口能显示不代表副本中相应服务器功能可用；区分 API 证据和游戏实测。
4. 在配置 Plugins 中增加固定 Project、InternalName、DeployDirectory。首次注册一次，之后只覆盖。

## 构建和热加载边界

- 不把 Hooks/dev 内容复制到插件目录；由 SDK 引用游戏程序集。实际输出的插件依赖、manifest 和资源需一起部署。
- 构建成功后部署，依赖和资源先写，主 DLL 最后原子替换，减少 watcher 读到半成品；整个依赖集合不具备事务性。
- 自动重载依赖该入口 AutomaticReloading。文件更新但游戏未重载时，先核对注册路径、开关和日志，不新建版本目录。
- 多个旧入口启用时只更新约定的一个。不要擅自删除其他目录或配置；确有重复加载时通过游戏 UI 定位处理，不把历史清理作为每次构建步骤。
- 重载失败先查 Dispose、引用和日志，不为逃避占用或旧程序集问题换插件身份。
