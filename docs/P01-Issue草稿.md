# [Bug] Action editor keeps expanding horizontally while open

> Draft for review; not published. Follows the upstream Bug Report fields and adds the proposed scope, rationale, and validation evidence.

## What happened?

When opening the Add/Edit Action window for a route step from the Build tab, the window gradually becomes wider without the user resizing it. It eventually occupies the screen width, obstructing the game view and making step editing difficult.

Expected behavior: the editor keeps a stable width while open, permits manual resizing, and uses the available content area for long inputs rather than expanding every frame.

This was reported during local fork testing. The same auto-sizing and layout patterns exist in the upstream baseline identified below, but reproduction on an unmodified upstream installation has not yet been performed.

## Version Number

| Item | Version / revision |
|---|---|
| Upstream source baseline | AutoDuty `0.0.0.399`, tag at commit `ee4eced8` |
| Build where the behavior was reported | Local development fork before the fix; the exact window-title version was not captured |
| Source parent immediately before the fix | `cd7b3094` |
| Local fix commit | `00b99e12` |
| Build environment | Windows x64, .NET 10, Dalamud API 15 |

The upstream baseline version is a source reference, not a claim that the bug has been reproduced on the released `0.0.0.399` binary.

## Steps to reproduce the error

1. Open AutoDuty and select the Build tab.
2. Select a route and open an existing step in the Add/Edit Action window.
3. Leave the window open without dragging its resize handles.
4. Observe its horizontal size over several seconds.
5. In the reported local build, the width repeatedly increases until it fills the screen.

## Why change this?

The editor currently combines `ImGuiWindowFlags.AlwaysAutoResize` with content-dependent layout. The resulting content width can feed back into the next frame's automatic size calculation.

The existing close-button placement also depends on available content width. In addition, the coordinate-input block pushes an item width without a corresponding pop in that block. The proposed patch addresses these layout issues together within the same window.

The observed symptom is confirmed by the user's report. The feedback-loop explanation comes from source inspection; it has not been independently confirmed by an upstream runtime trace.

## Scope of the proposed change

One production file: `AutoDuty/Windows/BuildTab.cs`, limited to `ActionEditWindow`.

| Area | Proposed change | Reason / expected benefit |
|---|---|---|
| Window sizing | Remove `AlwaysAutoResize`; retain `NoTitleBar` | Stop automatic per-frame width growth |
| Opening size | Set `Size = (760, 620)` with `SizeCondition = ImGuiCond.Appearing` | Start from a predictable size when the editor appears |
| Size constraints | Set the minimum size to `(620, 240)` | Keep the editor usable while allowing manual resizing |
| Close button | Calculate its width from text and frame padding; position it using local window coordinates and the preceding item's right edge | Keep the button placement consistent with the resized content area |
| Coordinate inputs | Add the matching `ImGui.PopItemWidth()` | Restore the item-width stack after the X/Y/Z inputs |

These values describe the local source patch, not independently measured physical screen pixels under every UI scale. The minimum width and behavior on narrow viewports need checking before submission.

The proposed PR does not change route execution, action semantics, saved route data, leveling selection, or configuration schema. It will not include the other features from the development branch.

## What improves after the change?

- The editor is expected to remain at a stable width while idle.
- Users can resize it deliberately while retaining more of the game view.
- Reopening the editor starts from a predictable size.
- The close-button layout and coordinate-input item-width stack are explicitly managed.

These are the intended acceptance criteria. Runtime results are listed separately below.

## Testing performed

| Check | How it was checked | Result |
|---|---|---|
| Local compilation | Release/x64 build of the integrated fork containing the patch, using Dalamud API 15 assemblies | Passed: 0 errors, 44 warnings; recorded in `artifacts/build-editor-width.log` |
| Patch scope | Inspected local commit `00b99e12` | One production file changed: 10 insertions, 3 deletions |
| Upstream source comparison | Inspected `BuildTab.cs` at `ee4eced8` | The relevant auto-sizing, close-button layout, and unbalanced coordinate-input width pattern are present |
| Independent cherry-picked branch | Not created or built yet | Pending |
| Post-fix in-game editor behavior | Not yet verified in game | Pending; compilation does not establish that the width is stable |
| Scaling / narrow viewport behavior | Not yet verified in game | Pending |

The build result is from the integrated development branch, not an isolated P01 branch. It must be repeated after extracting the patch onto the agreed upstream base. No UI automation result is claimed for P01.

## Planned runtime verification

Before presenting the PR as validated:

1. Reproduce the width growth on the unmodified target version and record its window-title version.
2. Build and load the isolated P01 branch.
3. Open a step with long arguments or notes and leave the editor open for at least 30 seconds. Confirm that it does not expand without input.
4. Resize the window manually; verify that the controls and close button remain accessible.
5. Edit the X/Y/Z fields and confirm that subsequent widgets retain the correct width.
6. Close and reopen the editor; check the opening-size behavior.
7. Repeat at default and increased UI scale, including a narrow viewport. Confirm that long content can be scrolled and the minimum size does not prevent access to controls.
8. Confirm that editing and saving a step still work and that the change does not alter route execution.

## Relevant log output

No game log directly documenting the width growth was captured. The compiler result above only establishes that the local patch builds; it is not evidence of the runtime UI fix. A before/after recording should accompany the runtime verification.

## Other relevant plugins installed

Exact plugin versions were not captured for the original report. No external plugin interaction has been established as a cause. The proposed change is confined to AutoDuty's ImGui editor layout.

## Plugin config file

No configuration-specific trigger has been established. A full personal configuration is not included in this draft. If upstream reproduction requires one, provide a minimal configuration with character identifiers and unrelated settings removed.

## Readiness for submission

The local patch and its intended scope are available for discussion. The affected runtime version, clean upstream reproduction, isolated-branch build, and post-fix in-game checks remain to be completed before marking the fix as fully tested.
