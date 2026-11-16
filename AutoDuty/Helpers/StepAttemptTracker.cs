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
    internal readonly NavigationStopProgress NavigationStop = new();
    internal Vector3 StartPosition;
    internal string? LastFailReason;
    internal string? SkippedReason;
}

internal sealed record StepFailure(int Index, string Reason, string Detail, int Attempts, DateTime At);
