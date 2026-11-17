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
    private static int metricsRevision;
    private static int sampleCount;
    private static (ulong Character, uint Job, short Level, ushort ItemLevel, DutyMode Mode, int Revision) context;
    private static Content? nextDuty;
    private static readonly Dictionary<uint, (ContentPathsManager.DutyPath? Path, RouteMetricSummary? All, RouteMetricSummary? Level)> evidence = [];
    private static readonly Vector4 Warning = new(1, .7f, .3f, 1);
    private static string Text(string key) => Loc.Get("LevelingPlan." + key);
    private static DutyMode Mode => Plugin.TrustLevelingEnabled ? DutyMode.Trust : Configuration.Meta.DutyModeEnum;

    internal static void Draw()
    {
        MainWindow.CurrentTabName = "LevelingPlan";
        ImGui.TextWrapped(Text("Source"));
        ImGui.TextWrapped(Text("Rules"));
        ImGui.TextWrapped(Text("SharedPlan"));
        var config = Configuration.DutyConfig;
        bool only = config.LevelingIncludedOnly;
        if (ImGui.Checkbox(DutyToolsUi.Text("IncludedOnly"), ref only)) { config.LevelingIncludedOnly = only; Save(); }
        if (ImGui.Button(Text("Refresh"))) refreshed = 0;
        ImGui.TextWrapped(Text("MissingNote"));
        ImGui.SetNextItemWidth(260);
        ImGui.InputText(DutyToolsUi.Text("Filter"), ref search, 128);
        ImGui.SameLine(); ImGui.Checkbox(Text("PlannedOnly"), ref plannedOnly);
        if (!Player.Available) { ImGui.TextWrapped(Text("Login")); return; }
        short level = PlayerHelper.GetCurrentLevelFromSheet();
        ImGui.TextWrapped($"{Text("Context")}: {Player.Job} / L{level} / {Mode}");
        if (Mode is not (DutyMode.Support or DutyMode.Trust)) ImGui.TextColored(Warning, Text("SelectMode"));
        EnsureFresh(level);
        var next = nextDuty;
        ImGui.TextWrapped(Text("Next") + ": " + (next?.Name ?? Text("NoEligible")));
        var catalog = LevelingHelper.Catalog;
        var planned = LevelingHelper.LevelingDuties.Select(c => c.TerritoryType).ToHashSet();
        if (!ImGui.BeginTable("LevelingPlan", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY,
            new Vector2(0, Math.Max(180, ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeightWithSpacing() * 2)))) return;
        foreach (var key in new[] { "Duty", "Policy", "Script", "Completion", "Efficiency", "Status" })
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
            ImGui.TableNextColumn(); DrawScript(id, row.Path);
            ImGui.TableNextColumn();
            if (row.All == null) ImGui.TextDisabled(Text("NoSamples"));
            else
            {
                ImGui.TextUnformatted($"{Text("Best")}: {row.All.BestCompletion:F1}%");
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
        if (duty.ClassJobLevelRequired > level) return Text("OutsideRange");
        if (InventoryHelper.CurrentItemLevel < duty.ItemLevelRequired)
            return $"{Text("Unavailable")} (i{InventoryHelper.CurrentItemLevel} / i{duty.ItemLevelRequired})";
        return duty.CanRun(level, Mode) ? Text("Ready") : Text("Unavailable");
    }

    private static void DrawScript(uint id, ContentPathsManager.DutyPath? selected)
    {
        if (!ContentPathsManager.DictionaryPaths.TryGetValue(id, out var container) || container.Paths.Count == 0)
        {
            ImGui.TextColored(Warning, Text("MissingScript"));
            using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(Plugin.Stage != Stage.Stopped || Plugin.Recorder.Recording))
                if (ImGui.SmallButton(Text("CreateRoute"))) RoleRoutesUi.Create(id, JobWithRole.All, null);
            return;
        }
        ImGui.TextWrapped(selected?.FileName ?? Text("MissingScript"));
        ImGui.TextDisabled(Text("AutoRole"));
        if (ImGui.SmallButton(Text("ManageRoles"))) ImGui.OpenPopup("RoleRoutes");
        if (ImGui.BeginPopup("RoleRoutes"))
        {
            RoleRoutesUi.Draw(container);
            ImGui.EndPopup();
        }
    }

    internal static void DrawSummary()
    {
        ImGui.TextWrapped(Text("SharedPlan"));
        if (!Player.Available) return;
        short level = PlayerHelper.GetCurrentLevelFromSheet();
        EnsureFresh(level);
        var planned = LevelingHelper.LevelingDuties;
        if (!ImGui.BeginTable("MainLevelingPlan", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY,
            new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 11))) return;
        foreach (var key in new[] { "Duty", "Script", "Completion", "Status" }) ImGui.TableSetupColumn(Text(key));
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        foreach (var duty in planned)
        {
            evidence.TryGetValue(duty.TerritoryType, out var row);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            bool current = Plugin.CurrentTerritoryContent?.TerritoryType == duty.TerritoryType;
            ImGui.TextWrapped($"{(current ? "> " : "")}L{duty.ClassJobLevelRequired} {duty.Name}");
            ImGui.TableNextColumn(); ImGui.TextWrapped(row.Path?.FileName ?? Text("MissingScript"));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(row.All == null ? Text("NoSamples") : $"{row.All.BestCompletion:F1}%");
            ImGui.TableNextColumn(); ImGui.TextWrapped((current ? Text("Selected") + " / " : "") + Status(duty, level, true, row.Path != null));
        }
        ImGui.EndTable();
        if (planned.Length == 0) ImGui.TextWrapped(Text("NoEligible"));
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

    private static void EnsureFresh(short level)
    {
        var current = (Player.CID, (uint)Player.Job, level, InventoryHelper.CurrentItemLevel, Mode, LevelingHelper.PlanRevision);
        // Keep row content and popup layout stable while browsing. Refresh only
        // for an explicit refresh, route/plan change, or player context change.
        int count = ConfigurationMain.Instance.stats.routeRuns.Count;
        if (current != context || refreshed == 0 || metricsRevision != Plugin.MetricsEvidenceRevision || sampleCount != count)
        {
            sampleCount = count;
            metricsRevision = Plugin.MetricsEvidenceRevision;
            context = current;
            Refresh(level);
        }
    }

    private static void Save()
    {
        LevelingHelper.ResetLevelingDuties(); refreshed = 0;
        ConfigurationProfileV2.Save();
        if (Plugin.LevelingEnabled && Plugin.Stage == Stage.Stopped)
            Plugin.CurrentTerritoryContent = LevelingHelper.SelectHighestLevelingRelevantDuty(Plugin.LevelingModeEnum);
    }
}
