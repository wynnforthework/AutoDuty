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
    internal StepAttempt? currentAttempt;
    internal StepFailure? currentFailure;
    internal readonly Dictionary<int, StepAttempt> attemptHistory = [];
    private (string Reason, string Detail)? pendingStepFailure;

    internal void BeginAttempt(int index)
    {
        this.currentAttempt = index >= 0 && index < this.Actions.Count
            ? new StepAttempt { Index = index, Action = this.Actions[index], StartPosition = Player.Available ? Player.Position : default }
            : null;
        if (this.currentAttempt != null)
            this.attemptHistory[index] = this.currentAttempt;
        StuckHelper.ResetForStep();
    }

    // TaskManager callbacks must not mutate its queue while it is enumerating a task.
    internal void ReportTaskFailure(string reason, string detail) => this.pendingStepFailure ??= (reason, detail);

    private void CancelStepWork()
    {
        this.taskManager.Abort();
        VNavmesh_IPCSubscriber.Path_Stop();
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
        attempt.LastFailReason = reason;
        attempt.Attempts++;
        Svc.Log.Warning($"Step {this.indexer + 1} ({attempt.Action.ToCustomString()}): {reason}; {detail}; failure {attempt.Attempts}/{Configuration.DutyConfig.MaxStepRetries}");
        this.CancelStepWork();
        this.pendingStepFailure = null;
        if (attempt.Attempts < Math.Max(1, Configuration.DutyConfig.MaxStepRetries))
        {
            attempt.StartedAt = attempt.LastTickAt = Environment.TickCount64;
            attempt.ActionStartedAt = 0;
            attempt.MovementStarted = false;
            StuckHelper.ResetForStep();
            this.Stage = Stage.Reading_Path;
            return;
        }
        this.FailCurrentStep(reason, detail);
    }

    internal void FailCurrentStep(string reason, string detail = "")
    {
        if (this.currentFailure != null) return;
        this.CancelStepWork();
        if (!Configuration.DutyConfig.PauseOnStepFailure)
        {
            Svc.Chat.Print($"[AutoDuty] Step {this.indexer + 1} failed: {reason}. Skipped by configuration.");
            if (this.currentAttempt != null) this.currentAttempt.SkippedReason = reason;
            this.indexer++;
            this.Stage = Stage.Reading_Path;
            return;
        }
        this.currentFailure = new(this.indexer, reason, detail, this.currentAttempt?.Attempts ?? 1, DateTime.UtcNow);
        this.Stage = Stage.Paused;
        this.States |= PluginState.Failed;
        Svc.Chat.PrintError($"[AutoDuty] Step {this.indexer + 1} failed: {reason} ({detail}). Paused after {this.currentFailure.Attempts} failures.");
        Svc.Log.Error($"Step {this.indexer + 1} paused: {reason}; {detail}");
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
        this.taskManager.StepMode = false;
        this.Stage = this.previousStage;
        if (this.currentAttempt != null) this.currentAttempt.LastTickAt = Environment.TickCount64;
    }

    internal void SeekToStep(int index, bool resume = true, bool broadcast = true)
    {
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
            MultiboxUtility.Server.SendStepStart();
        }
        Svc.Log.Info($"Seek to step {this.indexer + 1}; resume={resume}");
    }

    private bool UpdateStepAttempt()
    {
        if (this.currentAttempt?.Index != this.indexer) this.BeginAttempt(this.indexer);
        if (this.pendingStepFailure is { } failure && this.Stage != Stage.Paused)
        {
            this.pendingStepFailure = null;
            this.RegisterStepFailure(failure.Reason, failure.Detail);
            return false;
        }
        if (this.currentAttempt is not { } attempt) return true;
        long now = Environment.TickCount64;
        if (this.Stage is Stage.Paused or Stage.Waiting_For_Combat or Stage.Dead or Stage.Revived
            || Svc.Condition[ConditionFlag.WatchingCutscene] || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Svc.Condition[ConditionFlag.OccupiedInQuestEvent] || Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.BetweenAreas51])
        {
            long delta = now - attempt.LastTickAt;
            attempt.StartedAt += delta;
            if (attempt.ActionStartedAt != 0) attempt.ActionStartedAt += delta;
            this.taskManager.RemainingTimeMS += delta;
        }
        attempt.LastTickAt = now;
        return true;
    }
}
