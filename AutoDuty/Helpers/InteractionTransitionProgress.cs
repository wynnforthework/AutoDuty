using System.Numerics;
using System.Collections.Generic;

namespace AutoDuty.Helpers;

[System.Flags]
internal enum TransitionSignal { None = 0, Loading = 1, ZoneInit = 2, Territory = 4, ForcedMotion = 8, PositionAnomaly = 16 }

internal enum TransitionPhase { AwaitingTrigger, Suspected, Travelling, ValidatingDestination, DestinationConfirmed, StepCompleted }

// Transfer evidence belongs to one active step. Game signals establish observation;
// subsequent ground/readiness/destination evidence establishes completion.
internal sealed class InteractionTransitionProgress
{
    internal PathAction? Step { get; private set; }
    internal int Index { get; private set; } = -1;
    internal bool Pending { get; private set; }
    internal TransitionSignal Signals { get; private set; }
    internal void RecordSignal(TransitionSignal signal) => this.Signals |= signal;
    private uint territory;
    private Vector3 origin;
    private long clickedAt;
    private long loadingAt;
    private PathAction? movementStep;
    private int movementIndex;
    private uint movementTerritory;
    private Vector3 movementOrigin;
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
        return this.ObserveLoading(step, index, now, false, TransitionSignal.PositionAnomaly);
    }

    internal bool BeginDisplacement(PathAction step, int index, uint territory, Vector3 source, long now,
        TransitionRegion? destination)
    {
        if (destination == null || !destination.Valid) return false;
        this.Click(step, index, territory, source, now);
        this.Expect(destination);
        return this.ObserveLoading(step, index, now, false, TransitionSignal.PositionAnomaly);
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
        if (this.Destination.IsContinuationTarget
            && (this.Signals & ~TransitionSignal.PositionAnomaly) == TransitionSignal.None)
        { this.WaitingReason = "destination-unknown"; return false; }
        if (!conditions) { this.WaitingReason = "step-conditions"; return false; }
        if ((this.Destination.RequirePath && !pathConfirmed) || !pathWaitFinished)
        { this.WaitingReason = "path-evidence"; return false; }
        this.Phase = TransitionPhase.StepCompleted;
        return true;
    }

    internal bool ObserveForcedMovement(PathAction step, int index, uint territory, Vector3 position,
        long now, bool deathOrReturn, PathAction? next, PathAction? landing, bool forcedMotion = false)
    {
        // Only an actual game forced-motion signal may authorize legacy launch
        // ownership. Position/speed heuristics merely stop unsafe navigation.
        if (this.Pending || deathOrReturn || !forcedMotion || step.Name != "MoveTo"
            || !ReferenceEquals(this.movementStep, step) || this.movementIndex != index
            || this.movementTerritory != territory || landing == null
            || landing.Position == Vector3.Zero || !HasReadinessBarrier(next)) return false;
        Vector3 source = this.movementOrigin;
        this.Click(step, index, territory, source, now);
        this.Expect(step.Transition ?? ContinuationEvidence(landing.Position));
        return this.ObserveLoading(step, index, now, false, TransitionSignal.ForcedMotion);
    }

    private static bool HasReadinessBarrier(PathAction? next) => next is { Name: "WaitFor", Arguments.Count: 1 }
        && next.Position == Vector3.Zero && WaitForArguments.Normalize(next.Arguments[0]) is ["IsReady"];
    internal static bool IsScriptedTransfer(PathAction step, PathAction? next) => step.Name == "MoveTo"
        && step.Arguments.Count > 0 && bool.TryParse(step.Arguments[0], out bool mesh) && !mesh
        && HasReadinessBarrier(next);

    internal void TrackMovement(PathAction step, int index, uint territory, Vector3 position, long now,
        PathAction? next = null)
    {
        if (this.Pending) return;
        if (ReferenceEquals(this.movementStep, step) && this.movementIndex == index
            && this.movementTerritory == territory) return;
        this.movementStep = step;
        this.movementIndex = index;
        this.movementTerritory = territory;
        this.movementOrigin = position;
    }

    internal bool ObserveTrackedMovementLoading(PathAction step, int index, long now, bool deathOrReturn,
        TransitionSignal signal = TransitionSignal.Loading)
    {
        if (deathOrReturn) return false;
        if (this.Pending) return this.ObserveLoading(step, index, now, false, signal);
        if (step.Name != "MoveTo" || !ReferenceEquals(this.movementStep, step) || this.movementIndex != index) return false;
        uint sourceTerritory = this.movementTerritory;
        Vector3 source = this.movementOrigin;
        this.Click(step, index, sourceTerritory, source, now);
        return this.ObserveLoading(step, index, now, false, signal);
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
        uint territory, Vector3 position, long now, bool deathOrReturn, TransitionSignal signal = TransitionSignal.Loading)
    {
        // The loading signal belongs to the active movement, not to a guessed
        // distance around its target. Completion still requires destination evidence.
        if (!moving || deathOrReturn || step.Name != "MoveTo") return false;
        this.Click(step, index, territory, position, now);
        return this.ObserveLoading(step, index, now, false, signal);
    }

    internal bool ObserveLoading(PathAction step, int index, long now, bool deathOrReturn,
        TransitionSignal signal = TransitionSignal.Loading)
    {
        if (deathOrReturn || !ReferenceEquals(this.Step, step) || this.Index != index
            || (!this.Pending && now - this.clickedAt >= 30_000)) return false;
        this.RecordSignal(signal);
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
                && ((this.Signals & (TransitionSignal.Loading | TransitionSignal.ZoneInit | TransitionSignal.Territory)) != 0
                    || territory != this.territory || position != this.origin),
                position, now, expected.StableMs, expected.RequireStillness);
            if (processEnded && destinationStable) this.Phase = TransitionPhase.DestinationConfirmed;
            return processEnded && destinationStable;
        }
        this.WaitingReason = "destination-unknown";
        return this.stability.Observe(territory != this.territory || Vector3.DistanceSquared(this.origin, position) > 4,
            position, now, 1000, true);
    }

    internal bool TimedOut(long now) => this.Step != null
        && now - (this.Pending ? this.loadingAt : this.clickedAt) >= (this.Destination?.TimeoutMs ?? (this.Pending ? 60_000 : 30_000));

    internal void Reset()
    {
        this.movementStep = null;
        this.Step = null;
        this.Index = -1;
        this.Pending = false;
        this.Signals = TransitionSignal.None;
        this.Destination = null;
        this.stability.Reset();
        this.processStability.Reset();
        this.Phase = TransitionPhase.AwaitingTrigger;
        this.WaitingReason = "waiting-for-transfer";
    }
}
