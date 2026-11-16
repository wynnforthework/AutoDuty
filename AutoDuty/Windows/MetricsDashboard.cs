using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoDuty.Configurations;
using AutoDuty.Data;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using ECommons.ExcelServices;
using ECommons.DalamudServices;
using ECommons.GameFunctions;

namespace AutoDuty.Windows;

internal static class MetricsDashboard
{
    private static bool sessionOnly = true, includeImported = true, currentJobOnly = true;
    private static int minimumSamples = 5;
    private static string message = "";
    private static long coverageRefreshed;
    private static int coverageRevision = -1;
    private static readonly List<(int Start, int End, uint Territory)> coverageBands = [];
    private static string Text(string key) => Loc.Get("Metrics." + key);
    private static readonly Vector4 Good = new(.35f, .85f, .55f, 1), Attention = new(1, .7f, .3f, 1);

    internal static void Draw()
    {
        var stats = ConfigurationMain.Instance.stats;
        var session = Plugin.MetricsSession ?? stats.automationSessions.LastOrDefault();
        ImGui.TextUnformatted(Text("Title"));
        if (session != null)
        {
            var runs = stats.routeRuns.Where(r => r.SessionId == session.Id && !r.Imported).ToList();
            double hours = session.Duration.TotalHours;
            ImGui.TextWrapped($"{Text(session.EndedAt == null ? "CurrentSession" : "LastSession")}: {Format(session.Duration)} | {Text("DutyCount")}: {runs.Count(r => r.DutyCompleted)}/{runs.Count} | {Text("CleanCount")}: {runs.Count(r => r.VerifiedComplete)}");
            ImGui.TextWrapped($"{Text("Experience")}: {session.Experience:N0} | {Text("ExperiencePerHour")}: {(hours > 0 ? session.Experience / hours : 0):N0} | {Text("Levels")}: +{session.LevelsGained}");
        }
        else ImGui.TextWrapped(Text("NoSession"));

        if (Plugin.MetricsRun is { } tracker)
        {
            var run = tracker.Record;
            ImGui.TextWrapped($"{run.RouteName} | {(Job)run.JobId} | {run.Mode} | {run.PartySize} {Text("Players")}");
            ImGui.ProgressBar((float)(run.CompletionPercent / 100), new Vector2(-1, 0), $"{Text("Completion")}: {run.CompletionPercent:F1}% ({run.AutonomousSteps}/{run.TotalSteps})");
            if (run.FirstProblem.Length > 0) ImGui.TextColored(Attention, $"{Text("FirstProblem")}: #{run.ProblemStep + 1} {run.FirstProblem}");
            if (ImGui.Button(Text("MarkIntervention"))) Plugin.MetricsProblem("operator-marked", true);
        }
        ImGui.TextWrapped(Text("Definition"));
        ImGui.Checkbox(Text("SessionOnly"), ref sessionOnly);
        ImGui.SameLine(); ImGui.Checkbox(Text("IncludeImported"), ref includeImported);
        ImGui.SameLine(); ImGui.Checkbox(Text("CurrentJobOnly"), ref currentJobOnly);
        var filtered = stats.routeRuns.Where(r => (!sessionOnly || r.SessionId == session?.Id && !r.Imported)
            && (includeImported || !r.Imported) && (!currentJobOnly || !Player.Available || r.JobId == (uint)Player.Job)).ToList();
        if (ImGui.Button(Text("Export"))) Try(() => { ImGui.SetClipboardText(AutomationReports.Export(filtered)); message = Text("Exported"); });
        ImGui.SameLine();
        if (ImGui.Button(Text("Import"))) Try(() =>
        {
            var incoming = AutomationReports.Read(ImGui.GetClipboardText());
            int added = AutomationReports.Merge(stats.routeRuns, incoming);
            ConfigurationProfileV2.Save(); sessionOnly = false; message = $"{Text("Imported")}: {added}";
        });
        if (message.Length > 0) ImGui.TextWrapped(message);
        ImGui.TextWrapped(Text("ReportsNote"));

        var summaries = AutomationReports.Summarize(filtered);
        if (ImGui.CollapsingHeader(Text("Compare"), ImGuiTreeNodeFlags.DefaultOpen)) DrawSummaries(summaries);
        if (ImGui.CollapsingHeader(Text("History"), ImGuiTreeNodeFlags.DefaultOpen)) DrawHistory(filtered);
        if (ImGui.CollapsingHeader(Text("Coverage"))) DrawCoverage(stats.routeRuns.Where(r => includeImported || !r.Imported));
        ImGui.Separator();
    }

    private static void DrawSummaries(List<RouteMetricSummary> groups)
    {
        if (groups.Count == 0) { ImGui.TextWrapped(Text("NoData")); return; }
        ImGui.TextWrapped(Text("ComparisonNote"));
        if (!ImGui.BeginTable("RouteMetricSummary", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY, new(0, 230))) return;
        foreach (string key in new[] { "Route", "Samples", "CleanRate", "MeanCompletion", "MeanDuration", "RouteExperiencePerHour", "Problems" }) ImGui.TableSetupColumn(Text(key));
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        foreach (var group in groups)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{DutyName(group.Key.Territory)}\n{group.Name}\n{group.Key.Hash[..8]} | {(Job)group.Key.Job} | {group.Key.Mode} | {group.Key.PartySize} {(group.Key.Multibox ? "Multibox" : "")}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{group.Samples} / {group.Participants} {Text("Contributors")}");
            ImGui.TableNextColumn(); ImGui.TextColored(group.CleanRuns == group.Samples ? Good : Attention, $"{group.Reliability:F1}% ({group.CleanRuns}/{group.Samples})");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{group.MeanCompletion:F1}%");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(Format(TimeSpan.FromSeconds(group.MeanSeconds)));
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{group.ExperiencePerHour:N0}");
            ImGui.TableNextColumn();
            foreach (var problem in group.Runs.Where(r => r.FirstProblem.Length > 0).GroupBy(r => (r.ProblemStep, r.FirstProblem)).OrderByDescending(g => g.Count()).Take(3))
                ImGui.TextUnformatted($"#{problem.Key.ProblemStep + 1} {problem.Key.FirstProblem}: {problem.Count()}");
        }
        ImGui.EndTable();
    }

    private static void DrawHistory(List<RouteRunRecord> records)
    {
        var recent = records.OrderByDescending(r => r.StartedAt).Take(50).ToArray();
        if (recent.Length == 0) return;
        if (!ImGui.BeginTable("RouteMetricHistory", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY, new(0, 200))) return;
        foreach (string key in new[] { "Time", "Route", "Completion", "Experience", "Outcome" }) ImGui.TableSetupColumn(Text(key));
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        foreach (var run in recent)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextUnformatted(run.StartedAt.ToLocalTime().ToString("MM-dd HH:mm:ss"));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(run.RouteName + " / " + ((Job)run.JobId));
            ImGui.TableNextColumn(); ImGui.TextColored(run.VerifiedComplete ? Good : Attention, $"{run.CompletionPercent:F1}% ({run.AutonomousSteps}/{run.TotalSteps})");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{Text("Executed")}: {run.ExecutedSteps}; {Text("PolicySkipped")}: {run.PolicySkippedSteps}; {Text("Failures")}: {run.Failures}; {Text("Interventions")}: {run.Interventions}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{run.Experience:N0} / +{run.LevelsGained}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{run.Outcome}\n{run.FirstProblem}" + (run.ProblemStep is { } step ? $" #{step + 1}" : ""));
        }
        ImGui.EndTable();
    }

    private static void DrawCoverage(IEnumerable<RouteRunRecord> records)
    {
        ImGui.TextWrapped(Text("CoverageNote"));
        ImGui.SetNextItemWidth(120);
        ImGui.InputInt(Text("MinimumSamples"), ref minimumSamples);
        minimumSamples = Math.Clamp(minimumSamples, 1, 1000);
        if (!Player.Available) { ImGui.TextWrapped(Text("NoData")); return; }
        var groups = AutomationReports.Summarize(records.Where(r => r.JobId == (uint)Player.Job));
        if (Environment.TickCount64 - coverageRefreshed > 5000 || coverageRevision != LevelingHelper.PlanRevision)
        {
            coverageBands.Clear();
            for (int level = 16; level < 100; level++)
            {
                uint territory = LevelingHelper.SelectForLevel((short)level, Configuration.Meta.DutyModeEnum)?.TerritoryType ?? 0;
                if (coverageBands.Count > 0 && coverageBands[^1].Territory == territory) coverageBands[^1] = (coverageBands[^1].Start, level, territory);
                else coverageBands.Add((level, level, territory));
            }
            coverageRefreshed = Environment.TickCount64;
            coverageRevision = LevelingHelper.PlanRevision;
        }
        if (!ImGui.BeginTable("LevelingCoverage", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX)) return;
        foreach (string key in new[] { "LevelBand", "Route", "Samples", "Status" }) ImGui.TableSetupColumn(Text(key));
        ImGui.TableHeadersRow();
        foreach (var band in coverageBands)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextUnformatted($"{band.Start}–{band.End}");
            ImGui.TableNextColumn();
            if (band.Territory == 0 || !ContentPathsManager.DictionaryPaths.TryGetValue(band.Territory, out var container) || container.Paths.Count == 0)
            { ImGui.TextUnformatted(Text("NoRoute")); ImGui.TableNextColumn(); ImGui.TableNextColumn(); ImGui.TextColored(Attention, Text("NoRoute")); continue; }
            var path = container.SelectPath(out _);
            if (path?.PathFile == null) { ImGui.TextUnformatted(Text("NoRoute")); ImGui.TableNextColumn(); ImGui.TableNextColumn(); continue; }
            string hash = AutomationReports.Fingerprint(path.Actions);
            string mode = Configuration.Meta.DutyModeEnum + (QueueHelper.ShouldBeUnSynced() ? "/Unsynced" : "/Synced");
            var best = groups.Where(g => g.Key.Territory == band.Territory && g.Key.Hash == hash && g.Key.Mode == mode && g.Key.Multibox == Multibox.MultiboxUtility.Config.MultiBox && g.Key.PartySize == Math.Max(1, Svc.Party.Length))
                .OrderByDescending(g => g.Reliability).ThenByDescending(g => g.Samples).FirstOrDefault();
            ImGui.TextUnformatted(DutyName(band.Territory) + "\n" + path.FileName);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(best == null ? "0" : $"{best.CleanRuns}/{best.Samples} | {best.Participants} {Text("Contributors")} | {best.Key.PartySize} {Text("Players")}");
            ImGui.TableNextColumn();
            bool candidate = best != null && best.Samples >= minimumSamples && best.CleanRuns == best.Samples;
            ImGui.TextColored(candidate ? Good : Attention, Text(candidate ? "Candidate" : best == null || best.Samples < minimumSamples ? "NeedSamples" : "NeedsTuning"));
        }
        ImGui.EndTable();
    }

    private static string DutyName(uint territory) => ContentHelper.DictionaryContent.TryGetValue(territory, out var content) ? content.Name : territory.ToString();
    private static string Format(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    private static void Try(Action action) { try { action(); } catch (Exception ex) { message = ex.Message; } }
}
