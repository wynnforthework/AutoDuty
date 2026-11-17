# Identify localized bosses and preserve confirmed defeat evidence

A Boss action can fail to identify its target on a localized client or wait for a boss killed before the Boss step. Conversely, disappearance or a wipe alone should not count as a kill.

Resolve route names through BNpcName, use localized identity before ranked/module fallback, and cache confirmed defeat evidence per territory/name/position. Observe death or a matching completed game objective; live candidates invalidate stale evidence. Reacquire objects during approach, wait for party combat to finish, and pause the same route step when completion/search/approach/engagement cannot be confirmed instead of treating timeout as success.

Scope: ObjectHelper boss matching, BossIdentity/BossProgress helpers, a partial AutoDuty observer, and Boss tasks in ActionsManager; plus isolated BossProgress.Tests.

Why: Boss identification and completion evidence must work across duties rather than rely on a dungeon-specific exception. Retaining confirmed evidence also lets an already defeated boss action complete after retracing the route. Only the existing upstream pause mechanism is used; the larger retry/recovery framework is not included.

Validation: independent Release/x64 full build on `ee4eced8` (AutoDuty 0.0.0.399, Dalamud API 15, .NET 10), 0 errors, no added warning messages against the upstream full rebuild.

dotnet run --project tests/BossProgress.Tests/BossProgress.Tests.csproj -c Release: all assertions passed against linked production helpers and the actual partial lifecycle source with mocked game services. Covers missing/early-killed/despawned bosses, phase disappearance, respawn precedence, wipe, encounter isolation, objective names/counts, fresh-duty reset, localized name matching and deferred pause despite upstream clearing pathAction. Real Lumina/object-table and NeoTaskManager timing require game verification.

Independent of the other first-round PRs. All 120 merge orders are conflict-free with an identical final tree; all 26 multi-PR subsets compile, as do the five standalone branches. Base is `master`, not another feature branch. No submodule URL/gitlink changes.

Draft pending in-game verification: In-game localized clients, BossMod/ranked fallback, early kills, corpses/objectives, phase transitions, wipes, fresh duty reset, timeout pause and Resume on the same step. No running-game result is claimed for this isolated branch.

Fixes #651
