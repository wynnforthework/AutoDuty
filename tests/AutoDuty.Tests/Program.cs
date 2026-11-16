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
    Console.WriteLine("PASS: 9 action relocation cases");
    TestPaths();
    TestRecording();
    TestValidation();
    TestRunMetrics();
    TestAutomationReports();
    TestLevelingPlan();
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
