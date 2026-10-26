namespace AutoDuty.Windows;

using Configurations;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Linq;

internal static class MathematicsOreSettings
{
    internal static void Draw()
    {
        if (!ImGui.CollapsingHeader("数理神典石购买矿石 / Mathematics ore exchange")) return;
        var config = Configuration.MathematicsOre;
        using var disabled = ImRaii.Disabled(Plugin.States != PluginState.None);
        bool enabled = config.Enabled;
        if (ImGui.Checkbox("出本后数理 > 1500 时购买 3 个矿石", ref enabled))
        { config.Enabled = enabled; ConfigurationProfileV2.Save(); }
        ImGui.TextWrapped("每次购买一种 ×3，成功后轮换到下一种。可选 1–4 种，按下列列表顺序购买。仅在挂机出本时检查；失败停止挂机。");
        config.Ores ??= [];
        for (int i = 0; i < MathematicsOreConfig.Allowed.Length; i++)
        {
            uint id = MathematicsOreConfig.Allowed[i];
            bool selected = config.Ores.Contains(id);
            using (ImRaii.Disabled(selected && config.Ores.Count == 1))
                if (ImGui.Checkbox(MathematicsOreConfig.Names[i], ref selected))
                {
                    if (selected) config.Ores.Add(id); else config.Ores.Remove(id);
                    config.NextIndex = 0;
                    ConfigurationProfileV2.Save();
                }
        }
        for (int i = 0; i < config.Ores.Count; i++)
        {
            int index = Array.IndexOf(MathematicsOreConfig.Allowed, config.Ores[i]);
            ImGui.Text($"{i + 1}. {(index >= 0 ? MathematicsOreConfig.Names[index] : config.Ores[i].ToString())}");
            ImGui.SameLine();
            using (ImRaii.Disabled(i == 0))
                if (ImGui.SmallButton($"上移##ore{i}"))
                { (config.Ores[i - 1], config.Ores[i]) = (config.Ores[i], config.Ores[i - 1]); config.NextIndex = 0; ConfigurationProfileV2.Save(); }
        }
        if (config.Valid) ImGui.Text($"下次：{MathematicsOreConfig.Names[Array.IndexOf(MathematicsOreConfig.Allowed, config.NextOre)]} ×3");
        else ImGui.Text("请选择 1–4 种不重复矿石。");
    }
}
