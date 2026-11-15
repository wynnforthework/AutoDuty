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
        var decoded = AutoDuty.Data.PathJson.ReadFile(encoded);
        Check(encoded == AutoDuty.Data.PathJson.WriteFile(decoded), "path round trip: " + path);
        count++;
    }
    var old = AutoDuty.Data.PathJson.ReadFile("""{"Actions":[{"Name":"Wait","Position":{"X":1,"Y":2,"Z":3},"Arguments":["100"],"Conditions":[{"$type":"AutoDuty.Data.Classes+PathActionConditionToDo, AutoDuty","index":0,"count":1,"operatorValue":"<"}]}]}""");
    Check(old.Actions[0].Conditions.Count == 1, "legacy condition");
    Check(AutoDuty.Data.PathJson.ReadFile(AutoDuty.Data.PathJson.WriteFile(old)).Actions[0].Conditions.Count == 1, "new condition");
    try { AutoDuty.Data.PathJson.ReadFile("""{"SchemaVersion":999,"Actions":[]}"""); throw new Exception("accepted future schema"); } catch (Newtonsoft.Json.JsonException) { }
    Console.WriteLine($"PASS: {count} bundled paths round trip; legacy/new conditions and future schema rejection");
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
