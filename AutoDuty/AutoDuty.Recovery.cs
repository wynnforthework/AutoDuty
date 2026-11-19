using System;
using System.Numerics;
using AutoDuty.Helpers;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    private uint entranceTerritory;
    private Vector3? entrancePosition;
    private long returnObservedUntil;
    private long transitionObservedAt;
    private bool transitionPending;
    private Vector3 transitionOrigin;
    private bool transitionWasReturn;
    private readonly LoadingRecoveryProgress loadingRecovery = new();
    private bool transitionWorkCancelled;

    private unsafe void TickPositionRecovery()
    {
        if (this.Stage == Stage.Stopped) return;
        if (Player.Available && FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()->CastActionId == 6)
            this.returnObservedUntil = Environment.TickCount64 + 15000;
        if (!this.transitionPending) return;
        if (!this.transitionWorkCancelled)
        {
            this.CancelStepWork();
            this.pendingStepFailure = null;
            this.transitionWorkCancelled = true;
        }
        if (Environment.TickCount64 - this.transitionObservedAt > 60000)
        {
            this.transitionPending = false;
            this.transitionWorkCancelled = false;
            NavigationDiagnostics.Export("transition-settle-timeout");
            this.FailCurrentStep("unexpected-position-transition", "loading did not settle within 60 seconds", forcePause: true);
            return;
        }
        bool ready = Player.Available && PlayerHelper.IsReadyFull && !Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            && !Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
        if (!this.loadingRecovery.Ready(Environment.TickCount64, ready)) return;
        this.transitionPending = false;
        this.transitionWorkCancelled = false;
        if (this.IsAtRouteEntrance() && (this.transitionWasReturn || Vector3.DistanceSquared(this.transitionOrigin, Player.Position) > 400))
            this.RestartAtEntrance("returned-to-entrance");
        else if (this.Stage != Stage.Paused && RouteCompletionPolicy.HasStep(this.indexer, this.Actions.Count)
            && LoadingRecoveryProgress.CanResume(this.Actions[this.indexer].Name,
                this.transitionWasReturn || DeathHelper.DeathState != PlayerLifeState.Alive,
                this.currentFailure != null))
        {
            NavigationDiagnostics.Record("position-transition-resume-same-step", detail: new { origin = this.transitionOrigin, position = Player.Position });
            Svc.Log.Info($"Step {this.indexer}: loading settled; restarting the same movement from the new position (no step skipped).");
            var previousAttempt = this.currentAttempt;
            this.SeekToStep(this.indexer, broadcast: false, manual: false);
            // Loading must not replenish a previously consumed failure budget.
            if (previousAttempt != null && this.currentAttempt != null)
            {
                this.currentAttempt.Attempts = previousAttempt.Attempts;
                this.currentAttempt.StartReported = previousAttempt.StartReported;
            }
        }
        else if (this.Stage != Stage.Paused)
        {
            NavigationDiagnostics.Record("transition-unresolved", detail: Player.Position);
            NavigationDiagnostics.Export("transition-unresolved");
            this.FailCurrentStep("unexpected-position-transition", "transfer settled without safe movement recovery evidence", forcePause: true);
        }
    }

    internal bool IsAtRouteEntrance() => this.entrancePosition is { } entrance && Player.Available
        && EntranceRecovery.IsEntrance(this.entranceTerritory, Svc.ClientState.TerritoryType, entrance, Player.Position);

    internal void RestartAtEntrance(string reason)
    {
        this.MetricsProblem(reason);
        NavigationDiagnostics.Record("restart-from-entrance", detail: reason);
        this.CancelStepWork();
        this.attemptHistory.Clear();
        this.pendingStepFailure = null;
        this.combatCheckpoints.Reset();
        this.DutyData?.StopForCombat = true;
        this.skipTreasureCoffer = false;
        this.returnObservedUntil = 0;
        this.transitionPending = false;
        this.SeekToStep(0, manual: false);
        Svc.Chat.Print("[AutoDuty] " + Loc.Get("DutyTools.RestartEntrance"));
    }
}
