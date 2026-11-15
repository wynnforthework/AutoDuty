using System;
using System.IO;
using System.Linq;
using System.Numerics;
using AutoDuty.Configurations;
using AutoDuty.Data;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;

namespace AutoDuty.Windows;

internal static class RouteOptionsUi
{
    private static string filter = "";
    internal static void Leveling()
    {
        if (!ImGui.CollapsingHeader(DutyToolsUi.Text("Leveling"))) return;
        var config = Configuration.DutyConfig;
        bool only = config.LevelingIncludedOnly;
        if (ImGui.Checkbox(DutyToolsUi.Text("IncludedOnly"), ref only)) { config.LevelingIncludedOnly = only; Save(); }
        int threshold = config.LevelingAutoExcludeAfterFailures;
        if (ImGui.InputInt(DutyToolsUi.Text("AutoExcludeThreshold"), ref threshold)) { config.LevelingAutoExcludeAfterFailures = Math.Max(0, threshold); Save(); }
        bool reset = config.LevelingResetAutoExcludedOnSuccess;
        if (ImGui.Checkbox(DutyToolsUi.Text("ResetOnSuccess"), ref reset)) { config.LevelingResetAutoExcludedOnSuccess = reset; Save(); }
        if (ImGui.Button(DutyToolsUi.Text("ClearAutoExcluded"))) { config.LevelingAutoExcluded.Clear(); config.LevelingFailureCounts.Clear(); Save(); }
        ImGui.InputText(DutyToolsUi.Text("Filter"), ref filter, 128);
        if (ImGui.BeginChild("LevelingRoutes", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 10)))
        {
            foreach (var pair in ContentPathsManager.DictionaryPaths.OrderBy(p => p.Value.Content.ClassJobLevelRequired))
            {
                if (!pair.Value.Content.DutyModes.HasFlag(DutyMode.Support) && !pair.Value.Content.DutyModes.HasFlag(DutyMode.Trust)) continue;
                string label = $"{pair.Key}: {pair.Value.Content.Name}";
                if (!label.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                ImGui.PushID((int)pair.Key);
                bool excluded = config.LevelingExcludedDuties.Contains(pair.Key);
                bool included = config.LevelingIncludedDuties.Contains(pair.Key);
                if (ImGui.Checkbox(DutyToolsUi.Text("Exclude"), ref excluded)) { if (excluded) config.LevelingExcludedDuties.Add(pair.Key); else config.LevelingExcludedDuties.Remove(pair.Key); Save(); }
                ImGui.SameLine();
                if (ImGui.Checkbox(DutyToolsUi.Text("Include"), ref included)) { if (included) config.LevelingIncludedDuties.Add(pair.Key); else config.LevelingIncludedDuties.Remove(pair.Key); Save(); }
                ImGui.SameLine();
                ImGui.TextDisabled(label + (config.LevelingAutoExcluded.Contains(pair.Key) ? " [AUTO]" : ""));
                if (config.LevelingAutoExcluded.Contains(pair.Key))
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton(DutyToolsUi.Text("Restore"))) { config.LevelingAutoExcluded.Remove(pair.Key); config.LevelingFailureCounts.Remove(pair.Key); Save(); }
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
    }

    internal static void Metadata(ContentPathsManager.DutyPath path)
    {
        if (path.PathFile == null || !ImGui.CollapsingHeader(DutyToolsUi.Text("Metadata"))) return;
        var meta = path.PathFile.Meta;
        string author = meta.Author ?? "", description = meta.Description ?? "", source = meta.SourceUrl ?? "";
        if (ImGui.InputText(DutyToolsUi.Text("Author"), ref author, 256)) meta.Author = author;
        if (ImGui.InputText(DutyToolsUi.Text("Description"), ref description, 1024)) meta.Description = description;
        if (ImGui.InputText(DutyToolsUi.Text("Source"), ref source, 1024)) meta.SourceUrl = source;
        bool limited = meta.Jobs.HasValue;
        if (ImGui.Checkbox(DutyToolsUi.Text("LimitJobs"), ref limited)) meta.Jobs = limited ? JobWithRole.All : null;
        if (limited)
        {
            var jobs = meta.Jobs ?? JobWithRole.All;
            JobWithRoleHelper.DrawCategory(JobWithRole.All, ref jobs);
            meta.Jobs = jobs;
            if (path.container.Paths.Any(p => p != path && p.PathFile?.Meta.Jobs is { } other && (other & jobs) != 0))
                ImGui.TextColored(new Vector4(1, .75f, .3f, 1), DutyToolsUi.Text("JobConflict"));
        }
        if (ImGui.Button(DutyToolsUi.Text("SaveMetadata")))
        {
            File.WriteAllText(path.FilePath, PathJson.WriteFile(path.PathFile));
            ConfigurationMain.Instance.DoNotUpdatePathFiles.Add(path.FileName);
            ConfigurationProfileV2.Save();
        }
    }

    private static void Save() { LevelingHelper.ResetLevelingDuties(); ConfigurationProfileV2.Save(); }
}
