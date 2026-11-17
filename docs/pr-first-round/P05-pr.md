# [Fix] Identify localized bosses and preserve confirmed defeat evidence

## Problem and resulting behavior

A Boss action can fail to identify its target on a localized client or wait for a boss killed before the Boss step. Conversely, disappearance or a wipe alone should not count as a kill.

Expected behavior: the acceptance criteria below are met without requiring the other first-round patches.

This was reported during local fork testing. A reproduction on an unmodified upstream installation has not yet been recorded; the upstream source version below is a reference, not a claim of released-binary reproduction.

## Version and source baseline

| Item | Version / revision |
|---|---|
| Upstream source baseline | AutoDuty `0.0.0.399`, tag at commit `ee4eced8` |
| Build where the behavior was reported | Local development fork; exact window-title version was not captured |
| Parent of the isolated patch | Upstream `ee4eced8` |
| Isolated upstream PR commit | `9f3f13a5`, branch `fix/boss-identification-and-completion` |
| Build environment | Windows x64, .NET 10, Dalamud API 15 |

The upstream baseline version is a source reference, not a claim that the bug has been reproduced on the released `0.0.0.399` binary.

## Reproduction / before-and-after verification

1. On a localized client, select a route with an English boss name in the Boss step note.
2. Fight the boss before the route reaches that Boss step; then allow execution to reach it.
3. Observe whether the action waits for an already defeated boss or selects an unrelated nearby enemy.
4. Repeat after a wipe, a temporary phase disappearance and a later boss encounter; these must not inherit unconfirmed completion.
5. Test an unnamed Boss step, a fresh run of the same duty, and a search/approach/engagement timeout.
6. Verify that a timeout pauses the same step and Resume retries that step instead of advancing.

## Why change this?

Boss identification and completion evidence must work across duties rather than rely on a dungeon-specific exception. Retaining confirmed evidence also lets an already defeated boss action complete after retracing the route. Only the existing upstream pause mechanism is used; the larger retry/recovery framework is not included.

The reported symptom and intended behavior are distinguished from the automated checks below. The isolated branch has not yet been exercised in the running game.

## Scope and implementation

ObjectHelper boss matching, BossIdentity/BossProgress helpers, a partial AutoDuty observer, and Boss tasks in ActionsManager; plus isolated BossProgress.Tests.

| Area | Proposed change | Reason / expected benefit |
|---|---|---|
| Identity | Resolve route boss names through BNpcName and localized names before existing ranked/module fallback | Match the intended boss across supported client languages |
| Object lifetime | Reacquire the boss from the current object table during approach | Avoid relying on a despawned object wrapper |
| Completion evidence | Observe a matching death or a matching completed game objective | Handle bosses killed before their route action |
| Evidence isolation | Key evidence by territory, note and route position; live candidates invalidate stale evidence | Prevent unrelated encounters, respawns and new duties from inheriting a kill |
| Combat completion | Wait for party combat to finish and require completion evidence | Do not treat disappearance or leaving combat alone as victory |
| Unconfirmed encounter | Pause the existing route step through upstream pause support after timeout | Prevent queue timeout from silently advancing the route; no P06 dependency |

This patch is based directly on upstream and introduces no configuration migration, unrelated development features or dependency on another first-round PR.

## What improves after the change?

- Localized and named Boss steps select the intended encounter.
- A confirmed early kill can satisfy its later Boss action.
- Missing objects, wipes and temporary disappearances do not automatically count as victory.
- An unconfirmed encounter pauses the current step instead of silently advancing.

These are the intended acceptance criteria. Runtime results are listed separately below.

## Testing performed

| Check | How it was checked | Result |
|---|---|---|
| Independent branch build | Full Release/x64 rebuild of `fix/boss-identification-and-completion` at `9f3f13a5`, based directly on `ee4eced8` | Passed: 0 errors, 70 warnings |
| Warning baseline | Compare normalized warning messages with a full rebuild of unmodified upstream `ee4eced8` | No added warning messages |
| Automated regression | Exercise linked production source with isolated service stubs where needed | Passed; method and limits described below |
| Merge order | All 120 orders of P01–P05 | No conflicts; identical final tree |
| Build independence | All 31 nonempty subsets of P01–P05 | Every subset compiled successfully |
| Combined regression | Run the four isolated regression projects again after merging P01–P05 | Passed |
| Post-fix in-game behavior | Not yet verified for this isolated branch | Pending |

dotnet run --project tests/BossProgress.Tests/BossProgress.Tests.csproj -c Release: all assertions passed against linked production helpers and the actual partial lifecycle source with mocked game services. Covers missing/early-killed/despawned bosses, phase disappearance, respawn precedence, wipe, encounter isolation, objective names/counts, fresh-duty reset, localized name matching and deferred pause despite upstream clearing pathAction. Real Lumina/object-table and NeoTaskManager timing require game verification.

The isolated-branch build result above is not borrowed from the integrated development branch. The automated results do not establish that the running game behaves correctly.

## Planned runtime verification

Before presenting the PR as fully validated:

1. Record the affected runtime version and reproduce the problem on the unmodified target version.
2. Build and load this isolated PR branch.
3. Repeat the reproduction steps above and check each intended improvement.
4. Verify the following specific cases: In-game localized clients, BossMod/ranked fallback, early kills, corpses/objectives, phase transitions, wipes, fresh duty reset, timeout pause and Resume on the same step.
5. Record actual results and relevant logs, including failures; only then update the Draft readiness status.

## Relevant log output

No sanitized game log from an isolated upstream-binary reproduction is attached. Compiler and automated-test logs establish their respective results only, not the game-runtime fix. The local evidence index is `docs/pr-first-round/README.md`.

## Other relevant plugins installed

Exact versions were not captured for the original report. No exact external plugin versions are claimed. Actual runtime verification must record relevant vnavmesh/BossMod versions where navigation or combat is involved.

## Plugin config file

No personal configuration is attached. No configuration migration is introduced. If reproduction requires configuration, provide a minimal example with character identifiers and unrelated settings removed.

## Validation status

This PR is available for discussion. Independent build and automated checks have passed. The PR remains Draft pending the running-game checks above and has not been marked as fully game-tested.
