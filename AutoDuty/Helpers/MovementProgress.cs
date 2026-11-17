using System;
using System.Numerics;

namespace AutoDuty.Helpers;

internal sealed class MovementProgress
{
    internal static long EffectiveLimit(long configured) => Math.Max(5000, configured);
    private Vector3 anchor;
    private long since;
    private bool initialized;

    internal void Reset(Vector3 position, long now)
    {
        this.anchor = position;
        this.since = now;
        this.initialized = true;
    }

    internal bool IsStuck(Vector3 position, long now, long limit)
    {
        if (!this.initialized || Vector3.DistanceSquared(position, this.anchor) > .25f)
            this.Reset(position, now);
        // Path running/finding toggles deliberately do not reset this timer.
        return now - this.since >= EffectiveLimit(limit);
    }
}
