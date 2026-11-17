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
    TestCombatCheckpoint();
    TestLevelingPlan();
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
    failed.FirstProblem = "stuck"; failed.ProblemStep = 1; failed.Failures = 1; failed.AutonomousSteps = 1;
    var groups = AutomationReports.Summarize([good, failed]);
    Check(groups.Count == 1 && groups[0].Reliability == 50 && groups[0].MeanCompletion == 75 && groups[0].Participants == 2, "failed attempts and multiple contributors are included");
    Check(groups[0].ExperiencePerHour > 3500000 && groups[0].ExperiencePerHour < 3700000, "XP/hour uses all attempt durations");
    var tank = Clean(); tank.JobId = 21;
    var party = Clean(); party.PartySize = 4;
    var mode = Clean(); mode.Mode = "Regular/Unsynced";
    var version = Clean(); version.RouteHash = AutomationReports.Fingerprint(actions);
    Check(AutomationReports.Summarize([good, tank, party, mode, version]).Count == 5, "job, party, mode and version cohorts remain separate");
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
    var navStop = new NavigationStopProgress();
    Check(!navStop.StoppedShort(false, 1000) && !navStop.StoppedShort(false, 2999), "idle navigation allows asynchronous path startup grace");
    Check(navStop.StoppedShort(false, 3000), "stopped short is detected after 2 seconds, not 60");
    Check(!navStop.StoppedShort(true, 4000) && !navStop.StoppedShort(false, 10000), "active pathfinding clears idle grace");
    navStop.Reset();
    Check(!navStop.StoppedShort(false, 100000), "retry and combat resume start fresh idle observation");
    Console.WriteLine("PASS: adjacent early-combat switch, no empty-area guessing, no boss/remote reuse, new waves, death/reset and scoped arrival tolerance");
}
