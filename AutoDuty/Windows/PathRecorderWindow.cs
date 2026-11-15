using System;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;

namespace AutoDuty.Windows;

internal sealed class PathRecorderWindow : Window
{
    private string name = "Recorded route";
    private string message = "";
    internal PathRecorderWindow() : base("AutoDuty Recorder###AutoDutyRecorder") { }

    public override void Draw()
    {
        var recorder = Plugin.Recorder;
        ImGui.Text(DutyToolsUi.Text(recorder.Recording ? "Recording" : "RecorderStopped") + $" ({Plugin.Actions.Count})");
        if (ImGui.Button(DutyToolsUi.Text(recorder.Recording ? "StopRecording" : "StartRecording")))
            this.Try(() => { if (recorder.Recording) recorder.Stop(); else recorder.Start(); });
        ImGui.SameLine();
        if (ImGui.Button(DutyToolsUi.Text("Undo"))) recorder.Undo();
        foreach (string action in new[] { "Interactable", "Boss", "TreasureCoffer", "Wait", "MoveTo" })
        {
            if (ImGui.Button("+ " + action)) this.Try(() => recorder.Add(action));
            ImGui.SameLine();
        }
        ImGui.NewLine();
        ImGui.Checkbox(DutyToolsUi.Text("DetectBoss"), ref recorder.DetectBoss);
        ImGui.Checkbox(DutyToolsUi.Text("DetectTreasure"), ref recorder.DetectTreasure);
        ImGui.Checkbox(DutyToolsUi.Text("DetectInteractions"), ref recorder.DetectInteractions);
        ImGui.Checkbox(DutyToolsUi.Text("DetectDialogs"), ref recorder.DetectDialogs);
        ImGui.SliderFloat(DutyToolsUi.Text("SampleDistance"), ref recorder.MinSampleDistance, 1, 20);
        ImGui.SliderFloat(DutyToolsUi.Text("SimplifyTolerance"), ref recorder.SimplifyTolerance, 0, 5);
        ImGui.InputText(DutyToolsUi.Text("RouteName"), ref this.name, 120);
        if (ImGui.Button(DutyToolsUi.Text("SaveRecording"))) this.Try(() => this.message = recorder.Save(this.name));
        ImGui.SameLine();
        if (ImGui.Button(DutyToolsUi.Text("AppendRecording"))) this.Try(() => this.message = recorder.Save(this.name, true));
        ImGui.TextWrapped(DutyToolsUi.Text("ReviewRecording"));
        ImGui.TextWrapped(this.message);
        if (ImGui.BeginChild("RecordedSteps", new System.Numerics.Vector2(0, 0)))
            for (int i = 0; i < Plugin.Actions.Count; i++)
                if (ImGui.Selectable($"{i + 1}. {Plugin.Actions[i].ToCustomString()}")) BuildTab.EditStep(i);
        ImGui.EndChild();
    }

    private void Try(Action action)
    {
        try { action(); } catch (Exception ex) { this.message = ex.Message; Svc.Log.Warning(ex.ToString()); }
    }
}
