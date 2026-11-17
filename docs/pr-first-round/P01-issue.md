# [Bug] Action editor grows wider while open

### What happened?

The action editor grows wider while left open, especially with full-width arguments or notes. It should retain its chosen width.

These symptoms were reported while testing a downstream integration. A reproduction against the released upstream binary has not yet been recorded. This proposal is adapted independently to the upstream source below.

### Version Number

Source baseline: AutoDuty 0.0.0.399, upstream `master` at `ee4eced8`. Build environment: .NET 10, Dalamud API 15, Windows x64. The exact downstream binary version used for the original report was not preserved.

### Steps to reproduce the error

Open an action with long arguments and a long note; leave the editor open, resize it, then close and reopen it. Repeat at different UI scales.

### Scope

AutoDuty/Windows/BuildTab.cs only.

### Proposed change

Remove automatic window sizing, initialize a 760 x 620 window when it appears, set a 620 x 240 minimum, align the close button within available width, and balance the coordinate input width stack.

### Why change this / expected benefit

Content width previously fed back into automatic window sizing. Stable bounds keep the editor usable while allowing manual resizing.

### Testing performed and results

Independent Release/x64 build from the common upstream base: passed, 0 errors. Full rebuild reports the same 70 warnings as an unmodified upstream rebuild; normalized warning messages add none.

Native cimgui harness executed the actual constructor and Draw code extracted from this branch with mocked game services: 3 scales (1, 1.5, 2) x 3 widths (620, 760, 1000), 120 frames each. All 9 cases retained their selected width. Harness is local; it does not initialize the actual Dalamud WindowSystem.

All five first-round patches were also tested in all 120 merge orders (no conflicts, identical resulting tree). Every one of the 26 multi-patch subsets compiled successfully; each of the five single patches compiled independently. No application dependency on another first-round PR.

### Remaining validation

Not yet tested in the running game for this isolated branch. In-game opening/reopening, save/delete controls, small screen layout and actual Dalamud font scaling.

### Relevant log output

No sanitized log from an isolated upstream-binary reproduction is attached.

### Other relevant plugins installed

Original exact plugin versions were not captured. vnavmesh/BossMod are relevant to navigation/combat validation; no exact versions are claimed here.

### Plugin config file

No personal configuration attached. No schema migration or new user setting is introduced by this patch.
