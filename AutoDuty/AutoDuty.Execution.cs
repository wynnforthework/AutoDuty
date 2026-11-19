using System;
using System.Collections.Generic;
using System.Numerics;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using AutoDuty.Managers;
using AutoDuty.Multibox;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace AutoDuty;

public partial class AutoDuty
{
    internal readonly CombatCheckpointProgress combatCheckpoints = new();
    private readonly NavigationReadiness navigationReadiness = new();
    private readonly StepExecutionGate stepStartGate = new();
    private readonly StepExecutionGate actionCompletionGate = new();
    private readonly StepExecutionGate movementCompletionGate = new();
    private readonly StepExecutionGate taskResumeGate = new();
    internal readonly InteractionTransitionProgress interactionTransition = new();
    private bool interactionTransitionCancelled;
    private bool interactionRequiresTransfer;
    private bool levelingFailureCounted;
    internal StepAttempt? currentAttempt;
    internal StepFailure? currentFailure;
    internal readonly Dictionary<int, StepAttempt> attemptHistory = [];
    internal long ActiveActionTime => this.currentAttempt is { ActionStartedAt: > 0 } attempt
        ? Math.Max(0, Environment.TickCount64 - attempt.ActionStartedAt) : Environment.TickCount64;
    private (string Reason, string Detail)? pendingStepFailure;

    internal void BeginAttempt(int index)
    {
        this.transitionPathProbe.Reset();
        this.displacementMonitor.Reset();
        this.displacementSource = null;
        this.stepStartGate.Reset();
        this.actionCompletionGate.Reset();
        this.movementCompletionGate.Reset();
        this.interactionTransition.Reset();
        this.interactionTransitionCancelled = false;
        this.interactionRequiresTransfer = false;
        this.unownedWorldSignal = TransitionSignal.None;
        this.currentAttempt = index >= 0 && index < this.Actions.Count
            ? new StepAttempt { Index = index, Action = this.Actions[index], StartPosition = Player.Available ? Player.Position : default }
            : null;
        if (this.currentAttempt != null)
            this.attemptHistory[index] = this.currentAttempt;
        StuckHelper.ResetForStep();
    }

    // TaskManager callbacks must not mutate its queue while it is enumerating a task.
    private void ReportAttemptStart()
    {
        if (this.currentAttempt is not { StartReported: false } attempt) return;
        attempt.StartReported = true;
        // The mandatory initial execution is not a retry and does not need a chat message.
        if (attempt.Attempts > 0)
            Svc.Chat.Print("[AutoDuty] " + Loc.Get("DutyTools.AttemptStart", this.indexer,
                attempt.Attempts, Math.Max(0, Configuration.DutyConfig.MaxStepRetries)));
        NavigationDiagnostics.Record("step-attempt-start");
    }

    internal void ReportTaskFailure(string reason, string detail) => this.pendingStepFailure ??= (reason, detail);

    private void CancelStepWork()
    {
        this.transitionPathProbe.Reset();
        this.taskManager.Abort();
        VNavmesh_IPCSubscriber.Path_Stop();
        if (PlayerHelper.IsReady)
            Chat.ExecuteCommand("/automove off");
        FollowHelper.SetFollow(null);
        SchedulerHelper.DescheduleAction("ConditionChangeStageReadingPath");
        SchedulerHelper.DescheduleAction("StuckHelperReturn");
        SchedulerHelper.DescheduleAction("StuckHelperReturnInsurance");
        SchedulerHelper.DescheduleAction("StuckHelperUnschedule");
    }

    internal void RegisterStepFailure(string reason, string detail = "")
    {
        if (this.currentFailure != null) return;
        if (this.currentAttempt?.Index != this.indexer) this.BeginAttempt(this.indexer);
        if (this.currentAttempt == null) return;
        var attempt = this.currentAttempt;
        this.MetricsProblem(reason, automaticRetry: true);
        attempt.LastFailReason = reason;
        attempt.Attempts++;
        NavigationDiagnostics.Record("step-failure", detail: new { reason, detail });
        NavigationDiagnostics.Export(reason);
        Svc.Log.Warning($"Step {this.indexer} ({attempt.Action.ToCustomString()}): {reason}; {detail}; failure count={attempt.Attempts}; retry limit={Math.Max(0, Configuration.DutyConfig.MaxStepRetries)}");
        this.interactionTransition.Reset();
        this.interactionRequiresTransfer = false;
        this.interactionTransitionCancelled = false;
        this.CancelStepWork();
        this.pendingStepFailure = null;
        if (StepRetryPolicy.CanRetry(attempt.Attempts, Configuration.DutyConfig.MaxStepRetries))
        {
            Svc.Chat.Print("[AutoDuty] " + Loc.Get("DutyTools.RetryQueued", this.indexer, reason, attempt.Attempts, Math.Max(0, Configuration.DutyConfig.MaxStepRetries)));
            attempt.StartReported = false;
            attempt.StartedAt = attempt.LastTickAt = Environment.TickCount64;
            attempt.ActionStartedAt = 0;
            attempt.MovementStarted = false;
            attempt.StuckCount = 0;
            attempt.CombatApproach.Reset();
            attempt.RetryAfter = Environment.TickCount64 + 1000;
            this.stepStartGate.Reset();
            StuckHelper.ResetForStep();
            this.Stage = Stage.Reading_Path;
            return;
        }
        this.FailCurrentStep(reason, detail);
    }

    internal void FailCurrentStep(string reason, string detail = "", bool forcePause = false)
    {
        if (this.currentFailure != null) return;
        // Exhausted retries or a direct protection stop are real interruptions.
        if (this.MetricsRun is { } run) run.Record.CompletionInterrupted = true;
        NavigationDiagnostics.Record("step-paused", detail: new { reason, detail });
        NavigationDiagnostics.Export(reason);
        this.interactionTransition.Reset();
        this.CancelStepWork();
        if (this.LevelingEnabled && !this.levelingFailureCounted)
        {
            this.levelingFailureCounted = true;
            LevelingHelper.RecordFailure(Svc.ClientState.TerritoryType);
        }
        if (!forcePause && !StepRecoveryPolicy.MustPause(reason) && !Configuration.DutyConfig.PauseOnStepFailure)
        {
            Svc.Chat.Print($"[AutoDuty] Step {this.indexer} failed: {reason}. Skipped by configuration.");
            if (this.currentAttempt != null) this.currentAttempt.SkippedReason = reason;
            this.indexer++;
            this.Stage = Stage.Reading_Path;
            return;
        }
        this.currentFailure = new(this.indexer, reason, detail, this.currentAttempt?.Attempts ?? 1, DateTime.UtcNow);
        this.Stage = Stage.Paused;
        this.States |= PluginState.Failed;
        string pauseDetail = this.currentFailure.Attempts > 0 ? $"Paused after {this.currentFailure.Attempts} failures." : "Paused for safety.";
        Svc.Chat.PrintError($"[AutoDuty] Step {this.indexer} failed: {reason} ({detail}). {pauseDetail}");
        Svc.Log.Error($"Step {this.indexer} paused: {reason}; {detail}");
        if (Configuration.DutyConfig.PlaySoundOnFailure) SoundHelper.StartSound(Sounds.Sound01);
    }

    internal void ResumeFromPause()
    {
        if (this.Stage != Stage.Paused) return;
        if (this.currentFailure != null)
        {
            this.SeekToStep(this.indexer);
            return;
        }
        if (this.interactionTransition.Pending)
        {
            // Resume the observed transfer, never navigate back to its old coordinates.
            this.Stage = Stage.Interactable;
            return;
        }
        var target = PauseResumePolicy.Target(this.previousStage, this.States.HasFlag(PluginState.Navigating));
        if (this.previousStage is Stage.Moving or Stage.Interactable)
        {
            // Pausing stops navmesh; reading the same step again must issue a new navigation request.
            this.SeekToStep(this.indexer, manual: false);
            return;
        }
        this.Stage = target;
        if (this.currentAttempt != null) this.currentAttempt.LastTickAt = Environment.TickCount64;
    }

    internal unsafe void TeleportToStep(System.Numerics.Vector3 position)
    {
        if (!Player.Available) return;
        if (this.Stage != Stage.Stopped && this.indexer >= 0 && this.indexer < this.Actions.Count)
            this.SeekToStep(this.indexer, resume: false, broadcast: false);
        else
        {
            this.CancelStepWork();
            this.MetricsProblem("manual-teleport", true);
        }
        NavigationDiagnostics.Record("manual-teleport", detail: position);
        Player.GameObject->SetPosition(position.X, position.Y, position.Z);
    }

    internal void SeekToStep(int index, bool resume = true, bool broadcast = true, bool manual = true)
    {
        this.navigationReadiness.Reset();
        this.transitionPending = false;
        this.returnObservedUntil = 0;
        this.combatCheckpoints.Reset();
        if (manual) this.MetricsProblem("manual-seek", true);
        this.CancelStepWork();
        this.pendingStepFailure = null;
        this.currentFailure = null;
        this.States &= ~PluginState.Failed;
        this.indexer = Math.Clamp(index, 0, this.Actions.Count);
        this.BeginAttempt(this.indexer);
        this.pathAction = this.currentAttempt?.Action ?? new();
        if (resume)
        {
            this.States |= PluginState.Navigating;
            this.Stage = Stage.Reading_Path;
        }
        else
        {
            this.Stage = Stage.Paused;
            this.previousStage = Stage.Reading_Path;
        }
        if (broadcast && MultiboxUtility.Config.MultiBox && MultiboxUtility.Config.Host)
        {
            MultiboxUtility.stepBlock = false;
            MultiboxUtility.Server.SendPath();
            MultiboxUtility.Server.SendStepStart(manual);
        }
        Svc.Log.Info($"Seek to step {this.indexer}; resume={resume}");
    }

    internal void ObserveInteractionClick(uint objectId)
    {
        if (this.Stage != Stage.Action || this.pathAction.Name != "Interactable" || !Player.Available) return;
        this.interactionTransition.Click(this.pathAction, this.indexer, Svc.ClientState.TerritoryType, Player.Position, Environment.TickCount64);
        this.SetTransferDestination();
        this.interactionRequiresTransfer |= this.pathAction.Transition != null || InteractionTransitionProgress.RequiresTransfer(Svc.ClientState.TerritoryType, objectId);
        NavigationDiagnostics.Record("interaction-click", detail: new { objectId, requiresTransfer = this.interactionRequiresTransfer });
    }

    private bool ObserveInteractionLoading(TransitionSignal signal = TransitionSignal.Loading)
    {
        if ((this.Stage is not (Stage.Action or Stage.Interactable)
                && !(this.Stage == Stage.Moving && this.interactionTransition.Pending))
            || !this.States.HasFlag(PluginState.Navigating)) return false;
        if (!this.interactionTransition.ObserveLoading(this.pathAction, this.indexer, Environment.TickCount64,
                this.Stage is Stage.Dead or Stage.Revived || DeathHelper.DeathState != PlayerLifeState.Alive
                || Environment.TickCount64 < this.returnObservedUntil, signal)) return false;
        // Latch either load flag immediately. Queue mutation waits for Framework_Update.
        VNavmesh_IPCSubscriber.Path_Stop();
        this.SetTransferDestination();
        NavigationDiagnostics.Record(this.pathAction.Name == "MoveTo" ? "movement-transfer-loading" : "interaction-transfer-start");
        return true;
    }

    private unsafe bool ObserveMovementLoading(TransitionSignal signal = TransitionSignal.Loading)
    {
        // Loading may already have invalidated the player or changed Moving to
        // Reading_Path. Use the last valid approach sample, not loading-time coordinates.
        if (this.Stage is not (Stage.Moving or Stage.Reading_Path or Stage.Waiting_For_Combat or Stage.Interactable)
            || this.dutyState == DutyState.DutyComplete
            || !this.States.HasFlag(PluginState.Navigating) || this.currentFailure != null
            || this.currentAttempt?.Index != this.indexer
            || !RouteCompletionPolicy.HasStep(this.indexer, this.Actions.Count)
            || !ReferenceEquals(this.pathAction, this.Actions[this.indexer])) return false;
        bool deathOrReturn = DeathHelper.DeathState != PlayerLifeState.Alive
            || (Player.Available && Player.IsDead)
            || Environment.TickCount64 < this.returnObservedUntil
            || FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()->CastActionId == 6;
        bool observed = this.interactionTransition.ObserveTrackedMovementLoading(this.pathAction,
            this.indexer, Environment.TickCount64, deathOrReturn, signal);
        if (!observed && Player.Available && this.Stage == Stage.Moving)
            observed = this.interactionTransition.ObserveMovementLoading(this.pathAction, this.indexer,
                true, Svc.ClientState.TerritoryType, Player.Position, Environment.TickCount64, deathOrReturn, signal);
        if (!observed)
        {
            NavigationDiagnostics.Record("movement-transfer-rejected", detail: new { deathOrReturn, playerAvailable = Player.Available });
            return false;
        }
        VNavmesh_IPCSubscriber.Path_Stop();
        this.SetTransferDestination();
        NavigationDiagnostics.Record("movement-transfer-start", detail: new { target = this.pathAction.Position, playerAvailable = Player.Available });
        return true;
    }

    private readonly DisplacementMonitor displacementMonitor = new();
    private TransitionSignal unownedWorldSignal;
    private void ClientState_ZoneInit(Dalamud.Game.ClientState.ZoneInitEventArgs args)
        => this.ObserveWorldChange(TransitionSignal.ZoneInit);
    private void ClientState_MapIdChanged(uint map)
    {
        if (!this.States.HasFlag(PluginState.Navigating)) return;
        // Map changes may simply select another map floor. Invalidate probes
        // and record the change, but never infer transport from MapId alone.
        this.transitionPathProbe.Reset();
        this.nextGroundProbe = 0;
        this.transferGroundReady = false;
        NavigationDiagnostics.Record("map-id-changed", detail: new { map });
    }
    private void ObserveWorldChange(TransitionSignal signal)
    {
        if (!this.States.HasFlag(PluginState.Navigating) || this.Stage is Stage.Stopped or Stage.Paused or Stage.Dead or Stage.Revived
            || this.currentFailure != null || DeathHelper.DeathState != PlayerLifeState.Alive
            || Environment.TickCount64 < this.returnObservedUntil || this.dutyState == DutyState.DutyComplete) return;
        // Initial duty entry has no executing step yet. Its zone initialization
        // is not an unexpected mid-step transfer.
        if (this.currentAttempt?.Index != this.indexer || this.indexer < 0 || this.indexer >= this.Actions.Count) return;
        if (this.ObserveInteractionLoading(signal) || this.ObserveMovementLoading(signal))
            NavigationDiagnostics.Record("world-transition-signal", detail: signal.ToString());
        else
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            this.unownedWorldSignal |= signal;
        }
    }
    private bool TickUnownedWorldChange()
    {
        if (this.unownedWorldSignal == TransitionSignal.None) return false;
        var signal = this.unownedWorldSignal;
        this.unownedWorldSignal = TransitionSignal.None;
        if (!this.States.HasFlag(PluginState.Navigating) || this.Stage is Stage.Stopped or Stage.Dead or Stage.Revived) return false;
        // Defer queue mutation from client callbacks to Framework_Update.
        this.FailCurrentStep("transition-unconfirmed", $"{signal}: no owning step; old navigation cancelled", forcePause: true);
        return true;
    }
    private Vector3? displacementSource;
    private long nextGroundProbe;
    private bool transferGroundReady;
    internal readonly TransitionPathProbe transitionPathProbe = new();

    private void SetTransferDestination()
    {
        var destination = this.pathAction.Transition ?? InteractionTransitionProgress.InferDestination(this.Actions, this.indexer);
        this.interactionTransition.Expect(destination);
        NavigationDiagnostics.Record("transfer-destination", detail: new
        {
            source = this.pathAction.Transition != null ? "explicit" : destination != null ? "route-inferred" : "unknown",
            destination,
            continuation = InteractionTransitionProgress.ContinuationStep(this.Actions, this.indexer)?.Position,
        });
        this.transitionPathProbe.Reset();
        this.nextGroundProbe = 0;
        this.transferGroundReady = false;
    }

    private unsafe void ObserveForcedMovementTransfer()
    {
        if (this.Stage is not (Stage.Moving or Stage.Reading_Path or Stage.Action)
            || !this.States.HasFlag(PluginState.Navigating) || this.currentFailure != null
            || !Player.Available || this.currentAttempt?.Index != this.indexer
            || this.indexer < 0 || this.indexer >= this.Actions.Count || this.transitionPending
            || this.interactionTransition.Pending
            || !ReferenceEquals(this.pathAction, this.Actions[this.indexer])) { this.displacementMonitor.Reset(); this.displacementSource = null; return; }
        bool deathOrReturn = DeathHelper.DeathState != PlayerLifeState.Alive || Player.IsDead
            || Environment.TickCount64 < this.returnObservedUntil
            || FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()->CastActionId == 6;
        if (deathOrReturn || this.pathAction.Name == "TeleportTo")
        { this.displacementMonitor.Reset(); this.displacementSource = null; return; }
        long now = Environment.TickCount64;
        var position = Player.Position;
        uint territory = Svc.ClientState.TerritoryType;
        string? anomaly = this.displacementMonitor.Observe(territory, position, now);
        if (Svc.Condition[ConditionFlag.BeingMoved]) anomaly ??= "forced-movement";
        var source = anomaly != null ? this.displacementMonitor.Source : this.displacementSource ?? position;
        this.displacementSource = position;
        var next = this.indexer + 1 < this.Actions.Count ? this.Actions[this.indexer + 1] : null;
        var after = InteractionTransitionProgress.ContinuationStep(this.Actions, this.indexer);
        bool launch = this.interactionTransition.ActivateExpectedMovement(this.pathAction, this.indexer, territory, position, now)
            || this.interactionTransition.ObserveForcedMovement(this.pathAction, this.indexer,
                territory, position, now, false, next, after, Svc.Condition[ConditionFlag.BeingMoved]);
        if (!launch && anomaly == null) return;
        // An expected region alone is not proof that this displacement belongs
        // to it: require a source close to this step's trigger/interaction.
        var expected = this.pathAction.Transition ?? InteractionTransitionProgress.InferDestination(this.Actions, this.indexer);
        bool owned = launch || (this.pathAction.Transition?.NearTrigger(this.pathAction.Position, source) == true
            && this.interactionTransition.BeginDisplacement(this.pathAction, this.indexer, this.displacementMonitor.SourceTerritory, source, now, expected));
        VNavmesh_IPCSubscriber.Path_Stop();
        if (!owned)
        {
            if (this.IsAtRouteEntrance()) this.RestartAtEntrance("displacement-to-entrance");
            else
            {
                NavigationDiagnostics.Record("unowned-displacement", detail: new { source, sourceTerritory = this.displacementMonitor.SourceTerritory, position, territory, anomaly });
                this.FailCurrentStep("transition-unconfirmed", $"{anomaly}: unexpected displacement; old navigation cancelled; configure destination region", forcePause: true);
            }
            return;
        }
        this.SetTransferDestination();
        NavigationDiagnostics.Record(launch ? "movement-launch-start" : "displacement-transfer-start", detail: new { source, position, anomaly, expected });
    }

    private bool TickInteractionTransition()
    {
        if (this.Stage is Stage.Stopped or Stage.Paused or Stage.Dead or Stage.Revived) return false;
        if (this.interactionTransition.Step == null) return false;
        string transferKind = this.interactionTransition.Step.Name == "MoveTo" ? "movement" : "interaction";
        long now = Environment.TickCount64;
        if (now < this.returnObservedUntil && this.interactionTransition.Pending)
        {
            this.transitionPending = true;
            this.loadingRecovery.Reset();
            this.transitionWorkCancelled = false;
            this.transitionObservedAt = now;
            this.transitionOrigin = Player.Available ? Player.Position : default;
            this.transitionWasReturn = true;
            this.MetricsProblem("unexpected-position-transition");
            this.FailCurrentStep("unexpected-position-transition", "Return during step transfer", forcePause: true);
            return true;
        }
        if (this.interactionTransition.Pending)
        {
            if (!this.interactionTransitionCancelled)
            {
                this.CancelStepWork();
                this.pendingStepFailure = null;
                this.combatCheckpoints.Reset();
                BossMod_IPCSubscriber.SetMovement(false);
                this.Stage = Stage.Interactable;
                this.action = $"Waiting for {transferKind} transfer";
                this.interactionTransitionCancelled = true;
            }
            bool loading = Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];
            if (now >= this.nextGroundProbe)
            {
                this.nextGroundProbe = now + 250;
                this.transferGroundReady = false;
                if (PlayerHelper.IsReadyFull && !loading && VNavmesh_IPCSubscriber.Nav_IsReady)
                {
                    try
                    {
                        var floor = VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(Player.Position + new Vector3(0, .5f, 0), false, 2);
                        this.transferGroundReady = floor is { } ground && MathF.Abs(ground.Y - Player.Position.Y) <= 1.5f;
                    }
                    catch (Exception ex) { Svc.Log.Debug($"Transfer floor probe unavailable: {ex.Message}"); }
                }
            }
            bool forcedMovement = Svc.Condition[ConditionFlag.BeingMoved] || PlayerHelper.IsJumping;
            var previousPhase = this.interactionTransition.Phase;
            bool settled = this.interactionTransition.Settled(now, loading, PlayerHelper.IsReadyFull,
                Svc.ClientState.TerritoryType, Player.Available ? Player.Position : default, this.transferGroundReady, forcedMovement);
            if (!settled && previousPhase == TransitionPhase.DestinationConfirmed) this.transitionPathProbe.Reset();
            if (settled && this.interactionTransition.Destination != null)
                this.transitionPathProbe.Tick(now, Svc.ClientState.TerritoryType, Player.Position,
                    TransitionPathProbe.ContinuationTarget(this.Actions, this.indexer), VNavmesh_IPCSubscriber.Nav_IsReady,
                    VNavmesh_IPCSubscriber.Query_Path);
            this.action = Loc.Get("BuildTab.TransferWaiting") + Loc.Get("BuildTab.TransferReason_" + this.interactionTransition.WaitingReason);
            if (settled && this.interactionTransition.CompleteStep(this.pathAction, this.indexer,
                this.pathAction.ConditionsUntil.TrueForAll(c => c.IsFulfilled()), this.transitionPathProbe.Connected,
                this.transitionPathProbe.OptionalWaitFinished(now)))
            {
                if (this.IsAtRouteEntrance())
                {
                    this.interactionTransition.Reset();
                    this.RestartAtEntrance($"{transferKind}-returned-to-entrance");
                    return true;
                }
                NavigationDiagnostics.Record($"{transferKind}-transfer-complete");
                Svc.Log.Info($"Step {this.indexer}: {transferKind} transfer settled; old navigation and interaction cancelled.");
                this.MetricsStepCompleted();
                this.indexer++;
                this.interactionTransition.Reset();
                this.currentAttempt = null;
                this.action = "";
                StuckHelper.ResetForStep();
                this.Stage = Stage.Reading_Path;
                return true;
            }
            this.action = Loc.Get("BuildTab.TransferWaiting") + Loc.Get("BuildTab.TransferReason_" + this.interactionTransition.WaitingReason);
        }
        if ((this.interactionTransition.Pending || this.interactionRequiresTransfer) && this.interactionTransition.TimedOut(now))
        {
            bool pending = this.interactionTransition.Pending;
            string waiting = this.interactionTransition.WaitingReason;
            NavigationDiagnostics.Record("transfer-timeout-evidence", detail: new { waiting, this.interactionTransition.Phase, this.interactionTransition.Destination, this.transferGroundReady, pathEvidence = this.transitionPathProbe.Status });
            this.interactionTransition.Reset();
            if (pending)
            {
                this.MetricsProblem($"{transferKind}-transfer-timeout");
                this.FailCurrentStep($"{transferKind}-transfer-timeout", $"waiting for {waiting}; old coordinates will not be retried", forcePause: true);
            }
            else this.RegisterStepFailure("interaction-transfer-timeout", "interaction did not start a transfer");
            return true;
        }
        return this.interactionTransition.Pending;
    }

    private bool TryApplyNearbyCombatSwitch()
    {
        if (!Player.Available || !this.combatCheckpoints.CanApply(this.pathAction, Player.Position, PartyHelper.PartyInCombat())
            || !CombatCheckpointProgress.HasArrived(this.pathAction, Player.Position, this.currentAttempt?.Attempts ?? 0)) return false;
        VNavmesh_IPCSubscriber.Path_Stop();
        Svc.Log.Info($"Step {this.indexer}: applying nearby StopForCombat after observed combat ended; executing switch without waiting for another fight.");
        this.Stage = Stage.Action;
        return true;
    }

    private bool UpdateStepAttempt()
    {
        if (this.currentAttempt?.Index != this.indexer) this.BeginAttempt(this.indexer);
        long now = Environment.TickCount64;
        if (this.Stage is Stage.Paused or Stage.Dead or Stage.Revived)
        {
            this.stepStartGate.Reset();
            this.movementCompletionGate.Reset();
            this.actionCompletionGate.Reset();
            this.SuspendAttemptClock(now, suspendRetry: true);
            return true;
        }
        bool navigationStage = this.Stage is Stage.Reading_Path or Stage.Moving;
        if (this.Stage == Stage.Reading_Path && this.currentAttempt is { } routeAttempt)
        {
            this.EnsureDutyContext();
            // Claim the next step before readiness waits, not after starting its path.
            this.pathAction = routeAttempt.Action;
            this.DutyData?.StopForCombat = RouteMovementPolicy.InterruptsApproach(this.pathAction);
            this.ApplyStepRotation();
            BossMod_IPCSubscriber.SetMovement(false,
                RouteMovementPolicy.PreferInstants(this.pathAction, Stage.Moving));
        }
        bool worldReady = this.ExecutionWorldReady();
        bool taskReady = this.taskResumeGate.Ready(worldReady, now);
        if (!worldReady || (this.Stage == Stage.Action && !taskReady)
            || (this.Stage == Stage.Reading_Path && !this.stepStartGate.Ready(this.CanStartStep(), now)))
        {
            if (!worldReady) this.stepStartGate.Reset();
            this.actionCompletionGate.Reset();
            this.movementCompletionGate.Reset();
            this.SuspendAttemptClock(now, suspendRetry: true);
            this.action = !worldReady ? "等待角色就绪：加载、过场或不可操作" : "等待步骤前置状态持续就绪";
            if (this.Stage == Stage.Moving)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                this.Stage = Stage.Reading_Path;
            }
            return false;
        }
        if (!navigationStage) this.navigationReadiness.Reset();
        var step = this.currentAttempt?.Action;
        bool needsMesh = step != null && StepExecutionGate.NeedsMesh(step.Name,
            step.Position != System.Numerics.Vector3.Zero,
            step.Name == "MoveTo" && step.Arguments.Count > 0 && bool.TryParse(step.Arguments[0], out bool mesh) && !mesh);
        bool canNavigate = !navigationStage || this.navigationReadiness.Observe(this.CanStartStep(),
            !InDungeon || Svc.DutyState.IsDutyStarted, !needsMesh || VNavmesh_IPCSubscriber.Nav_IsReady, now);
        if (navigationStage && !canNavigate)
        {
            if (needsMesh && this.currentAttempt is { RetryAfter: > 0 } retry && PlayerHelper.IsReadyFull
                && !VNavmesh_IPCSubscriber.Nav_IsReady
                && now - retry.RetryAfter > Math.Max(1000, Configuration.DutyConfig.NavmeshRebuildTimeoutMs))
            {
                this.FailCurrentStep("navmesh-not-ready", "rebuilt mesh did not become ready within retry readiness limit", forcePause: true);
                return false;
            }
            this.SuspendAttemptClock(now);
            if (this.Stage == Stage.Moving)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                this.Stage = Stage.Reading_Path;
            }
            this.action = "Waiting for player, duty and navigation readiness";
            return false;
        }
        if (this.pendingStepFailure is { } failure && this.Stage != Stage.Paused)
        {
            this.pendingStepFailure = null;
            this.RegisterStepFailure(failure.Reason, failure.Detail);
            return false;
        }
        if (this.currentAttempt is not { } attempt) return true;
        if (this.Stage is Stage.Waiting_For_Combat
            || (this.Stage == Stage.Action && PartyHelper.PartyInCombat()
                && this.taskManager.CurrentTask?.Name == "Wait")
            || Svc.Condition[ConditionFlag.WatchingCutscene] || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Svc.Condition[ConditionFlag.OccupiedInQuestEvent] || Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.BetweenAreas51])
        {
            this.SuspendAttemptClock(now);
        }
        attempt.LastTickAt = now;
        return true;
    }

    private bool ExecutionWorldReady() => PlayerHelper.IsValid
        && !Svc.Condition[ConditionFlag.WatchingCutscene]
        && !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        && !Svc.Condition[ConditionFlag.WatchingCutscene78];

    private bool CanStartStep() => this.ExecutionWorldReady() &&
        (StepExecutionGate.PlayerReady(this.Stage is Stage.Reading_Path or Stage.Moving,
            PlayerHelper.IsReady, PlayerHelper.IsReadyFull) || Svc.Condition[ConditionFlag.CarryingItem]
            || Svc.Condition[ConditionFlag.CarryingObject]
            || (RouteCompletionPolicy.HasStep(this.indexer, this.Actions.Count)
                && this.Actions[this.indexer].Name is "SelectString" or "SelectYesno" or "SelectJournalResult" or "WaitFor" or "ClickTalk"));

    private void SuspendAttemptClock(long now, bool suspendRetry = false)
    {
        if (this.currentAttempt is not { } attempt) return;
        long delta = attempt.Suspend(now, Player.Available ? Player.Position : default, suspendRetry);
        if (this.taskManager.CurrentTask != null) this.taskManager.RemainingTimeMS += delta;
    }

    // The route and its queue share one framework ordering. A separate queue
    // callback must not consume work after readiness was rejected by the route.
    private void TickTaskQueue(Dalamud.Plugin.Services.IFramework _)
    {
        bool route = this.States.HasFlag(PluginState.Navigating);
        if (!route) this.taskResumeGate.Reset();
        bool allowed = StepExecutionGate.CanTick(route, this.Stage == Stage.Paused,
            this.ExecutionWorldReady(), this.transitionPending || this.interactionTransition.Pending,
            this.currentFailure != null || this.pendingStepFailure != null);
        if (!allowed)
        {
            this.taskResumeGate.Reset();
            if (route) this.SuspendAttemptClock(Environment.TickCount64, suspendRetry: true);
            return;
        }
        if (route && !this.taskResumeGate.Ready(true, Environment.TickCount64))
        {
            this.SuspendAttemptClock(Environment.TickCount64);
            return;
        }
        if (this.taskManager.IsBusy) this.taskManager.Step();
    }
}
