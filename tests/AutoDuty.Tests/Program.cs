using System.Runtime.Loader;
using AutoDuty.Helpers;

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
    TestDutyExecutionBoundary();
    TestBossProgress();
    TestCombatCheckpoint();
    TestLevelingPlan();
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
    TestNavigationReadiness();
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
    Check(BossIdentity.Matches("Myrmidon Princess", "蚁后", "myrmidon princess"), "English route boss name matches localized NPC via English sheet");
    Check(BossIdentity.Matches(" 蚁后 ", "蚁后", "myrmidon princess"), "localized exact boss identity is accepted");
    Check(!BossIdentity.Matches("Myrmidon Princess", "蚁兵", "myrmidon soldier"), "combat add cannot substitute for named boss");
    Check(!BossIdentity.Matches("", "蚁后", "myrmidon princess"), "empty note does not match arbitrary NPC");
    Check(!BossIdentity.Matches("Myrmidon", "蚁后", "myrmidon princess"), "partial route note is not boss identity evidence");
    Console.WriteLine("PASS: reported sands endpoint, height guard, exact localized/English boss identity and add rejection");
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
static void TestNavigationReadiness()
{
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
