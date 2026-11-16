using System;
using System.Numerics;
using AutoDuty.Configurations;
using Dalamud.Bindings.ImGui;

namespace AutoDuty.Windows;

internal static class DutyToolsUi
{
    internal static bool ManualSelection;
    internal static bool ScrollToCurrentRequested = true;
    private static int selectedStep = -1;
    internal static void ResetSelection() { ManualSelection = false; selectedStep = -1; ScrollToCurrentRequested = true; }

    internal static void StepRow(PathAction action, int index, bool scrollToCurrent = false)
    {
        ImGui.PushID(index);
        bool failed = Plugin.currentFailure?.Index == index;
        bool current = index == Plugin.indexer && Plugin.Stage != Stage.Stopped;
        if (Plugin.attemptHistory.TryGetValue(index, out var attempt) && attempt.SkippedReason is { } skipped)
        {
            ImGui.TextColored(new Vector4(1, .65f, .25f, 1), $"[!] {skipped}");
            ImGui.SameLine();
        }
        if (failed || current) ImGui.PushStyleColor(ImGuiCol.Text, failed ? new Vector4(1, .35f, .3f, 1) : new Vector4(0, 1, 1, 1));
        bool selected = ImGui.Selectable($"{(current ? "> " : "")}{index + 1}. {action.ToCustomString()}###StepRow", current || selectedStep == index, ImGuiSelectableFlags.AllowDoubleClick);
        if (selected)
        {
            selectedStep = index;
            ManualSelection = true;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) Plugin.SeekToStep(index);
        }
        if (current && scrollToCurrent && !ManualSelection) ImGui.SetScrollHereY(.25f);
        if (failed || current) ImGui.PopStyleColor();
        if (ImGui.BeginPopupContextItem("StepMenu"))
        {
            if (ImGui.MenuItem(Text("RunHere"))) Plugin.SeekToStep(index);
            if (ImGui.MenuItem(Text("Previous"))) Plugin.SeekToStep(index - 1);
            if (ImGui.MenuItem(Text("Skip"))) Plugin.SeekToStep(index + 1);
            if (ImGui.MenuItem(Text("Edit"))) BuildTab.EditStep(index);
            if (ImGui.MenuItem(Loc.Get("BuildTab.Delete"))) BuildTab.DeleteStep(action);
            if (ImGui.MenuItem(Text("CopyStep"))) ImGui.SetClipboardText(Newtonsoft.Json.JsonConvert.SerializeObject(action, ConfigurationMain.JsonSerializerSettings));
            if (ImGui.MenuItem(Text("InsertAfter"))) Helpers.PathEditHelper.MutateActions(list => list.Insert(index + 1, new PathAction { Name = "MoveTo", Position = Player.Position }), "insert after");
            if (ImGui.MenuItem(Text("StrictConditions"), "", action.Flags.HasFlag(PathActionFlags.StrictConditions)))
                Helpers.PathEditHelper.MutateActions(list => list[index].Flags ^= PathActionFlags.StrictConditions, "strict conditions");
            ImGui.EndPopup();
        }
        ImGui.PopID();
    }

    internal static string Text(string key) => Loc.Get("DutyTools." + key);

    internal static void ExecutionStatus()
    {
        if (Plugin.Stage == Stage.Stopped || Plugin.indexer < 0 || Plugin.indexer >= Plugin.Actions.Count) return;
        var step = Plugin.Actions[Plugin.indexer];
        string phase = Plugin.Stage switch
        {
            Stage.Moving => Loc.Get("LevelingPlan.ProgressMoving"),
            Stage.Action => Loc.Get("LevelingPlan.ProgressAction"),
            Stage.Waiting_For_Combat => Loc.Get("LevelingPlan.ProgressCombat"),
            Stage.Paused => Loc.Get("LevelingPlan.ProgressPaused"),
            _ => Plugin.Stage.ToString(),
        };
        string distance = Player.Available && step.Position != Vector3.Zero
            ? $" / {Loc.Get("LevelingPlan.ProgressDistance")}: {Vector3.Distance(Player.Position, step.Position):F2}m" : "";
        string task = Plugin.taskManager.CurrentTask?.Name is { } name ? " / " + name : "";
        ImGui.TextWrapped($"{Loc.Get("LevelingPlan.ProgressState")}: {phase} / #{Plugin.indexer + 1}{distance}{task}");
    }

    internal static void FailureBanner(bool compact = false)
    {
        if (Plugin.currentFailure is not { } failure) return;
        ImGui.PushID(compact ? "OverlayFailure" : "MainFailure");
        ImGui.TextColored(new Vector4(1, .35f, .3f, 1), Text("Failed") + $" #{failure.Index + 1}: {failure.Reason} ({failure.Attempts})");
        if (failure.Detail.Length > 0 && !compact) ImGui.TextWrapped(failure.Detail);
        if (ImGui.Button(Text("Retry"))) Plugin.SeekToStep(failure.Index);
        ImGui.SameLine();
        if (ImGui.Button(Text("Skip"))) Plugin.SeekToStep(failure.Index + 1);
        ImGui.SameLine();
        if (ImGui.Button(Text("Previous"))) Plugin.SeekToStep(failure.Index - 1);
        ImGui.SameLine();
        if (ImGui.Button(Text("Edit"))) BuildTab.EditStep(failure.Index);
        ImGui.PopID();
    }

    internal static void ReliabilityConfig()
    {
        if (!ImGui.CollapsingHeader(Text("Reliability"))) return;
        var config = Configuration.DutyConfig;
        foreach (string name in new[] { "MaxStepRetries", "MoveToStepTimeoutMs", "ActionStepTimeoutMs", "BossSpawnTimeoutMs", "BossStepTimeoutMs", "WaitForStepTimeoutMs", "NavmeshRebuildTimeoutMs" })
        {
            var property = config.GetType().GetProperty(name)!;
            int value = (int)property.GetValue(config)!;
            if (ImGui.InputInt(Text(name), ref value))
            {
                property.SetValue(config, Math.Clamp(value, name == "WaitForStepTimeoutMs" ? 0 : name == "MaxStepRetries" ? 1 : 1000, name == "MaxStepRetries" ? 100 : int.MaxValue));
                ConfigurationProfileV2.Save();
            }
        }
        foreach (string name in new[] { "PauseOnStepFailure", "PlaySoundOnFailure", "RebuildNavmeshOnFirstVisitThisSession" })
        {
            var property = config.GetType().GetProperty(name)!;
            bool value = (bool)property.GetValue(config)!;
            if (ImGui.Checkbox(Text(name), ref value))
            {
                property.SetValue(config, value);
                ConfigurationProfileV2.Save();
            }
        }
    }
}
