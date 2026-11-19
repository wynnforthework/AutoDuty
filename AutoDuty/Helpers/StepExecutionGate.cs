namespace AutoDuty.Helpers;

// A stable observation window is not a sleep that grants permission.
internal sealed class StepExecutionGate
{
    private long? readySince;
    internal bool Ready(bool ready, long now)
    {
        if (!ready) { this.Reset(); return false; }
        this.readySince ??= now;
        return now - this.readySince.Value >= 1000;
    }
    internal void Reset() => this.readySince = null;
    internal static bool CanTick(bool route, bool paused, bool worldReady, bool transfer, bool failure)
        => !paused && (!route || (worldReady && !transfer && !failure));

    internal static bool NeedsMesh(string action, bool hasPosition, bool directMove)
        => hasPosition && action != "TeleportTo" && !directMove;

    // Skill animation locks do not invalidate a walking route. Requiring a
    // continuous lock-free second here repeatedly cancels paths during attacks.
    // Non-navigation actions retain their stricter readiness requirement.
    internal static bool PlayerReady(bool navigationStage, bool ready, bool readyFull)
        => navigationStage ? ready : readyFull;
}
