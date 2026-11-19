namespace AutoDuty.Helpers;

// Loading is neither a failed step nor proof of completion.
internal sealed class LoadingRecoveryProgress
{
    private long? readySince;
    internal bool Ready(long now, bool ready)
    {
        if (!ready) { this.readySince = null; return false; }
        this.readySince ??= now;
        return now - this.readySince.Value >= 1000;
    }
    internal void Reset() => this.readySince = null;
    internal static bool CanResume(string action, bool deathOrReturn, bool failed)
        => action == "MoveTo" && !deathOrReturn && !failed;
}
