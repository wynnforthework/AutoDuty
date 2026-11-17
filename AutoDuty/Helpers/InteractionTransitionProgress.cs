using System.Numerics;

namespace AutoDuty.Helpers;

// Evidence belongs to one actual interaction, never to a nearby movement step.
internal sealed class InteractionTransitionProgress
{
    internal PathAction? Step { get; private set; }
    internal int Index { get; private set; } = -1;
    internal bool Pending { get; private set; }
    private uint territory;
    private Vector3 origin;
    private long clickedAt;
    private long loadingAt;
    private long? readySince;

    internal static bool RequiresTransfer(uint territory, uint objectId)
        => territory == 1303 && objectId is >= 2000460 and <= 2000465;

    internal void Click(PathAction step, int index, uint territory, Vector3 position, long now)
    {
        if (ReferenceEquals(this.Step, step) && this.Index == index) return;
        this.Reset();
        this.Step = step;
        this.Index = index;
        this.territory = territory;
        this.origin = position;
        this.clickedAt = now;
    }

    internal bool ObserveLoading(PathAction step, int index, long now, bool deathOrReturn)
    {
        if (deathOrReturn || !ReferenceEquals(this.Step, step) || this.Index != index
            || (!this.Pending && now - this.clickedAt >= 30_000)) return false;
        if (!this.Pending) this.loadingAt = now;
        this.Pending = true;
        this.readySince = null;
        return true;
    }

    internal bool Settled(long now, bool loading, bool valid, uint territory, Vector3 position)
    {
        if (!this.Pending) return false;
        if (loading || !valid) { this.readySince = null; return false; }
        this.readySince ??= now;
        return now - this.readySince.Value >= 1000
            && (territory != this.territory || Vector3.DistanceSquared(this.origin, position) > 4);
    }

    internal bool TimedOut(long now) => this.Step != null
        && now - (this.Pending ? this.loadingAt : this.clickedAt) >= (this.Pending ? 60_000 : 30_000);

    internal void Reset()
    {
        this.Step = null;
        this.Index = -1;
        this.Pending = false;
        this.readySince = null;
    }
}
