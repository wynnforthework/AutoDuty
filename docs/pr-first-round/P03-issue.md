# [Bug] Brayflox key interaction continues after receiving the key

### What happened?

In Brayflox Longstop, the key NPC can remain targetable after awarding the key, so interacting may repeat indefinitely. Dialogue completion can also be missed when shared UI edges are consumed by multiple polls.

These symptoms were reported while testing a downstream integration. A reproduction against the released upstream binary has not yet been recorded. This proposal is adapted independently to the upstream source below.

### Version Number

Source baseline: AutoDuty 0.0.0.399, upstream `master` at `ee4eced8`. Build environment: .NET 10, Dalamud API 15, Windows x64. The exact downstream binary version used for the original report was not preserved.

### Steps to reproduce the error

In territory 1041 interact with NPC 1004346 to receive item 2000521. Verify the interaction ends, then replay it with the key already present. Also exercise a normal door/NPC dialogue.

### Scope

InteractionCompletion helper and Interactable polling in ActionsManager; plus isolated Interaction.Tests.

### Proposed change

Recognize the key only for the matching territory/NPC/item combination. Track dialogue appearance and closure in a separate object for each interaction, and wait for casting to finish before considering dialogue completion.

### Why change this / expected benefit

Inventory reward evidence ends the key loop without depending on the NPC becoming untargetable. Per-interaction dialogue state prevents one poll from consuming another poll's completion signal.

### Testing performed and results

Independent Release/x64 build from the common upstream base: passed, 0 errors. Full rebuild reports the same 70 warnings as an unmodified upstream rebuild; normalized warning messages add none.

dotnet run --project tests/Interaction.Tests/Interaction.Tests.csproj -c Release: 10 checks passed against the actual helper. Covers missing/present key, wrong NPC/territory, no dialogue, open/closed dialogue, casting and independent interaction state.

All five first-round patches were also tested in all 120 merge orders (no conflicts, identical resulting tree). Every one of the 26 multi-patch subsets compiled successfully; each of the five single patches compiled independently. No application dependency on another first-round PR.

### Remaining validation

Not yet tested in the running game for this isolated branch. In-game key acquisition and rerun, normal doors, Talk/YesNo progression and casting interactions.

### Relevant log output

No sanitized log from an isolated upstream-binary reproduction is attached.

### Other relevant plugins installed

Original exact plugin versions were not captured. vnavmesh/BossMod are relevant to navigation/combat validation; no exact versions are claimed here.

### Plugin config file

No personal configuration attached. No schema migration or new user setting is introduced by this patch.
