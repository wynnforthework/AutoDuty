using System;
using System.Numerics;

namespace AutoDuty.Helpers;

// Optional, portable completion contract attached to the step that triggers travel.
public sealed class TransitionRegion
{
    public uint Territory { get; set; }
    public Vector3 Center { get; set; }
    public float Radius { get; set; } = 15;
    public float HeightTolerance { get; set; } = 3;
    public int StableMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 60000;
    public bool RequireGround { get; set; } = true;
    public bool RequireStillness { get; set; } = true;

    internal bool Contains(uint territory, Vector3 position) => (this.Territory == 0 || this.Territory == territory)
        && MathF.Abs(position.Y - this.Center.Y) <= this.HeightTolerance
        && Vector2.DistanceSquared(new(position.X, position.Z), new(this.Center.X, this.Center.Z)) <= this.Radius * this.Radius;

    internal bool Valid => float.IsFinite(this.Center.X) && float.IsFinite(this.Center.Y) && float.IsFinite(this.Center.Z)
        && float.IsFinite(this.Radius) && this.Radius is >= 1 and <= 500
        && float.IsFinite(this.HeightTolerance) && this.HeightTolerance is >= .5f and <= 100
        && this.StableMs is >= 500 and <= 10000 && this.TimeoutMs >= this.StableMs + 2000 && this.TimeoutMs <= 600000;
}

internal sealed class TransitionStability
{
    private Vector3 anchor;
    private long? since;
    internal bool Observe(bool ready, Vector3 position, long now, int stableMs, bool stillness)
    {
        if (!ready) { this.Reset(); return false; }
        if (this.since == null || (stillness && Vector3.DistanceSquared(this.anchor, position) > .25f))
        { this.anchor = position; this.since = now; }
        return now - this.since.Value >= stableMs;
    }
    internal void Reset() => this.since = null;
}

internal sealed class DisplacementMonitor
{
    private Vector3 previous;
    private uint territory;
    private long? at;
    internal Vector3 Source { get; private set; }
    internal uint SourceTerritory { get; private set; }
    internal string? Observe(uint territory, Vector3 position, long now)
    {
        string? reason = null;
        if (this.at is { } at && now > at && now - at <= 10000)
        {
            if (territory == this.territory && now - at < 100) return null;
            Vector3 delta = position - this.previous;
            float seconds = Math.Max(.016f, (now - at) / 1000f);
            float horizontal = new Vector2(delta.X, delta.Z).Length();
            if (territory != this.territory) reason = "territory-changed";
            else if ((horizontal > 3 && horizontal / seconds > 30)
                || (MathF.Abs(delta.Y) > 2 && MathF.Abs(delta.Y) / seconds > 20)) reason = "forced-displacement";
        }
        this.Source = this.previous; this.SourceTerritory = this.territory;
        this.at = now; this.previous = position; this.territory = territory;
        return reason;
    }
    internal void Reset() => this.at = null;
}
