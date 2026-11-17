using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using AutoDuty.Configurations;
using AutoDuty.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace AutoDuty.Windows;

// An independent WindowSystem window: closing the main UI must not hide execution.
internal sealed class StepDeckWindow : Window
{
    private static readonly Vector4 Accent = new(.32f, .83f, .82f, 1);
    private static readonly Vector4 Muted = new(.65f, .70f, .76f, 1);
    private static readonly Vector4 Danger = new(1, .43f, .38f, 1);
    private bool wasRunning;
    private bool follow = true;
    private bool centerRequested = true;
    private int lastIndex = -1, lastCount = -1, browseIndex;
    private string lastRoute = "";
    private float lastScroll, lastRailWidth;
    private Action? pendingAction;

    internal static string Text(string key) => Loc.Get("StepDeck." + key);

    internal StepDeckWindow() : base("AutoDuty###AutoDutyStepDeck", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(780, 390);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(460, 310), MaximumSize = new Vector2(float.MaxValue) };
    }

    internal void ObserveExecution()
    {
        bool running = Plugin.Stage != Stage.Stopped && Plugin.Actions.Count > 0;
        if (running && !this.wasRunning && AutoDuty.Configuration.Overlay.AutoOpenStepDeck)
        {
            this.IsOpen = true;
            this.follow = true;
            this.centerRequested = true;
        }
        this.wasRunning = running;
    }

    public override void OnOpen() => this.centerRequested = true;

    public override void Draw()
    {
        this.WindowName = Text("Title") + "###AutoDutyStepDeck";
        this.pendingAction = null;
        int count = Plugin.Actions.Count;
        int current = Plugin.indexer;
        bool active = Plugin.Stage != Stage.Stopped && current >= 0 && current < count;
        if (this.lastRoute != Plugin.pathFile || this.lastCount != count)
        {
            this.lastRoute = Plugin.pathFile;
            this.lastCount = count;
            this.centerRequested = true;
        }
        if (this.lastIndex != current)
        {
            this.lastIndex = current;
            if (this.follow) this.centerRequested = true;
        }
        this.browseIndex = Math.Clamp(this.browseIndex, 0, Math.Max(0, count - 1));
        if (this.follow) this.browseIndex = Math.Clamp(current, 0, Math.Max(0, count - 1));

        ImGui.TextColored(Accent, Plugin.CurrentTerritoryContent?.Name ?? Text("Title"));
        if (!string.IsNullOrEmpty(Plugin.pathFile)) ImGui.TextWrapped(Path.GetFileName(Plugin.pathFile));
        if (count == 0)
        {
            ImGui.TextWrapped(Text("Empty"));
            this.DrawSettings();
            return;
        }

        int position = active ? current + 1 : Math.Clamp(current, 0, count);
        ImGui.TextUnformatted($"{Text("Position")}  {position} / {count}   ·   {this.Phase()}");
        ImGui.SameLine();
        if (ImGui.SmallButton(Text("Main"))) Plugin.MainWindow.IsOpen = true;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Accent);
        ImGui.ProgressBar((float)position / count, new Vector2(-1, ImGui.GetTextLineHeight()), $"{100f * position / count:F1}%");
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Text("PositionHint"));

        if (ImGui.SmallButton("<##PreviousCard")) this.Browse(-1, count);
        ImGui.SameLine();
        if (ImGui.SmallButton(">##NextCard")) this.Browse(1, count);
        ImGui.SameLine();
        if (ImGui.Checkbox(Text("Follow"), ref this.follow) && this.follow) this.centerRequested = true;
        ImGui.SameLine();
        if (ImGui.SmallButton(Text("Center"))) { this.follow = true; this.centerRequested = true; }
        ImGui.SameLine();
        if (ImGui.SmallButton(Text("Options"))) ImGui.OpenPopup("DeckOptions");
        if (ImGui.BeginPopup("DeckOptions")) { this.DrawSettings(); ImGui.EndPopup(); }

        // A drag strip provides horizontal panning without competing with card buttons or detail scrolling.
        float scale = ImGui.GetTextLineHeight() / 17f;
        Vector2 strip = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("PanDeck", new Vector2(-1, ImGui.GetTextLineHeight()));
        bool panning = ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left);
        ImGui.GetWindowDrawList().AddText(strip, ImGui.GetColorU32(Muted), Text("BrowseHint"));
        if (panning) { this.follow = false; this.centerRequested = false; }

        float width = 304 * scale;
        float gap = 12 * scale;
        float railWidth = ImGui.GetContentRegionAvail().X;
        if (this.follow && MathF.Abs(railWidth - this.lastRailWidth) > .5f) this.centerRequested = true;
        this.lastRailWidth = railWidth;
        float padding = Math.Max(12 * scale, (railWidth - width) / 2 - ImGui.GetStyle().WindowPadding.X);
        ImGui.SetNextWindowContentSize(new Vector2(2 * padding + count * width + (count - 1) * gap + ImGui.GetStyle().WindowPadding.X, 0));
        if (ImGui.BeginChild("CardRail", new Vector2(0, 0), false, ImGuiWindowFlags.HorizontalScrollbar))
        {
            float height = Math.Max(140 * scale, ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ScrollbarSize - 4 * scale);
            var io = ImGui.GetIO();
            if (panning) ImGui.SetScrollX(ImGui.GetScrollX() - io.MouseDelta.X);
            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && (io.MouseWheelH != 0 || (io.KeyShift && io.MouseWheel != 0)))
            {
                this.follow = false;
                this.centerRequested = false;
            }
            // Dragging the native scrollbar also suspends following.
            if (!this.centerRequested && ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && MathF.Abs(ImGui.GetScrollX() - this.lastScroll) > .1f) this.follow = false;
            this.lastScroll = ImGui.GetScrollX();
            bool centered = this.centerRequested;
            if (centered)
            {
                int target = this.follow ? Math.Clamp(current, 0, count - 1) : this.browseIndex;
                ImGui.SetScrollX(Math.Max(0, padding + target * (width + gap) + width / 2 - ImGui.GetWindowWidth() / 2));
                this.centerRequested = false;
            }
            ImGui.Dummy(new Vector2(padding, height));
            for (int i = 0; i < count; i++)
            {
                ImGui.SameLine(0, i == 0 ? 0 : gap);
                this.DrawCard(Plugin.Actions[i], i, active && current == i, width, height, scale);
            }
            ImGui.SameLine(0, 0);
            ImGui.Dummy(new Vector2(padding + ImGui.GetStyle().WindowPadding.X, height));
            if (panning || (!this.follow && !centered))
                this.browseIndex = Math.Clamp((int)MathF.Round((ImGui.GetScrollX() + ImGui.GetWindowWidth() / 2 - padding - width / 2) / (width + gap)), 0, count - 1);
        }
        ImGui.EndChild();
        // Apply commands after rendering so a seek cannot change card state halfway through the frame.
        this.pendingAction?.Invoke();
    }

    private void Browse(int delta, int count)
    {
        this.follow = false;
        this.browseIndex = Math.Clamp(this.browseIndex + delta, 0, count - 1);
        this.centerRequested = true;
    }

    private void DrawSettings()
    {
        bool autoOpen = AutoDuty.Configuration.Overlay.AutoOpenStepDeck;
        if (ImGui.Checkbox(Text("AutoOpen"), ref autoOpen))
        {
            AutoDuty.Configuration.Overlay.AutoOpenStepDeck = autoOpen;
            ConfigurationProfileV2.Save();
        }
    }

    private string Phase() => Plugin.Stage switch
    {
        Stage.Stopped => Text("Stopped"),
        Stage.Paused => Text("Paused"),
        Stage.Moving => Loc.Get("LevelingPlan.ProgressMoving"),
        Stage.Waiting_For_Combat => Loc.Get("LevelingPlan.ProgressCombat"),
        Stage.Action => Loc.Get("LevelingPlan.ProgressAction"),
        _ => Plugin.Stage.ToString(),
    };

    private void DrawCard(PathAction step, int index, bool current, float width, float height, float scale)
    {
        bool failed = Plugin.currentFailure?.Index == index;
        (string kind, Vector4 color) = Kind(step);
        Vector4 border = failed ? Danger : current ? Accent : new Vector4(.24f, .29f, .35f, 1);
        Vector2 start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(width, height), ImGui.GetColorU32(current ? new Vector4(.09f, .17f, .20f, .97f) : new Vector4(.10f, .12f, .15f, .94f)), 10 * scale);
        draw.AddRect(start, start + new Vector2(width, height), ImGui.GetColorU32(border), 10 * scale);
        draw.AddRectFilled(start + new Vector2(12, 12) * scale, start + new Vector2(16, 34) * scale, ImGui.GetColorU32(color), 2 * scale);
        ImGui.PushID(index);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16, 12) * scale);
        if (ImGui.BeginChild("Card", new Vector2(width, height), false, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            ImGui.TextColored(color, $"    {index + 1:00}  /  {Text(kind)}");
            string state = failed ? Text("Failed") : current ? this.Phase() : Plugin.attemptHistory.TryGetValue(index, out var history) && history.SkippedReason != null ? Text("Skipped") : Text("Step");
            ImGui.TextColored(failed ? Danger : current ? Accent : Muted, state);
            // Fixed controls stay visible even for very long arguments, notes and condition lists.
            using (ImRaii.Disabled(!Player.Available))
            {
                if (current)
                {
                    if (ImGui.Button(Text(Plugin.Stage == Stage.Paused ? "Resume" : "Pause")))
                        this.pendingAction = () => { if (Plugin.Stage == Stage.Paused) Plugin.ResumeFromPause(); else Plugin.Stage = Stage.Paused; };
                    ImGui.SameLine();
                    if (ImGui.Button(DutyToolsUi.Text("Skip"))) this.pendingAction = () => Plugin.SeekToStep(index + 1);
                }
                else
                {
                    if (ImGui.Button(DutyToolsUi.Text("RunHere"))) this.pendingAction = () => Plugin.SeekToStep(index);
                    ImGui.SameLine();
                    if (ImGui.Button(Loc.Get("BuildTab.TeleportTo"))) this.pendingAction = () => Plugin.TeleportToStep(step.Position);
                }
            }
            ImGui.Separator();
            if (ImGui.BeginChild("Details", new Vector2(0, 0)))
            {
                ImGui.TextWrapped(step.Name);
                Field("Tag", step.Tag.ToString());
                for (int i = 0; i < step.Arguments.Count; i++) Field("Argument", $"[{i + 1}] {step.Arguments[i]}");
                if (!string.IsNullOrWhiteSpace(step.Note)) Field("Note", step.Note);
                Field("Coordinates", $"X {step.Position.X:F3}   Y {step.Position.Y:F3}   Z {step.Position.Z:F3}");
                if (current && Player.Available) Field("Distance", $"{Vector3.Distance(Player.Position, step.Position):F2} m");
                if (step.Flags != PathActionFlags.None) Field("Flags", step.Flags.ToString());
                Conditions("Before", step.Conditions);
                Conditions("While", step.ConditionsWhile);
                Conditions("Until", step.ConditionsUntil);
                if (current && Plugin.currentAttempt is { } attempt)
                {
                    Field("Attempt", (failed ? Math.Max(1, attempt.Attempts) : attempt.Attempts + 1).ToString());
                    if (attempt.LastFailReason is { } reason) Field("LastFailure", reason);
                }
                if (failed && Plugin.currentFailure is { } failure) Field("LastFailure", failure.Reason + "\n" + failure.Detail);
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.PopID();
    }

    private static void Field(string label, string value)
    {
        ImGui.TextColored(Muted, Text(label));
        ImGui.SameLine();
        ImGui.TextWrapped(value);
    }

    private static void Conditions(string label, List<PathActionCondition> conditions)
    {
        if (conditions.Count == 0) return;
        Field(label, string.Join("\n", conditions.Select(c => string.Concat(c.DrawStepEntry().Select(part => part.text)))));
    }

    private static (string, Vector4) Kind(PathAction step)
    {
        if (step.Tag.HasFlag(ActionTag.Comment)) return ("Comment", Muted);
        return step.Name switch
        {
            "Boss" => ("Boss", new Vector4(.94f, .49f, .45f, 1)),
            "StopForCombat" => ("Combat", new Vector4(.93f, .68f, .37f, 1)),
            "MoveTo" or "MoveToObject" => ("Movement", new Vector4(.40f, .69f, .94f, 1)),
            "TeleportTo" => ("Teleport", new Vector4(.72f, .59f, .94f, 1)),
            "Interactable" or "TreasureCoffer" or "DutyAction" => ("Interaction", new Vector4(.42f, .78f, .62f, 1)),
            "Wait" or "WaitFor" => ("Wait", new Vector4(.79f, .71f, .48f, 1)),
            _ => ("Action", Accent),
        };
    }
}
