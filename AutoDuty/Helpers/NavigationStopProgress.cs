namespace AutoDuty.Helpers;

// Observe only during movement, after checking arrival and combat interruption.
internal sealed class NavigationStopProgress
{
    internal const long GraceMs = 2000;
    private long? idleSince;

    internal void Reset() => this.idleSince = null;

    internal bool StoppedShort(bool navigationBusy, long now)
    {
        if (navigationBusy) { this.Reset(); return false; }
        this.idleSince ??= now;
        return now - this.idleSince.Value >= GraceMs;
    }
}
