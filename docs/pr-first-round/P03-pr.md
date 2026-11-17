# Finish Brayflox key interactions after receiving the reward

In Brayflox Longstop, the key NPC can remain targetable after awarding the key, so interacting may repeat indefinitely. Dialogue completion can also be missed when shared UI edges are consumed by multiple polls.

Recognize the key only for the matching territory/NPC/item combination. Track dialogue appearance and closure in a separate object for each interaction, and wait for casting to finish before considering dialogue completion.

Scope: InteractionCompletion helper and Interactable polling in ActionsManager; plus isolated Interaction.Tests.

Why: Inventory reward evidence ends the key loop without depending on the NPC becoming untargetable. Per-interaction dialogue state prevents one poll from consuming another poll's completion signal.

Validation: independent Release/x64 full build on `ee4eced8` (AutoDuty 0.0.0.399, Dalamud API 15, .NET 10), 0 errors, no added warning messages against the upstream full rebuild.

dotnet run --project tests/Interaction.Tests/Interaction.Tests.csproj -c Release: 10 checks passed against the actual helper. Covers missing/present key, wrong NPC/territory, no dialogue, open/closed dialogue, casting and independent interaction state.

Independent of the other first-round PRs. All 120 merge orders are conflict-free with an identical final tree; all 26 multi-PR subsets compile, as do the five standalone branches. Base is `master`, not another feature branch. No submodule URL/gitlink changes.

Draft pending in-game verification: In-game key acquisition and rerun, normal doors, Talk/YesNo progression and casting interactions. No running-game result is claimed for this isolated branch.

Fixes #647
