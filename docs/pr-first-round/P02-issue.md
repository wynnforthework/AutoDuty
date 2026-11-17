# [Bug] Route selection flickers and refreshes during browsing

### What happened?

Route controls appear and disappear during temporary player unavailability. Route browsing also loses list state when read-only file access invalidates the catalog.

These symptoms were reported while testing a downstream integration. A reproduction against the released upstream binary has not yet been recorded. This proposal is adapted independently to the upstream source below.

### Version Number

Source baseline: AutoDuty 0.0.0.399, upstream `master` at `ee4eced8`. Build environment: .NET 10, Dalamud API 15, Windows x64. The exact downstream binary version used for the original report was not preserved.

### Steps to reproduce the error

Browse a long route list, trigger temporary player unavailability, and repeatedly read/hash route files. Then create, edit, rename and delete a route file.

### Scope

AutoDuty/Windows/MainTab.cs, AutoDuty/Updater/FileHelper.cs and the framework refresh hook; plus isolated RouteCatalog.Tests.

### Proposed change

Keep the selector visible but disabled while unavailable; rebind the selected duty after catalog replacement. Watch file names/writes/sizes rather than access metadata, debounce events for 300 ms, and publish a fully built catalog from the framework tick in sorted order.

### Why change this / expected benefit

Read-only browsing should not invalidate the catalog, and file-watcher threads should not replace UI data. Genuine file changes must still refresh the catalog.

### Testing performed and results

Independent Release/x64 build from the common upstream base: passed, 0 errors. Full rebuild reports the same 70 warnings as an unmodified upstream rebuild; normalized warning messages add none.

dotnet run --project tests/RouteCatalog.Tests/RouteCatalog.Tests.csproj -c Release: 8 checks passed against the actual FileHelper source with mocked catalog/UI services and real Windows FileSystemWatcher events. Covers reads/hashes, deferred main-thread publication, burst write debounce, rename, creation and deletion.

All five first-round patches were also tested in all 120 merge orders (no conflicts, identical resulting tree). Every one of the 26 multi-patch subsets compiled successfully; each of the five single patches compiled independently. No application dependency on another first-round PR.

### Remaining validation

Not yet tested in the running game for this isolated branch. In-game readiness transitions, long-list scroll retention and an actual route export/import cycle.

### Relevant log output

No sanitized log from an isolated upstream-binary reproduction is attached.

### Other relevant plugins installed

Original exact plugin versions were not captured. vnavmesh/BossMod are relevant to navigation/combat validation; no exact versions are claimed here.

### Plugin config file

No personal configuration attached. No schema migration or new user setting is introduced by this patch.
