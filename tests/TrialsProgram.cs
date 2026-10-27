using System;
using AutoDuty.Helpers;
using static AutoDuty.Helpers.TrialsRoulettePolicy;

int checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
foreach (string name in new[] { "Duty Roulette: Trials", "随机任务：讨伐歼灭战", "随机任务:讨伐歼灭战", " 随机任务：讨伐歼灭战 ", "隨機任務：討伐殲滅戰" })
    Check(TrialsRoulettePolicy.IsTrialsName(name), "localized trials name accepted");
foreach (string name in new[] { "", "Duty Roulette: Main Scenario", "随机任务：主线任务", "随机任务：行会令", "Trials", "随机任务：讨伐歼灭战（其他）" })
    Check(!TrialsRoulettePolicy.IsTrialsName(name), "other roulette rejected");
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

// Configurable waits, continuous solo grace and hard completion cap.
var t = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
foreach (int seconds in new[] { 0, 1, 10, 300, 86400 })
{
    var p = new TrialsRoulettePolicy(); p.Start(2, seconds, seconds); p.Enter();
    Check(p.Complete(t), "first completion sends once");
    Check(!p.Complete(t), "duplicate completion must not send");
    Check(p.Deadline == t.AddSeconds(seconds), "configured completion wait");
    if (seconds > 0) { p.Tick(t.AddSeconds(seconds - 1)); Check(p.State == Phase.CompletionWait, "completion wait boundary"); }
    p.Tick(t.AddSeconds(seconds)); Check(p.State == Phase.Exiting, "configured exit");
    DateTime leaveTime = t.AddSeconds(seconds + 10);
    Check(p.Leave(leaveTime), "configured normal leave");
    Check(p.Deadline == leaveTime.AddSeconds(seconds), "requeue starts on actual leave");
    if (seconds > 0) { p.Tick(leaveTime.AddSeconds(seconds - 1)); Check(p.State == Phase.RequeueWait, "configured requeue boundary"); }
    p.Tick(leaveTime.AddSeconds(seconds)); Check(p.State == Phase.Queueing, "configured requeue");
}
{
    var p = new TrialsRoulettePolicy(); p.Start(1); p.Enter(); p.Complete(t);
    p.ObserveParty(t.AddSeconds(60), true);
    Check(p.Deadline == t.AddSeconds(90), "alone starts thirty second grace");
    p.ObserveParty(t.AddSeconds(70), true);
    Check(p.Deadline == t.AddSeconds(90), "solo observation does not extend timer");
    p.Tick(t.AddSeconds(89)); Check(p.State == Phase.CompletionWait, "full thirty seconds");
    p.Tick(t.AddSeconds(90)); Check(p.State == Phase.Exiting, "early solo exit");
}
{
    var p = new TrialsRoulettePolicy(); p.Start(1); p.Enter(); p.Complete(t);
    p.ObserveParty(t.AddSeconds(290), true);
    Check(p.Deadline == t.AddSeconds(300), "solo grace cannot exceed hard cap");
    p.Tick(t.AddSeconds(300)); Check(p.State == Phase.Exiting, "hard cap wins");
}
{
    var p = new TrialsRoulettePolicy(); p.Start(1); p.Enter(); p.Complete(t);
    p.ObserveParty(t.AddSeconds(60), true); p.ObserveParty(t.AddSeconds(70), false);
    Check(p.Deadline == t.AddSeconds(300), "others returning cancels solo grace");
    p.ObserveParty(t.AddSeconds(80), true);
    Check(p.Deadline == t.AddSeconds(110), "fresh thirty seconds when alone again");
    p.Stop(); p.ObserveParty(t.AddDays(1), true); p.Tick(t.AddDays(1));
    Check(p.State == Phase.Stopped && !p.Complete(t), "no message or exit after stopping");
    p.Start(1, -1, -1); p.Enter(); p.Complete(t); p.Tick(t);
    Check(p.State == Phase.Exiting, "negative delay clamps to zero");
}
Check(TrialsRoulettePolicy.ClampSeconds(int.MaxValue) == 86400, "delay bounded");
Check(TrialsRoulettePolicy.PartyCommand("/p 辛苦啦~对位已赞，可以放心退本了<se.2>") == "/p 辛苦啦~对位已赞，可以放心退本了<se.2>", "default preserved with sound tag");
Check(TrialsRoulettePolicy.PartyCommand("hello") == "/p hello", "plain message routed to party");
Check(TrialsRoulettePolicy.PartyCommand("/party hello") == "/p hello", "party alias normalized");
foreach (string invalid in new[] { "", " /p ", "/logout", "hello\n/p again", "hello\0", new string('中', 200) })
    Check(TrialsRoulettePolicy.PartyCommand(invalid) == null, "invalid party message rejected");
Console.WriteLine($"Trials policy: {checks} total checks passed including timings, solo grace and party chat.");
