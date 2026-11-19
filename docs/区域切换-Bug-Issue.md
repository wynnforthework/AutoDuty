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
