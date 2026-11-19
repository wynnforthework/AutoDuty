# [Bug] Route advances before area transitions finish, skipping mandatory interactions and breaking duty progression (CN)

### What happened?

Area transitions can leave route execution ahead of the actual game state: AutoDuty starts the next step before the character has finished transferring and is able to act at the destination. A movement/interaction then fails and is skipped, although later steps depend on its result. This can make the entire duty route impossible to finish unattended.

**Confirmed in-game case: The Tower of Babil (魔塔神门巴别塔), Chinese client/server.**

- Step 10 is a movement step that triggers an area transition.
- Before that transition has finished, AutoDuty immediately starts step 11, an interaction.
- While the transfer is still in progress, the character cannot reach the interaction target at its intended destination. Step 11 fails and is skipped.
- This interaction is required to reach the next location. The subsequent route therefore loses its prerequisite and the automated run cannot continue successfully.
- The reporter tested this multiple times and reproduced it on every attempt (**100% of the reported attempts**; an exact attempt count was not recorded).

Step numbers here follow the reporter's route display; this report does not apply an additional index offset. The exact route JSON/revision is not attached.

**Expected behavior**

Keep the current step while a transition is in progress. Stop navigation to coordinates that belong to the previous area, wait for the actual game state to become ready, and verify that the character reached the expected destination before starting the dependent step. A failed mandatory interaction should retain a recoverable step or pause the route with a reason rather than silently letting dependent steps continue.

**Related transition symptoms reported during local testing**

- Movement triggers a transfer, but after arrival navigation continues toward the old area's coordinate, producing an unreachable target or a stuck/timeout failure.
- An interaction successfully triggers a transfer, but the interaction task remains active and keeps trying to navigate to or interact with the old location.
- A launch pad transfers the character to another platform in the same territory without the usual loading flags; the movement step remains unfinished and resumes navigation toward the source platform.
- A step fails during or immediately after loading, while manually retrying once the destination is ready succeeds.

These are related observations, not a claim that every symptom has been independently reproduced on the same released binary or that all of them have one proven root cause. The Tower of Babil case above is the concrete reproduction for this report.

Related report: #641 concerns a partial path at the platform exit after boss 2 on 0.0.0.398. This report concerns the earlier step 10-to-11 transition/readiness race on 0.0.0.399, so it is not a claim that the same platform-exit workaround fixes this problem.

**Why this affects more than one route**

Transition duration varies with computer performance and loading conditions. A fixed delay that happens to work on one machine can expire before another player's transition finishes. Starting or timing out steps without checking loading, character readiness, forced movement, destination position and the current step's expected outcome can therefore turn a single premature failure into a chain of skipped prerequisites. Repeated reports suggest a shared execution/transition-handling problem, rather than only an incorrect coordinate in one duty. The exact scheduling/root cause still needs upstream confirmation; this report does not claim that a fixed timer has been proven to be the sole cause.

**Suggested scope and validation**

Review the common transition, movement, interaction-completion and failure/skip logic. Separate evidence that a transition started, that movement/loading ended, that the expected destination was reached, and that the owning step completed. Navigation mesh/path evidence may help, but should not by itself prove that a mandatory interaction succeeded. Unknown transitions should stop invalid navigation and preserve diagnostics rather than guess a successful step.

This should prevent premature execution on slower machines, avoid retries against old coordinates, and preserve mandatory route prerequisites. Validate the Tower of Babil steps 10–11 with different loading durations, then test ordinary loading, same-territory platform transfers, interaction-triggered transfers, death/Return recovery and manual retry. Confirm that steps neither skip nor complete twice and that the duty finishes unattended.

This is a bug report, not a claim that an upstream fix has already been tested. Local development changes exist, but do not establish a verified upstream resolution.

**Proposed solution / local implementation approach**

Use state-based transition handling shared by movement and interaction steps, rather than adding a fixed delay before every step:

| Stage | Evidence | Behavior |
| --- | --- | --- |
| Suspected transition | Territory change, loading begins, abnormal displacement, forced movement, or a recognized launch/drop mechanism | Stop the old navigation and cancel obsolete step work, while retaining ownership of the current step. |
| Travel has ended | Loading has ended, the character can act, forced movement has ended, and readiness/position remain stable over multiple samples | Begin destination verification; elapsed time alone must not mean success. Moving platforms need an explicit exception to world-position stillness. |
| Destination confirmed | Expected territory, horizontal area and height range match; nearby floor/mesh evidence is checked where applicable | Confirm arrival in the destination region, allowing normal landing offsets while distinguishing vertically overlapping platforms. |
| Owning step completed | The confirmed transition belongs to this step, its completion conditions hold, and any configured mandatory path check passes | Complete this step exactly once, then start the dependent step. Arrival alone must not imply that a separate required interaction succeeded. |

Concrete implementation details:

1. **Describe travel intent in the route.** Attach optional transition metadata to the triggering MoveTo/Interactable step: source trigger radius/height, destination territory, destination center/radius/height, stability window, overall timeout, and optional ground/path requirements. Existing routes can infer candidate destination regions from adjacent spatial steps or the MoveTo → WaitFor IsReady → destination idiom; ambiguous cases should request explicit configuration rather than guess a completed step. Record source, bounded motion/state samples and landing region for new routes so authors can review the intended mechanism.
2. **Use vnavmesh as supporting evidence.** Query nearby ground and asynchronously query a path from the settled destination to the next spatial target. This must be a query, not a command to move. Limit it to one outstanding request and rate-limit new requests; reject empty/invalid paths and paths whose last point does not reach the requested endpoint within horizontal/height tolerances. Discard results after a step reset, territory change, target change, significant origin movement or mesh invalidation. A connected path cannot replace destination/interaction evidence. Missing mesh does not prove the character is still airborne; exceptional platforms need explicit route settings.
3. **Wait for sustained state, not a machine-specific sleep.** Restart the short stability window whenever loading/readiness/forced movement becomes invalid. The overall timeout produces a recoverable "destination not confirmed" failure, not successful completion or a jump to the next step. Path evidence can be optional by default, or explicitly mandatory for a route that needs it.
4. **Preserve prerequisites and recover safely.** On an unowned/unknown displacement, stop invalid navigation, pause and export before/after positions, territory, loading/readiness/forced-motion state, destination evidence and path-query status. Keep death/Return recovery separate from intentional travel; a confirmed return to the duty entrance should restart the route rather than continue from a stale middle step. If a mandatory interaction fails, retry only when the game is ready or pause on that step; do not continue into steps that depend on that interaction.

For the reported Babil case, completing step 10 must wait until its transfer has actually reached a confirmed destination. Only then may step 11 start navigating to and interacting with its target. Step 11 must retain its own interaction/transfer completion evidence before subsequent travel is allowed.

Local fork status: transition-region/stability handling was added in `cf6a0bc6`; separate destination/step-completion checks, forced-movement checks, asynchronous path evidence and bounded recording samples were added in `ab44b58e` (local build 0.0.0.406). Release compilation succeeded and 32 offline test groups passed, including delayed readiness, different floors, same coordinates in different territories, no-loading launch pads, stale/partial path results and completion ownership. These are local source references, **not an upstream PR or confirmation that this Babil reproduction is fixed in-game**. The reported affected version remains 0.0.0.399. Real-duty validation of the new local implementation is still required, including different loading durations and death/Return cases.

### Version Number

AutoDuty **0.0.0.399**. **Chinese (CN) client/server.**

### Steps to reproduce the error

1. On the Chinese client/server, run The Tower of Babil (魔塔神门巴别塔) using AutoDuty 0.0.0.399 and its route.
2. Allow the route to reach movement step 10 and trigger its area transition.
3. Observe step 11 starting before the transition is complete and the character can reach the destination interaction target.
4. Observe the movement/interaction failure and step 11 being skipped.
5. Observe that the required interaction never completes, preventing travel to the next location and breaking subsequent route progression.
6. Repeat the run. The reporter encountered the same failure on every reported attempt.

### Relevant log output

No exact `/xllog` excerpt for this Tower of Babil reproduction is attached. The reproduction and outcome above are the reporter's in-game observations; no log lines have been reconstructed or invented.

### Other relevant plugins installed

vnavmesh (navigation), BossMod (movement/combat AI), RotationSolver Reborn (automatic combat output). Exact versions were not supplied for this reproduction.

### Plugin config file

Not attached. The exact AutoDuty.json and route JSON/revision were not provided with this reproduction report.
