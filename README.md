# AutoDuty — personal fork

[English](README.md) · [简体中文](README.zh-CN.md)

<img align="right" width="150" height="150" src="logo.png">

AutoDuty is a Dalamud plugin for creating and following FFXIV duty routes, including repeat runs with Duty Support, Trusts, or Squadrons. This repository maintains an experimental, AI-assisted fork of [erdelf/AutoDuty](https://github.com/erdelf/AutoDuty) on **`dev_01`**. It is independently maintained and is not an upstream release.

Current test version: **0.0.0.407**, targeting Dalamud API 15. The latest code passed a Release build with zero errors and 33 offline test groups. **This version has not been tested in game.** These results do not establish that every duty, transition, or an unattended level 16–100 run works.

## What this fork adds

| Area | Behavior |
| --- | --- |
| Route editing | Multiple route versions per duty, protected originals, editable copies, role-specific routes, step highlighting, deletion through the editor, and saved changes. |
| Execution controls | Pause/resume, retry, skip, and restart from a selected step; an independent card view shows step details, actions, and progress. Manual intervention may invalidate later prerequisites. |
| Recording and sharing | Record movement and supported events, supplement other actions manually, and share route configurations through import/export. Recording does not infer every game mechanic. |
| Leveling | Include or skip duties, select role routes, inspect configured duties, and reselect eligible duties as the job levels. |
| Measurements | Session duration, duty runs, XP and levels gained; route completion displays the highest recorded completion rather than an average. A clean full clear reaches 100%; recovered automatic retries alone do not count as manual intervention. |
| Execution reliability | Game readiness gates, suspended action clocks, navigation diagnostics, session lifecycle fixes, and step-specific navigation/combat control. |

## Recent reliability work

Area transitions use separate checks for suspected movement, travel ending, destination confirmation, and completion of the owning step. Scripts can describe a destination territory, horizontal range, and height range. Ground checks and asynchronous, rate-limited vnavmesh path queries provide additional evidence. Unknown displacements stop navigation and pause when completion cannot be established. Older routes may need explicit destination configuration; this does not automatically fix every route.

Combat control belongs to the current step: MoveTo disables managed BossMod movement and rotation; StopForCombat false disables BossMod movement while enabling rotation; StopForCombat true stops route navigation and enables combat movement and rotation.

Boss discovery uses NPC Rank, BossMod module information, and tracked entity identity. The script-note/name lookup introduced by an earlier patch has been removed. Encounter completion requires evidence; an object disappearing or combat ending alone does not prove a kill. Bosses without usable identification evidence remain a limitation.

See the [change and maintenance record](docs/上游PR拆分与提交建议.md), [transition design](docs/区域转换判定与录制.md), [Boss audit](docs/Boss识别本地审查.md), and [usage/acceptance guide](docs/挂机功能使用与验收.md) (Chinese).

The official-route download progress and clean-refresh behavior proposed in [upstream issue #654](https://github.com/erdelf/AutoDuty/issues/654) is **not implemented as a complete feature** in this fork.

## Build and load this fork

The upstream installer repository installs upstream AutoDuty. To test this fork, build the `dev_01` sources and load the result as a Dalamud development plugin.

```powershell
git clone --recurse-submodules --branch dev_01 https://github.com/wynnforthework/AutoDuty.git
cd AutoDuty
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -PlanOnly
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

The checked-in build workflow targets Windows x64, .NET SDK 10 and Dalamud SDK/API 15. Read the [development skill](skills/dalamud-plugin-dev/SKILL.md) and adapt its [local environment configuration](skills/dalamud-plugin-dev/references/local-environment.json) to your machine, including the Dalamud dependency and deployment paths. The wrapper prefers the installed skill under `%USERPROFILE%/.codex/skills/dalamud-plugin-dev` when present, otherwise it uses the repository copy. `-PlanOnly` checks the plan; `-BuildOnly` builds without deployment.

The maintainer's fixed hot-reload entry is `D:/UGit/AutoDuty/artifacts/AutoDuty-session-stop-fix/AutoDuty.dll`. Add the configured DLL once to Dalamud's development plugin locations, then overwrite the same location on subsequent builds. Do not add a new plugin entry for each version or commit. Open settings with `/ad`. Do not load upstream AutoDuty and this fork simultaneously.

## Integrations

- [vnavmesh](https://github.com/awgil/ffxiv_navmesh): route navigation and mesh queries.
- [BossMod](https://github.com/awgil/ffxiv_bossmod) or BossModReborn: encounter support and combat movement.
- [Rotation Solver Reborn](https://github.com/FFXIV-CombatReborn/RotationSolverReborn) or [Wrath Combo](https://github.com/PunishXIV/WrathCombo): rotation execution. Availability and compatibility depend on the client and plugin versions.
- Optional integrations include Gearsetter and AutoRetainer; inherited automation also includes repairs, loot handling, and between-run actions.

## Reports and contribution policy

Report fork-specific problems in [this repository](https://github.com/wynnforthework/AutoDuty/issues). Include the plugin/client version and region, duty, route filename/version, **zero-based** step index, relevant logs, and whether manual retry or intervention changed the outcome. Do not ask upstream to support this fork's changes.

This fork contains AI-assisted code. Following upstream's rejection of AI code and the owner's explicit policy, no AI-assisted code PRs will be prepared or submitted to `erdelf/AutoDuty`, including PRs opened through this fork. The local push guard blocks direct upstream pushes but cannot prevent browser/API PR creation; see [AGENTS.md](AGENTS.md). Personal fork maintenance continues here. Historical PR preparation documents are archived records, not an active submission plan.

## Attribution and licensing

AutoDuty originates from [erdelf/AutoDuty](https://github.com/erdelf/AutoDuty); original authorship and notices are retained. This fork does not grant a new license to the original code.

_Unlicensed code, which follows [default copyright law](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository#choosing-the-right-license)._
