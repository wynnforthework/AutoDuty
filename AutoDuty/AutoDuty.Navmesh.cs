using System;
using System.Collections.Generic;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    internal readonly HashSet<uint> rebuiltTerritoriesThisSession = [];
    private uint rebuildingTerritory;
    private long rebuildStartedAt;

    internal bool EnsureNavmeshForTerritory(uint territory)
    {
        if (!Configuration.DutyConfig.RebuildNavmeshOnFirstVisitThisSession || !VNavmesh_IPCSubscriber.IsEnabled) return true;
        if (this.rebuiltTerritoriesThisSession.Contains(territory) && this.rebuildingTerritory != territory) return true;
        long now = Environment.TickCount64;
        if (this.rebuildingTerritory != territory)
        {
            if (PartyHelper.PartyInCombat()) return false;
            this.rebuildingTerritory = territory;
            this.rebuildStartedAt = now;
            this.rebuiltTerritoriesThisSession.Add(territory);
            try
            {
                if (!VNavmesh_IPCSubscriber.GetNav_Rebuild())
                {
                    Svc.Log.Warning($"Navmesh rebuild rejected for {territory}; using existing mesh");
                    this.rebuildingTerritory = 0;
                    return true;
                }
                Svc.Log.Info($"Rebuilding navmesh on first visit this session: {territory}");
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"Navmesh rebuild failed for {territory}: {ex.Message}");
                this.rebuildingTerritory = 0;
                return true;
            }
        }
        if (now - this.rebuildStartedAt < 500) return false;
        if (VNavmesh_IPCSubscriber.Nav_IsReady)
        {
            this.rebuildingTerritory = 0;
            return true;
        }
        if (now - this.rebuildStartedAt < Math.Max(1000, Configuration.DutyConfig.NavmeshRebuildTimeoutMs)) return false;
        Svc.Log.Warning($"Navmesh rebuild timed out for {territory}; continuing with existing mesh");
        this.rebuildingTerritory = 0;
        return true;
    }
}
