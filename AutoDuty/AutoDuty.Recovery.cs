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

    private unsafe void TickPositionRecovery()
    {
        if (this.Stage == Stage.Stopped || !Player.Available) return;
        if (FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()->CastActionId == 6)
            this.returnObservedUntil = Environment.TickCount64 + 15000;
        if (!this.transitionPending) return;
        if (Environment.TickCount64 - this.transitionObservedAt > 30000)
        {
            this.transitionPending = false;
            NavigationDiagnostics.Export("transition-settle-timeout");
            return;
        }
        if (!PlayerHelper.IsReadyFull || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return;
        // Let position and character state settle after the load flag clears.
        if (Environment.TickCount64 - this.transitionObservedAt < 1000) return;
        this.transitionPending = false;
        if (this.IsAtRouteEntrance() && (this.transitionWasReturn || Vector3.DistanceSquared(this.transitionOrigin, Player.Position) > 400))
            this.RestartAtEntrance("returned-to-entrance");
        else
        {
            NavigationDiagnostics.Record("transition-unresolved", detail: Player.Position);
            NavigationDiagnostics.Export("transition-unresolved");
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
