using System;
using System.Collections.Generic;
using AutoDuty.Multibox;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers;

internal static class PathEditHelper
{
    internal static int FindReference<T>(IReadOnlyList<T> actions, T? target) where T : class
    {
        if (target != null)
            for (int i = 0; i < actions.Count; i++)
                if (ReferenceEquals(actions[i], target)) return i;
        return -1;
    }

    // Reference identity preserves the running action across insert/delete/reorder operations.
    internal static int Relocate<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, int index) where T : class
    {
        if (index < 0) return index;
        if (index == before.Count) return after.Count;
        if (index < before.Count)
            for (int i = 0; i < after.Count; i++)
                if (ReferenceEquals(before[index], after[i])) return i;
        return Math.Clamp(index, 0, after.Count);
    }

    internal static void MutateActions(Action<List<PathAction>> mutate, string reason)
    {
        if (!Plugin.Recorder.Recording && (PathFileStore.ShouldPersistEdit(reason, false) || reason == "clear")
            && PathFileStore.IsReadOnly(Plugin.pathFile))
        {
            Svc.Chat.PrintError("[AutoDuty] " + Windows.DutyToolsUi.Text("OriginalReadOnly"));
            return;
        }
        Plugin.combatCheckpoints.Reset();
        var before = Plugin.Actions.ToArray();
        int oldIndex = Plugin.indexer;
        var running = oldIndex >= 0 && oldIndex < before.Length ? before[oldIndex] : null;
        bool active = Plugin.States.HasFlag(PluginState.Navigating);
        bool paused = Plugin.Stage == Stage.Paused;
        mutate(Plugin.Actions);
        if (Plugin.MetricsRun != null && (reason != "host path" ||
            Data.PathJson.WriteFile(new() { Actions = [.. before] }, Newtonsoft.Json.Formatting.None) !=
            Data.PathJson.WriteFile(new() { Actions = [.. Plugin.Actions] }, Newtonsoft.Json.Formatting.None)))
            Plugin.MetricsProblem("path-edited", true);
        int relocated = Relocate(before, Plugin.Actions, oldIndex);
        bool same = running != null && relocated >= 0 && relocated < Plugin.Actions.Count && ReferenceEquals(running, Plugin.Actions[relocated]);
        Plugin.indexer = relocated;
        Plugin.attemptHistory.Clear();
        if (same)
        {
            if (Plugin.currentAttempt != null)
            {
                Plugin.currentAttempt.Index = relocated;
                Plugin.attemptHistory[relocated] = Plugin.currentAttempt;
            }
            if (Plugin.currentFailure != null) Plugin.currentFailure = Plugin.currentFailure with { Index = relocated };
        }
        else if (active && relocated >= 0)
            Plugin.SeekToStep(relocated, !paused);
        else
            Plugin.BeginAttempt(relocated);
        if (MultiboxUtility.Config.MultiBox && MultiboxUtility.Config.Host)
        {
            MultiboxUtility.Server.SendPath();
            if (active && !paused) MultiboxUtility.Server.SendStepStart(true);
        }
        if (PathFileStore.ShouldPersistEdit(reason, Plugin.Recorder.Recording))
            Windows.BuildTab.SavePath(running: true);
        Svc.Log.Info($"Path edit: {reason}; step {oldIndex + 1} -> {relocated + 1}");
    }
}
