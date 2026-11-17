using AutoDuty.Helpers;
using AutoDuty.Windows;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    private readonly DutyExecutionBoundary executionBoundary = new();

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
