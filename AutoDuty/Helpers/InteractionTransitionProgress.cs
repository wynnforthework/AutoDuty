using System.Numerics;

namespace AutoDuty.Helpers;

// Transfer evidence belongs to one step: an actual interaction, or loading triggered
// while actively approaching a nearby MoveTo destination.
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
    private PathAction? approachingStep;
    private int approachingIndex;
    private uint approachingTerritory;
    private Vector3 approachingPosition;
    private long approachedAt;
    private PathAction? movementStep;
    private int movementIndex;
    private uint movementTerritory;
    private Vector3 movementOrigin;

    internal static bool IsScriptedTransfer(PathAction step, PathAction? next) => step.Name == "MoveTo"
        && step.Arguments.Count > 0 && bool.TryParse(step.Arguments[0], out bool mesh) && !mesh
        && next is { Name: "WaitFor", Arguments.Count: 1 } && next.Position == Vector3.Zero
        && WaitForArguments.Normalize(next.Arguments[0]) is ["IsReady"];

    internal void TrackMovement(PathAction step, int index, uint territory, Vector3 position, long now,
        PathAction? next = null)
    {
        if (this.Pending) return;
        if (!ReferenceEquals(this.movementStep, step) || this.movementIndex != index || this.movementTerritory != territory)
        {
            this.movementStep = step;
            this.movementIndex = index;
            this.movementTerritory = territory;
            this.movementOrigin = position;
        }
        // Direct MoveTo + WaitFor IsReady is an existing portal idiom. Its
        // loading trigger can precede the destination by more than 10m. Accept
        // actual progress along that specific segment, not arbitrary loading.
        Vector3 segment = step.Position - this.movementOrigin;
        float lengthSquared = segment.LengthSquared();
        float fraction = lengthSquared > .25f ? Vector3.Dot(position - this.movementOrigin, segment) / lengthSquared : -1;
        bool onTransferSegment = IsScriptedTransfer(step, next) && fraction > 0 && fraction <= 1
            && Vector3.DistanceSquared(position, this.movementOrigin) >= .25f
            && Vector3.DistanceSquared(position, this.movementOrigin + fraction * segment) <= 9;
        this.approachingStep = step.Name == "MoveTo"
            && (Vector3.DistanceSquared(step.Position, position) <= 100 || onTransferSegment) ? step : null;
        this.approachingIndex = index;
        this.approachingTerritory = territory;
        this.approachingPosition = position;
        this.approachedAt = now;
    }

    internal bool ObserveTrackedMovementLoading(PathAction step, int index, long now, bool deathOrReturn)
    {
        if (deathOrReturn) return false;
        if (this.Pending) return this.ObserveLoading(step, index, now, false);
        if (!ReferenceEquals(this.approachingStep, step) || this.approachingIndex != index
            || now < this.approachedAt || now - this.approachedAt > 2000) return false;
        this.Click(step, index, this.approachingTerritory, this.approachingPosition, now);
        return this.ObserveLoading(step, index, now, false);
    }

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

    internal bool ObserveMovementLoading(PathAction step, int index, bool moving,
        uint territory, Vector3 position, long now, bool deathOrReturn)
    {
        // Loading alone must never skip an arbitrary movement. A portal can trigger
        // before the recorded destination (Dusk Vigil triggers about 7m before it).
        // Restrict automatic ownership to a 10m sphere around that destination.
        if (!moving || deathOrReturn || step.Name != "MoveTo"
            || Vector3.DistanceSquared(step.Position, position) > 100) return false;
        this.Click(step, index, territory, position, now);
        return this.ObserveLoading(step, index, now, false);
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
        this.approachingStep = null;
        this.movementStep = null;
        this.Step = null;
        this.Index = -1;
        this.Pending = false;
        this.readySince = null;
    }
}
