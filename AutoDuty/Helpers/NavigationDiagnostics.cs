using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoDuty.IPC;
using ECommons.DalamudServices;
using Newtonsoft.Json;

namespace AutoDuty.Helpers;

// Bounded in-memory trace. Disk IO happens only on failure or explicit export.
internal static class NavigationDiagnostics
{
    private static readonly Queue<string> entries = new();
    private static readonly object gate = new();
    private static long nextSample;
    private static long sequence;
    internal static string? LastExport { get; private set; }

    internal static unsafe void Record(string operation, string source = "", object? detail = null)
    {
        try
        {
            if (Plugin.Stage == Stage.Stopped) return;
            string json = JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow, sequence = System.Threading.Interlocked.Increment(ref sequence), operation, source, detail,
                territory = Svc.ClientState.TerritoryType, step = Plugin.indexer + 1, stage = Plugin.Stage.ToString(),
                route = Path.GetFileName(Plugin.pathFile),
                action = Plugin.currentAttempt?.Action.ToCustomString(), attempt = (Plugin.currentAttempt?.Attempts ?? 0) + 1,
                position = Player.Available ? Player.Position : default,
                rotation = Player.Available ? Player.Character->Rotation : 0,
                combat = PartyHelper.PartyInCombat(),
                finding = VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress,
                running = VNavmesh_IPCSubscriber.Path_IsRunning, waypoints = VNavmesh_IPCSubscriber.Path_NumWaypoints,
                tolerance = VNavmesh_IPCSubscriber.Path_GetTolerance,
                meshReady = VNavmesh_IPCSubscriber.Nav_IsReady,
                casting = Player.Available && PlayerHelper.IsCasting,
                betweenAreas = Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas],
                betweenAreas51 = Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51],
                task = Plugin.taskManager.CurrentTask?.Name
            });
            lock (gate)
            {
                entries.Enqueue(json);
                while (entries.Count > 1200) entries.Dequeue();
            }
        }
        catch (Exception ex) { Svc.Log.Debug($"Navigation trace unavailable: {ex.Message}"); }
    }

    internal static void Tick()
    {
        if (Environment.TickCount64 < nextSample) return;
        nextSample = Environment.TickCount64 + 500;
        Record("sample");
    }

    internal static void Export(string reason)
    {
        try
        {
            Record("export", detail: new { reason, routeHash = AutomationReports.Fingerprint(Plugin.Actions),
                mode = AutoDuty.Configuration.Meta.DutyModeEnum.ToString(),
                managesBossMovement = AutoDuty.Configuration.DutyConfig.AutoManageBossModAISettings,
                version = typeof(AutoDuty).Assembly.GetName().Version?.ToString() });
            string directory = Path.Combine(AutoDuty.PluginInterface.ConfigDirectory.FullName, "navigation-diagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"navigation-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl");
            string[] snapshot;
            lock (gate) snapshot = entries.ToArray();
            File.WriteAllLines(path, snapshot);
            LastExport = path;
            foreach (var old in new DirectoryInfo(directory).GetFiles("navigation-*.jsonl").OrderByDescending(f => f.Name).Skip(20)) old.Delete();
            Svc.Log.Info($"Navigation diagnostic saved: {path}");
        }
        catch (Exception ex) { Svc.Log.Warning($"Navigation diagnostic export failed: {ex.Message}"); }
    }
}
