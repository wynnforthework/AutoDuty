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
    private PathAction? approachingStep;
    private int approachingIndex;
    private uint approachingTerritory;
    private Vector3 approachingPosition;
    private long approachedAt;
    private PathAction? movementStep;
    private int movementIndex;
    private uint movementTerritory;
    private Vector3 movementOrigin;
    private Vector3? forcedLanding;
    private PathAction? launchStep;
    private int launchIndex;
    private uint launchTerritory;
    private Vector3 launchOrigin;
    private long launchAt;
    private readonly TransitionStability stability = new();
    internal TransitionRegion? Destination { get; private set; }
    internal string WaitingReason { get; private set; } = "waiting-for-transfer";
    internal void Expect(TransitionRegion? destination) => this.Destination = destination ?? this.Step?.Transition;
    internal bool ActivateExpectedMovement(PathAction step, int index, uint territory, Vector3 position, long now)
    {
        if (this.Pending || step.Transition == null || !ReferenceEquals(this.Step, step) || this.Index != index
            || (territory == this.territory && Vector3.DistanceSquared(this.origin, position) <= 4)) return false;
        return this.ObserveLoading(step, index, now, false);
    }

    internal bool BeginDisplacement(PathAction step, int index, uint territory, Vector3 source, long now,
        TransitionRegion? destination)
    {
        if (destination == null || !destination.Valid) return false;
        this.Click(step, index, territory, source, now);
        this.Expect(destination);
        return this.ObserveLoading(step, index, now, false);
    }

    internal static TransitionRegion? InferDestination(PathAction? next, PathAction? after) =>
        next is { Name: "WaitFor", Arguments.Count: 1 } && next.Position == Vector3.Zero
        && WaitForArguments.Normalize(next.Arguments[0]) is ["IsReady"] && after != null && after.Position != Vector3.Zero
            ? new() { Center = after.Position, Radius = 100 } : null;

    internal bool ObserveForcedMovement(PathAction step, int index, uint territory, Vector3 position,
        long now, bool deathOrReturn, PathAction? next, PathAction? landing)
    {
        // Launch pads need not raise BetweenAreas at all. Require route intent,
        // a recent approach on the source floor, and actual travel toward a
        // different destination floor. Ordinary jumps/knockbacks are insufficient.
        if (this.Pending || deathOrReturn || step.Name != "MoveTo"
            || next is not { Name: "WaitFor", Arguments.Count: 1 } || next.Position != Vector3.Zero
            || WaitForArguments.Normalize(next.Arguments[0]) is not ["IsReady"]
            || landing == null || landing.Position == Vector3.Zero
            || !ReferenceEquals(this.launchStep, step) || this.launchIndex != index
            || this.launchTerritory != territory || now < this.launchAt || now - this.launchAt > 2000) return false;
        float floorChange = landing.Position.Y - step.Position.Y;
        float travelled = position.Y - this.launchOrigin.Y;
        if (System.MathF.Abs(floorChange) < 10 || travelled * System.MathF.Sign(floorChange) < 6
            || System.MathF.Abs(position.Y - landing.Position.Y) >= System.MathF.Abs(this.launchOrigin.Y - landing.Position.Y)) return false;
        Vector3 source = this.launchOrigin;
        this.Click(step, index, territory, source, now);
        this.forcedLanding = landing.Position;
        this.Expect(step.Transition ?? new TransitionRegion { Center = landing.Position, Radius = 100 });
        return this.ObserveLoading(step, index, now, false);
    }

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
            this.launchStep = null;
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
        if (ReferenceEquals(this.approachingStep, step) && System.MathF.Abs(position.Y - step.Position.Y) <= 3)
        {
            this.launchStep = step;
            this.launchIndex = index;
            this.launchTerritory = territory;
            this.launchOrigin = position;
            this.launchAt = now;
        }
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
        this.Expect(step.Transition);
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
        this.stability.Reset();
        return true;
    }

    internal bool Settled(long now, bool loading, bool valid, uint territory, Vector3 position, bool groundReady = true)
    {
        if (!this.Pending) return false;
        if (loading || !valid) { this.stability.Reset(); this.WaitingReason = "player-or-loading"; return false; }
        if (this.Destination is { } expected)
        {
            bool arrived = expected.Contains(territory, position);
            this.WaitingReason = !arrived ? "destination-region" : expected.RequireGround && !groundReady ? "ground-or-mesh" : "position-stability";
            return this.stability.Observe(arrived && (!expected.RequireGround || groundReady)
                && (territory != this.territory || Vector3.DistanceSquared(this.origin, position) > 4),
                position, now, expected.StableMs, expected.RequireStillness);
        }
        if (this.forcedLanding is { } landing
            && (System.MathF.Abs(position.Y - landing.Y) > 3
                || new Vector2(position.X - landing.X, position.Z - landing.Z).LengthSquared() > 10000))
        { this.stability.Reset(); return false; }
        this.WaitingReason = "position-stability";
        return this.stability.Observe(territory != this.territory || Vector3.DistanceSquared(this.origin, position) > 4,
            position, now, 1000, true);
    }

    internal bool TimedOut(long now) => this.Step != null
        && now - (this.Pending ? this.loadingAt : this.clickedAt) >= (this.Destination?.TimeoutMs ?? (this.Pending ? 60_000 : 30_000));

    internal void Reset()
    {
        this.approachingStep = null;
        this.movementStep = null;
        this.Step = null;
        this.Index = -1;
        this.Pending = false;
        this.forcedLanding = null;
        this.launchStep = null;
        this.Destination = null;
        this.stability.Reset();
        this.WaitingReason = "waiting-for-transfer";
    }
}
