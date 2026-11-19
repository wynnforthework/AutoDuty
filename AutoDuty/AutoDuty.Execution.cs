using System;
using System.Collections.Generic;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using AutoDuty.Multibox;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace AutoDuty;

public partial class AutoDuty
{
    internal readonly CombatCheckpointProgress combatCheckpoints = new();
    private readonly NavigationReadiness navigationReadiness = new();
    internal readonly InteractionTransitionProgress interactionTransition = new();
    private bool interactionTransitionCancelled;
    private bool interactionRequiresTransfer;
    private bool levelingFailureCounted;
    internal StepAttempt? currentAttempt;
    internal StepFailure? currentFailure;
    internal readonly Dictionary<int, StepAttempt> attemptHistory = [];
    private (string Reason, string Detail)? pendingStepFailure;

    internal void BeginAttempt(int index)
    {
        this.interactionTransition.Reset();
        this.interactionTransitionCancelled = false;
        this.interactionRequiresTransfer = false;
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
        this.interactionRequiresTransfer |= InteractionTransitionProgress.RequiresTransfer(Svc.ClientState.TerritoryType, objectId);
        NavigationDiagnostics.Record("interaction-click", detail: new { objectId, requiresTransfer = this.interactionRequiresTransfer });
    }

    private bool ObserveInteractionLoading()
    {
        if ((this.Stage is not (Stage.Action or Stage.Interactable)
                && !(this.Stage == Stage.Moving && this.interactionTransition.Pending))
            || !this.States.HasFlag(PluginState.Navigating)) return false;
        if (!this.interactionTransition.ObserveLoading(this.pathAction, this.indexer, Environment.TickCount64,
                this.Stage is Stage.Dead or Stage.Revived || DeathHelper.DeathState != PlayerLifeState.Alive
                || Environment.TickCount64 < this.returnObservedUntil)) return false;
        // Latch either load flag immediately. Queue mutation waits for Framework_Update.
        VNavmesh_IPCSubscriber.Path_Stop();
        NavigationDiagnostics.Record(this.pathAction.Name == "MoveTo" ? "movement-transfer-loading" : "interaction-transfer-start");
        return true;
    }

    private unsafe bool ObserveMovementLoading()
    {
        if (!Player.Available || !InDungeon || this.dutyState == DutyState.DutyComplete
            || !this.States.HasFlag(PluginState.Navigating) || this.currentFailure != null
            || this.currentAttempt?.Index != this.indexer
            || !RouteCompletionPolicy.HasStep(this.indexer, this.Actions.Count)
            || !ReferenceEquals(this.pathAction, this.Actions[this.indexer])) return false;
        bool deathOrReturn = DeathHelper.DeathState != PlayerLifeState.Alive || Player.IsDead
            || Environment.TickCount64 < this.returnObservedUntil
            || FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()->CastActionId == 6;
        if (!this.interactionTransition.ObserveMovementLoading(this.pathAction, this.indexer,
                this.Stage == Stage.Moving, Svc.ClientState.TerritoryType, Player.Position,
                Environment.TickCount64, deathOrReturn)) return false;
        VNavmesh_IPCSubscriber.Path_Stop();
        NavigationDiagnostics.Record("movement-transfer-start", detail: new { target = this.pathAction.Position, origin = Player.Position });
        return true;
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
            if (this.interactionTransition.Settled(now, loading, PlayerHelper.IsValid,
                    Svc.ClientState.TerritoryType, Player.Available ? Player.Position : default)
                && this.pathAction.ConditionsUntil.TrueForAll(c => c.IsFulfilled()))
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
        }
        if ((this.interactionTransition.Pending || this.interactionRequiresTransfer) && this.interactionTransition.TimedOut(now))
        {
            bool pending = this.interactionTransition.Pending;
            this.interactionTransition.Reset();
            if (pending)
            {
                this.MetricsProblem($"{transferKind}-transfer-timeout");
                this.FailCurrentStep($"{transferKind}-transfer-timeout", "load, position or completion conditions did not settle; old coordinates will not be retried", forcePause: true);
            }
            else this.RegisterStepFailure("interaction-transfer-timeout", "interaction did not start a transfer");
            return true;
        }
        return this.interactionTransition.Pending;
    }

    private bool TryApplyNearbyCombatSwitch()
    {
        if (!Player.Available || !this.combatCheckpoints.CanApply(this.pathAction, Player.Position, PartyHelper.PartyInCombat())) return false;
        VNavmesh_IPCSubscriber.Path_Stop();
        Svc.Log.Info($"Step {this.indexer}: applying nearby StopForCombat after observed combat ended; executing switch without waiting for another fight.");
        this.Stage = Stage.Action;
        return true;
    }

    private bool UpdateStepAttempt()
    {
        if (this.currentAttempt?.Index != this.indexer) this.BeginAttempt(this.indexer);
        long now = Environment.TickCount64;
        bool navigationStage = this.Stage is Stage.Reading_Path or Stage.Moving;
        if (!navigationStage) this.navigationReadiness.Reset();
        bool canNavigate = !navigationStage || this.navigationReadiness.Observe(PlayerHelper.IsReadyFull,
            !InDungeon || Svc.DutyState.IsDutyStarted, VNavmesh_IPCSubscriber.Nav_IsReady, now);
        if (navigationStage && !canNavigate)
        {
            if (this.currentAttempt is { } waiting)
            {
                waiting.StartedAt += now - waiting.LastTickAt;
                waiting.LastTickAt = now;
                waiting.Progress.Reset(Player.Available ? Player.Position : default, now);
                waiting.NavigationStop.Reset();
            }
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
        if (this.Stage is Stage.Paused or Stage.Waiting_For_Combat or Stage.Dead or Stage.Revived
            || Svc.Condition[ConditionFlag.WatchingCutscene] || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Svc.Condition[ConditionFlag.OccupiedInQuestEvent] || Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.BetweenAreas51])
        {
            long delta = now - attempt.LastTickAt;
            attempt.StartedAt += delta;
            if (attempt.ActionStartedAt != 0) attempt.ActionStartedAt += delta;
            this.taskManager.RemainingTimeMS += delta;
            attempt.Progress.Reset(Player.Available ? Player.Position : default, now);
            attempt.NavigationStop.Reset();
        }
        attempt.LastTickAt = now;
        return true;
    }
}
