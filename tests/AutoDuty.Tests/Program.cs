using System.Runtime.Loader;
using AutoDuty.Helpers;
using PathAction = AutoDuty.Data.Classes.PathAction;

// Load game API managed assemblies for model tests; no game services are initialized.
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string? hooks = Environment.GetEnvironmentVariable("AUTODUTY_TEST_DALAMUD");
    string file = Path.Combine(hooks ?? "", name.Name + ".dll");
    return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(file)) : null;
};
try { Run(); } catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
static void Run()
{
    object a = new(), b = new(), c = new(), x = new();
    object[] before = [a,b,c];
    Check(PathEditHelper.Relocate(before, new[]{x,a,b,c}, 1) == 2, "insert before");
    Check(PathEditHelper.Relocate(before, new[]{a,b,x,c}, 1) == 1, "insert after");
    Check(PathEditHelper.Relocate(before, new[]{b,c}, 1) == 0, "delete before");
    Check(PathEditHelper.Relocate(before, new[]{a,c}, 1) == 1, "delete current");
    Check(PathEditHelper.Relocate(before, new[]{a,x,c}, 1) == 1, "replace current");
    Check(PathEditHelper.Relocate(before, new[]{b,c,a}, 1) == 0, "reorder");
    Check(PathEditHelper.Relocate(before, new[]{a,b,c,x}, 3) == 4, "end sentinel");
    Check(PathEditHelper.Relocate(before, Array.Empty<object>(), 2) == 0, "delete all");
    Check(PathEditHelper.Relocate(before, new[]{a,b,c}, -1) == -1, "unselected");
    Check(PathEditHelper.FindReference(new[] {x, a, b, c}, b) == 2, "delete resolves edited step after insertion");
    Check(PathEditHelper.FindReference(new[] {c, b, a}, b) == 1, "delete resolves edited step after reorder");
    Check(PathEditHelper.FindReference(new[] {a, c}, b) == -1, "stale editor never deletes a different step");
    Check(PathEditHelper.FindReference<object>(new[] {a}, null) == -1, "new action editor cannot delete an unrelated step");
    Console.WriteLine("PASS: action relocation and identity-based deletion after insertion, reorder and removal");
    TestPaths();
    TestRecording();
    TestValidation();
    TestRunMetrics();
    TestAutomationReports();
    Check(PauseResumePolicy.Target(AutoDuty.Data.Enums.Stage.Moving, true) == AutoDuty.Data.Enums.Stage.Reading_Path, "resume movement restarts navigation instead of waiting on stopped path");
    Check(PauseResumePolicy.Target(AutoDuty.Data.Enums.Stage.Action, true) == AutoDuty.Data.Enums.Stage.Action, "resume action preserves task queue");
    Check(!PauseResumePolicy.ShouldInvokeAction(AutoDuty.Data.Enums.Stage.Paused), "resume never invokes completed action twice");
    Check(PauseResumePolicy.ShouldInvokeAction(AutoDuty.Data.Enums.Stage.Moving), "normal action entry still invokes action");
    Check(PauseResumePolicy.Target(AutoDuty.Data.Enums.Stage.Paused, true) == AutoDuty.Data.Enums.Stage.Reading_Path, "recover nested paused navigation state");
    Check(PauseResumePolicy.Target(AutoDuty.Data.Enums.Stage.Waiting_For_Combat, true) == AutoDuty.Data.Enums.Stage.Waiting_For_Combat, "combat wait restores its state");
    Check(PauseResumePolicy.Target(AutoDuty.Data.Enums.Stage.Looping, false) == AutoDuty.Data.Enums.Stage.Looping, "queue loop preserves work");
    Console.WriteLine("PASS: movement, action, combat and loop resume policies; no duplicate action invocation");
    Check(RouteCompletionPolicy.Finished(31, 31, true, false), "Dzemael final Boss advances to end sentinel and finishes without duty/nav readiness");
    Check(!RouteCompletionPolicy.HasStep(31, 31), "route end is not a failed step during departure");
    Check(RouteCompletionPolicy.HasStep(30, 31), "last Boss remains protected against unexpected return");
    Check(!RouteCompletionPolicy.Finished(30, 31, true, false), "duty victory does not skip an unfinished last step");
    Check(!RouteCompletionPolicy.Finished(31, 31, true, true), "manual pause does not automatically finish route");
    Check(!RouteCompletionPolicy.Finished(31, 31, false, false), "route finish is dispatched only once");
    Check(!RouteCompletionPolicy.Finished(0, 0, true, false), "missing route is not a completed route");
    Check(!RouteCompletionPolicy.HasStep(-1, 31), "unselected index is not a running step");
    Check(!StepRetryPolicy.CanRetry(1, 0), "zero retries: initial failure pauses immediately");
    Check(StepRetryPolicy.CanRetry(1, 3), "initial execution failure schedules retry one of three");
    Check(StepRetryPolicy.CanRetry(2, 3), "first retry failure schedules retry two");
    Check(StepRetryPolicy.CanRetry(3, 3), "second retry failure schedules final retry three");
    Check(!StepRetryPolicy.CanRetry(4, 3), "third retry failure exhausts budget after four executions");
    Check(!StepRetryPolicy.CanRetry(0, 3), "initial execution does not consume retry budget");
    Check(!StepRetryPolicy.CanRetry(1, -1), "negative retry configuration behaves as zero");
    Check(RouteCompletionPolicy.CompletedBoss(true, true, false, 1, ["MoveTo", "Boss"]), "authoritative duty victory finishes final Boss even if corpse was missed");
    Check(!RouteCompletionPolicy.CompletedBoss(false, true, false, 1, ["MoveTo", "Boss"]), "combat end without duty victory cannot finish Boss");
    Check(!RouteCompletionPolicy.CompletedBoss(true, true, true, 1, ["MoveTo", "Boss"]), "completed duty respects manual pause");
    Check(!RouteCompletionPolicy.CompletedBoss(true, true, false, 0, ["Boss", "MoveTo", "Boss"]), "victory cannot skip unfinished route progression");
    Check(RouteCompletionPolicy.CompletedBoss(true, true, false, 0, ["Boss", "TreasureCoffer", "<-- Comment -->"]), "only passive tail markers may follow completed final Boss");
    Check(!RouteCompletionPolicy.DrainFinished(true, 9999), "allow optional loot tasks a bounded grace period");
    Check(RouteCompletionPolicy.DrainFinished(true, 10000), "stuck loot cannot block confirmed victory beyond ten seconds");
    Check(RouteCompletionPolicy.DrainFinished(false, 0), "completed queue exits without extra delay");
    Console.WriteLine("PASS: authoritative final Boss completion, bounded loot wait and manual pause preservation");
    Check(!DutyActivationPolicy.AutoStartFollower(true, false, true, false), "idle multibox follower cannot start automation merely by entering duty");
    Check(DutyActivationPolicy.AutoStartFollower(true, false, true, true), "explicit active follower loop may continue into next duty");
    Check(!DutyActivationPolicy.AutoStartFollower(false, false, true, true), "disabled multibox cannot start follower");
    Check(!DutyActivationPolicy.AutoStartFollower(true, true, true, true), "host does not run follower entry branch");
    Check(!DutyActivationPolicy.AutoStartFollower(true, false, false, true), "outdoor entry cannot start follower");
    foreach (var stage in new[] { AutoDuty.Data.Enums.Stage.Stopped, AutoDuty.Data.Enums.Stage.Looping, AutoDuty.Data.Enums.Stage.Dead, AutoDuty.Data.Enums.Stage.Revived, AutoDuty.Data.Enums.Stage.Paused, AutoDuty.Data.Enums.Stage.Reading_Path })
        Check(!DutyActivationPolicy.RouteActive(stage, false), "non-navigation states never activate step cards, death recovery or route AI");
    Check(!DutyActivationPolicy.RouteActive(AutoDuty.Data.Enums.Stage.Stopped, true), "stopped route cannot recover from a stale navigation flag");
    Check(DutyActivationPolicy.RouteActive(AutoDuty.Data.Enums.Stage.Moving, true), "explicit navigation retains execution");
    Check(DutyActivationPolicy.RouteActive(AutoDuty.Data.Enums.Stage.Paused, true), "paused active route retains step cards");
    Check(DutyActivationPolicy.RouteActive(AutoDuty.Data.Enums.Stage.Revived, true), "active route may recover after death");
    Check(!DutyActivationPolicy.CanStop(AutoDuty.Data.Enums.Stage.Stopped, false, false, false, false, false), "idle window does not report active automation");
    Check(DutyActivationPolicy.CanStop(AutoDuty.Data.Enums.Stage.Stopped, true, false, false, false, false), "loop owns Stop even between steps with stopped route stage");
    Check(DutyActivationPolicy.DisplayStage(AutoDuty.Data.Enums.Stage.Stopped, true) == AutoDuty.Data.Enums.Stage.Looping, "window shows active loop instead of idle step state");
    Check(DutyActivationPolicy.CanStop(AutoDuty.Data.Enums.Stage.Stopped, false, true, false, false, false), "navigation ownership overrides stale stopped stage for Stop visibility");
    Check(DutyActivationPolicy.CanStop(AutoDuty.Data.Enums.Stage.Stopped, false, false, false, true, false), "pending queue work always exposes Stop");
    Check(DutyActivationPolicy.CanStop(AutoDuty.Data.Enums.Stage.Stopped, false, false, false, false, true), "running helper exposes Stop with no selected content");
    Check(DutyActivationPolicy.CanStop(AutoDuty.Data.Enums.Stage.Stopped, false, false, true, false, false), "between-duty actions expose Stop");
    foreach (var stage in new[] { AutoDuty.Data.Enums.Stage.Looping, AutoDuty.Data.Enums.Stage.Moving, AutoDuty.Data.Enums.Stage.Paused, AutoDuty.Data.Enums.Stage.Revived })
        Check(DutyActivationPolicy.CanStop(stage, false, false, false, false, false), "non-idle phase remains stoppable during temporary flag changes");
    Check(DutyActivationPolicy.DisplayStage(AutoDuty.Data.Enums.Stage.Paused, true) == AutoDuty.Data.Enums.Stage.Paused, "paused loop preserves displayed pause state");
    Console.WriteLine("PASS: session Stop visibility across queue, route, pause, loop boundaries and missing content; idle versus active-loop display");
    Console.WriteLine("PASS: idle duty entry, multibox activation boundary, route-owned cards and death recovery");
    TestDutyExecutionBoundary();
    TestBossProgress();
    TestUnnamedBossTracking();
    TestCombatCheckpoint();
    TestLevelingPlan();
    var combatPursuit = new CombatApproachProgress();
    var pursuitStart = new System.Numerics.Vector3(96, 24, -256);
    var pursuitTarget = new System.Numerics.Vector3(24, 28, -211);
    Check(!combatPursuit.IsStuck(pursuitStart, 0, 500), "combat approach starts its own progress window");
    Check(combatPursuit.ShouldRequest(pursuitTarget, false), "first distant enemy approach requests mesh path");
    for (int tick = 500; tick < 5000; tick += 500)
    {
        Check(!combatPursuit.ShouldRequest(pursuitTarget, true), "busy pursuit does not overwrite mesh path every combat tick");
        Check(!combatPursuit.IsStuck(pursuitStart, tick, 500), "pursuit startup uses bounded grace");
    }
    Check(combatPursuit.IsStuck(pursuitStart, 5000, 500), "combat wall running fails even while pathfinding stays true");
    Check(combatPursuit.ShouldRequest(pursuitTarget + new System.Numerics.Vector3(3, 0, 0), true), "moving enemy may update approach without resetting watchdog");
    Check(combatPursuit.IsStuck(pursuitStart, 5500, 500), "target updates cannot hide stationary pursuit");
    Check(combatPursuit.ShouldRequest(pursuitTarget, false), "stopped pursuit may request a fresh path");
    Check(!combatPursuit.IsStuck(pursuitStart + new System.Numerics.Vector3(1, 0, 0), 6000, 500), "actual combat pursuit movement resets watchdog");
    combatPursuit.Reset();
    Check(!combatPursuit.IsStuck(pursuitStart, 20000, 500), "combat end, cast or retry resets pursuit window");
    Check(combatPursuit.ShouldRequest(pursuitTarget, true), "fresh pursuit owns a new request even if another path is active");
    Console.WriteLine("PASS: combat approach watchdog, persistent finding/running, duplicate request suppression, moving targets and recovery reset");
    var movementProgress = new MovementProgress();
    var anchor = new System.Numerics.Vector3(104, 0, 150);
    movementProgress.Reset(anchor, 0);
    Check(!movementProgress.IsStuck(anchor, 1000, 5000), "navigation startup allows progress window");
    Check(!movementProgress.IsStuck(anchor + new System.Numerics.Vector3(.1f, 0, 0), 4000, 5000), "collision jitter does not reset progress timer");
    Check(movementProgress.IsStuck(anchor, 5000, 5000), "path restarts without position progress reach bounded stuck timeout");
    Check(!movementProgress.IsStuck(anchor + new System.Numerics.Vector3(1, 0, 0), 5100, 5000), "real movement resets progress timer");
    movementProgress.Reset(anchor, 6000);
    Check(!movementProgress.IsStuck(anchor, 7000, 5000), "combat resume starts fresh progress window");
    Check(EntranceRecovery.IsEntrance(1303, 1303, anchor, anchor + new System.Numerics.Vector3(3, 0, 0)), "confirmed entrance spawn tolerates small offset");
    Check(!EntranceRecovery.IsEntrance(1303, 1303, anchor, anchor + new System.Numerics.Vector3(0, 6, 0)), "other floor is not entrance");
    Check(!EntranceRecovery.IsEntrance(1303, 1303, anchor, anchor + new System.Numerics.Vector3(20, 0, 0)), "normal portal destination is not entrance");
    Check(!EntranceRecovery.IsEntrance(1303, 1041, anchor, anchor), "another map does not reuse entrance coordinates");
    Console.WriteLine("PASS: position-based movement watchdog, collision jitter, retry/combat reset and entrance map/floor validation");
    foreach (string reason in new[] { "stuck", "navigation-stopped-short", "move-timeout", "no-path", "pathfind-rejected", "unexpected-position-transition" })
        Check(StepRecoveryPolicy.MustPause(reason), "movement recovery never skips missing prerequisites: " + reason);
    Check(StepRecoveryPolicy.UnexpectedTransition(AutoDuty.Data.Enums.Stage.Moving), "loading during failed navigation cannot mark step completed");
    Check(StepRecoveryPolicy.UnexpectedTransition(AutoDuty.Data.Enums.Stage.Reading_Path), "loading during retry preserves failed step");
    Check(!StepRecoveryPolicy.UnexpectedTransition(AutoDuty.Data.Enums.Stage.Action), "action transfer uses actual-click evidence before movement recovery");
    Check(!StepRecoveryPolicy.UnexpectedTransition(AutoDuty.Data.Enums.Stage.Paused), "repeated transition flags cannot advance paused steps");
    Check(!StepRecoveryPolicy.UnexpectedTransition(AutoDuty.Data.Enums.Stage.Revived), "real death recovery remains separate");
    Console.WriteLine("PASS: bounded movement recovery forces pause, transition cannot advance failed step, scripted teleport and death recovery isolated");
    Check(InteractionCompletion.HasReward(1041, 1004346, id => id == 2000521 ? 1 : 0), "Brayflox key completes persistent NPC interaction, including retry");
    Check(!InteractionCompletion.HasReward(1041, 1004346, _ => 0), "missing key does not complete NPC interaction");
    Check(!InteractionCompletion.HasReward(1041, 2001462, _ => 1), "holding key does not complete gate interaction");
    Check(!InteractionCompletion.HasReward(1193, 1004346, _ => 1), "key evidence is scoped to Brayflox duty");
    Console.WriteLine("PASS: Brayflox key reward completion, retry idempotence and unrelated interactions isolated");
    var dialogueProgress = new InteractionDialogueProgress();
    Check(!dialogueProgress.Observe(false, false), "no dialogue is not proof of interaction completion");
    Check(!dialogueProgress.Observe(true, false) && !dialogueProgress.Observe(true, false), "visible dialogue never completes while clicking");
    Check(!dialogueProgress.Observe(false, true), "wait for interaction cast to finish");
    Check(dialogueProgress.Observe(false, false) && dialogueProgress.Observe(false, false), "dialogue close remains completed across throttled polls");
    Check(!new InteractionDialogueProgress().Observe(false, false), "new interaction cannot reuse previous dialogue evidence");
    TestInteractionTransfer();
    TestMovementTransfer();
    TestLaunchTransfer();
    TestBossSelection();
    TestTransitionRegions();
    TestTransitionStages();
    TestTransitionPathProbe();
    TestTransitionRecordingTrace();
    var loadingRecovery = new LoadingRecoveryProgress();
    Check(!loadingRecovery.Ready(0, false), "delayed area loading suspends execution");
    Check(!loadingRecovery.Ready(5000, true), "load clear alone cannot resume movement");
    Check(!loadingRecovery.Ready(5500, false), "a second load flag restarts the stable window");
    Check(!loadingRecovery.Ready(6000, true) && !loadingRecovery.Ready(6999, true)
        && loadingRecovery.Ready(7000, true), "resume only after continuous one-second readiness");
    loadingRecovery.Reset();
    Check(!loadingRecovery.Ready(10000, true), "new transition cannot reuse readiness");
    Check(LoadingRecoveryProgress.CanResume("MoveTo", false, false), "Babil step 24 and Vanaspati step 9 retry same MoveTo after delayed relocation");
    Check(!LoadingRecoveryProgress.CanResume("MoveTo", true, false), "Return never automatically retries old movement");
    Check(!LoadingRecoveryProgress.CanResume("MoveTo", false, true), "existing failure remains paused");
    foreach (var name in new[] { "Boss", "Interactable", "AutoMoveFor", "TeleportTo" })
        Check(!LoadingRecoveryProgress.CanResume(name, false, false), "unknown action cannot replay after loading: " + name);
    Console.WriteLine("PASS: delayed loading recovery, stable readiness, same-step movement replay and unsafe replay exclusions");
    TestNavigationReadiness();
    TestActionCombatCatalogue();
    TestStepExecutionGates();
    TestPathPersistence();
    var bindings = new Dictionary<string, AutoDuty.Data.Enums.JobWithRole>
    {
        ["shared.json"] = AutoDuty.Data.Enums.JobWithRole.All,
    };
    PathSelectionHelper.AssignRole(bindings, AutoDuty.Data.Enums.JobWithRole.Tanks, "tank.json");
    Check(bindings["shared.json"] == (AutoDuty.Data.Enums.JobWithRole.Healers | AutoDuty.Data.Enums.JobWithRole.DPS), "assign tank preserves healer and DPS");
    Check(bindings["tank.json"] == AutoDuty.Data.Enums.JobWithRole.Tanks, "all tank jobs share override");
    PathSelectionHelper.AssignRole(bindings, AutoDuty.Data.Enums.JobWithRole.Tanks, "shared.json");
    Check(bindings.Count == 1 && bindings["shared.json"] == AutoDuty.Data.Enums.JobWithRole.All, "reassignment removes stale role binding");
    PathSelectionHelper.AssignRole(bindings, AutoDuty.Data.Enums.JobWithRole.Healers, null);
    Check((bindings["shared.json"] & AutoDuty.Data.Enums.JobWithRole.Healers) == 0, "remove role restores automatic matching");
    Console.WriteLine("PASS: role assignment, reassignment and removal preserve other roles");
}
static void TestDutyExecutionBoundary()
{
    var boundary = new DutyExecutionBoundary();
    Check(!boundary.Leave(129, false), "idle outside territory events do not clear a user selection");
    boundary.Enter(1042);
    Check(!boundary.Leave(0, false), "temporary loading territory preserves paused/active duty progress");
    Check(!boundary.Leave(1042, true), "same duty zone transfer/death return keeps progress");
    Check(!boundary.Leave(999, true), "supported internal duty map is not treated as leaving");
    Check(boundary.Leave(129, false), "manual exit without Stop invalidates retained duty execution");
    Check(!boundary.Leave(130, false), "subsequent outdoor travel does not clear newly edited selection");
    boundary.Enter(1042);
    Check(boundary.Leave(129, false), "second run in identical duty still resets at exit");
    boundary.Enter(1041);
    boundary.Enter(0);
    Check(boundary.Leave(129, false), "loading placeholder cannot erase tracked duty identity");
    Console.WriteLine("PASS: duty exit without Stop, loading placeholders, internal transfers, repeated same duty and outdoor selection preservation");
}
static void TestBossProgress()
{
    var progress = new BossProgress();
    var first = new BossProgress.Key(1042, "Chudo-Yudo", new(0, 0, 116));
    var second = new BossProgress.Key(1042, "Koshchei", new(46, 4, -80));
    Check(!progress.Observe(first, false, false, false) && !progress.IsDefeated(first), "missing boss at entry is not victory");
    progress.Observe(first, true, false, false);
    Check(!progress.Observe(first, false, false, false) && !progress.IsDefeated(first), "phase disappearance is not victory");
    Check(progress.Observe(first, false, true, false), "witnessed corpse confirms defeat before Boss step starts");
    progress.Observe(first, false, false, false);
    Check(progress.IsDefeated(first), "despawn and death entrance restart retain confirmed first boss");
    Check(!progress.IsDefeated(second), "wipe at second boss does not complete it");
    Check(!progress.IsDefeated(first with { Territory = 1041 }), "evidence cannot leak to another dungeon");
    Check(!progress.IsDefeated(first with { Position = new(0, 0, -100) }), "same name at another route encounter is isolated");
    Check(progress.Observe(second, false, false, true), "completed game objective handles corpse missed or plugin reload");
    Check(!progress.Observe(second, true, true, true) && !progress.IsDefeated(second), "live respawn/phase overrides corpse and stale completion");
    Check(!BossProgress.ObjectiveComplete("Defeat Chudo-Yudo", 0, 1, new[] { "Chudo-Yudo" }), "named unfinished objective does not prove death");
    Check(!BossProgress.ObjectiveComplete("Defeat Chudo-Yudo", 0, 0, new[] { "Chudo-Yudo" }), "zero target count is not completed objective evidence");
    Check(BossProgress.ObjectiveComplete("Defeat Chudo-Yudo", 1, 1, new[] { "Chudo-Yudo" }), "matching completed objective proves death");
    progress.Reset();
    Check(!progress.IsDefeated(first), "new duty clears prior run evidence even in same dungeon");
    Check(BossProgress.ObjectiveMatches("Defeat Chudo-Yudo", "Chudo-Yudo"), "English boss objective matches full name");
    Check(BossProgress.ObjectiveMatches("击败蚁后", "蚁后"), "localized objective needs no whitespace separator");
    Check(!BossProgress.ObjectiveMatches("Defeat the Kingdom", "King"), "partial English name is not completion proof");
    Check(!BossProgress.ObjectiveMatches("Defeat all enemies", ""), "empty note never matches arbitrary completed objective");
    Check(!BossProgress.ObjectiveMatches("Defeat Koshchei", "Chudo-Yudo"), "another completed objective cannot complete current boss");
    Console.WriteLine("PASS: generic boss defeat evidence, early kills, corpse despawn, wipe/entrance restart, phase/respawn, objective names and fresh duty reset");
}
static void TestUnnamedBossTracking()
{
    var progress = new BossProgress();
    var step = new BossProgress.Key(974, "", new(.37f, 630, 48.82f));
    var other = step with { Position = new(-.34f, 0, -53.97f) };
    // Recorded failure: a living, targetable rank-6 Ladon Lord was in the
    // object table while Boss-GetBossObject kept retrying on an empty route note.
    progress.Remember(step, 1073746932, 13349, "拉冬之王");
    Check(progress.Matches(step, 1073746932, 13349), "unnamed Boss retains observed identity without a DutyData boss wrapper");
    Check(!progress.Matches(step, 1073746932, 13269), "a reused entity ID with a different NPC base cannot prove death");
    Check(!progress.Matches(step, 123, 13349), "an unseen entity with the same base is not an observed corpse");
    Check(!progress.Matches(other, 1073746932, 13349), "identity cannot complete a different Boss step");
    Check(progress.Observe(step, false, progress.Matches(step, 1073746932, 13349), false),
        "observed death completes unnamed Boss even after its rank/module or temporary context is lost");
    progress.Observe(step, false, false, false);
    Check(progress.IsDefeated(step), "confirmed death survives corpse despawn and retries");
    Check(BossProgress.ObjectiveComplete("击败拉冬之王", 1, 1, progress.Names(step)),
        "observed localized name allows completion when corpse snapshot was missed");
    Check(!BossProgress.ObjectiveComplete("突破创造生物评价室", 1, 1, progress.Names(step)),
        "unrelated room objectives cannot guess victory");
    progress.Remember(other, 0xE0000000, 13349, "unnetworked");
    Check(!progress.Names(other).Any(), "non-networked placeholder is never bound to a Boss encounter");
    progress.Reset();
    Check(!progress.Matches(step, 1073746932, 13349) && !progress.Names(step).Any(), "new duty clears both entity and name evidence");
    progress.Remember(step, 1073746932, 13349, "拉冬之王");
    Check(!progress.Observe(step, false, false, false) && !progress.IsDefeated(step),
        "having seen a Boss before disappearance is not itself victory");
    Console.WriteLine("PASS: unnamed boss identity through context loss, rank loss, corpse despawn, localized objectives and run/encounter isolation");
}
static void TestLevelingPlan()
{
    var choices = new[] { new LevelingCandidate(1, 20), new LevelingCandidate(2, 23), new LevelingCandidate(3, 24) };
    uint? At(int level) => LevelingPlanRanking.Choose(choices.Where(c => c.RequiredLevel <= level));
    Check(At(15) == null, "cannot enter a dungeon before its entry level");
    Check(At(20) == 1 && At(22) == 1, "use level 20 dungeon until next enabled entry level");
    Check(At(23) == 2 && At(24) == 3, "switch at each enabled dungeon entry level");
    Check(LevelingPlanRanking.Choose(choices.Where(c => c.RequiredLevel <= 23 && c.Territory != 2)) == 1, "fall back when higher dungeon is skipped or ineligible");
    Check(LevelingPlanRanking.Choose([]) == null, "empty plan does not select outside the plan");
    Check(LevelingPlanRanking.Choose([new(9, 23), new(2, 23)]) == 2, "same-level choice is deterministic");
    var dawntrail = new[] { new LevelingCandidate(1193, 93), new LevelingCandidate(1194, 95), new LevelingCandidate(1198, 97) };
    Check(LevelingPlanRanking.Choose(dawntrail.Where(c => c.RequiredLevel <= 93)) == 1193, "93 queues Worqor Zormor");
    Check(LevelingPlanRanking.Choose(dawntrail.Where(c => c.RequiredLevel <= 95)) == 1194, "95 reselects Skydeep Cenote from the current plan");
    Check(LevelingPlanRanking.Choose(dawntrail.Where(c => c.RequiredLevel <= 95 && c.Territory != 1194)) == 1193, "95 falls back only when Skydeep Cenote is ineligible");
    Console.WriteLine("PASS: entry-level progression, skipped/unavailable fallback, empty plan and deterministic selection");
}

static void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
}

static void TestPaths()
{
    string directory = Path.GetFullPath("AutoDuty/Paths");
    int count = 0;
    foreach (string path in Directory.GetFiles(directory, "*.json"))
    {
        var file = AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(path));
        string encoded = AutoDuty.Data.PathJson.WriteFile(file);
        Check(!encoded.Contains("$type"), "no assembly type names: " + path);
        var shared = PathCodec.Encode(file);
        var imported = PathCodec.Decode(shared, out string error);
        Check(imported != null && shared == PathCodec.Encode(imported), "sharing round trip: " + path + ": " + error);
        var chunks = PathCodec.Split(shared);
        Check(chunks.All(c => c.Length <= 450), "chat size limit");
        Check(PathCodec.TryAssemble(chunks, out string assembled, out error) && assembled == shared, "chunk round trip: " + path);
        if (chunks.Length > 1)
        {
            Check(!PathCodec.TryAssemble(chunks.Reverse(), out _, out _), "reordered chunks rejected");
            Check(!PathCodec.TryAssemble(chunks.SkipLast(1), out _, out _), "missing chunks rejected");
        }
        Check(PathCodec.Decode(shared[..^1], out _) == null, "truncated share rejected");
        Check(PathCodec.Decode(shared[..^1] + (shared[^1] == '0' ? "1" : "0"), out _) == null, "bad checksum rejected");
        var decoded = AutoDuty.Data.PathJson.ReadFile(encoded);
        Check(encoded == AutoDuty.Data.PathJson.WriteFile(decoded), "path round trip: " + path);
        count++;
    }
    var old = AutoDuty.Data.PathJson.ReadFile("""{"Actions":[{"Name":"Wait","Position":{"X":1,"Y":2,"Z":3},"Arguments":["100"],"Conditions":[{"$type":"AutoDuty.Data.Classes+PathActionConditionToDo, AutoDuty","index":0,"count":1,"operatorValue":"<"}]}]}""");
    Check(old.Actions[0].Conditions.Count == 1, "legacy condition");
    Check(AutoDuty.Data.PathJson.ReadFile(AutoDuty.Data.PathJson.WriteFile(old)).Actions[0].Conditions.Count == 1, "new condition");
    var clone = Newtonsoft.Json.JsonConvert.DeserializeObject<AutoDuty.Data.Classes.PathFile>(
        Newtonsoft.Json.JsonConvert.SerializeObject(old, AutoDuty.Configurations.ConfigurationMain.JsonSerializerSettings),
        AutoDuty.Configurations.ConfigurationMain.JsonSerializerSettings)!;
    Check(clone.Actions.Count == 1 && clone.Actions[0].Conditions.Count == 1 && !ReferenceEquals(clone.Actions[0], old.Actions[0]), "legacy clone/wire settings preserve polymorphic conditions and replace collections");
    try { AutoDuty.Data.PathJson.ReadFile("""{"SchemaVersion":999,"Actions":[]}"""); throw new Exception("accepted future schema"); } catch (Newtonsoft.Json.JsonException) { }
    Console.WriteLine($"PASS: {count} bundled paths JSON and sharing round trips, chunks, corruption; legacy/new conditions and future schema rejection");
}

static void TestValidation()
{
    foreach (string argument in new[] { "IsReady", "Ready", "IsOcupied", "combat", "OOC", "Addon;XBMResult", "ConditionFlag;Jumping61;FALSE", "DistanceTo;1, 2, 3;20", "400.10, 0, -177.38;20" })
        Check(PathValidator.Error(new() { Name = "WaitFor", Arguments = [argument] }) == null, "valid wait: " + argument);
    foreach (string argument in new[] { "", "Typo", "Addon;", "ConditionFlag;Typo;true", "ConditionFlag;Casting;typo", "DistanceTo;bad;20", "DistanceTo;1, 2, 3;NaN", "BNpcInRadius;-1" })
        Check(PathValidator.Error(new() { Name = "WaitFor", Arguments = [argument] }) != null, "invalid wait rejected: " + argument);
    Check(PathValidator.Error(new() { Name = "Wait", Arguments = ["2147483648"] }) != null, "overflow rejected before execution");
    Check(PathValidator.Error(new() { Name = "MoveTo", Arguments = ["typo"] }) != null, "bad mesh boolean");
    foreach (string name in new[] { "Wait", "AutoMoveFor" })
    {
        Check(PathValidator.Error(new() { Name = name, Arguments = ["-1"] }) != null, "negative duration cannot silently complete: " + name);
        Check(PathValidator.Error(new() { Name = name, Arguments = ["0"] }) == null, "zero duration remains valid: " + name);
    }
    foreach (string range in new[] { "typo", "-1", "0", "4294967296" })
        Check(PathValidator.Error(new() { Name = "KillInRange", Arguments = [range] }) != null, "bad combat range cannot silently complete: " + range);
    Check(PathValidator.Error(new() { Name = "KillInRange", Arguments = ["30"] }) == null, "valid combat range");
    Check(PathValidator.Error(new() { Name = "Interactable", Arguments = ["NPC without ID"] }) != null, "invalid object id cannot silently complete");
    Check(PathValidator.Error(new() { Name = "Interactable", Arguments = ["2000460 Sand"] }) == null, "legacy labelled object id remains valid");
    foreach (string json in new[] { """{"Actions":[null]}""", """{"Actions":[{"Name":"Wait","Arguments":null}]}""", """{"Actions":[{"Position":[1,2]}]}""", """{"Actions":[],"Actions":[]}""", """{"Actions":[{"Conditions":[{"Condition":"Unknown"}]}]}""" })
    {
        try { AutoDuty.Data.PathJson.ReadFile(json); throw new Exception("accepted malformed path: " + json); }
        catch (Newtonsoft.Json.JsonException) { }
    }
    Console.WriteLine("PASS: legacy wait aliases, typed arguments, overflow, malformed paths and clone/wire compatibility");
}

static void TestRunMetrics()
{
    AutoDuty.Data.RouteRunRecord Record(int total = 4) => new() { TotalSteps = total, StartedAtFirstStep = true, StartedAt = DateTime.UtcNow };
    var record = Record();
    var run = new RouteRunTracker(record);
    run.CompleteStep(0); run.CompleteStep(0); run.CompleteStep(1, true);
    Check(record.CompletedSteps == 2 && record.AutonomousSteps == 2 && record.ExecutedSteps == 1 && record.PolicySkippedSteps == 1, "distinct successful and authored skips");
    run.Problem("stuck", 2);
    run.CompleteStep(2); run.CompleteStep(3);
    record.DutyCompleted = record.RouteFinished = true;
    run.Finish(DateTime.UtcNow, "recovered");
    Check(record.CompletionPercent == 50 && !record.VerifiedComplete && record.ProblemStep == 2 && record.CompletedSteps == 4, "recovery never erases first interruption");
    record = Record(); run = new(record);
    run.CompleteStep(0); run.Problem("stuck", 1, automaticRetry: true);
    run.CompleteStep(1); run.Problem("stuck", 2, automaticRetry: true);
    run.CompleteStep(2); run.CompleteStep(3);
    record.DutyCompleted = record.RouteFinished = true;
    run.Finish(DateTime.UtcNow, "duty-complete");
    Check(record.CompletionPercent == 100 && record.Failures == 2 && record.Interventions == 0 && record.AutonomousSteps == 4,
        "automatic retries preserve autonomous completion and retain diagnostics");
    record = Record(); run = new(record);
    run.CompleteStep(0); run.Problem("stuck", 1, automaticRetry: true);
    Check(record.CompletionPercent == 25, "pending retry does not award unfinished steps");
    run.Problem("retry-exhausted", 1); run.CompleteStep(1); run.CompleteStep(2); run.CompleteStep(3);
    record.DutyCompleted = record.RouteFinished = true;
    Check(record.CompletionPercent == 25 && !record.VerifiedComplete, "terminal pause after retries freezes completion");
    record = Record(46); record.CompletedSteps = 46; record.ExecutedSteps = 46; record.AutonomousSteps = 45;
    record.FirstProblem = "stuck"; record.Failures = 1; record.RouteFinished = record.DutyCompleted = true;
    Check(record.VerifiedComplete && record.CompletionPercent == 100, "reported legacy 45/46 fully completed automatic run is compatible");
    record.Interventions = 1;
    Check(!record.VerifiedComplete, "legacy manual intervention is never upgraded");
    record.Interventions = 0; record.CompletedSteps = 45;
    Check(!record.VerifiedComplete, "legacy missing final step is not upgraded");
    record.CompletedSteps = 46; record.CompletionInterrupted = true;
    Check(!record.VerifiedComplete, "explicit interruption cannot use legacy compatibility");
    record = Record(); run = new(record);
    for (int i = 0; i < 4; i++) run.CompleteStep(i);
    record.RouteFinished = true;
    Check(record.CompletionPercent == 99.9 && !record.VerifiedComplete, "end of route without duty complete is not 100 percent");
    record.DutyCompleted = true;
    run.Finish(DateTime.UtcNow, "duty-complete");
    Check(record.CompletionPercent == 100 && record.Outcome == "complete", "full clean completion");
    record = Record(); run = new(record);
    run.CompleteStep(0); run.Problem("manual-seek", 1, true);
    run.CompleteStep(3); record.DutyCompleted = record.RouteFinished = true;
    Check(record.CompletionPercent == 25 && record.Interventions == 1, "manual jumps cannot inflate completion");
    record = Record(); record.StartedAtFirstStep = false; run = new(record);
    for (int i = 0; i < 4; i++) run.CompleteStep(i);
    record.DutyCompleted = record.RouteFinished = true;
    Check(!record.VerifiedComplete && record.CompletionPercent == 0, "mid-route start is not clean evidence");
    record = Record(0); record.DutyCompleted = record.RouteFinished = true;
    Check(record.CompletionPercent == 0, "empty route cannot be certified");
    var xp = new ExperienceAccumulator();
    Check(xp.Sample(new(1, 25, 16, 900), _ => 1000) == (0L, 0), "experience baseline");
    Check(xp.Sample(new(1, 25, 17, 200), _ => 1000) == (300L, 1), "level-up experience includes consumed level");
    Check(xp.Sample(new(1, 25, 19, 50), _ => 1000) == (1850L, 2), "multiple level ups");
    Check(xp.Sample(new(1, 26, 70, 2000), _ => 1000) == (0L, 0), "job switch has separate baseline");
    Check(xp.Sample(new(1, 25, 19, 150), _ => 1000) == (100L, 0), "shared job experience index is not double counted");
    Check(xp.Sample(new(2, 25, 19, 1500), _ => 1000) == (0L, 0), "character switch has separate baseline");
    Check(xp.Sample(new(1, 25, 20, 0), _ => 0) == (0L, 0), "missing XP table is not fabricated");
    var saved = Newtonsoft.Json.JsonConvert.SerializeObject(record);
    Check(!saved.Contains("CompletionPercent") && !saved.Contains("VerifiedComplete"), "completion is computed from evidence");
    Console.WriteLine("PASS: completion evidence, freeze after failure/intervention, deduplication and cross-level/job/character XP");
}

static void TestAutomationReports()
{
    var actions = new List<AutoDuty.Data.Classes.PathAction> { new() { Name = "MoveTo", Position = new(1, 2, 3) } };
    string hash = AutomationReports.Fingerprint(actions);
    actions[0].Position = new(2, 2, 3);
    Check(hash != AutomationReports.Fingerprint(actions), "route edits create separate metric versions");
    AutoDuty.Data.RouteRunRecord Clean() => new()
    {
        ParticipantId = Guid.NewGuid(), CharacterId = 123456, TerritoryId = 1037, JobId = 19, RouteHash = hash, RouteName = "Test route",
        Mode = "Support/Synced", StartedAt = DateTime.UtcNow.AddMinutes(-10), UpdatedAt = DateTime.UtcNow, EndedAt = DateTime.UtcNow,
        TotalSteps = 2, CompletedSteps = 2, ExecutedSteps = 2, AutonomousSteps = 2, StartedAtFirstStep = true,
        DutyCompleted = true, RouteFinished = true, Experience = 600000, Outcome = "complete",
    };
    var good = Clean(); var failed = Clean();
    failed.FirstProblem = "stuck"; failed.ProblemStep = 1; failed.Failures = 1; failed.AutonomousSteps = 1; failed.CompletionInterrupted = true;
    var groups = AutomationReports.Summarize([good, failed]);
    Check(groups.Count == 1 && groups[0].Reliability == 50 && groups[0].MeanCompletion == 75 && groups[0].Participants == 2, "failed attempts and multiple contributors are included");
    Check(AutomationReports.CompletionEvidence([failed], 1037, hash, 19, good.Mode, false)!.BestCompletion == 50,
        "single half run shows fifty percent");
    Check(AutomationReports.CompletionEvidence([failed, good, failed], 1037, hash, 19, good.Mode, false)!.BestCompletion == 100,
        "successful run establishes best completion and later failures never reduce it");
    Check(groups[0].ExperiencePerHour > 3500000 && groups[0].ExperiencePerHour < 3700000, "XP/hour uses all attempt durations");
    var tank = Clean(); tank.JobId = 21;
    var party = Clean(); party.PartySize = 4;
    var mode = Clean(); mode.Mode = "Regular/Unsynced";
    var version = Clean(); version.RouteHash = AutomationReports.Fingerprint(actions);
    Check(AutomationReports.Summarize([good, tank, party, mode, version]).Count == 5, "job, party, mode and version cohorts remain separate");
    var best = AutomationReports.CompletionEvidence([failed, party, tank, mode, version], 1037, hash, 19, good.Mode, false)!;
    Check(best.Samples == 2 && best.BestCompletion == 100, "completion survives party size changes but separates job mode and script content");
    Check(AutomationReports.CompletionEvidence([tank, mode, version], 1037, hash, 19, good.Mode, false) == null,
        "no evidence never borrows another job mode or route version");
    var running = Clean(); running.EndedAt = null;
    Check(AutomationReports.CompletionEvidence([running], 1037, hash, 19, good.Mode, false) == null,
        "unfinished live attempt is not final completion evidence");
    string json = AutomationReports.Export([good, failed]);
    var imported = AutomationReports.Read(json);
    Check(imported.Count == 2 && imported.All(r => r.CharacterId == 0 && r.Imported) && imported[0].VerifiedComplete, "report export removes character identity and preserves evidence");
    var destination = new List<AutoDuty.Data.RouteRunRecord> { good };
    Check(AutomationReports.Merge(destination, imported) == 1 && AutomationReports.Merge(destination, imported) == 0, "report imports deduplicate run IDs");
    Check(AutomationReports.Export([new()]) == "{\"Version\":1,\"Runs\":[]}", "unfinished attempts are not shared as final samples");
    foreach (string invalid in new[] { json.Replace("\"Version\":1", "\"Version\":99"), json.Replace("\"AutonomousSteps\":2", "\"AutonomousSteps\":99"), json.Replace("\"CompletedSteps\":2", "\"CompletedSteps\":-1"), json.Replace("\"PartySize\":1", "\"PartySize\":0") })
    {
        try { AutomationReports.Read(invalid); throw new Exception("accepted invalid metrics report"); }
        catch (Newtonsoft.Json.JsonException) { }
    }
    var oldStats = Newtonsoft.Json.JsonConvert.DeserializeObject<AutoDuty.Configurations.ConfigurationMain.StatData>("{\"dungeonsRun\":5}")!;
    Check(oldStats.dungeonsRun == 5 && oldStats.routeRuns.Count == 0 && oldStats.automationSessions.Count == 0, "old statistics gain empty metrics collections");
    Console.WriteLine("PASS: report privacy, deduplication, validation, cohort separation and failure-inclusive efficiency");
}

static void TestRecording()
{
    var actions = Enumerable.Range(0, 101).Select(i => new AutoDuty.Data.Classes.PathAction { Name = "MoveTo", Position = new(i, 0, i % 2 == 0 ? .1f : -.1f) }).ToList();
    var simplified = PathRecorder.Simplify(actions, .5f);
    Check(simplified.Count == 2 && ReferenceEquals(simplified[0], actions[0]) && ReferenceEquals(simplified[^1], actions[^1]), "RDP endpoints/jitter");
    actions.Insert(50, new() { Name = "Interactable", Position = new(50, 0, 0), Arguments = ["1"] });
    simplified = PathRecorder.Simplify(actions, .5f);
    Check(simplified.Count == 5 && simplified[2].Name == "Interactable", "RDP event boundaries");
    var corner = new List<AutoDuty.Data.Classes.PathAction> { new() { Name="MoveTo", Position=new(0,0,0) }, new() { Name="MoveTo", Position=new(10,0,0) }, new() { Name="MoveTo", Position=new(10,0,10) } };
    Check(PathRecorder.Simplify(corner, 2.5f).Count == 3, "RDP corner preserved");
    Console.WriteLine("PASS: recorder jitter, endpoints, action boundaries and corners");
}

static void TestPathPersistence()
{
    string directory = Path.Combine(Path.GetTempPath(), "AutoDuty-persistence-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        string path = Path.Combine(directory, "(1036) role-route.json");
        var original = AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(Directory.GetFiles("AutoDuty/Paths", "*.json")[0]));
        string protectedPath = Path.Combine(directory, "(1036) original.json");
        original.Meta.IsUserVersion = false;
        original.Meta.IsDraft = false;
        string protectedJson = AutoDuty.Data.PathJson.WriteFile(original);
        File.WriteAllText(protectedPath, protectedJson);
        Check(PathFileStore.IsReadOnly(protectedPath), "unmarked original is protected");
        bool originalRejected = false;
        try { PathFileStore.Save(protectedPath, []); } catch (InvalidOperationException) { originalRejected = true; }
        Check(originalRejected && File.ReadAllText(protectedPath) == protectedJson, "original save rejected without changing bytes");
        string editableCopy = PathFileStore.CopyAs(protectedPath, directory, 1036, "editable-copy", original.Actions);
        var enabledCopy = AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(editableCopy));
        enabledCopy.Meta.IsDraft = false;
        File.WriteAllText(editableCopy, AutoDuty.Data.PathJson.WriteFile(enabledCopy));
        PathFileStore.Save(editableCopy, []);
        Check(!PathFileStore.IsReadOnly(editableCopy) && File.ReadAllText(protectedPath) == protectedJson, "enabled user copy stays editable and preserves original");
        original.Meta.IsUserVersion = true;
        original.Meta.Author = "Persistence test";
        original.Meta.Jobs = AutoDuty.Data.Enums.JobWithRole.Tanks;
        File.WriteAllText(path, AutoDuty.Data.PathJson.WriteFile(original));
        var cached = AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(path));
        var edited = new AutoDuty.Data.Classes.PathAction { Name = "Wait", Arguments = ["2345"], Note = "saved edit" };
        PathFileStore.Save(path, [edited], "edited step", 999);
        var reentered = AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(path));
        Check(reentered.Actions.Count == 1 && reentered.Actions[0].Arguments[0] == "2345", "edits survive a fresh load from disk");
        Check(reentered.Meta.Author == original.Meta.Author && reentered.Meta.Jobs == original.Meta.Jobs, "preserve role and author metadata");
        Check(reentered.Meta.Changelog.Last().Change == "edited step", "persist changelog");
        Check(cached.Actions[0].Note != "saved edit", "save does not rely on mutating an old catalog cache");
        string sourceBeforeCopy = File.ReadAllText(path);
        string copyA = PathFileStore.CopyAs(path, directory, 1036, "tank-test-A", [edited]);
        string copyB = PathFileStore.CopyAs(path, directory, 1036, "tank-test-B", [edited]);
        Check(copyA != copyB && File.ReadAllText(path) == sourceBeforeCopy, "multiple variants never overwrite the source");
        var draft = AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(copyA));
        Check(draft.Meta.IsDraft && !PathFileStore.CanAutoSelect(draft), "test copies excluded from automatic matching");
        Check(PathFileStore.CanAutoSelect(reentered), "existing stable routes remain eligible");
        Check(draft.Meta.Jobs == original.Meta.Jobs && draft.Meta.Territory == 1036, "copies preserve role and map identity");
        var sharedDraft = PathCodec.Decode(PathCodec.Encode(draft), out _);
        Check(sharedDraft?.Meta.IsDraft == true, "sharing preserves draft state");
        PathFileStore.Save(copyA, [new() { Name = "Wait", Arguments = ["9876"] }]);
        Check(File.ReadAllText(path) == sourceBeforeCopy && AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(copyB)).Actions[0].Arguments[0] == "2345", "editing one variant leaves original and other variants intact");
        bool duplicateRejected = false;
        try { PathFileStore.CopyAs(path, directory, 1036, "tank-test-A", [edited]); } catch (IOException) { duplicateRejected = true; }
        Check(duplicateRejected && AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(copyA)).Actions[0].Arguments[0] == "9876", "duplicate copy names cannot overwrite existing variants");
        bool traversalRejected = false;
        try { PathFileStore.CopyAs(path, directory, 1036, "../escape", [edited]); } catch (ArgumentException) { traversalRejected = true; }
        Check(traversalRejected, "copy filename cannot escape route directory");
        string saved = File.ReadAllText(path);
        bool rejected = false;
        try { PathFileStore.Save(path, [new() { Name = "MoveTo", Position = new(float.NaN, 0, 0) }]); }
        catch { rejected = true; }
        Check(rejected && File.ReadAllText(path) == saved, "failed validation leaves saved file intact");
        PathFileStore.Save(path, []);
        Check(AutoDuty.Data.PathJson.ReadFile(File.ReadAllText(path)).Actions.Count == 0, "deleting last step persists an empty route");
        Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "no temporary files remain");
        foreach (string reason in new[] { "replace action", "add action", "add MoveTo", "delete", "reorder", "duplicate", "insert after", "strict conditions" })
        {
            Check(PathFileStore.ShouldPersistEdit(reason, false), "autosave confirmed edit: " + reason);
            Check(!PathFileStore.ShouldPersistEdit(reason, true), "recording edits do not overwrite existing route: " + reason);
        }
        foreach (string reason in new[] { "host path", "load route for editing", "start recording", "record MoveTo", "record undo", "simplify recording", "clear" })
            Check(!PathFileStore.ShouldPersistEdit(reason, false), "loading and recording do not autosave: " + reason);
        Console.WriteLine("PASS: route edits persist on reload, preserve metadata, reject invalid writes, save last-step deletion and isolate recording/loading");
    }
    finally { Directory.Delete(directory, true); }
}

static void TestCombatCheckpoint()
{
    var move = new AutoDuty.Data.Classes.PathAction { Name = "MoveTo", Position = new(44.74f, -20.54f, 198.37f) };
    var stop = new AutoDuty.Data.Classes.PathAction { Name = "StopForCombat", Position = new(41.68f, -19.10f, 196.76f), Arguments = ["True"] };
    var boss = new AutoDuty.Data.Classes.PathAction { Name = "Boss", Position = stop.Position };
    AutoDuty.Data.Classes.PathAction[] actions = [move, stop, boss];
    var evidence = new CombatCheckpointProgress();
    evidence.Observe(actions, 0, move.Position, false, true);
    Check(!evidence.CanApply(stop, move.Position, false), "absence of enemies is not combat evidence");
    evidence.Observe(actions, 0, move.Position, true, true);
    Check(!evidence.CanApply(stop, move.Position, true), "ongoing early battle must not be treated as ended");
    evidence.Observe(actions, 0, move.Position, false, true);
    evidence.Observe(actions, 1, move.Position, false, true);
    Check(evidence.CanApply(stop, move.Position, false), "screenshot: battle at step 2 satisfies nearby switch at step 3");
    Check(!evidence.CanApply(boss, move.Position, false), "completed trash fight never satisfies a boss step");
    Check(!evidence.CanApply(stop, move.Position + new System.Numerics.Vector3(50, 0, 0), false), "cannot use local battle evidence from far away");
    Check(!evidence.CanApply(new() { Name = stop.Name, Position = stop.Position, Arguments = ["True"] }, move.Position, false), "evidence bound to original step identity");
    evidence.Observe(actions, 1, stop.Position, true, true);
    Check(!evidence.CanApply(stop, stop.Position, false), "new enemy wave clears prior ended-battle evidence");
    evidence.Observe(actions, 1, stop.Position, false, true);
    evidence.Observe(actions, 1, stop.Position, false, false);
    Check(!evidence.CanApply(stop, stop.Position, false), "death or invalid player invalidates combat evidence");
    evidence.Observe(actions, 0, move.Position, true, true);
    evidence.Observe(actions, 0, move.Position, false, true);
    evidence.Reset();
    Check(!evidence.CanApply(stop, stop.Position, false), "manual seek and route editing cannot reuse stale evidence");
    var farMove = new AutoDuty.Data.Classes.PathAction { Name = "MoveTo", Position = move.Position + new System.Numerics.Vector3(100, 0, 0) };
    evidence.Observe([farMove, stop], 0, stop.Position, true, true);
    evidence.Observe([farMove, stop], 1, stop.Position, false, true);
    Check(!evidence.CanApply(stop, stop.Position, false), "non-adjacent combat not associated with switch");
    var off = new AutoDuty.Data.Classes.PathAction { Name = "StopForCombat", Position = stop.Position, Arguments = ["False"] };
    evidence.Observe([move, off], 0, move.Position, true, true);
    evidence.Observe([move, off], 1, move.Position, false, true);
    Check(!evidence.CanApply(off, move.Position, false), "pull continuation switch remains unchanged");
    Check(CombatCheckpointProgress.ArrivalRadius(stop, 0) == 1.5f && CombatCheckpointProgress.ArrivalRadius(move, 0) == .25f, "switch arrival tolerance does not relax MoveTo");
    Check(CombatCheckpointProgress.ArrivalRadius(boss, 0) == .25f, "boss navigation remains precise");
    var slopeSwitch = new AutoDuty.Data.Classes.PathAction { Name = "StopForCombat", Arguments = ["True"], Position = new(41.68471f, -19.103832f, 196.75746f) };
    var stoppedOnSlope = new System.Numerics.Vector3(43.124752f, -19.920034f, 196.42093f);
    Check(System.Numerics.Vector3.Distance(stoppedOnSlope, slopeSwitch.Position) > 1.5f, "reported failure reproduces 3D distance exceeding tolerance");
    Check(CombatCheckpointProgress.HasArrived(slopeSwitch, stoppedOnSlope, 0), "reported slope endpoint is inside horizontal switch tolerance");
    Check(CombatCheckpointProgress.HasArrived(slopeSwitch, stoppedOnSlope, 2), "retry at reached switch does not need new navigation");
    Check(!CombatCheckpointProgress.HasArrived(slopeSwitch, slopeSwitch.Position + new System.Numerics.Vector3(0, 3, 0), 0), "another floor cannot satisfy switch arrival");
    Check(!CombatCheckpointProgress.HasArrived(slopeSwitch, slopeSwitch.Position + new System.Numerics.Vector3(1.6f, 0, 0), 0), "outside horizontal tolerance still requires navigation");
    Check(!CombatCheckpointProgress.HasArrived(move, move.Position + new System.Numerics.Vector3(0, 1, 0), 0), "precise movement is not relaxed by switch slope handling");
    var sands = new AutoDuty.Data.Classes.PathAction { Name = "Interactable", Position = new(-9.087362f, -6.1487885f, 215.57568f) };
    var sandsEndpoint = new System.Numerics.Vector3(-10.536299f, -6.821846f, 214.21039f);
    Check(System.Numerics.Vector3.Distance(sandsEndpoint, sands.Position) > 2, "reported interaction endpoint reproduces 3D range mismatch");
    Check(CombatCheckpointProgress.HasArrived(sands, sandsEndpoint, 0), "reported sands endpoint proceeds to interaction");
    Check(CombatCheckpointProgress.HasArrived(sands, sandsEndpoint, 2), "interaction retries preserve ground arrival semantics");
    Check(!CombatCheckpointProgress.HasArrived(sands, sands.Position + new System.Numerics.Vector3(0, 3, 0), 0), "different floor cannot satisfy interaction arrival");
    Check(!CombatCheckpointProgress.HasArrived(sands, sands.Position + new System.Numerics.Vector3(2.1f, 0, 0), 0), "interaction still requires horizontal proximity");
    Console.WriteLine("PASS: reported sands endpoint and height guard");
    var navStop = new NavigationStopProgress();
    Check(!navStop.StoppedShort(false, 1000) && !navStop.StoppedShort(false, 2999), "idle navigation allows asynchronous path startup grace");
    Check(navStop.StoppedShort(false, 3000), "stopped short is detected after 2 seconds, not 60");
    Check(!navStop.StoppedShort(true, 4000) && !navStop.StoppedShort(false, 10000), "active pathfinding clears idle grace");
    navStop.Reset();
    Check(!navStop.StoppedShort(false, 100000), "retry and combat resume start fresh idle observation");
    Console.WriteLine("PASS: adjacent early-combat switch, no empty-area guessing, no boss/remote reuse, new waves, death/reset and scoped arrival tolerance");
}

static void TestInteractionTransfer()
{
    var progress = new InteractionTransitionProgress();
    var step = new AutoDuty.Data.Classes.PathAction { Name = "Interactable", Arguments = ["2000461"] };
    var start = new System.Numerics.Vector3(104.04624f, -.8942185f, 151.06163f);
    var end = new System.Numerics.Vector3(-20.41f, -.95f, 146.28f);
    Check(InteractionTransitionProgress.RequiresTransfer(1303, 2000460)
        && InteractionTransitionProgress.RequiresTransfer(1303, 2000461), "both reported sand portals require observed transfer before advancing");
    Check(!InteractionTransitionProgress.RequiresTransfer(1041, 2000461)
        && !InteractionTransitionProgress.RequiresTransfer(1303, 1004346), "persistent key NPC and unrelated duty retain normal completion");
    Check(!progress.ObserveLoading(step, 7, 1000, false), "loading without actual click cannot complete movement");
    progress.Click(step, 7, 1303, start, 1000);
    Check(!progress.ObserveLoading(step, 7, 1200, true), "death or Return takes priority over recent interaction");
    Check(!progress.ObserveLoading(step, 5, 1200, false), "step 5 cannot consume step 7 interaction evidence");
    Check(!progress.ObserveLoading(new AutoDuty.Data.Classes.PathAction(), 7, 1200, false), "replacement route cannot reuse click evidence");
    Check(!progress.Settled(2000, false, true, 1303, end), "object disappearance or displacement alone cannot finish transfer");
    Check(progress.ObserveLoading(step, 7, 3000, false), "delayed loading after click is latched on owning step");
    Check(!progress.Settled(3100, true, false, 1303, end), "old interaction stays blocked throughout load");
    Check(!progress.Settled(4000, false, true, 1303, end), "wait for stable player state after loading clears");
    Check(progress.ObserveLoading(step, 7, 4500, false), "second loading flag is idempotent");
    Check(!progress.Settled(4600, false, true, 1303, end)
        && !progress.Settled(5599, false, true, 1303, end), "second flag restarts stable-state grace");
    Check(progress.Settled(5600, false, true, 1303, end), "same-territory sand transfer completes after one stable second");
    Check(!progress.Settled(5700, false, true, 1303, start), "unchanged position does not prove teleport succeeded");
    Check(!progress.Settled(5800, false, false, 1303, end), "lost player validity resets settle timer");
    Check(!progress.Settled(6000, false, true, 1303, end)
        && progress.Settled(7000, false, true, 1303, end), "combat-capable valid state can settle without waiting combat end");
    Check(progress.ObserveLoading(step, 7, 40000, false), "long in-progress load accepts duplicate flags without becoming unexpected transition");
    Check(!progress.TimedOut(62999) && progress.TimedOut(63000), "duplicate flags do not extend sixty-second loading deadline");
    progress.Reset();
    Check(!progress.Pending && !progress.ObserveLoading(step, 7, 80000, false), "seek/retry/death/reset discards old transfer evidence");
    progress.Click(step, 7, 1303, start, 1000);
    progress.Click(step, 7, 1303, start, 29000);
    Check(!progress.TimedOut(30999) && progress.TimedOut(31000), "repeated interaction cannot extend transfer-start deadline forever");
    Check(!progress.ObserveLoading(step, 7, 31000, false), "expired click cannot authorize unrelated transition");
    progress.Reset();
    progress.Click(step, 7, 1303, start, 1000);
    progress.ObserveLoading(step, 7, 1100, false);
    Check(!progress.Settled(2000, false, true, 999, start) && progress.Settled(3000, false, true, 999, start), "territory change also provides transfer evidence");
    Console.WriteLine("PASS: sand transfer ownership, delayed/duplicate loading flags, settle grace, death/Return exclusion, stale state and bounded timeouts");
}
static void TestMovementTransfer()
{
    var portal = new InteractionTransitionProgress();
    var crossing = new AutoDuty.Data.Classes.PathAction { Name = "MoveTo", Position = new(-125.86f, 501.29f, -5.6f), Arguments = ["false"] };
    var waitReady = new AutoDuty.Data.Classes.PathAction { Name = "WaitFor", Arguments = ["IsReady"] };
    var entry = new System.Numerics.Vector3(-136.75784f, 500.28732f, 7.183294f);
    var loadPosition = new System.Numerics.Vector3(-133.499f, 501.48148f, 3.3606186f);
    var destination = new System.Numerics.Vector3(-155.4559f, 621.7324f, -2.44003f);
    Check(System.Numerics.Vector3.Distance(loadPosition, crossing.Position) > 10, "reported Ktisis trigger lies outside old portal sphere");
    portal.TrackMovement(crossing, 18, 974, entry, 0, waitReady);
    Check(!portal.ObserveTrackedMovementLoading(crossing, 18, 10, false), "portal pattern alone without actual movement cannot skip a step");
    portal.TrackMovement(crossing, 18, 974, loadPosition, 800, waitReady);
    Check(!portal.ObserveTrackedMovementLoading(crossing, 18, 801, true), "scripted portal does not authorize death or Return");
    Check(portal.ObserveTrackedMovementLoading(crossing, 18, 801, false), "Ktisis direct crossing owns observed loading before destination sphere");
    Check(!portal.Settled(900, true, false, 974, loadPosition)
        && !portal.Settled(1000, false, true, 974, loadPosition)
        && !portal.Settled(2100, false, true, 974, loadPosition), "flag ending before teleport cannot complete or replay old coordinates");
    Check(portal.ObserveTrackedMovementLoading(crossing, 18, 2300, false), "delayed second load flag keeps original portal owner");
    Check(!portal.Settled(2400, false, true, 974, destination)
        && portal.Settled(3400, false, true, 974, destination), "new floor settles before crossing completes");
    portal.Reset();
    portal.TrackMovement(crossing, 18, 974, entry, 0);
    portal.TrackMovement(crossing, 18, 974, loadPosition, 800);
    Check(!portal.ObserveTrackedMovementLoading(crossing, 18, 801, false), "ordinary distant movement retains strict portal guard");
    portal.Reset();
    portal.TrackMovement(crossing, 18, 974, entry, 0, waitReady);
    portal.TrackMovement(crossing, 18, 974, entry + new System.Numerics.Vector3(0, 5, 0), 800, waitReady);
    Check(!portal.ObserveTrackedMovementLoading(crossing, 18, 801, false), "another floor cannot authorize scripted segment");
    Check(!InteractionTransitionProgress.IsScriptedTransfer(crossing, new() { Name = "WaitFor", Arguments = ["Combat"] }), "other waits do not imply portal intent");
    var progress = new InteractionTransitionProgress();
    var step = new AutoDuty.Data.Classes.PathAction
    {
        Name = "MoveTo", Position = new(192.01451f, -8.05f, -212.5612f)
    };
    var trigger = new System.Numerics.Vector3(191.99313f, -8.050052f, -205.81342f);
    var upstairs = new System.Numerics.Vector3(196.04f, 24, -256.61f);
    Check(!progress.ObserveMovementLoading(step, 22, false, 1366, trigger, 1000, false), "idle/reading/combat movement cannot authorize a transfer");
    Check(!progress.ObserveMovementLoading(step, 22, true, 1366, trigger, 1000, true), "death or Return never completes a nearby MoveTo");
    Check(!progress.ObserveMovementLoading(step, 22, true, 1366, trigger + new System.Numerics.Vector3(20, 0, 0), 1000, false), "remote loading remains unexpected");
    var boss = new AutoDuty.Data.Classes.PathAction { Name = "Boss", Position = step.Position };
    Check(!progress.ObserveMovementLoading(boss, 22, true, 1366, trigger, 1000, false), "Boss prerequisite movement must not skip the encounter");
    Check(progress.Step == null && !progress.Pending, "rejected movement loading leaves no transferable evidence");
    Check(progress.ObserveMovementLoading(step, 22, true, 1366, trigger, 1000, false), "Dusk Vigil index 22 portal triggers before original coordinate is reached");
    Check(!progress.Settled(2000, true, false, 1366, upstairs), "old coordinate cannot complete while loading");
    Check(!progress.ObserveLoading(step, 23, 2100, false), "next step cannot consume previous movement transfer");
    Check(progress.ObserveLoading(step, 22, 2500, false), "second loading flag retains original owning step after displacement");
    Check(!progress.Settled(3000, false, true, 1366, upstairs), "wait for destination to settle");
    Check(progress.Settled(4000, false, true, 1366, upstairs), "same territory floor transfer completes movement without revisiting old coordinates");
    progress.Reset();
    Check(!progress.Pending && !progress.Settled(5000, false, true, 1366, upstairs), "stop/seek/death reset prevents duplicate advancement");
    progress.ObserveMovementLoading(step, 22, true, 1366, trigger, 1000, false);
    Check(!progress.Settled(2000, false, true, 1366, trigger)
        && !progress.Settled(3000, false, true, 1366, trigger), "loading without position or territory change never completes movement");
    Check(progress.TimedOut(61000), "unconfirmed movement transfer has a bounded timeout");
    progress.Reset();
    progress.ObserveMovementLoading(step, 22, true, 1366, trigger, 1000, false);
    Check(!progress.Settled(2000, false, true, 999, trigger)
        && progress.Settled(3000, false, true, 999, trigger), "changed territory also confirms movement transfer after settling");
    progress.Reset();
    progress.TrackMovement(step, 22, 1366, trigger, 1000);
    Check(!progress.ObserveTrackedMovementLoading(step, 22, 1100, true), "cached portal approach cannot authorize death or Return");
    Check(!progress.ObserveTrackedMovementLoading(step, 23, 1100, false), "cached approach belongs to the original index");
    Check(!progress.ObserveTrackedMovementLoading(new AutoDuty.Data.Classes.PathAction { Name = "MoveTo", Position = step.Position }, 22, 1100, false), "replacement step cannot reuse cached approach");
    Check(progress.ObserveTrackedMovementLoading(step, 22, 1100, false), "preload sample survives absent player, changed position or readiness fallback to Reading_Path");
    Check(progress.ObserveTrackedMovementLoading(step, 22, 5000, false), "second loading flag preserves pending transfer beyond approach sample lifetime");
    Check(!progress.Settled(5100, false, true, 1366, upstairs)
        && progress.Settled(6100, false, true, 1366, upstairs), "cached origin confirms upstairs arrival without revisiting old coordinates");
    progress.Reset();
    Check(!progress.ObserveTrackedMovementLoading(step, 22, 6200, false), "reset removes pending and cached approach evidence");
    progress.TrackMovement(step, 22, 1366, trigger, 7000);
    Check(!progress.ObserveTrackedMovementLoading(step, 22, 9001, false), "stale approach cannot authorize unrelated loading");
    progress.TrackMovement(step, 22, 1366, trigger, 10000);
    progress.TrackMovement(step, 22, 1366, trigger + new System.Numerics.Vector3(40, 0, 0), 10100);
    Check(!progress.ObserveTrackedMovementLoading(step, 22, 10200, false), "moving away from portal invalidates cached approach");
    Console.WriteLine("PASS: Dusk Vigil movement portal, duplicate flags, stable destination, ownership, reset, timeout and death/Return exclusion");
}
static void TestLaunchTransfer()
{
    var progress = new InteractionTransitionProgress();
    PathAction step = new() { Name = "MoveTo", Position = new(-.02f, -700, -1.35f), Arguments = ["true"] };
    PathAction ready = new() { Name = "WaitFor", Arguments = ["IsReady"] };
    PathAction landing = new() { Name = "Wait", Position = new(19.98f, -600, -45.01f), Arguments = ["1000"] };
    var source = new System.Numerics.Vector3(-.025f, -699.74f, 2.04f);
    var airborne = new System.Numerics.Vector3(.38f, -686.18f, 2.05f);
    var destination = new System.Numerics.Vector3(9.93f, -599.82f, -17.26f);
    progress.TrackMovement(step, 36, 974, source, 1000, ready);
    Check(!progress.ObserveForcedMovement(step, 36, 974, source, 1100, false, ready, landing), "approaching pad is not completed movement");
    Check(!progress.ObserveForcedMovement(step, 36, 974, airborne, 1200, true, ready, landing), "death and Return cannot authorize a launch");
    Check(!progress.ObserveForcedMovement(step, 37, 974, airborne, 1200, false, ready, landing), "launch evidence belongs to one index");
    Check(!progress.ObserveForcedMovement(step, 36, 974, source + new System.Numerics.Vector3(0, 2, 0), 1200, false, ready, landing), "ordinary jump cannot authorize launch");
    Check(!progress.ObserveForcedMovement(step, 36, 974, source - new System.Numerics.Vector3(0, 12, 0), 1200, false, ready, landing), "falling away from next floor is not successful launch");
    Check(!progress.ObserveForcedMovement(step, 36, 974, airborne, 1200, false, new() { Name = "WaitFor", Arguments = ["Combat"] }, landing), "ordinary movement without transfer intent stays bounded");
    progress.TrackMovement(step, 36, 974, source + new System.Numerics.Vector3(0, 4, 0), 1250, ready);
    Check(progress.ObserveForcedMovement(step, 36, 974, airborne, 1300, false, ready, landing), "mesh MoveTo launch is recognized without any loading event despite airborne samples");
    Check(!progress.Settled(2000, false, true, 974, airborne) && !progress.Settled(4000, false, true, 974, airborne), "readiness alone cannot complete while flying between floors");
    Check(!progress.Settled(4100, false, false, 974, destination), "landing without player readiness waits");
    Check(!progress.Settled(4200, false, true, 974, destination) && progress.Settled(5200, false, true, 974, destination), "ready on expected destination floor completes launch after settling");
    progress.Reset();
    Check(!progress.Pending && !progress.ObserveForcedMovement(step, 36, 974, airborne, 5300, false, ready, landing), "reset cannot reuse old launch evidence");
    progress.TrackMovement(step, 36, 974, source, 6000, ready);
    Check(!progress.ObserveForcedMovement(step, 36, 974, airborne, 8001, false, ready, landing), "stale pad sample cannot authorize unrelated displacement");
    Console.WriteLine("PASS: no-loading launch pad, mesh movement, airborne readiness, expected floor, death/Return and stale-step isolation");
}
static void TestTransitionRegions()
{
    var region = new TransitionRegion { Territory = 974, Center = new(20, -600, -45), Radius = 20 };
    Check(region.Valid && region.Contains(974, new(25, -599, -40)), "landing area allows normal offset");
    Check(!region.Contains(974, new(20, -700, -45)), "overlapping floors are not interchangeable");
    Check(!region.Contains(975, region.Center) && !region.Contains(974, new(100, -600, -45)), "territory and horizontal limits both apply");
    var step = new PathAction { Name = "MoveTo", Position = new(0, -700, 0), Transition = region };
    var transfer = new InteractionTransitionProgress();
    Check(transfer.BeginDisplacement(step, 36, 974, step.Position, 0, region), "explicit transition owns current step");
    Check(!transfer.Settled(1000, false, true, 974, region.Center, false), "mesh/ground failure never passes merely after waiting");
    Check(transfer.WaitingReason == "ground-or-mesh", "diagnostic explains missing evidence");
    Check(!transfer.Settled(2000, false, true, 974, region.Center, true), "start stable window on actual arrival");
    Check(!transfer.Settled(3000, false, true, 974, region.Center + new System.Numerics.Vector3(2, 0, 0), true), "sliding position resets stability");
    Check(!transfer.Settled(4000, false, false, 974, region.Center, true), "loss of readiness resets stability");
    Check(!transfer.Settled(5000, false, true, 974, region.Center, true) && transfer.Settled(6000, false, true, 974, region.Center, true), "only sustained ready grounded arrival succeeds");
    transfer.Reset();
    region.RequireStillness = false; region.RequireGround = false;
    transfer.BeginDisplacement(step, 36, 974, step.Position, 0, region);
    Check(!transfer.Settled(1000, false, true, 974, region.Center, false)
        && transfer.Settled(2000, false, true, 974, region.Center + new System.Numerics.Vector3(3, 0, 0), false), "declared moving platform may waive ground and stillness while retaining region and readiness");
    Check(transfer.TimedOut(60000), "transition remains bounded even if requirements never settle");
    transfer.Reset();
    step.Name = "Interactable"; step.Arguments = ["2000001"];
    transfer.Click(step, 5, 974, step.Position, 1000);
    Check(!transfer.ActivateExpectedMovement(step, 5, 974, step.Position, 1100), "interaction click alone is not travel");
    Check(!transfer.ActivateExpectedMovement(step, 6, 974, region.Center, 1200), "interaction evidence cannot advance another index");
    Check(transfer.ActivateExpectedMovement(step, 5, 974, region.Center, 1200), "configured same-height or gradual interaction transfer requires no loading flag");
    var monitor = new DisplacementMonitor();
    Check(monitor.Observe(974, new(0, 0, 0), 0) == null, "first displacement sample has no history");
    for (int i = 1; i <= 100; i++)
        Check(monitor.Observe(974, new(i * .06f, 0, 0), i * 10) == null, "ordinary walking at high frame rate is not a transition");
    Check(monitor.Observe(974, new(100, 0, 0), 1100) == "forced-displacement", "same-height instant relocation cancels old path");
    monitor.Reset(); monitor.Observe(974, new(0, 0, 0), 0);
    string? observed = null;
    for (int i = 1; i <= 20; i++) observed ??= monitor.Observe(974, new(0, i * .5f, 0), i * 10);
    Check(observed == "forced-displacement", "continuous fast launch is detected despite sub-two-metre frames");
    monitor.Reset(); monitor.Observe(974, region.Center, 0);
    Check(monitor.Observe(975, region.Center, 200) == "territory-changed", "territory change does not need coordinate change");
    Check(monitor.SourceTerritory == 974, "transition retains source territory rather than destination territory");
    monitor.Reset(); monitor.Observe(974, region.Center, 0);
    Check(monitor.Observe(975, region.Center, 10) == "territory-changed", "map changes are observed even inside the displacement sampling interval");
    transfer.Reset();
    transfer.BeginDisplacement(step, 36, 974, region.Center, 0, new() { Territory = 975, Center = region.Center });
    Check(!transfer.Settled(1000, false, true, 975, region.Center)
        && transfer.Settled(2000, false, true, 975, region.Center), "same coordinates on a different territory still constitute actual travel");
    var file = new AutoDuty.Data.Classes.PathFile { Actions = [step] };
    string json = AutoDuty.Data.PathJson.WriteFile(file);
    var restored = AutoDuty.Data.PathJson.ReadFile(json).Actions[0];
    Check(restored.Transition?.Center == region.Center && restored.Transition?.RequireGround == false,
        "transition settings survive file save and reload");
    var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<PathAction>(Newtonsoft.Json.JsonConvert.SerializeObject(step))!;
    copy.Transition!.Radius = 30;
    Check(step.Transition.Radius == 20, "editor clone does not mutate the running destination");
    Check(PathRecorder.Simplify([new() { Name = "MoveTo", Position = new(0, 0, 0) },
        new() { Name = "MoveTo", Position = new(1, 0, 0), Transition = region },
        new() { Name = "MoveTo", Position = new(2, 0, 0) }], 10).Count == 3, "simplification never removes transition boundaries");
    region.Radius = float.NaN;
    Check(!region.Valid && PathValidator.Error(step) != null, "invalid region cannot execute");
    var invalid = Newtonsoft.Json.Linq.JObject.Parse(json);
    invalid["Actions"]![0]!["Transition"]!["Radius"] = -1;
    bool rejected = false;
    try { AutoDuty.Data.PathJson.ReadFile(invalid.ToString()); }
    catch (Newtonsoft.Json.JsonException) { rejected = true; }
    Check(rejected, "invalid transition region is rejected at file import");
    Console.WriteLine("PASS: transition regions, grounded stability, moving-platform exceptions, unknown displacement, high frame rates, serialization and recording boundaries");
}
static void TestBossSelection()
{
    var origin = new System.Numerics.Vector3(0, 0, 0);
    var corpse = new { Id = 1, Name = "Boss", Position = new System.Numerics.Vector3(1, 0, 0), Rank = true, Module = false, Alive = false, Targetable = true, Tracked = false };
    var add = corpse with { Id = 2, Position = new(2, 0, 0), Rank = false, Alive = true };
    var localized = corpse with { Id = 3, Name = "localized name unrelated to note", Position = new(5, 0, 0), Alive = true };
    var module = localized with { Id = 4, Position = new(7, 0, 0), Rank = false, Module = true };
    var tracked = localized with { Id = 5, Position = new(8, 0, 0), Rank = false, Tracked = true };
    var wrongFloor = tracked with { Id = 6, Position = new(0, 30, 0) };
    var remote = tracked with { Id = 7, Position = new(101, 0, 0) };
    var untargetable = tracked with { Id = 8, Targetable = false };
    var objects = new[] { corpse, add, localized, module, tracked, wrongFloor, remote, untargetable };
    var selected = BossIdentity.SelectLive(objects, origin, o => o.Position, o => o.Rank || o.Module,
        o => o.Alive && o.Targetable, o => o.Tracked);
    Check(selected?.Id == 5, "confirmed primitive identity survives rank/module loss and wins reacquisition");
    selected = BossIdentity.SelectLive(objects, origin, o => o.Position, o => o.Rank || o.Module,
        o => o.Alive && o.Targetable, _ => false);
    Check(selected?.Id == 3, "nearest live ranked boss is selected despite closer corpse and identically named add");
    selected = BossIdentity.SelectLive(new[] { corpse, add, module }, origin, o => o.Position, o => o.Rank || o.Module,
        o => o.Alive && o.Targetable, _ => false);
    Check(selected?.Id == 4, "BossMod module identifies unranked boss without reading names");
    selected = BossIdentity.SelectLive(new[] { add, wrongFloor, remote, untargetable }, origin, o => o.Position, o => o.Rank || o.Module,
        o => o.Alive && o.Targetable, o => o.Tracked);
    Check(selected == null, "same-name add, other floor, distant and untargetable entities cannot replace boss");
    PathAction first = new() { Name = "Boss", Note = "English note", Position = new(5, 0, 0) };
    PathAction second = new() { Name = "Boss", Note = "Any remark", Position = new(40, 0, 0) };
    Check(BossIdentity.OwnedBy(first, new[] { first, second }, localized.Position, a => a.Position)
        && !BossIdentity.OwnedBy(second, new[] { first, second }, localized.Position, a => a.Position),
        "one observed live boss is bound to its nearest route encounter, not every step in range");
    Console.WriteLine("PASS: production boss selector, rank/module/tracked identity, corpse-before-live, add/floor rejection and encounter ownership without names");
}

static void TestTransitionStages()
{
    var region = new TransitionRegion { Center = new(20, 10, 0), RequirePath = true };
    PathAction step = new() { Name = "MoveTo", Transition = region };
    var transfer = new InteractionTransitionProgress();
    transfer.BeginDisplacement(step, 5, 1, new(0, 0, 0), 0, region);
    Check(transfer.Phase == TransitionPhase.Suspected && !transfer.CompleteStep(step, 5, true, true, true), "suspected conversion never means completed step");
    Check(!transfer.Settled(1000, false, true, 1, region.Center, true, true)
        && transfer.Phase == TransitionPhase.Travelling && transfer.WaitingReason == "forced-movement", "forced motion flag rejects even apparently correct stable coordinates");
    Check(!transfer.Settled(2000, false, true, 1, region.Center) && transfer.Settled(3000, false, true, 1, region.Center), "forced movement ending starts sustained confirmation window");
    Check(transfer.Phase == TransitionPhase.DestinationConfirmed, "destination confirmation is separate from step completion");
    Check(!transfer.CompleteStep(step, 6, true, true, true)
        && !transfer.CompleteStep(new() { Name = "MoveTo", Transition = region }, 5, true, true, true), "only owning step identity and index may consume evidence");
    Check(!transfer.CompleteStep(step, 5, false, true, true), "step conditions cannot be replaced by landing evidence");
    Check(!transfer.CompleteStep(step, 5, true, false, true), "required path cannot be bypassed by waiting");
    Check(transfer.CompleteStep(step, 5, true, true, true) && transfer.Phase == TransitionPhase.StepCompleted, "only combined evidence completes owning step");
    Check(!transfer.CompleteStep(step, 5, true, true, true), "completion cannot be consumed twice");
    transfer.Reset();
    step.Transition = null;
    transfer.Click(step, 5, 1, new(0, 0, 0), 0); transfer.ObserveLoading(step, 5, 10, false);
    transfer.Settled(1000, false, true, 1, region.Center); transfer.Settled(2000, false, true, 1, region.Center);
    Check(transfer.Phase == TransitionPhase.ValidatingDestination && !transfer.CompleteStep(step, 5, true, true, true), "unconfigured landing cannot guess step completion");
    Check(region.NearTrigger(new(0, 0, 0), new(5, 1, 0)) && !region.NearTrigger(new(0, 0, 0), new(0, 10, 0)), "trigger area separates overlapping floors");
    region.TriggerRadius = float.NaN; Check(!region.Valid, "invalid trigger range is rejected");
    Console.WriteLine("PASS: transition stages, forced movement, owning-step completion, mandatory path, missing destination and trigger floors");
}

static void TestTransitionPathProbe()
{
    var probe = new TransitionPathProbe();
    var target = new System.Numerics.Vector3(20, 0, 0);
    var from = new System.Numerics.Vector3(0, 0, 0);
    var pending = new TaskCompletionSource<List<System.Numerics.Vector3>>();
    int calls = 0;
    Task<List<System.Numerics.Vector3>> Query(System.Numerics.Vector3 a, System.Numerics.Vector3 b) { calls++; return pending.Task; }
    probe.Tick(0, 1, from, target, true, Query);
    for (int i = 1; i <= 100; i++) probe.Tick(i * 100, 1, from, target, true, Query);
    Check(calls == 1 && !probe.Connected, "slow IPC never queues additional path requests");
    Check(probe.OptionalWaitFinished(3000), "supplementary path evidence has bounded wait without claiming connectivity");
    probe.Reset(); probe.Tick(11000, 1, from, target, true, Query);
    Check(calls == 1, "reset does not abandon outstanding query and pile up more work");
    pending.SetResult([from, target]);
    probe.Tick(12000, 1, from, target, true, Query);
    Check(!probe.Connected && probe.Status == "stale-result", "previous-step result is discarded");
    pending = new(); probe.Tick(12100, 1, from, target, true, Query);
    pending.SetResult([from, new(10, 0, 0)]); probe.Tick(12200, 1, from, target, true, Query);
    Check(probe.Status == "endpoint-mismatch", "partial path is not accepted as reachable");
    int count = calls; probe.Tick(13000, 1, from, target, true, Query);
    Check(calls == count, "failed path retry is rate limited");
    pending = new(); probe.Tick(14200, 1, from, target, true, Query);
    pending.SetResult([from, target]); probe.Tick(14300, 2, from, target, true, Query);
    Check(!probe.Connected && probe.Status == "stale-result", "cross-map query result cannot confirm a destination");
    pending = new(); probe.Tick(16300, 2, from, target, true, Query);
    pending.SetResult([from, target]); probe.Tick(16400, 2, from, target, true, Query);
    Check(probe.Connected, "finite endpoint on expected floor proves connectivity");
    probe.Tick(16500, 2, from, target, false, Query);
    Check(!probe.Connected, "mesh loss invalidates cached connectivity");
    pending = new(); probe.Tick(18400, 2, from, target, true, Query);
    pending.SetException(new InvalidOperationException("IPC unavailable")); probe.Tick(18500, 2, from, target, true, Query);
    Check(probe.Status == "query-error" && !probe.Connected, "async IPC errors are observed without blocking framework");
    pending = new(); probe.Tick(20400, 2, from, target, true, Query);
    pending.SetResult([from, target]); probe.Tick(20500, 2, from + new System.Numerics.Vector3(3, 0, 0), target, true, Query);
    Check(probe.Status == "stale-result" && !probe.Connected, "moving query origin invalidates a previously requested route");
    Check(!TransitionPathProbe.HasEndpoint([], target)
        && !TransitionPathProbe.HasEndpoint([new(20, 100, 0)], target)
        && !TransitionPathProbe.HasEndpoint([new(float.NaN, 0, 0), target], target), "empty, wrong-floor and malformed paths fail");
    Check(TransitionPathProbe.ContinuationTarget([new() { Name = "MoveTo" }, new() { Name = "WaitFor" }, new() { Name = "MoveTo", Position = target }], 0) == target,
        "path target skips non-spatial waits");
    Console.WriteLine("PASS: async transition path query, endpoint validation, rate limit, outstanding request ownership, stale maps, mesh loss and IPC failure");
}

static void TestTransitionRecordingTrace()
{
    var trace = new RecordedTransitionTrace { SourceTerritory = 974, Source = new(0, -700, 0) };
    for (int i = 0; i < 200; i++) trace.Add(new() { ElapsedMs = i * 200, Territory = 974, Position = new(0, -700 + i, 0), ForcedMovement = i < 195, Ready = i >= 195 });
    Check(trace.Valid && trace.Samples.Count == 64 && trace.Samples[0].ElapsedMs == 0 && trace.Samples[^1].ElapsedMs == 39800,
        "recorded travel keeps source and latest landing within bounded sample count");
    PathAction step = new() { Name = "MoveTo", Position = trace.Source, Transition = new() { Center = new(0, -501, 0), Recording = trace } };
    var json = AutoDuty.Data.PathJson.WriteFile(new() { Actions = [step] });
    var restored = AutoDuty.Data.PathJson.ReadFile(json).Actions[0].Transition!;
    Check(restored.Recording?.Samples.Count == 64 && restored.Recording.SourceTerritory == 974, "travel evidence survives route save/share serialization");
    restored.Recording!.Samples[2].ElapsedMs = -1;
    Check(!restored.Valid, "malformed recorded travel is rejected as part of region validation");
    Console.WriteLine("PASS: bounded transition recording, source, motion state, landing and serialization validation");
}

static void TestStepExecutionGates()
{
    var gate = new StepExecutionGate();
    var duration = new ActiveDurationProgress(5000);
    Check(!duration.Completed(999999), "unstarted duration never completes");
    duration.Start(1000);
    Check(!duration.Completed(5999) && duration.Completed(6000), "wait measures active duration");
    var independent = new ActiveDurationProgress(5000);
    independent.Start(5500);
    Check(!independent.Completed(6000) && duration.Completed(6000), "overlapping waits do not share throttle state");
    var pausedWait = new StepAttempt { Index = 0, Action = new() { Name = "Wait" }, StartedAt = 1000, ActionStartedAt = 1000, LastTickAt = 3000 };
    duration.Start(0);
    pausedWait.Suspend(123000, default);
    Check(!duration.Completed(123000 - pausedWait.ActionStartedAt), "two-minute loading cannot consume five-second scripted wait");
    Check(duration.Completed(126000 - pausedWait.ActionStartedAt), "wait completes after remaining active time resumes");
    Check(!gate.Ready(false, 0) && !gate.Ready(false, 120000), "slow loading cannot open a step through elapsed wall time");
    Check(!gate.Ready(true, 120001) && !gate.Ready(true, 121000), "first ready frame is not permission to execute");
    Check(gate.Ready(true, 121001), "continuously ready state permits execution");
    Check(!gate.Ready(false, 121002) && !gate.Ready(true, 180000), "later loading invalidates earlier readiness");
    Check(gate.Ready(true, 181000), "recovered player starts a fresh readiness window");
    gate.Reset();
    Check(!gate.Ready(true, 200000), "next step cannot reuse previous completion/start evidence");
    foreach (bool world in new[] { false, true })
    foreach (bool paused in new[] { false, true })
    foreach (bool transfer in new[] { false, true })
    foreach (bool failure in new[] { false, true })
    {
        bool allowed = StepExecutionGate.CanTick(true, paused, world, transfer, failure);
        if (!world || paused || transfer || failure)
            Check(!allowed, "route queue cannot run across loading, pause, transfer or pending failure");
        else Check(allowed, "healthy route queue may tick");
    }
    Check(StepExecutionGate.CanTick(false, false, false, true, true), "queue-entry and between-run tasks may observe not-ready states");
    Check(!StepExecutionGate.CanTick(false, true, true, false, false), "manual pause also protects non-route queues");
    Check(!StepExecutionGate.NeedsMesh("SelectYesno", false, false), "dialogue selection does not require navmesh");
    Check(!StepExecutionGate.NeedsMesh("WaitFor", false, false), "state observation does not require navmesh");
    Check(!StepExecutionGate.NeedsMesh("TeleportTo", true, false), "explicit teleport does not wait for mesh");
    Check(!StepExecutionGate.NeedsMesh("MoveTo", true, true), "explicit direct movement does not require mesh readiness");
    Check(StepExecutionGate.NeedsMesh("MoveTo", true, false) && StepExecutionGate.NeedsMesh("Boss", true, false), "positioned movement and boss approach require mesh");
    var attempt = new StepAttempt { Index = 5, Action = new() { Name = "Interactable" },
        StartedAt = 1000, ActionStartedAt = 2000, LastTickAt = 3000, Attempts = 2 };
    long taskDeadline = 13000;
    taskDeadline += attempt.Suspend(63000, default);
    Check(attempt.StartedAt == 61000 && attempt.ActionStartedAt == 62000
        && taskDeadline == 73000, "one-minute loading freezes movement, action and task deadlines together");
    Check(attempt.Suspend(63000, default) == 0 && attempt.StartedAt == 61000,
        "main and queue callbacks cannot double-charge the same paused interval");
    Check(attempt.Attempts == 2 && attempt.Index == 5, "suspension neither replenishes retries nor advances step");
    attempt.LastTickAt = 64000; // One second of actual execution.
    taskDeadline += attempt.Suspend(124000, default); // Manual pause.
    Check(124000 - attempt.ActionStartedAt == 2000 && taskDeadline - 124000 == 9000,
        "multiple pauses preserve only actual execution time");
    var notStarted = new StepAttempt { Index = 0, Action = new() { Name = "Wait" }, LastTickAt = 0 };
    notStarted.Suspend(120000, default);
    Check(notStarted.ActionStartedAt == 0, "loading before invocation never creates an action start timestamp");
    notStarted.RetryAfter = 121000;
    notStarted.Suspend(180000, default, suspendRetry: true);
    Check(notStarted.RetryAfter == 181000, "loading suspends retry cooldown and mesh retry deadline");
    notStarted.Suspend(240000, default);
    Check(notStarted.RetryAfter == 181000, "waiting for mesh itself cannot extend its own readiness deadline forever");
    long Limit(string name, params string[] args) => StepTimeoutPolicy.Limit(new() { Name = name, Arguments = [.. args] }, 600000, 0, 30000);
    Check(Limit("Boss") == 600000 && Limit("KillInRange", "30") == 600000, "normal combat cannot hit the generic thirty-second action limit");
    Check(Limit("WaitFor", "Combat") == long.MaxValue, "disabled wait timeout remains disabled");
    Check(Limit("AutoMoveFor", "60000") == 90000 && Limit("Wait", "60000") == 90000, "script duration is never shorter than its action timeout");
    Check(Limit("Wait", int.MaxValue.ToString()) > int.MaxValue, "long duration plus grace cannot overflow");
    Check(Limit("Interactable", "2000001") == 30000, "interaction keeps bounded action timeout");
    Console.WriteLine("PASS: execution and completion readiness, queue ordering guards, mesh-free actions and shared suspended deadlines");
}
static void TestActionCombatCatalogue()
{
    var catalogue = new AutoDuty.Managers.ActionsManager(null!, null!).actionsList;
    var entryPoints = typeof(AutoDuty.Managers.ActionsManager).GetMethods()
        .Where(m => m.Name != "InvokeAction" // Dispatcher, not a script action.
            && m.GetParameters() is [{ ParameterType: var t }] && t == typeof(PathAction))
        .Select(m => m.Name);
    foreach (string name in catalogue.Select(a => a.actionName).Concat(entryPoints).Distinct())
    {
        Check(RouteMovementPolicy.IsReviewed(name), $"{name} requires an explicit combat policy review");
        foreach (string arg in new[] { "", "false", "true", "on", "off", "OOC", "Combat" })
        {
            PathAction step = new() { Name = name, Arguments = [arg] };
            bool fight = name is "Boss" or "KillInRange" || name == "StopForCombat" && arg == "true";
            Check(RouteMovementPolicy.StopsForCombat(step) == fight,
                $"{name}({arg}) must not wait for combat unless it is an explicit combat checkpoint");
            Check(RouteMovementPolicy.ActionMovement(step) == fight,
                $"{name}({arg}) must not grant implicit combat movement before invocation");
        }
    }
    PathAction unknown = new() { Name = "FutureAction", Arguments = ["true"] };
    Check(!RouteMovementPolicy.IsReviewed(unknown.Name) && !RouteMovementPolicy.StopsForCombat(unknown)
        && !RouteMovementPolicy.ActionMovement(unknown), "unreviewed future actions default to no combat interruption");
    Console.WriteLine("PASS: every editor action and PathAction entry point has reviewed combat ownership; explicit checkpoints and safe unknown defaults");
}

static void TestNavigationReadiness()
{
    PathAction pull = new() { Name = "StopForCombat", Arguments = ["false"] };
    PathAction move = new() { Name = "MoveTo", Arguments = ["true"] };
    PathAction fight = new() { Name = "StopForCombat", Arguments = ["true"] };
    foreach (var step in new[] { pull, move, fight, move, pull })
    {
        bool movingOnly = step.Name == "MoveTo";
        bool stopping = ReferenceEquals(step, fight);
        Check(RouteMovementPolicy.RotationEnabled(step) == !movingOnly, "rotation is owned by the current entry, independent of the preceding switch");
        Check(RouteMovementPolicy.StopsForCombat(step) == stopping, "combat cannot interrupt MoveTo or false approach, but interrupts true approach");
        Check(RouteMovementPolicy.ActionMovement(step) == stopping, "zero-position action cannot re-enable combat movement on MoveTo or false");
        Check(RouteMovementPolicy.PreferInstants(step, AutoDuty.Data.Enums.Stage.Moving) == ReferenceEquals(step, pull), "only false combat movement limits cast bars");
    }
    Check(RouteMovementPolicy.StopsForCombat(new() { Name = "Boss" }), "Boss restores normal combat even after a false step");
    Check(!RouteMovementPolicy.ActionMovement(new() { Name = "Interactable" }), "interaction retains ownership of movement");
    PathAction[] traversal =
    [
        pull, new() { Name = "MoveTo" }, new() { Name = "Jump", Arguments = ["200"] },
        new() { Name = "WaitFor", Arguments = ["IsReady"] },
        new() { Name = "Wait", Arguments = ["200"] },
        new() { Name = "Wait", Arguments = ["500"] }, move, fight,
    ];
    for (int i = 0; i < traversal.Length; i++)
    {
        Check(RouteMovementPolicy.StopsForCombat(traversal[i]) == (i == 7),
            $"pull sequence step {i} only stops for combat at explicit checkpoint 7");
        Check(RouteMovementPolicy.ActionMovement(traversal[i]) == (i == 7),
            $"pull sequence step {i} does not restore BossMod movement at timing/jump entries");
    }
    Check(!RouteMovementPolicy.StopsForCombat(new() { Name = "WaitFor", Arguments = ["OOC"] }),
        "explicit OOC condition owns its wait without enabling combat movement");
    Check(!RouteMovementPolicy.RotationEnabled(new() { Name = "MoveTo", Arguments = ["false"] }), "direct MoveTo uses the same rotation policy as navmesh MoveTo");
    foreach (var stage in new[] { AutoDuty.Data.Enums.Stage.Action, AutoDuty.Data.Enums.Stage.Waiting_For_Combat,
        AutoDuty.Data.Enums.Stage.Paused, AutoDuty.Data.Enums.Stage.Stopped, AutoDuty.Data.Enums.Stage.Interactable })
        Check(!RouteMovementPolicy.PreferInstants(pull, stage), "pull cast restriction ends outside route movement");
    var combatReady = new NavigationReadiness();
    for (long now = 0; now <= 10000; now += 100)
    {
        bool animationLocked = now % 2500 < 700;
        bool readyToWalk = StepExecutionGate.PlayerReady(true, true, !animationLocked);
        Check(combatReady.Observe(readyToWalk, true, true, now) == (now >= 1000),
            "repeated combat animation locks never interrupt navigation readiness");
    }
    Check(!StepExecutionGate.PlayerReady(true, false, false), "real occupancy still blocks navigation");
    Check(!StepExecutionGate.PlayerReady(false, true, false), "non-navigation actions still wait for animation lock");
    Check(StepExecutionGate.PlayerReady(false, true, true), "unlocked actions may start");
    Check(!combatReady.Observe(StepExecutionGate.PlayerReady(true, false, false), true, true, 10100),
        "actual loading or occupancy invalidates navigation evidence even during combat");
    Check(!combatReady.Observe(true, true, true, 10200) && combatReady.Observe(true, true, true, 11200),
        "after actual occupancy navigation still requires stable recovery");
    var ready = new NavigationReadiness();
    Check(!ready.Observe(false, false, false, 0), "loading never starts readiness timer");
    Check(!ready.Observe(false, true, true, 60000), "slow player loading cannot consume movement timeout");
    Check(!ready.Observe(true, false, true, 120000), "entry barrier waits for duty started");
    Check(!ready.Observe(true, true, false, 180000), "mesh build has no stuck verdict");
    Check(!ready.Observe(true, true, true, 200000) && !ready.Observe(true, true, true, 200999), "readiness requires a stable second");
    Check(ready.Observe(true, true, true, 201000), "ready after slow load eventually proceeds");
    Check(!ready.Observe(false, true, true, 202000), "later load resets readiness");
    Check(!ready.Observe(true, true, true, 300000) && ready.Observe(true, true, true, 301000), "new area starts a fresh stable window");
    var progress = new MovementProgress();
    var p = System.Numerics.Vector3.Zero;
    progress.Reset(p, 0);
    Check(!progress.IsStuck(p, 4999, 500) && progress.IsStuck(p, 5000, 500), "legacy 500ms setting gets five-second movement window");
    progress.Reset(p, 60000);
    Check(!progress.IsStuck(p, 64999, 500), "load suspension discards pre-load idle time");
    Check(MovementProgress.EffectiveLimit(10000) == 10000, "longer configured stuck window is preserved");
    Console.WriteLine("PASS: slow loading, duty barrier, mesh readiness, stable readiness and legacy stuck window");
}
