using System;

namespace AutoDuty.Helpers;

// Uses the suspended action clock, so loading and manual pause consume no wait.
internal sealed class CombatStopProgress(bool alreadyFinished)
{
    private bool observedCombat = alreadyFinished;
    private long startedAt;
    private long? quietSince;
    internal void Start(long now) => this.startedAt = now;
    internal bool Tick(bool inCombat, long now)
    {
        if (inCombat)
        {
            this.observedCombat = true;
            this.quietSince = null;
            return false;
        }
        if (!this.observedCombat) return false;
        this.quietSince ??= now;
        return now - this.quietSince.Value >= 500;
    }
    internal bool SpawnTimedOut(long now, int limit) => !this.observedCombat && now - this.startedAt >= Math.Max(1000, limit);
}
