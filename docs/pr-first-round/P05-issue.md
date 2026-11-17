# [Bug] Boss steps miss localized or already defeated bosses

### What happened?

A Boss action can fail to identify its target on a localized client or wait for a boss killed before the Boss step. Conversely, disappearance or a wipe alone should not count as a kill.

These symptoms were reported while testing a downstream integration. A reproduction against the released upstream binary has not yet been recorded. This proposal is adapted independently to the upstream source below.

### Version Number

Source baseline: AutoDuty 0.0.0.399, upstream `master` at `ee4eced8`. Build environment: .NET 10, Dalamud API 15, Windows x64. The exact downstream binary version used for the original report was not preserved.

### Steps to reproduce the error

Use an English-named Boss note on a localized client; kill a boss before reaching its Boss action. Also test a wipe, temporary phase disappearance, a later boss, an unnamed Boss action and a fresh run of the same duty.

### Scope

ObjectHelper boss matching, BossIdentity/BossProgress helpers, a partial AutoDuty observer, and Boss tasks in ActionsManager; plus isolated BossProgress.Tests.

### Proposed change

Resolve route names through BNpcName, use localized identity before ranked/module fallback, and cache confirmed defeat evidence per territory/name/position. Observe death or a matching completed game objective; live candidates invalidate stale evidence. Reacquire objects during approach, wait for party combat to finish, and pause the same route step when completion/search/approach/engagement cannot be confirmed instead of treating timeout as success.

### Why change this / expected benefit

Boss identification and completion evidence must work across duties rather than rely on a dungeon-specific exception. Retaining confirmed evidence also lets an already defeated boss action complete after retracing the route. Only the existing upstream pause mechanism is used; the larger retry/recovery framework is not included.

### Testing performed and results

Independent Release/x64 build from the common upstream base: passed, 0 errors. Full rebuild reports the same 70 warnings as an unmodified upstream rebuild; normalized warning messages add none.

dotnet run --project tests/BossProgress.Tests/BossProgress.Tests.csproj -c Release: all assertions passed against linked production helpers and the actual partial lifecycle source with mocked game services. Covers missing/early-killed/despawned bosses, phase disappearance, respawn precedence, wipe, encounter isolation, objective names/counts, fresh-duty reset, localized name matching and deferred pause despite upstream clearing pathAction. Real Lumina/object-table and NeoTaskManager timing require game verification.

All five first-round patches were also tested in all 120 merge orders (no conflicts, identical resulting tree). Every one of the 26 multi-patch subsets compiled successfully; each of the five single patches compiled independently. No application dependency on another first-round PR.

### Remaining validation

Not yet tested in the running game for this isolated branch. In-game localized clients, BossMod/ranked fallback, early kills, corpses/objectives, phase transitions, wipes, fresh duty reset, timeout pause and Resume on the same step.

### Relevant log output

No sanitized log from an isolated upstream-binary reproduction is attached.

### Other relevant plugins installed

Original exact plugin versions were not captured. vnavmesh/BossMod are relevant to navigation/combat validation; no exact versions are claimed here.

### Plugin config file

No personal configuration attached. No schema migration or new user setting is introduced by this patch.
