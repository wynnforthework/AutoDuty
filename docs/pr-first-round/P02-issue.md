# [Bug] Route selection flickers and refreshes during browsing

## What happened?

Route controls appear and disappear during temporary player unavailability. Route browsing also loses list state when read-only file access invalidates the catalog.

Expected behavior: the acceptance criteria below are met without requiring the other first-round patches.

This was reported during local fork testing. A reproduction on an unmodified upstream installation has not yet been recorded; the upstream source version below is a reference, not a claim of released-binary reproduction.

## Version Number

| Item | Version / revision |
|---|---|
| Upstream source baseline | AutoDuty `0.0.0.399`, tag at commit `ee4eced8` |
| Build where the behavior was reported | Local development fork; exact window-title version was not captured |
| Parent of the isolated patch | Upstream `ee4eced8` |
| Isolated upstream PR commit | `b131082c`, branch `fix/stable-path-selector` |
| Build environment | Windows x64, .NET 10, Dalamud API 15 |

The upstream baseline version is a source reference, not a claim that the bug has been reproduced on the released `0.0.0.399` binary.

## Steps to reproduce the error

1. Open AutoDuty outside a duty and select a route with several alternatives.
2. Open the Build editor and browse the route list; record the current selection and scroll position.
3. Trigger a temporary player-readiness transition, such as a loading screen. Observe whether the route selector disappears and reappears.
4. Read/hash the same route files without modifying them. Observe whether browsing state resets.
5. Create, edit, rename and delete a route file; verify that genuine changes still refresh the catalog.

## Why change this?

Read-only browsing should not invalidate the catalog, and file-watcher threads should not replace UI data. Genuine file changes must still refresh the catalog.

The reported symptom and intended behavior are distinguished from the automated checks below. The isolated branch has not yet been exercised in the running game.

## Scope of the proposed change

AutoDuty/Windows/MainTab.cs, AutoDuty/Updater/FileHelper.cs and the framework refresh hook; plus isolated RouteCatalog.Tests.

| Area | Proposed change | Reason / expected benefit |
|---|---|---|
| Readiness layout | Keep route selection visible but disabled while the player is unavailable | Prevent appearance/disappearance of the selector row |
| Catalog selection | Rebind the selected duty after catalog publication | Preserve a valid selection when a real refresh occurs |
| Watcher filters | Watch FileName, LastWrite and Size rather than read-access metadata | Avoid invalidation caused by read-only browsing |
| Refresh lifecycle | Debounce for 300 ms and publish a completed, sorted catalog on the framework tick | Avoid background-thread UI replacement and repeated burst refreshes |

This patch is based directly on upstream and introduces no configuration migration, unrelated development features or dependency on another first-round PR.

## What improves after the change?

- The route selector keeps its place during temporary readiness changes.
- Read-only route browsing does not rebuild the catalog.
- Real file changes are still discovered and published on the framework thread.

These are the intended acceptance criteria. Runtime results are listed separately below.

## Testing performed

| Check | How it was checked | Result |
|---|---|---|
| Independent branch build | Full Release/x64 rebuild of `fix/stable-path-selector` at `b131082c`, based directly on `ee4eced8` | Passed: 0 errors, 70 warnings |
| Warning baseline | Compare normalized warning messages with a full rebuild of unmodified upstream `ee4eced8` | No added warning messages |
| Automated regression | Exercise linked production source with isolated service stubs where needed | Passed; method and limits described below |
| Merge order | All 120 orders of P01–P05 | No conflicts; identical final tree |
| Build independence | All 31 nonempty subsets of P01–P05 | Every subset compiled successfully |
| Combined regression | Run the four isolated regression projects again after merging P01–P05 | Passed |
| Post-fix in-game behavior | Not yet verified for this isolated branch | Pending |

dotnet run --project tests/RouteCatalog.Tests/RouteCatalog.Tests.csproj -c Release: 8 checks passed against the actual FileHelper source with mocked catalog/UI services and real Windows FileSystemWatcher events. Covers reads/hashes, deferred main-thread publication, burst write debounce, rename, creation and deletion.

The isolated-branch build result above is not borrowed from the integrated development branch. The automated results do not establish that the running game behaves correctly.

## Planned runtime verification

Before presenting the PR as fully validated:

1. Record the affected runtime version and reproduce the problem on the unmodified target version.
2. Build and load this isolated PR branch.
3. Repeat the reproduction steps above and check each intended improvement.
4. Verify the following specific cases: In-game readiness transitions, long-list scroll retention and an actual route export/import cycle.
5. Record actual results and relevant logs, including failures; only then update the Draft readiness status.

## Relevant log output

No sanitized game log from an isolated upstream-binary reproduction is attached. Compiler and automated-test logs establish their respective results only, not the game-runtime fix. The local evidence index is `docs/pr-first-round/README.md`.

## Other relevant plugins installed

Exact versions were not captured for the original report. No exact external plugin versions are claimed. Actual runtime verification must record relevant vnavmesh/BossMod versions where navigation or combat is involved.

## Plugin config file

No personal configuration is attached. No configuration migration is introduced. If reproduction requires configuration, provide a minimal example with character identifiers and unrelated settings removed.

## Readiness for submission

Issue #645 and PR #646 are available for discussion. Independent build and automated checks have passed. The PR remains Draft pending the running-game checks above and has not been marked as fully game-tested.
