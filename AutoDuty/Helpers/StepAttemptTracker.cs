using System;
using System.Numerics;

namespace AutoDuty.Helpers;

internal sealed class StepAttempt
{
    internal required int Index;
    internal required PathAction Action;
    internal int Attempts;
    internal int StuckCount;
    internal long StartedAt = Environment.TickCount64;
    internal long ActionStartedAt;
    internal long LastTickAt = Environment.TickCount64;
    internal bool MovementStarted;
    internal bool StartReported;
    internal readonly NavigationStopProgress NavigationStop = new();
    internal readonly MovementProgress Progress = new();
    internal readonly CombatApproachProgress CombatApproach = new();
    internal long RetryAfter;
    internal Vector3 StartPosition;
    internal string? LastFailReason;
    internal string? SkippedReason;

    internal long Suspend(long now, Vector3 position, bool suspendRetry = false)
    {
        long delta = Math.Max(0, now - this.LastTickAt);
        this.StartedAt += delta;
        if (this.ActionStartedAt != 0) this.ActionStartedAt += delta;
        if (suspendRetry && this.RetryAfter > 0) this.RetryAfter += delta;
        this.LastTickAt = now;
        this.Progress.Reset(position, now);
        this.NavigationStop.Reset();
        return delta;
    }
}

internal sealed record StepFailure(int Index, string Reason, string Detail, int Attempts, DateTime At);
