namespace AutoDuty.Helpers;

using System;

// Independent of game APIs so completion, delay and cancellation rules can be tested.
internal sealed class TrialsRoulettePolicy
{
    internal enum Phase { Stopped, Queueing, InDuty, CompletionWait, Exiting, RequeueWait }
    internal Phase State { get; private set; }
    internal int Completed { get; private set; }
    internal int Limit { get; private set; }
    internal DateTime Deadline { get; private set; }
    internal bool Finished => Limit > 0 && Completed >= Limit;
    internal void Start(int limit) { Limit = Math.Max(0, limit); Completed = 0; State = Phase.Queueing; }
    internal void Stop() => State = Phase.Stopped;
    internal void Enter() { if (State == Phase.Queueing) State = Phase.InDuty; }
    internal void Complete(DateTime now)
    {
        if (State != Phase.InDuty) return;
        Completed++;
        Deadline = now.AddMinutes(5);
        State = Phase.CompletionWait;
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
        else { State = Phase.RequeueWait; Deadline = now.AddSeconds(30); }
        return true;
    }
}
