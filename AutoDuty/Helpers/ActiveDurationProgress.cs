using System;

namespace AutoDuty.Helpers;

// Per-action timer measured against the suspended step clock, not a shared
// throttle key. Starting another Wait cannot consume/reset this timer.
internal sealed class ActiveDurationProgress(long duration)
{
    private long? startedAt;
    internal void Start(long activeTime) => this.startedAt = activeTime;
    internal bool Completed(long activeTime) => this.startedAt is { } start
        && activeTime - start >= Math.Max(0, duration);
}
