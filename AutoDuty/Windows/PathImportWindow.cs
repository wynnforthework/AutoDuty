using System;
using System.IO;
using System.Linq;
using System.Numerics;
using AutoDuty.Configurations;
using AutoDuty.Data;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoDuty.Windows;

internal sealed class PathImportWindow : Window
{
    private readonly PathFile file;
    private readonly string source;
    private string name = "Imported route";
    private int territory;
    private bool protect = true;
    private string message = "";
    internal PathImportWindow(PathFile file, string source) : base("AutoDuty Route Import###AutoDutyRouteImport")
    {
        this.file = file; this.source = source;
        this.territory = (int)(file.Meta.Territory ?? Plugin.CurrentTerritoryContent?.TerritoryType ?? 0);
        this.SizeConstraints = new() { MinimumSize = new(450, 300), MaximumSize = new(float.MaxValue) };
    }

    public override void Draw()
    {
        ImGui.TextWrapped(DutyToolsUi.Text("ImportReview"));
        ImGui.TextWrapped($"{this.source}\n{this.file.Meta.Author}\n{this.file.Meta.SourceUrl}\n{this.file.Meta.Description}\n{this.file.Meta.Jobs}");
        ImGui.Text($"{DutyToolsUi.Text("Steps")}: {this.file.Actions.Count}");
        ImGui.InputText(DutyToolsUi.Text("RouteName"), ref this.name, 120);
        ImGui.InputInt(DutyToolsUi.Text("Territory"), ref this.territory);
        ImGui.Checkbox(DutyToolsUi.Text("ProtectImport"), ref this.protect);
        if (ImGui.Button(DutyToolsUi.Text("ConfirmImport")))
        {
            try
            {
                if (this.territory <= 0 || !ContentHelper.DictionaryContent.ContainsKey((uint)this.territory)) throw new ArgumentException("Select a supported territory ID");
                string path = Path.Combine(Plugin.pathsDirectory.FullName, $"({this.territory}) {PathRecorder.SafeName(this.name)}.json");
                if (File.Exists(path)) throw new IOException("File already exists; choose a new route name");
                this.file.Meta.Territory = (uint)this.territory;
                this.file.Meta.IsUserVersion = true;
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream)) writer.Write(PathJson.WriteFile(this.file));
                if (this.protect) ConfigurationMain.Instance.DoNotUpdatePathFiles.Add(Path.GetFileName(path));
                ConfigurationProfileV2.Save();
                this.message = DutyToolsUi.Text("ImportSaved");
                this.IsOpen = false;
            }
            catch (Exception ex) { this.message = ex.Message; }
        }
        ImGui.SameLine();
        if (ImGui.Button(DutyToolsUi.Text("Cancel"))) this.IsOpen = false;
        ImGui.TextWrapped(this.message);
        if (ImGui.BeginChild("ImportedSteps", new Vector2(0, 0)))
            for (int i = 0; i < this.file.Actions.Count; i++)
            {
                var action = this.file.Actions[i];
                bool sensitive = action.Name is "ChatCommand" or "Action" or "DutySpecificCode" or "BLULoad" or "ModifyIndex";
                if (sensitive) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, .65f, .25f, 1));
                ImGui.TextWrapped($"{i + 1}. {(sensitive ? "[!] " : "")}{action.ToCustomString()}");
                if (sensitive) ImGui.PopStyleColor();
                if (PathValidator.Error(action) is { } error) ImGui.TextColored(new Vector4(1, .35f, .3f, 1), error);
            }
        ImGui.EndChild();
    }
}
