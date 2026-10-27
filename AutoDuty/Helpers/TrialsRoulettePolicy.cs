namespace AutoDuty.Helpers;

using System;
using System.Linq;

// Independent of game APIs so completion, delay and cancellation rules can be tested.
internal sealed class TrialsRoulettePolicy
{
    // Ignore presentation-only whitespace and full-width punctuation, not duty names.
    internal static bool IsTrialsName(string name)
    {
        string normalized = string.Concat(name.Normalize(System.Text.NormalizationForm.FormKC)
            .Where(c => !char.IsWhiteSpace(c)));
        return normalized.Equals("DutyRoulette:Trials", StringComparison.OrdinalIgnoreCase)
            || normalized == "随机任务:讨伐歼灭战"
            || normalized == "隨機任務:討伐殲滅戰";
    }

    internal enum Phase { Stopped, Queueing, InDuty, CompletionWait, Exiting, RequeueWait }
    internal Phase State { get; private set; }
    internal int Completed { get; private set; }
    internal int Limit { get; private set; }
    internal DateTime Deadline { get; private set; }
    private int completionWaitSeconds = 300;
    private int requeueWaitSeconds = 30;
    private DateTime maximumExitTime;
    private DateTime? aloneSince;
    internal static int ClampSeconds(int value) => Math.Clamp(value, 0, 86400);
    internal static string? PartyCommand(string? input)
    {
        string text = (input ?? "").Trim();
        if (text.StartsWith("/p ", StringComparison.OrdinalIgnoreCase)) text = text[3..].Trim();
        else if (text.StartsWith("/party ", StringComparison.OrdinalIgnoreCase)) text = text[7..].Trim();
        if (text.Length == 0 || text.StartsWith('/') || text.Any(char.IsControl)) return null;
        string command = "/p " + text;
        return System.Text.Encoding.UTF8.GetByteCount(command) <= 500 ? command : null;
    }
    internal bool Finished => Limit > 0 && Completed >= Limit;
    internal void Start(int limit, int completionSeconds = 300, int requeueSeconds = 30)
    {
        Limit = Math.Max(0, limit); Completed = 0;
        completionWaitSeconds = ClampSeconds(completionSeconds);
        requeueWaitSeconds = ClampSeconds(requeueSeconds);
        aloneSince = null; State = Phase.Queueing;
    }
    internal void Stop() => State = Phase.Stopped;
    internal void Enter() { if (State == Phase.Queueing) State = Phase.InDuty; }
    internal bool Complete(DateTime now)
    {
        if (State != Phase.InDuty) return false;
        Completed++;
        Deadline = maximumExitTime = now.AddSeconds(completionWaitSeconds);
        aloneSince = null;
        State = Phase.CompletionWait;
        return true;
    }
    internal void ObserveParty(DateTime now, bool onlySelf)
    {
        if (State != Phase.CompletionWait) return;
        if (!onlySelf) { aloneSince = null; Deadline = maximumExitTime; return; }
        aloneSince ??= now;
        DateTime soloExit = aloneSince.Value.AddSeconds(30);
        Deadline = soloExit < maximumExitTime ? soloExit : maximumExitTime;
    }
    internal void Tick(DateTime now)
    {
        if (now < Deadline) return;
        if (State == Phase.CompletionWait) { State = Phase.Exiting; Deadline = now.AddMinutes(1); }
        else if (State == Phase.RequeueWait) State = Phase.Queueing;
    }
    internal bool Leave(DateTime now)
    {
        if (State != Phase.Exiting) { Stop(); return false; }
        if (Finished) Stop();
        else { State = Phase.RequeueWait; Deadline = now.AddSeconds(requeueWaitSeconds); }
        return true;
    }
}
