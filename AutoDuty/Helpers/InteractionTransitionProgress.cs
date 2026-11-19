using System.Numerics;
using System.Collections.Generic;

namespace AutoDuty.Helpers;

internal enum TransitionPhase { AwaitingTrigger, Suspected, Travelling, ValidatingDestination, DestinationConfirmed, StepCompleted }

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
    private readonly TransitionStability processStability = new();
    internal TransitionPhase Phase { get; private set; }
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
            ? ContinuationEvidence(after.Position)
            : next != null && next.Position != Vector3.Zero && next.Name is "MoveTo" or "Boss" or "Interactable"
                ? ContinuationEvidence(next.Position) : null;

    // Readiness barriers describe a transfer followed by independent navigation.
    // No coordinate envelope can be inferred for its landing from the next target.
    private static TransitionRegion ContinuationEvidence(Vector3 target) => new()
    { Center = target, IsContinuationTarget = true, RequirePath = true };

    // Inspect only a bounded, linear sequence of harmless zero-position entries.
    // Never search through a branch, interaction, combat checkpoint or teleport.
    internal static PathAction? ContinuationStep(IReadOnlyList<PathAction> actions, int index)
    {
        if (index < 0 || index >= actions.Count) return null;
        for (int i = index + 1; i < actions.Count && i - index <= 8; i++)
        {
            var step = actions[i];
            if (step.Conditions.Count != 0 || step.ConditionsWhile.Count != 0 || step.ConditionsUntil.Count != 0)
                return null;
            if (step.Position != Vector3.Zero)
                return step.Name is "MoveTo" or "Boss" or "Interactable" or "Wait" ? step : null;
            bool ready = step.Name == "WaitFor" && step.Arguments.Count == 1
                && WaitForArguments.Normalize(step.Arguments[0]) is ["IsReady"];
            bool pull = step.Name == "StopForCombat" && step.Arguments.Count == 1
                && bool.TryParse(step.Arguments[0], out bool stop) && !stop;
            if (!ready && !pull && step.Name != "<-- Comment -->") return null;
        }
        return null;
    }

    internal static TransitionRegion? InferDestination(IReadOnlyList<PathAction> actions, int index)
    {
        if (index < 0 || index >= actions.Count - 1) return null;
        // The recorded readiness barrier establishes transfer intent; intervening
        // zero-position pull switches are still executed normally after completion.
        var continuation = ContinuationStep(actions, index);
        return continuation == null ? null : InferDestination(actions[index + 1], continuation);
    }

    internal bool CompleteStep(PathAction step, int index, bool conditions, bool pathConfirmed, bool pathWaitFinished)
    {
        if (!ReferenceEquals(this.Step, step) || this.Index != index || !this.Pending
            || this.Phase != TransitionPhase.DestinationConfirmed || this.Destination == null) return false;
        if (!conditions) { this.WaitingReason = "step-conditions"; return false; }
        if ((this.Destination.RequirePath && !pathConfirmed) || !pathWaitFinished)
        { this.WaitingReason = "path-evidence"; return false; }
        this.Phase = TransitionPhase.StepCompleted;
        return true;
    }

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
        this.Expect(step.Transition ?? ContinuationEvidence(landing.Position));
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
            && ((step.Transition?.NearTrigger(step.Position, position) ?? (Vector3.DistanceSquared(step.Position, position) <= 100)) || onTransferSegment) ? step : null;
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
            || !(step.Transition?.NearTrigger(step.Position, position) ?? (Vector3.DistanceSquared(step.Position, position) <= 100))) return false;
        this.Click(step, index, territory, position, now);
        return this.ObserveLoading(step, index, now, false);
    }

    internal bool ObserveLoading(PathAction step, int index, long now, bool deathOrReturn)
    {
        if (deathOrReturn || !ReferenceEquals(this.Step, step) || this.Index != index
            || (!this.Pending && now - this.clickedAt >= 30_000)) return false;
        if (!this.Pending) this.loadingAt = now;
        this.Pending = true;
        this.Phase = TransitionPhase.Suspected;
        this.stability.Reset();
        this.processStability.Reset();
        return true;
    }

    internal bool Settled(long now, bool loading, bool valid, uint territory, Vector3 position, bool groundReady = true,
        bool forcedMovement = false)
    {
        if (!this.Pending) return false;
        if (loading || !valid || forcedMovement)
        {
            this.stability.Reset(); this.processStability.Reset(); this.Phase = TransitionPhase.Travelling;
            this.WaitingReason = forcedMovement ? "forced-movement" : "player-or-loading"; return false;
        }
        bool processEnded = this.processStability.Observe(true, position, now,
            this.Destination?.StableMs ?? 1000, this.Destination?.RequireStillness ?? true);
        this.Phase = processEnded ? TransitionPhase.ValidatingDestination : TransitionPhase.Travelling;
        if (this.Destination is { } expected)
        {
            // Inferred continuation points supply connectivity evidence only.
            // Ground, player/process stability and observed displacement still
            // gate completion; the next step performs the actual navigation.
            bool arrived = expected.IsContinuationTarget || expected.Contains(territory, position);
            this.WaitingReason = !arrived ? "destination-region" : expected.RequireGround && !groundReady ? "ground-or-mesh" : "position-stability";
            bool destinationStable = this.stability.Observe(arrived && (!expected.RequireGround || groundReady)
                && (territory != this.territory || Vector3.DistanceSquared(this.origin, position) > 4),
                position, now, expected.StableMs, expected.RequireStillness);
            if (processEnded && destinationStable) this.Phase = TransitionPhase.DestinationConfirmed;
            return processEnded && destinationStable;
        }
        if (this.forcedLanding is { } landing
            && (System.MathF.Abs(position.Y - landing.Y) > 3
                || new Vector2(position.X - landing.X, position.Z - landing.Z).LengthSquared() > 10000))
        { this.stability.Reset(); return false; }
        this.WaitingReason = "destination-unknown";
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
        this.processStability.Reset();
        this.Phase = TransitionPhase.AwaitingTrigger;
        this.WaitingReason = "waiting-for-transfer";
    }
}
