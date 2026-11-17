# [Bug] Interaction arrival and navigation corner tolerances are coupled

### What happened?

An interaction destination can be horizontally reachable on uneven ground while a strict 3D radius rejects it. A loose global path tolerance can also skip obstacle corners.

These symptoms were reported while testing a downstream integration. A reproduction against the released upstream binary has not yet been recorded. This proposal is adapted independently to the upstream source below.

### Version Number

Source baseline: AutoDuty 0.0.0.399, upstream `master` at `ee4eced8`. Build environment: .NET 10, Dalamud API 15, Windows x64. The exact downstream binary version used for the original report was not preserved.

### Steps to reproduce the error

Use an Interactable destination on a small slope; compare it with a destination on another floor. Exercise a route around an obstacle, and a precise MoveTo action.

### Scope

AutoDuty route movement and the pure RouteArrival helper; plus isolated RouteArrival.Tests.

### Proposed change

Retain upstream's 0.25 m path tolerance and restore it during movement so other actions/plugins cannot leave a larger corner tolerance. Evaluate Interactable arrival with a 2 m horizontal radius and a 4 m vertical guard; keep other movement arrival at the upstream 0.25 m 3D threshold.

### Why change this / expected benefit

vnavmesh Path.Tolerance applies to intermediate corners. Interaction reach and corner-following precision require separate checks. This is not a claim to fix every stuck navigation case.

### Testing performed and results

Independent Release/x64 build from the common upstream base: passed, 0 errors. Full rebuild reports the same 70 warnings as an unmodified upstream rebuild; normalized warning messages add none.

dotnet run --project tests/RouteArrival.Tests/RouteArrival.Tests.csproj -c Release: 6 checks passed against the actual helper. Covers a slope, another floor, outside interaction radius and precise MoveTo boundaries.

All five first-round patches were also tested in all 120 merge orders (no conflicts, identical resulting tree). Every one of the 26 multi-patch subsets compiled successfully; each of the five single patches compiled independently. No application dependency on another first-round PR.

### Remaining validation

Not yet tested in the running game for this isolated branch. In-game obstacle routing, slopes, bridges/floors and interaction reach. The 4 m vertical guard needs real-map validation.

### Relevant log output

No sanitized log from an isolated upstream-binary reproduction is attached.

### Other relevant plugins installed

Original exact plugin versions were not captured. vnavmesh/BossMod are relevant to navigation/combat validation; no exact versions are claimed here.

### Plugin config file

No personal configuration attached. No schema migration or new user setting is introduced by this patch.
