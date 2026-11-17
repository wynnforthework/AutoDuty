# [Bug] Interaction arrival and navigation corner tolerances are coupled

## What happened?

An interaction destination can be horizontally reachable on uneven ground while a strict 3D radius rejects it. A loose global path tolerance can also skip obstacle corners.

Expected behavior: the acceptance criteria below are met without requiring the other first-round patches.

This was reported during local fork testing. A reproduction on an unmodified upstream installation has not yet been recorded; the upstream source version below is a reference, not a claim of released-binary reproduction.

## Version Number

| Item | Version / revision |
|---|---|
| Upstream source baseline | AutoDuty `0.0.0.399`, tag at commit `ee4eced8` |
| Build where the behavior was reported | Local development fork; exact window-title version was not captured |
| Parent of the isolated patch | Upstream `ee4eced8` |
| Isolated upstream PR commit | `524d90ac`, branch `fix/navigation-waypoint-tolerance` |
| Build environment | Windows x64, .NET 10, Dalamud API 15 |

The upstream baseline version is a source reference, not a claim that the bug has been reproduced on the released `0.0.0.399` binary.

## Steps to reproduce the error

1. Use an Interactable route destination on uneven ground, within 2 m horizontally but outside a strict 2 m 3D sphere.
2. Observe whether navigation completes but the interaction arrival check rejects the position.
3. Repeat with a destination on another floor, outside the 4 m vertical guard; verify that it is not accepted as reached.
4. Run a route around a pillar after another action/plugin has changed the global path tolerance.
5. Run a precise MoveTo step and verify that the interaction radius does not broaden its arrival threshold.

## Why change this?

vnavmesh Path.Tolerance applies to intermediate corners. Interaction reach and corner-following precision require separate checks. This is not a claim to fix every stuck navigation case.

The reported symptom and intended behavior are distinguished from the automated checks below. The isolated branch has not yet been exercised in the running game.

## Scope of the proposed change

AutoDuty route movement and the pure RouteArrival helper; plus isolated RouteArrival.Tests.

| Area | Proposed change | Reason / expected benefit |
|---|---|---|
| Intermediate corners | Retain the upstream 0.25 m path tolerance and restore it during movement | Prevent a larger global value from discarding obstacle corners |
| Interaction arrival | Use a horizontal 2 m radius plus a 4 m vertical guard | Allow a reachable slope without accepting arbitrary floor differences |
| Other movement | Retain the upstream 0.25 m 3D arrival threshold | Preserve precise MoveTo behavior |
| Tests | Extract the pure RouteArrival predicate | Check slope, radius and vertical-boundary cases without game services |

This patch is based directly on upstream and introduces no configuration migration, unrelated development features or dependency on another first-round PR.

## What improves after the change?

- Obstacle corners retain a tight waypoint tolerance.
- Interaction reach is evaluated separately from corner precision.
- Slope and vertical guard behavior can be tested without broadening MoveTo arrival.

These are the intended acceptance criteria. Runtime results are listed separately below.

## Testing performed

| Check | How it was checked | Result |
|---|---|---|
| Independent branch build | Full Release/x64 rebuild of `fix/navigation-waypoint-tolerance` at `524d90ac`, based directly on `ee4eced8` | Passed: 0 errors, 70 warnings |
| Warning baseline | Compare normalized warning messages with a full rebuild of unmodified upstream `ee4eced8` | No added warning messages |
| Automated regression | Exercise linked production source with isolated service stubs where needed | Passed; method and limits described below |
| Merge order | All 120 orders of P01–P05 | No conflicts; identical final tree |
| Build independence | All 31 nonempty subsets of P01–P05 | Every subset compiled successfully |
| Combined regression | Run the four isolated regression projects again after merging P01–P05 | Passed |
| Post-fix in-game behavior | Not yet verified for this isolated branch | Pending |

dotnet run --project tests/RouteArrival.Tests/RouteArrival.Tests.csproj -c Release: 6 checks passed against the actual helper. Covers a slope, another floor, outside interaction radius and precise MoveTo boundaries.

The isolated-branch build result above is not borrowed from the integrated development branch. The automated results do not establish that the running game behaves correctly.

## Planned runtime verification

Before presenting the PR as fully validated:

1. Record the affected runtime version and reproduce the problem on the unmodified target version.
2. Build and load this isolated PR branch.
3. Repeat the reproduction steps above and check each intended improvement.
4. Verify the following specific cases: In-game obstacle routing, slopes, bridges/floors and interaction reach. The 4 m vertical guard needs real-map validation.
5. Record actual results and relevant logs, including failures; only then update the Draft readiness status.

## Relevant log output

No sanitized game log from an isolated upstream-binary reproduction is attached. Compiler and automated-test logs establish their respective results only, not the game-runtime fix. The local evidence index is `docs/pr-first-round/README.md`.

## Other relevant plugins installed

Exact versions were not captured for the original report. No exact external plugin versions are claimed. Actual runtime verification must record relevant vnavmesh/BossMod versions where navigation or combat is involved.

## Plugin config file

No personal configuration is attached. No configuration migration is introduced. If reproduction requires configuration, provide a minimal example with character identifiers and unrelated settings removed.

## Readiness for submission

Issue #649 and PR #650 are available for discussion. Independent build and automated checks have passed. The PR remains Draft pending the running-game checks above and has not been marked as fully game-tested.
