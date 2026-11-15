using System;
using System.Numerics;
using AutoDuty.Configurations;
using Dalamud.Bindings.ImGui;

namespace AutoDuty.Windows;

internal static class DutyToolsUi
{
    internal static string Text(string key) => Loc.Get("DutyTools." + key);

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
        ImGui.PopID();
    }

    internal static void ReliabilityConfig()
    {
        if (!ImGui.CollapsingHeader(Text("Reliability"))) return;
        var config = Configuration.DutyConfig;
        foreach (string name in new[] { "MaxStepRetries", "MoveToStepTimeoutMs", "ActionStepTimeoutMs", "BossSpawnTimeoutMs", "BossStepTimeoutMs", "WaitForStepTimeoutMs" })
        {
            var property = config.GetType().GetProperty(name)!;
            int value = (int)property.GetValue(config)!;
            if (ImGui.InputInt(Text(name), ref value))
            {
                property.SetValue(config, Math.Clamp(value, name == "WaitForStepTimeoutMs" ? 0 : name == "MaxStepRetries" ? 1 : 1000, name == "MaxStepRetries" ? 100 : int.MaxValue));
                ConfigurationProfileV2.Save();
            }
        }
        foreach (string name in new[] { "PauseOnStepFailure", "PlaySoundOnFailure" })
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
