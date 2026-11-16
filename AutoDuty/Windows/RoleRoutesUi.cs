using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoDuty.Configurations;
using AutoDuty.Data;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;

namespace AutoDuty.Windows;

internal static class RoleRoutesUi
{
    private static string Text(string key) => Loc.Get("LevelingPlan." + key);
    private static readonly (string Label, JobWithRole Mask)[] Roles =
        [("Tank", JobWithRole.Tanks), ("Healer", JobWithRole.Healers), ("Dps", JobWithRole.DPS)];

    internal static void Draw(ContentPathsManager.ContentPathContainer container)
    {
        ImGui.TextWrapped(container.Content.Name);
        ImGui.TextWrapped(Text("RoleHelp"));
        foreach (var (label, mask) in Roles)
        {
            ImGui.PushID(label);
            ImGui.Separator();
            ImGui.TextUnformatted(Text(label));
            var resolved = mask.ContainedJobs().Select(job => container.SelectPath(out _, job)?.FileName)
                .Where(p => p != null).Distinct().ToArray();
            ImGui.TextWrapped(Text("Effective") + ": " + string.Join(" / ", resolved));
            string? assigned = null;
            if (Configuration.Meta.PathSelectionsByPath.TryGetValue(container.ID, out var bindings) && bindings != null)
                assigned = bindings.FirstOrDefault(pair => (pair.Value & mask) == mask).Key;
            ImGui.SetNextItemWidth(440);
            if (ImGui.BeginCombo("##RoleRoute", assigned ?? Text("Automatic")))
            {
                if (ImGui.Selectable(Text("Automatic"), assigned == null)) Assign(container.ID, mask, null);
                foreach (var path in container.Paths.ToArray())
                    if (ImGui.Selectable(path.FileName, assigned == path.FileName)) Assign(container.ID, mask, path.FileName);
                ImGui.EndCombo();
            }
            if (ImGui.SmallButton(Text("RemoveBinding"))) Assign(container.ID, mask, null);
            using (ImRaii.Disabled(Plugin.Stage != Stage.Stopped || Plugin.Recorder.Recording))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(Text("CreateRoute"))) Create(container.ID, mask, null);
                var selected = container.Paths.FirstOrDefault(p => p.FileName == assigned)
                    ?? (resolved.Length == 1 ? container.Paths.FirstOrDefault(p => p.FileName == resolved[0]) : null);
                using (ImRaii.Disabled(selected == null))
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton(Text("CloneRoute"))) Create(container.ID, mask, selected);
                    ImGui.SameLine();
                    if (ImGui.SmallButton(Text("EditRoute"))) BuildTab.LoadForEditing(selected!);
                }
            }
            ImGui.PopID();
        }
    }

    private static void Assign(uint id, JobWithRole mask, string? file)
    {
        if (!Configuration.Meta.PathSelectionsByPath.TryGetValue(id, out var bindings) || bindings == null)
            Configuration.Meta.PathSelectionsByPath[id] = bindings = [];
        PathSelectionHelper.AssignRole(bindings, mask, file);
        ConfigurationProfileV2.Save();
        LevelingHelper.ResetLevelingDuties();
    }

    internal static void Create(uint id, JobWithRole mask, ContentPathsManager.DutyPath? source)
    {
        if (Plugin.Stage != Stage.Stopped || Plugin.Recorder.Recording) return;
        try
        {
            string name = $"({id}) {mask} {DateTime.Now:yyyyMMdd-HHmmss-fff}.json";
            string destination = Path.Combine(Plugin.pathsDirectory.FullName, name);
            var file = source == null ? new PathFile { Actions = [], Meta = new() { Territory = id } }
                : PathJson.ReadFile(File.ReadAllText(source.FilePath));
            file.Meta.Jobs = mask;
            File.WriteAllText(destination, PathJson.WriteFile(file));
            ConfigurationMain.Instance.DoNotUpdatePathFiles.Add(name);
            Assign(id, mask, name);
            if (!ContentHelper.DictionaryContent.TryGetValue(id, out var content)) return;
            BuildTab.LoadForEditing(new ContentPathsManager.DutyPath(destination, new(content)));
        }
        catch (Exception ex) { Svc.Log.Error(ex.ToString()); Svc.Chat.PrintError("[AutoDuty] " + ex.Message); }
    }
}
