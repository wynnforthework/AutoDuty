using System;
using AutoDuty.Helpers;
using static AutoDuty.Helpers.TrialsRoulettePolicy;

int checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
var now = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
foreach (int limit in new[] { 0, 1, 2, 5 })
{
    var p = new TrialsRoulettePolicy();
    p.Start(limit);
    Check(p.State == Phase.Queueing && p.Completed == 0, "start/reset");
    p.Complete(now);
    Check(p.Completed == 0, "ignore unrelated completion");
    for (int run = 1; run <= (limit == 0 ? 10 : limit); run++)
    {
        p.Enter();
        p.Complete(now);
        p.Complete(now.AddSeconds(1));
        Check(p.Completed == run, "completion exactly once");
        p.Tick(now.AddSeconds(299));
        Check(p.State == Phase.CompletionWait, "no early exit");
        p.Tick(now.AddMinutes(5));
        Check(p.State == Phase.Exiting, "exit at five minutes");
        now = now.AddMinutes(5).AddSeconds(8);
        Check(p.Leave(now), "expected exit");
        if (limit > 0 && run == limit) Check(p.State == Phase.Stopped, "automatic stop");
        else
        {
            Check(p.State == Phase.RequeueWait, "wait starts at actual exit");
            p.Tick(now.AddSeconds(29));
            Check(p.State == Phase.RequeueWait, "no early requeue");
            now = now.AddSeconds(30);
            p.Tick(now);
            Check(p.State == Phase.Queueing, "requeue after thirty seconds");
        }
    }
    p.Stop();
    p.Complete(now);
    p.Tick(now.AddDays(1));
    p.Enter();
    Check(p.State == Phase.Stopped, "manual stop cannot resurrect");
}
foreach (var phase in new[] { Phase.Queueing, Phase.InDuty, Phase.CompletionWait, Phase.Exiting, Phase.RequeueWait })
{
    var p = new TrialsRoulettePolicy();
    p.Start(0);
    if (phase != Phase.Queueing) p.Enter();
    if (phase is Phase.CompletionWait or Phase.Exiting or Phase.RequeueWait) p.Complete(now);
    if (phase is Phase.Exiting or Phase.RequeueWait) p.Tick(now.AddMinutes(5));
    if (phase == Phase.RequeueWait) p.Leave(now.AddMinutes(5));
    Check(p.State == phase, "test setup");
    p.Stop(); p.Tick(now.AddDays(1)); p.Complete(now); p.Enter();
    Check(p.State == Phase.Stopped, "cancel in each phase");
}
foreach (bool completed in new[] { false, true })
{
    var p = new TrialsRoulettePolicy(); p.Start(0); p.Enter();
    if (completed) p.Complete(now);
    Check(!p.Leave(now) && p.State == Phase.Stopped, "unexpected early exit stops loop");
}
Console.WriteLine($"Trials policy: {checks} checks passed.");
