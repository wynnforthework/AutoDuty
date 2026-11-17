# Separate waypoint tolerance from interaction arrival range

An interaction destination can be horizontally reachable on uneven ground while a strict 3D radius rejects it. A loose global path tolerance can also skip obstacle corners.

Retain upstream's 0.25 m path tolerance and restore it during movement so other actions/plugins cannot leave a larger corner tolerance. Evaluate Interactable arrival with a 2 m horizontal radius and a 4 m vertical guard; keep other movement arrival at the upstream 0.25 m 3D threshold.

Scope: AutoDuty route movement and the pure RouteArrival helper; plus isolated RouteArrival.Tests.

Why: vnavmesh Path.Tolerance applies to intermediate corners. Interaction reach and corner-following precision require separate checks. This is not a claim to fix every stuck navigation case.

Validation: independent Release/x64 full build on `ee4eced8` (AutoDuty 0.0.0.399, Dalamud API 15, .NET 10), 0 errors, no added warning messages against the upstream full rebuild.

dotnet run --project tests/RouteArrival.Tests/RouteArrival.Tests.csproj -c Release: 6 checks passed against the actual helper. Covers a slope, another floor, outside interaction radius and precise MoveTo boundaries.

Independent of the other first-round PRs. All 120 merge orders are conflict-free with an identical final tree; all 26 multi-PR subsets compile, as do the five standalone branches. Base is `master`, not another feature branch. No submodule URL/gitlink changes.

Draft pending in-game verification: In-game obstacle routing, slopes, bridges/floors and interaction reach. The 4 m vertical guard needs real-map validation. No running-game result is claimed for this isolated branch.

Fixes #649
