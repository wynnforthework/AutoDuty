# Fix action editor width growth

The action editor grows wider while left open, especially with full-width arguments or notes. It should retain its chosen width.

Remove automatic window sizing, initialize a 760 x 620 window when it appears, set a 620 x 240 minimum, align the close button within available width, and balance the coordinate input width stack.

Scope: AutoDuty/Windows/BuildTab.cs only.

Why: Content width previously fed back into automatic window sizing. Stable bounds keep the editor usable while allowing manual resizing.

Validation: independent Release/x64 full build on `ee4eced8` (AutoDuty 0.0.0.399, Dalamud API 15, .NET 10), 0 errors, no added warning messages against the upstream full rebuild.

Native cimgui harness executed the actual constructor and Draw code extracted from this branch with mocked game services: 3 scales (1, 1.5, 2) x 3 widths (620, 760, 1000), 120 frames each. All 9 cases retained their selected width. Harness is local; it does not initialize the actual Dalamud WindowSystem.

Independent of the other first-round PRs. All 120 merge orders are conflict-free with an identical final tree; all 26 multi-PR subsets compile, as do the five standalone branches. Base is `master`, not another feature branch. No submodule URL/gitlink changes.

Draft pending in-game verification: In-game opening/reopening, save/delete controls, small screen layout and actual Dalamud font scaling. No running-game result is claimed for this isolated branch.

Fixes #643
