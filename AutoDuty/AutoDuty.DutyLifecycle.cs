using System.Linq;
using AutoDuty.Helpers;
using AutoDuty.Windows;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    private readonly DutyExecutionBoundary executionBoundary = new();

    private bool IsCompletedBossRoute()
    {
        if (this.dutyState != DutyState.DutyComplete || !InDungeon
            || !this.States.HasFlag(PluginState.Navigating) || this.Stage == Stage.Paused) return false;
        return RouteCompletionPolicy.CompletedBoss(true, true, false,
            this.indexer, this.Actions.Select(a => a.Name).ToArray());
    }

    private bool TickCompletedBossRoute()
    {
        // DutyCompleted is authoritative victory evidence, even when the final boss
        // corpse/objective was missed. Never infer victory merely from leaving combat.
        if (!this.IsCompletedBossRoute()) return false;
        this.pendingStepFailure = null;
        if (!PlayerHelper.IsReadyFull || PartyHelper.PartyInCombat()) return true;
        // Let existing optional chest tasks finish, but never retry the completed
        // encounter because a loot movement/callback timed out.
        if (!RouteCompletionPolicy.DrainFinished(this.taskManager.IsBusy,
                System.Environment.TickCount64 - this.dutyCompletedAt)) return true;
        Svc.Log.Info($"[AutoDuty] Duty completed: finishing final Boss step {this.indexer}; pending task={this.taskManager.CurrentTask?.Name ?? "none"}.");
        this.CancelStepWork();
        this.MetricsStepCompleted();
        this.indexer++;
        while (this.indexer < this.Actions.Count)
        {
            this.MetricsStepCompleted(policySkip: true);
            this.indexer++;
        }
        this.currentAttempt = null;
        this.navigationReadiness.Reset();
        this.Stage = Stage.Reading_Path;
        this.DoneNavigating();
        return true;
    }

    private void ResetDutyStepState()
    {
        this.navigationReadiness.Reset();
        this.interactionTransition.Reset();
        this.interactionTransitionCancelled = false;
        this.interactionRequiresTransfer = false;
        this.transitionPending = false;
        this.transitionObservedAt = 0;
        this.returnObservedUntil = 0;
        this.entrancePosition = null;
        this.entranceTerritory = 0;
        this.combatCheckpoints.Reset();
        this.bossProgress.Reset();
        DeathHelper.CancelForDutyChange();
        this.currentAttempt = null;
        this.currentFailure = null;
        this.pendingStepFailure = null;
        this.attemptHistory.Clear();
        this.indexer = 0;
        this.pathAction = new();
        this.mainListClicked = false;
        DutyToolsUi.ResetSelection();
        this.Interactables.Clear();
        this.skipTreasureCoffer = false;
        this.treasureCofferGameObject = null;
        this.action = "";
        this.previousStage = Stage.Stopped;
        this.States &= ~(PluginState.Navigating | PluginState.Paused | PluginState.Failed);
        this.DutyData?.Dispose();
        this.DutyData = null;
    }

    private void ResetDepartedDuty(uint territory)
    {
        if (!this.executionBoundary.Leave(territory, ContentHelper.DictionaryContent.ContainsKey(territory))) return;
        bool wasPaused = this.Stage == Stage.Paused;
        bool hadStepWork = this.States.HasFlag(PluginState.Navigating) || wasPaused;
        NavigationDiagnostics.Record("left-duty-reset", detail: new { territory, previousStep = this.indexer });
        // Preserve the loop/queue pipeline when no route task is running.
        if (hadStepWork) this.CancelStepWork();
        bool keepLooping = !wasPaused && this.States.HasFlag(PluginState.Looping);
        if (hadStepWork)
            this.Stage = keepLooping ? Stage.Looping : Stage.Stopped;
        this.ResetDutyStepState();
        Svc.Log.Info("[AutoDuty] Left duty: cleared route execution state; next run starts at step 0.");
    }
}
