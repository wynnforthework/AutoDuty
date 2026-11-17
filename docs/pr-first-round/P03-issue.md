# [Bug] Brayflox key interaction continues after receiving the key

## What happened?

In Brayflox Longstop, the key NPC can remain targetable after awarding the key, so interacting may repeat indefinitely. Dialogue completion can also be missed when shared UI edges are consumed by multiple polls.

Expected behavior: the acceptance criteria below are met without requiring the other first-round patches.

This was reported during local fork testing. A reproduction on an unmodified upstream installation has not yet been recorded; the upstream source version below is a reference, not a claim of released-binary reproduction.

## Version Number

| Item | Version / revision |
|---|---|
| Upstream source baseline | AutoDuty `0.0.0.399`, tag at commit `ee4eced8` |
| Build where the behavior was reported | Local development fork; exact window-title version was not captured |
| Parent of the isolated patch | Upstream `ee4eced8` |
| Isolated upstream PR commit | `1031bc41`, branch `fix/brayflox-key-interaction` |
| Build environment | Windows x64, .NET 10, Dalamud API 15 |

The upstream baseline version is a source reference, not a claim that the bug has been reproduced on the released `0.0.0.399` binary.

## Steps to reproduce the error

1. Enter Brayflox Longstop (territory 1041) using a route with an Interactable step for NPC 1004346.
2. Interact with the NPC to receive key item 2000521.
3. Observe whether the action continues to interact after the key has been received and the NPC remains targetable.
4. Replay the same step while already holding the key; verify that it can complete without further interaction.
5. Test an ordinary NPC/door with Talk or YesNo dialogue and an interaction involving casting.

## Why change this?

Inventory reward evidence ends the key loop without depending on the NPC becoming untargetable. Per-interaction dialogue state prevents one poll from consuming another poll's completion signal.

The reported symptom and intended behavior are distinguished from the automated checks below. The isolated branch has not yet been exercised in the running game.

## Scope of the proposed change

InteractionCompletion helper and Interactable polling in ActionsManager; plus isolated Interaction.Tests.

| Area | Proposed change | Reason / expected benefit |
|---|---|---|
| Reward evidence | Recognize item 2000521 only for territory 1041 / NPC 1004346 | End the key loop when its reward is already held |
| Dialogue lifecycle | Keep dialogue-seen/closed state separately for each interaction | Avoid repeated consumption of shared UI completion edges |
| Casting | Defer dialogue completion while casting | Do not prematurely finish a casting interaction |
| Other interactions | Require the exact duty/NPC/item combination for the reward shortcut | Do not apply the Brayflox key evidence to other NPCs or doors |

This patch is based directly on upstream and introduces no configuration migration, unrelated development features or dependency on another first-round PR.

## What improves after the change?

- The key interaction ends after the player has the matching reward.
- Dialogue completion remains available across repeated polls.
- State is isolated between interactions and casting still waits appropriately.

These are the intended acceptance criteria. Runtime results are listed separately below.

## Testing performed

| Check | How it was checked | Result |
|---|---|---|
| Independent branch build | Full Release/x64 rebuild of `fix/brayflox-key-interaction` at `1031bc41`, based directly on `ee4eced8` | Passed: 0 errors, 70 warnings |
| Warning baseline | Compare normalized warning messages with a full rebuild of unmodified upstream `ee4eced8` | No added warning messages |
| Automated regression | Exercise linked production source with isolated service stubs where needed | Passed; method and limits described below |
| Merge order | All 120 orders of P01–P05 | No conflicts; identical final tree |
| Build independence | All 31 nonempty subsets of P01–P05 | Every subset compiled successfully |
| Combined regression | Run the four isolated regression projects again after merging P01–P05 | Passed |
| Post-fix in-game behavior | Not yet verified for this isolated branch | Pending |

dotnet run --project tests/Interaction.Tests/Interaction.Tests.csproj -c Release: 10 checks passed against the actual helper. Covers missing/present key, wrong NPC/territory, no dialogue, open/closed dialogue, casting and independent interaction state.

The isolated-branch build result above is not borrowed from the integrated development branch. The automated results do not establish that the running game behaves correctly.

## Planned runtime verification

Before presenting the PR as fully validated:

1. Record the affected runtime version and reproduce the problem on the unmodified target version.
2. Build and load this isolated PR branch.
3. Repeat the reproduction steps above and check each intended improvement.
4. Verify the following specific cases: In-game key acquisition and rerun, normal doors, Talk/YesNo progression and casting interactions.
5. Record actual results and relevant logs, including failures; only then update the Draft readiness status.

## Relevant log output

No sanitized game log from an isolated upstream-binary reproduction is attached. Compiler and automated-test logs establish their respective results only, not the game-runtime fix. The local evidence index is `docs/pr-first-round/README.md`.

## Other relevant plugins installed

Exact versions were not captured for the original report. No exact external plugin versions are claimed. Actual runtime verification must record relevant vnavmesh/BossMod versions where navigation or combat is involved.

## Plugin config file

No personal configuration is attached. No configuration migration is introduced. If reproduction requires configuration, provide a minimal example with character identifiers and unrelated settings removed.

## Readiness for submission

Issue #647 and PR #648 are available for discussion. Independent build and automated checks have passed. The PR remains Draft pending the running-game checks above and has not been marked as fully game-tested.
