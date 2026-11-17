namespace AutoDuty.Helpers;

internal sealed class NavigationReadiness
{
    private long? readySince;
    internal void Reset() => this.readySince = null;
    internal bool Observe(bool playerReady, bool dutyStarted, bool meshReady, long now)
    {
        if (!playerReady || !dutyStarted || !meshReady) { this.Reset(); return false; }
        this.readySince ??= now;
        return now - this.readySince.Value >= 1000;
    }
}
