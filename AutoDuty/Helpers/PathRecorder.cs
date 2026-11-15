using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using AutoDuty.Configurations;
using AutoDuty.Data;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers;

internal sealed class PathRecorder
{
    internal bool Recording { get; private set; }
    internal uint Territory { get; private set; }
    internal float MinSampleDistance = 8;
    internal float SimplifyTolerance = 2.5f;
    internal bool DetectBoss = true, DetectTreasure = true, DetectInteractions = true, DetectDialogs = true;
    private readonly HashSet<ulong> seenObjects = [];
    private readonly HashSet<string> visibleDialogs = [];
    private long lastTick;
    private string author = "", job = "";

    internal void Start()
    {
        if (!PlayerHelper.IsReady || !InDungeon) throw new InvalidOperationException("Enter a duty before recording");
        Plugin.Stage = Stage.Stopped;
        this.Territory = Svc.ClientState.TerritoryType;
        this.author = Player.Name;
        this.job = Player.Job.ToString();
        this.seenObjects.Clear(); this.visibleDialogs.Clear();
        PathEditHelper.MutateActions(list => list.Clear(), "start recording");
        this.Recording = true;
        this.Add("MoveTo");
    }

    internal void Stop()
    {
        if (!this.Recording) return;
        if (Player.Available && Svc.ClientState.TerritoryType == this.Territory && !PartyHelper.PartyInCombat()) this.Add("MoveTo");
        this.Recording = false;
        var simplified = Simplify(Plugin.Actions, this.SimplifyTolerance);
        PathEditHelper.MutateActions(list => { list.Clear(); list.AddRange(simplified); }, "simplify recording");
    }

    internal unsafe void Tick()
    {
        if (!this.Recording || Environment.TickCount64 - this.lastTick < 200) return;
        this.lastTick = Environment.TickCount64;
        if (Svc.ClientState.TerritoryType != this.Territory) { this.Stop(); Svc.Chat.Print("[AutoDuty] Recording stopped: territory changed."); return; }
        if (!Player.Available) return;
        if (Plugin.Actions.Count >= 10000) { this.Stop(); return; }
        if (PlayerHelper.IsReady && !PartyHelper.PartyInCombat())
        {
            var last = Plugin.Actions.LastOrDefault();
            if (last == null || Vector3.Distance(last.Position, Player.Position) >= this.MinSampleDistance || Math.Abs(last.Position.Y - Player.Position.Y) >= 3)
                this.Add("MoveTo");
        }
        foreach (var obj in Svc.Objects)
        {
            if (!obj.IsTargetable || this.seenObjects.Contains(obj.GameObjectId)) continue;
            if (this.DetectBoss && ObjectHelper.IsBoss(obj) && Vector3.Distance(obj.Position, Player.Position) < 50)
            {
                this.seenObjects.Add(obj.GameObjectId);
                this.Append(new() { Name = "Boss", Position = obj.Position, Note = obj.Name.TextValue });
            }
            else if (this.DetectTreasure && obj.ObjectKind == ObjectKind.Treasure && Vector3.Distance(obj.Position, Player.Position) < 30)
            {
                this.seenObjects.Add(obj.GameObjectId);
                this.Append(new() { Name = "TreasureCoffer", Tag = ActionTag.Treasure, Position = obj.Position, Note = obj.Name.TextValue });
            }
        }
        if (this.DetectInteractions && Svc.Targets.Target is { ObjectKind: ObjectKind.EventObj } target && this.seenObjects.Add(target.GameObjectId))
            this.Append(new() { Name = "Interactable", Position = target.Position, Arguments = [target.BaseId.ToString()], Note = target.Name.TextValue });
        if (!this.DetectDialogs) return;
        foreach (string addon in new[] { "SelectYesno", "Talk", "SelectString" })
        {
            bool visible = GenericHelpers.TryGetAddonByName<AtkUnitBase>(addon, out var ptr) && GenericHelpers.IsAddonReady(ptr) && ptr->IsVisible;
            if (!visible) { this.visibleDialogs.Remove(addon); continue; }
            if (!this.visibleDialogs.Add(addon)) continue;
            this.Append(new() { Name = addon == "Talk" ? "ClickTalk" : addon, Arguments = addon == "SelectYesno" ? ["Yes"] : addon == "SelectString" ? [""] : ["false"], Note = "Recorded dialog: review choice before running" });
        }
    }

    internal void Add(string name)
    {
        if (!Player.Available) return;
        var target = Svc.Targets.Target;
        var action = new PathAction { Name = name, Position = Player.Position };
        if (name is "Interactable" or "Target" or "MoveToObject")
        {
            if (target == null) throw new InvalidOperationException("Select a target first");
            action.Position = target.Position; action.Arguments = [target.BaseId.ToString()]; action.Note = target.Name.TextValue;
        }
        else if (name == "Boss")
        {
            action.Position = target?.Position ?? Player.Position; action.Note = target?.Name.TextValue ?? "";
        }
        else if (name == "TreasureCoffer") action.Tag = ActionTag.Treasure;
        else if (name == "Wait") action.Arguments = ["1000"];
        else if (name == "SelectYesno") action.Arguments = ["Yes"];
        this.Append(action);
    }

    private void Append(PathAction action)
    {
        if (Plugin.Actions.LastOrDefault() is { } last && last.Name == action.Name && Vector3.Distance(last.Position, action.Position) < 2) return;
        PathEditHelper.MutateActions(list => list.Add(action), "record " + action.Name);
    }

    internal void Undo()
    {
        if (Plugin.Actions.Count > 0) PathEditHelper.MutateActions(list => list.RemoveAt(list.Count - 1), "record undo");
    }

    internal string Save(string name, bool append = false)
    {
        this.Stop();
        if (this.Territory == 0 || Plugin.Actions.Count == 0) throw new InvalidOperationException("No recording to save");
        string fileName = SafeName(name);
        string path = Path.Combine(Plugin.pathsDirectory.FullName, $"({this.Territory}) {fileName}.json");
        PathFile file;
        if (append)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Append target does not exist");
            file = PathJson.ReadFile(File.ReadAllText(path));
            file.Actions.AddRange(Plugin.Actions);
        }
        else
        {
            if (File.Exists(path)) throw new InvalidOperationException("File exists; choose another name or use append");
            file = new() { Actions = [.. Plugin.Actions], Meta = new() { CreatedAt = Plugin.Version, Territory = this.Territory, Author = this.author, Notes = [$"Recorded job: {this.job}"] } };
        }
        PathValidator.ValidateStructure(file);
        File.WriteAllText(path, PathJson.WriteFile(file));
        ConfigurationMain.Instance.DoNotUpdatePathFiles.Add(Path.GetFileName(path));
        ConfigurationProfileV2.Save();
        Plugin.pathFile = path;
        return path;
    }

    internal static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' '))
            throw new ArgumentException("Invalid route name");
        return name;
    }

    internal static List<PathAction> Simplify(IReadOnlyList<PathAction> actions, float tolerance)
    {
        var result = new List<PathAction>();
        for (int start = 0; start < actions.Count;)
        {
            if (actions[start].Name != "MoveTo") { result.Add(actions[start++]); continue; }
            int end = start;
            while (end + 1 < actions.Count && actions[end + 1].Name == "MoveTo") end++;
            var keep = new HashSet<int> { start, end };
            var work = new Stack<(int A, int B)>(); work.Push((start, end));
            while (work.TryPop(out var segment))
            {
                Vector3 a = actions[segment.A].Position, b = actions[segment.B].Position;
                float max = tolerance * tolerance; int farthest = -1;
                for (int i = segment.A + 1; i < segment.B; i++)
                {
                    Vector3 ab = b - a;
                    float t = ab.LengthSquared() > .0001f ? Math.Clamp(Vector3.Dot(actions[i].Position - a, ab) / ab.LengthSquared(), 0, 1) : 0;
                    float distance = Vector3.DistanceSquared(actions[i].Position, a + t * ab);
                    if (distance > max) { max = distance; farthest = i; }
                }
                if (farthest >= 0) { keep.Add(farthest); work.Push((segment.A, farthest)); work.Push((farthest, segment.B)); }
            }
            for (int i = start; i <= end; i++) if (keep.Contains(i)) result.Add(actions[i]);
            start = end + 1;
        }
        return result;
    }
}
