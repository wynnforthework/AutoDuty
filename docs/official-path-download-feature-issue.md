# [Feature Request] Show official path download progress and completion, and offer a clean refresh of local scripts

### What problem are you trying to fix?

The **Download Official Paths** button on the Paths page does not clearly communicate whether the script library has actually finished updating.

Observed behavior on the Chinese client/server:

1. Clicking the button temporarily disables it.
2. Once downloading has started, the button becomes clickable again even though the download is still ongoing.
3. There is no visible progress or reliable completion indicator. The button becoming enabled therefore looks as though the update has finished.
4. Downloading the approximately **314 scripts** in the library took at least several minutes in the reporter's environment; this is an observed duration, not a universal download-time estimate.
5. The reporter observed that closing the plugin window during this period interrupted the update. Whether closing the window directly cancels the operation needs implementation-level confirmation, but the user-visible result was an incomplete refresh.

In practice, many of my local scripts were still approximately **one year old**, despite my attempts to update them. I encountered frequent problems during automated duty runs and had no clear indication that the script update had not completed. Stale scripts are a plausible contributor to those failures, not a proven explanation for every navigation or execution problem.

The important problem is that a partial or interrupted download can look successful, leaving users running a mixture of old and newly downloaded paths.

### What is the solution to the problem?

Please make the official script-library refresh an explicit operation with a reliable lifecycle:

- Keep the download button disabled for the **entire** refresh, including fetching the file list, downloading scripts, validating/writing files and reloading the path index. Do not allow overlapping refreshes.
- Display the current phase, downloaded/total script count (for example, `125 / 314`), progress and any failures. Clearly distinguish **updating**, **completed**, **failed** and **cancelled/incomplete** states.
- Announce success only after all expected scripts have been downloaded and installed successfully. Surface failed files and provide retry/recovery rather than silently treating a partial update as complete.
- Keep the update running independently of whether the Paths page or plugin window is visible. If closing the window must interrupt it, explicitly tell the user and retain an unmistakable incomplete-update status.

**My proposed clean-refresh behavior:** when clicking Download Official Paths, **delete all local scripts first, then download the complete official library again**. This is intended to eliminate old files left behind by incomplete updates, rather than repeatedly downloading over an existing collection whose freshness is unclear. Here, "all local scripts" literally includes the local script collection, not only scripts detected as outdated. This request is a proposal for the maintainers to assess; it does not claim that deletion is already implemented.

Because a literal delete-first refresh also removes locally edited/custom scripts and leaves no scripts available if downloading fails, the UI should explain the exact scope and provide a backup/recovery option. That consideration should not hide or change the requested delete-first behavior into a normal incremental overwrite.

The expected benefit is a library whose update status and freshness users can actually verify, reducing duty failures caused by inadvertently running outdated routes.

### Alternative solutions

- Download the complete official library to a staging directory, validate it, then replace the managed official scripts as one completed refresh. This avoids an empty or partially updated library after a network failure, but differs from the requested immediate delete-first approach.
- Separate official scripts from custom scripts, and offer an explicit **clean reinstall** action alongside an ordinary update. Clearly state whether custom scripts are preserved or removed.
- Show a last-successful-update timestamp and source revision/file count so that users can verify that the installed library is current.
- Manually deleting scripts and keeping the window open until downloads stop is a workaround, but it should not be necessary to guess whether the operation is complete.

### Additional context

Environment: Chinese (CN) client/server. This request follows reports from the AutoDuty 0.0.0.399 usage context; the exact binary version used for the observed downloader behavior was not separately recorded.

The count of 314 scripts and the multi-minute duration describe the reporter's observed update, not a fixed current repository count or a benchmark for all machines/connections. No exact downloader log, screenshot or network trace is attached.

Suggested acceptance checks:

1. Start a complete library refresh on a slow connection and verify that progress remains visible and the button stays disabled until installation finishes.
2. Close/reopen the plugin window and switch pages while downloading; verify that the operation continues and its state remains correct.
3. Interrupt the network or fail one file; verify that the operation does not report success and that recovery is possible.
4. Test the clean-refresh policy with stale, deleted-upstream and locally modified scripts; verify that deletion/backup scope matches the UI description and that the final installed official library is complete.

This is a feature proposal based on user observations; the proposed downloader/UI behavior has not been implemented or tested as part of this issue.
