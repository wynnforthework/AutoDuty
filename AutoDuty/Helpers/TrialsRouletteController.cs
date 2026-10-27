namespace AutoDuty.Helpers;

using System;
using Configurations;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using static TrialsRoulettePolicy;

internal static unsafe class TrialsRouletteController
{
    // ContentRoulette row 6 is Duty Roulette: Trials (shared across client languages).
    private const byte RouletteId = 6;
    internal static readonly TrialsRoulettePolicy Policy = new();
    internal static bool Active => Policy.State != Phase.Stopped;
    internal static string Status { get; private set; } = "Stopped";
    private static DateTime nextAttempt;
    private static DateTime queueDeadline;
    private static bool queueRequested;
    private static bool queueSeen;
    private static bool wasInDuty;

    internal static void Start()
    {
        if (Active || InDungeon || !PlayerHelper.IsReadyFull || Conditions.Instance()->InDutyQueue || ActiveHelper.AnyHelperRunning()) return;
        // Validate against the client's sheet before sending a queue request.
        // Use English explicitly so this check also works on Chinese clients.
        var roulette = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.ContentRoulette>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(RouletteId);
        if (roulette?.Name.ExtractText() != "Duty Roulette: Trials")
        {
            Status = "Trials roulette data mismatch; queueing stopped";
            Svc.Chat.PrintError("[AutoDuty] " + Status);
            return;
        }
        Plugin.Stage = Stage.Stopped;
        Policy.Start(Configuration.TrialsRouletteRepeats);
        queueRequested = queueSeen = wasInDuty = false;
        nextAttempt = DateTime.MinValue;
        queueDeadline = DateTime.UtcNow.AddSeconds(60);
        Plugin.Stage = Stage.Idle;
        Plugin.SetGeneralSettings(false);
        Status = "Queueing trials roulette";
    }

    internal static void Stop(string reason = "Stopped manually")
    {
        if (!Active) return;
        Policy.Stop(); // Invalidate first: no later frame or completion callback can queue again.
        Status = reason;
        if ((queueRequested || queueSeen) && Conditions.Instance()->InDutyQueue &&
            ContentsFinder.Instance()->QueueInfo.QueuedContentRouletteId == RouletteId)
            ContentsFinder.Instance()->QueueInfo.CancelQueue();
    }

    private static void Finish(string reason)
    {
        Stop(reason);
        Plugin.Stage = Stage.Stopped;
    }

    internal static void Completed()
    {
        if (!Active) return;
        Policy.Complete(DateTime.UtcNow);
        Plugin.SetRotationPluginSettings(false);
    }

    internal static void Update()
    {
        if (!Active) return;
        if (!Svc.ClientState.IsLoggedIn) { Finish("Logged out"); return; }
        DateTime now = DateTime.UtcNow;
        if (!PlayerHelper.IsReadyFull) return; // Do not mistake loading screens for leaving a duty.
        if (InDungeon)
        {
            if (!wasInDuty)
            {
                if (Policy.State != Phase.Queueing || !queueRequested) { Finish("Unexpected duty entry"); return; }
                var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(Svc.ClientState.TerritoryType);
                if (territory.ContentFinderCondition.Value.ContentType.RowId != 4) { Finish("Entered a non-trial duty"); return; }
                wasInDuty = true;
                Policy.Enter();
            }
            if (Policy.State == Phase.InDuty)
                Plugin.SetRotationPluginSettings(true);
            Policy.Tick(now);
            if (Policy.State == Phase.Exiting)
            {
                if (now >= Policy.Deadline) { Finish("Timed out leaving duty"); return; }
                if (ExitDutyHelper.State != ActionState.Running) ExitDutyHelper.Invoke();
            }
        }
        else
        {
            if (wasInDuty)
            {
                wasInDuty = false;
                bool validExit = Policy.Leave(now);
                queueRequested = queueSeen = false;
                if (!validExit || !Active) { FinishStopped(validExit ? "Completed configured runs" : "Left duty before scheduled exit"); return; }
                Plugin.SetGeneralSettings(false);
            }
            var previous = Policy.State;
            Policy.Tick(now);
            if (previous == Phase.RequeueWait && Policy.State == Phase.Queueing)
                queueDeadline = now.AddSeconds(60);
            if (Policy.State == Phase.Queueing && now >= nextAttempt)
            {
                nextAttempt = now.AddSeconds(1);
                bool inQueue = Conditions.Instance()->InDutyQueue;
                if (inQueue)
                {
                    if (ContentsFinder.Instance()->QueueInfo.QueuedContentRouletteId != RouletteId) { Finish("Another queue is active"); return; }
                    queueSeen = true;
                }
                else if (queueSeen) { Finish("Queue was cancelled"); return; }
                if (GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out AtkUnitBase* confirm) && GenericHelpers.IsAddonReady(confirm))
                {
                    if (queueRequested) AddonHelper.FireCallBack(confirm, true, 8);
                    return;
                }
                if (!inQueue)
                {
                    if (now >= queueDeadline) { Finish("Queue registration timed out (check unlocks or penalties)"); return; }
                    // One request per session: never spam registration if the game rejects it.
                    if (!queueRequested)
                    {
                        queueRequested = true;
                        ContentsFinder.Instance()->QueueInfo.QueueRoulette(RouletteId);
                    }
                }
            }
        }
        Status = $"{Policy.State}: {Policy.Completed}/{(Policy.Limit == 0 ? "∞" : Policy.Limit.ToString())}";
        if (Policy.State is Phase.CompletionWait or Phase.RequeueWait)
            Status += $" ({Math.Max(0, (int)(Policy.Deadline - now).TotalSeconds)}s)";
        Plugin.action = Status;
    }

    private static void FinishStopped(string reason)
    {
        Status = reason;
        Plugin.Stage = Stage.Stopped;
    }
}
