# Keep route selection stable across readiness and catalog refreshes

Route controls appear and disappear during temporary player unavailability. Route browsing also loses list state when read-only file access invalidates the catalog.

Keep the selector visible but disabled while unavailable; rebind the selected duty after catalog replacement. Watch file names/writes/sizes rather than access metadata, debounce events for 300 ms, and publish a fully built catalog from the framework tick in sorted order.

Scope: AutoDuty/Windows/MainTab.cs, AutoDuty/Updater/FileHelper.cs and the framework refresh hook; plus isolated RouteCatalog.Tests.

Why: Read-only browsing should not invalidate the catalog, and file-watcher threads should not replace UI data. Genuine file changes must still refresh the catalog.

Validation: independent Release/x64 full build on `ee4eced8` (AutoDuty 0.0.0.399, Dalamud API 15, .NET 10), 0 errors, no added warning messages against the upstream full rebuild.

dotnet run --project tests/RouteCatalog.Tests/RouteCatalog.Tests.csproj -c Release: 8 checks passed against the actual FileHelper source with mocked catalog/UI services and real Windows FileSystemWatcher events. Covers reads/hashes, deferred main-thread publication, burst write debounce, rename, creation and deletion.

Independent of the other first-round PRs. All 120 merge orders are conflict-free with an identical final tree; all 26 multi-PR subsets compile, as do the five standalone branches. Base is `master`, not another feature branch. No submodule URL/gitlink changes.

Draft pending in-game verification: In-game readiness transitions, long-list scroll retention and an actual route export/import cycle. No running-game result is claimed for this isolated branch.

Fixes #645
