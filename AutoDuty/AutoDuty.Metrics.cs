using System;
using System.IO;
using System.Linq;
using AutoDuty.Configurations;
using AutoDuty.Data;
using AutoDuty.Helpers;
using AutoDuty.Multibox;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    internal AutomationSessionRecord? MetricsSession { get; private set; }
    internal RouteRunTracker? MetricsRun { get; private set; }
    private ExperienceAccumulator experienceSamples = new();
    private bool metricsInitialized;
    private long lastMetricsSave, lastExperienceSample;
    private bool manualMovementHeld;
    private volatile bool metricsTextInputActive;

    private void BeginMetricsSession()
    {
        if (this.MetricsSession is { EndedAt: null }) return;
        var stats = ConfigurationMain.Instance.stats;
        if (!this.metricsInitialized)
        {
            // A reload/crash cannot turn an unfinished attempt into a clean completion.
            foreach (var run in stats.routeRuns.Where(r => r.EndedAt == null && !r.Imported))
            {
                run.EndedAt = run.UpdatedAt;
                run.Outcome = "plugin-interrupted";
                if (run.FirstProblem.Length == 0) run.FirstProblem = "plugin-interrupted";
            }
            foreach (var session in stats.automationSessions.Where(s => s.EndedAt == null)) session.EndedAt = session.UpdatedAt;
            this.metricsInitialized = true;
        }
        this.experienceSamples = new();
        this.MetricsSession = new() { StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        stats.automationSessions.Add(this.MetricsSession);
        this.SampleExperience();
        this.SaveMetrics();
    }

    private void BeginRouteMetrics()
    {
        this.BeginMetricsSession();
        this.EndRouteMetrics("restarted");
        DateTime now = DateTime.UtcNow;
        var record = new RouteRunRecord
        {
            SessionId = this.MetricsSession!.Id, ParticipantId = ConfigurationMain.Instance.stats.telemetryParticipantId,
            CharacterId = Player.CID, TerritoryId = Svc.ClientState.TerritoryType,
            RouteName = Path.GetFileName(this.pathFile), RouteHash = AutomationReports.Fingerprint(this.Actions),
            JobId = (uint)Player.Job, Mode = Configuration.Meta.DutyModeEnum + (QueueHelper.ShouldBeUnSynced() ? "/Unsynced" : "/Synced"),
            StartingLevel = PlayerHelper.GetCurrentLevelFromSheet(),
            PartySize = Math.Max(1, Svc.Party.Length), Multibox = MultiboxUtility.Config.MultiBox,
            StartedAt = this.lastDutyStart >= this.MetricsSession.StartedAt && this.lastDutyStart <= now ? this.lastDutyStart : now,
            UpdatedAt = now, TotalSteps = this.Actions.Count, StartedAtFirstStep = this.indexer == 0,
        };
        if (!record.StartedAtFirstStep) { record.FirstProblem = "started-mid-route"; record.ProblemStep = Math.Max(0, this.indexer); }
        // Starting a script after the completion event cannot prove a clean duty run.
        if (this.dutyState == DutyState.DutyComplete) record.FirstProblem = "started-after-duty-completion";
        this.manualMovementHeld = false;
        this.MetricsRun = new(record);
        ConfigurationMain.Instance.stats.routeRuns.Add(record);
        this.SampleExperience();
        this.SaveMetrics();
    }

    internal void MetricsProblem(string reason, bool manual = false)
    {
        if (this.MetricsRun == null) return;
        this.MetricsRun.Problem(reason, this.indexer, manual);
        this.SaveMetrics();
    }

    private void MetricsStepCompleted(bool policySkip = false) => this.MetricsRun?.CompleteStep(this.indexer, policySkip);

    private void MetricsDutyCompleted()
    {
        this.SampleExperience();
        if (this.MetricsRun is not { } tracker) return;
        tracker.Record.DutyCompleted = true;
        if (tracker.Record.RouteFinished) this.EndRouteMetrics("duty-complete"); else this.SaveMetrics();
    }

    private void MetricsRouteFinished()
    {
        if (this.MetricsRun is not { } tracker) return;
        tracker.Record.RouteFinished = true;
        if (tracker.Record.DutyCompleted) this.EndRouteMetrics("duty-complete"); else this.SaveMetrics();
    }

    private void MetricsTerritoryChanged(uint territory)
    {
        if (territory == 0 || this.MetricsRun is not { } tracker || tracker.Record.TerritoryId == territory) return;
        // The final explicit ExitDuty task completes when the territory actually changes.
        if (tracker.Record.DutyCompleted && this.indexer == this.Actions.Count - 1 && this.pathAction?.Name == "ExitDuty")
        {
            tracker.CompleteStep(this.indexer);
            tracker.Record.RouteFinished = true;
        }
        this.EndRouteMetrics("left-duty");
    }

    private void EndRouteMetrics(string outcome)
    {
        this.SampleExperience();
        if (this.MetricsRun is not { } tracker) return;
        tracker.Finish(DateTime.UtcNow, outcome);
        this.MetricsRun = null;
        this.SaveMetrics();
    }

    private void EndMetricsSession()
    {
        this.EndRouteMetrics("stopped");
        if (this.MetricsSession is not { EndedAt: null } session) return;
        session.UpdatedAt = DateTime.UtcNow;
        session.EndedAt = session.UpdatedAt;
        this.SaveMetrics();
    }

    private void TickMetrics()
    {
        if (this.MetricsSession is not { EndedAt: null } session) return;
        session.UpdatedAt = DateTime.UtcNow;
        if (this.MetricsRun is { } tracker) tracker.Record.UpdatedAt = session.UpdatedAt;
        this.ObserveManualMovement();
        if (Environment.TickCount64 - this.lastExperienceSample >= 500)
        {
            this.lastExperienceSample = Environment.TickCount64;
            this.SampleExperience();
        }
        if (Environment.TickCount64 - this.lastMetricsSave >= 30000) this.SaveMetrics();
    }

    private unsafe void ObserveManualMovement()
    {
        if (this.MetricsRun == null || !Player.Available) { this.manualMovementHeld = false; return; }
        var input = UIModule.Instance()->GetUIInputData();
        // Use game bindings, rather than assuming WASD. Typing in chat or an ImGui input is not movement.
        bool typing = RaptureAtkModule.Instance()->IsTextInputActive() || this.metricsTextInputActive;
        bool moving = !typing && input != null && (input->IsInputIdDown(InputId.MOVE_FORE) || input->IsInputIdDown(InputId.MOVE_BACK)
            || input->IsInputIdDown(InputId.MOVE_LEFT) || input->IsInputIdDown(InputId.MOVE_RIGHT)
            || input->IsInputIdDown(InputId.MOVE_STRIFE_L) || input->IsInputIdDown(InputId.MOVE_STRIFE_R)
            || input->IsInputIdDown(InputId.MOVE_AND_STEER) || input->IsInputIdDown(InputId.JUMP));
        if (moving && !this.manualMovementHeld) this.MetricsProblem("manual-movement", true);
        this.manualMovementHeld = moving;
    }

    private unsafe void SampleExperience()
    {
        if (this.MetricsSession is not { EndedAt: null } session || !Player.Available || Player.CID == 0) return;
        var player = PlayerState.Instance();
        var job = Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault((uint)Player.Job);
        if (player == null || job == null || job.Value.ExpArrayIndex < 0) return;
        int index = job.Value.ExpArrayIndex;
        var delta = this.experienceSamples.Sample(new(Player.CID, index, player->ClassJobLevels[index], player->ClassJobExperience[index]),
            level => Svc.Data.GetExcelSheet<ParamGrow>().GetRowOrDefault((uint)level)?.ExpToNext ?? 0);
        session.Experience += delta.Experience;
        session.LevelsGained += delta.Levels;
        if (this.MetricsRun is { } tracker)
        {
            tracker.Record.Experience += delta.Experience;
            tracker.Record.LevelsGained += delta.Levels;
        }
    }

    private void SaveMetrics()
    {
        this.lastMetricsSave = Environment.TickCount64;
        if (this.MetricsRun is { } tracker) tracker.Record.UpdatedAt = DateTime.UtcNow;
        ConfigurationProfileV2.Save();
    }
}
