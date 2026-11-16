using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoDuty.Configurations;
using AutoDuty.Data;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Content = AutoDuty.Data.Classes.Content;

namespace AutoDuty.Windows;

internal static class LevelingPlanTab
{
    private static string search = "";
    private static bool plannedOnly;
    private static long refreshed;
    private static Content? nextDuty;
    private static readonly Dictionary<uint, (ContentPathsManager.DutyPath? Path, RouteMetricSummary? All, RouteMetricSummary? Level)> evidence = [];
    private static readonly Vector4 Warning = new(1, .7f, .3f, 1);
    private static string Text(string key) => Loc.Get("LevelingPlan." + key);
    private static DutyMode Mode => Plugin.TrustLevelingEnabled ? DutyMode.Trust : Configuration.Meta.DutyModeEnum;

    internal static void Draw()
    {
        ImGui.TextWrapped(Text("Source"));
        ImGui.TextWrapped(Text("Rules"));
        var config = Configuration.DutyConfig;
        bool only = config.LevelingIncludedOnly;
        if (ImGui.Checkbox(DutyToolsUi.Text("IncludedOnly"), ref only)) { config.LevelingIncludedOnly = only; Save(); }
        bool measured = config.LevelingPreferMeasuredEfficiency;
        if (ImGui.Checkbox(Text("Measured"), ref measured)) { config.LevelingPreferMeasuredEfficiency = measured; Save(); }
        ImGui.SetNextItemWidth(120);
        int samples = config.LevelingEfficiencyMinimumSamples;
        if (ImGui.InputInt(Text("MinimumSamples"), ref samples)) { config.LevelingEfficiencyMinimumSamples = Math.Clamp(samples, 1, 1000); Save(); }
        ImGui.TextWrapped(Text("EfficiencyNote"));
        if (ImGui.Button(DutyToolsUi.Text("Recorder"))) Plugin.ShowRecorder();
        ImGui.SameLine();
        if (ImGui.Button(DutyToolsUi.Text("PastePath"))) Plugin.PathCommand(["path", "import"]);
        ImGui.SameLine();
        if (ImGui.Button(Text("Refresh"))) refreshed = 0;
        ImGui.TextWrapped(Text("MissingNote"));
        ImGui.SetNextItemWidth(260);
        ImGui.InputText(DutyToolsUi.Text("Filter"), ref search, 128);
        ImGui.SameLine(); ImGui.Checkbox(Text("PlannedOnly"), ref plannedOnly);
        if (!Player.Available) { ImGui.TextWrapped(Text("Login")); return; }
        short level = PlayerHelper.GetCurrentLevelFromSheet();
        ImGui.TextWrapped($"{Text("Context")}: {Player.Job} / L{level} / {Mode}");
        if (Mode is not (DutyMode.Support or DutyMode.Trust)) ImGui.TextColored(Warning, Text("SelectMode"));
        if (Environment.TickCount64 - refreshed > 2000 || refreshed == 0) Refresh(level);
        var next = nextDuty;
        ImGui.TextWrapped(Text("Next") + ": " + (next?.Name ?? Text("NoEligible")));
        var catalog = LevelingHelper.Catalog;
        var planned = LevelingHelper.LevelingDuties.Select(c => c.TerritoryType).ToHashSet();
        if (!ImGui.BeginTable("LevelingPlan", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY,
            new Vector2(0, Math.Max(180, ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeightWithSpacing() * 2)))) return;
        foreach (var key in new[] { "Duty", "Policy", "Range", "Priority", "Script", "Completion", "Efficiency", "Status" })
            ImGui.TableSetupColumn(Text(key), ImGuiTableColumnFlags.WidthFixed, key == "Duty" ? 210 : key == "Script" ? 220 : key == "Status" ? 175 : 145);
        ImGui.TableSetupScrollFreeze(1, 1); ImGui.TableHeadersRow();
        foreach (var duty in catalog)
        {
            uint id = duty.TerritoryType;
            if (plannedOnly && !planned.Contains(id)) continue;
            if (search.Length > 0 && !($"{id} {duty.Name} {duty.EnglishName}").Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            evidence.TryGetValue(id, out var row);
            ImGui.PushID((int)id); ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.TextWrapped($"L{duty.ClassJobLevelRequired} (i{duty.ItemLevelRequired}) {duty.Name}\n#{id}");
            ImGui.TableNextColumn();
            int policy = config.LevelingExcludedDuties.Contains(id) ? 2 : config.LevelingIncludedDuties.Contains(id) ? 1 : 0;
            bool use = policy == 1, skip = policy == 2;
            if (ImGui.Checkbox(Text("Use"), ref use)) SetPolicy(id, use ? 1 : 0);
            ImGui.SameLine(); if (ImGui.Checkbox(Text("Skip"), ref skip)) SetPolicy(id, skip ? 2 : 0);
            ImGui.TextDisabled(Text(policy == 0 ? planned.Contains(id) ? "DefaultUsed" : "DefaultUnused" : "Explicit"));
            if (config.LevelingAutoExcluded.Contains(id) && ImGui.SmallButton(Text("Restore")))
            { config.LevelingAutoExcluded.Remove(id); config.LevelingFailureCounts.Remove(id); Save(); }
            var rule = LevelingHelper.Rule(id);
            ImGui.TableNextColumn();
            int start = rule.MinimumLevel, end = rule.MaximumLevel;
            ImGui.SetNextItemWidth(65); bool changed = ImGui.InputInt("##min", ref start, 0);
            ImGui.SameLine(); ImGui.SetNextItemWidth(65); changed |= ImGui.InputInt("##max", ref end, 0);
            if (changed) { rule.MinimumLevel = Math.Clamp(start, 15, 99); rule.MaximumLevel = Math.Clamp(end, rule.MinimumLevel, 99); config.LevelingDutyRules[id] = rule; Save(); }
            ImGui.TableNextColumn(); int priority = rule.Priority;
            ImGui.SetNextItemWidth(110);
            if (ImGui.InputInt("##priority", ref priority)) { rule.Priority = Math.Clamp(priority, -1000, 1000); config.LevelingDutyRules[id] = rule; Save(); }
            ImGui.TableNextColumn(); DrawScript(id, row.Path);
            ImGui.TableNextColumn();
            if (row.All == null) ImGui.TextDisabled(Text("NoSamples"));
            else
            {
                ImGui.TextUnformatted($"{Text("Mean")}: {row.All.MeanCompletion:F1}%");
                ImGui.TextUnformatted($"{Text("Clean")}: {row.All.CleanRuns}/{row.All.Samples} ({row.All.Reliability:F1}%)");
            }
            ImGui.TableNextColumn();
            if (row.Level == null) ImGui.TextDisabled(Text("NoLevelSamples"));
            else
            {
                ImGui.TextUnformatted($"L{level}: {row.Level.ExperiencePerHour:N0} XP/h");
                ImGui.TextUnformatted($"{Text("Samples")}: {row.Level.Samples}");
            }
            ImGui.TableNextColumn();
            string status = Status(duty, level, planned.Contains(id), row.Path != null);
            if (next?.TerritoryType == id) status = Text("Selected") + "\n" + status;
            ImGui.TextWrapped(status);
            ImGui.PopID();
        }
        ImGui.EndTable();
        ImGui.TextWrapped(Text("Timing"));
    }

    private static unsafe string Status(Content duty, short level, bool planned, bool script)
    {
        uint id = duty.TerritoryType;
        if (Mode is not (DutyMode.Support or DutyMode.Trust)) return Text("SelectMode");
        if (Configuration.DutyConfig.LevelingExcludedDuties.Contains(id)) return Text("Skipped");
        if (Configuration.DutyConfig.LevelingAutoExcluded.Contains(id)) return Text("AutoSkipped");
        if (!script) return Text("MissingScript");
        if (!duty.DutyModes.HasFlag(Mode)) return Text("WrongMode");
        if (!UIState.IsInstanceContentUnlocked(duty.Id)) return Text("Locked");
        if (!planned) return Text("NotPlanned");
        if (!LevelingHelper.Rule(id).Allows(level) || duty.ClassJobLevelRequired > level) return Text("OutsideRange");
        return duty.CanRun(level, Mode) ? Text("Ready") : Text("Unavailable");
    }

    private static void DrawScript(uint id, ContentPathsManager.DutyPath? selected)
    {
        if (!ContentPathsManager.DictionaryPaths.TryGetValue(id, out var container) || container.Paths.Count == 0)
        { ImGui.TextColored(Warning, Text("MissingScript")); return; }
        ImGui.SetNextItemWidth(210);
        if (ImGui.BeginCombo("##route", selected?.FileName ?? Text("MissingScript")))
        {
            foreach (var path in container.Paths.ToArray())
                if (ImGui.Selectable(path.FileName, path == selected))
                {
                    if (!Configuration.Meta.PathSelectionsByPath.TryGetValue(id, out var selections) || selections == null)
                        Configuration.Meta.PathSelectionsByPath[id] = selections = [];
                    var job = Player.Job.JobToJobWithRole();
                    foreach (var name in selections.Keys.ToArray()) selections[name] &= ~job;
                    selections[path.FileName] = selections.GetValueOrDefault(path.FileName) | job;
                    Save();
                }
            ImGui.EndCombo();
        }
    }

    private static void SetPolicy(uint id, int policy)
    {
        var config = Configuration.DutyConfig;
        config.LevelingIncludedDuties.Remove(id); config.LevelingExcludedDuties.Remove(id);
        if (policy == 1) { config.LevelingIncludedDuties.Add(id); config.LevelingAutoExcluded.Remove(id); config.LevelingFailureCounts.Remove(id); }
        if (policy == 2) config.LevelingExcludedDuties.Add(id);
        Save();
    }

    private static void Refresh(short level)
    {
        nextDuty = LevelingHelper.SelectForLevel(level, Mode);
        evidence.Clear();
        foreach (var duty in LevelingHelper.Catalog)
        {
            var path = LevelingHelper.SelectedRoute(duty.TerritoryType);
            evidence[duty.TerritoryType] = path == null ? (null, null, null)
                : (path, LevelingHelper.Evidence(path, Mode), LevelingHelper.Evidence(path, Mode, level));
        }
        refreshed = Environment.TickCount64;
    }

    private static void Save()
    {
        LevelingHelper.ResetLevelingDuties(); refreshed = 0;
        ConfigurationProfileV2.Save();
        if (Plugin.LevelingEnabled && Plugin.Stage == Stage.Stopped)
            Plugin.CurrentTerritoryContent = LevelingHelper.SelectHighestLevelingRelevantDuty(Plugin.LevelingModeEnum);
    }
}
