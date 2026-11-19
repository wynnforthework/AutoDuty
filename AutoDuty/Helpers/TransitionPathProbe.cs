using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoDuty.Helpers;

// Query only: never starts movement. One outstanding IPC task, including across resets.
internal sealed class TransitionPathProbe
{
    private Task<List<Vector3>>? pending;
    private int generation, queryGeneration;
    private long nextQuery, startedAt;
    private Vector3 queryFrom, queryTarget;
    private uint queryTerritory;
    internal string Status { get; private set; } = "not-requested";
    internal bool Connected => this.Status == "connected";

    internal void Reset()
    {
        this.generation++;
        this.Status = "not-requested";
        // Nav.Pathfind exposes no per-request cancellation. Drain before issuing another.
    }

    internal void Tick(long now, uint territory, Vector3 from, Vector3? target, bool meshReady,
        Func<Vector3, Vector3, Task<List<Vector3>>> query)
    {
        if (this.pending is { } task)
        {
            if (!task.IsCompleted) return;
            this.pending = null;
            bool current = meshReady && this.queryGeneration == this.generation && territory == this.queryTerritory
                && target == this.queryTarget && Vector3.DistanceSquared(from, this.queryFrom) <= 4;
            // Observe faults even when the owning step or territory has already changed.
            var error = task.Exception;
            if (!current) { this.Status = "stale-result"; return; }
            this.Status = task.IsCanceled ? "cancelled" : error != null ? "query-error"
                : HasEndpoint(task.Result, this.queryTarget) ? "connected" : "endpoint-mismatch";
            return;
        }
        if (!meshReady) { this.Status = "mesh-unavailable"; return; }
        if (this.Connected && (territory != this.queryTerritory || target != this.queryTarget
            || Vector3.DistanceSquared(from, this.queryFrom) > 4)) this.Status = "stale-result";
        if (this.Connected) return;
        if (target == null) { this.Status = "no-next-target"; return; }
        if (now < this.nextQuery) return;
        this.queryFrom = from; this.queryTarget = target.Value; this.queryTerritory = territory;
        this.queryGeneration = this.generation; this.startedAt = now;
        this.nextQuery = now + 2000;
        try
        {
            this.pending = query(from, target.Value);
            this.Status = this.pending == null ? "query-error" : "pending";
            if (this.pending != null)
                _ = this.pending.ContinueWith(static t => { _ = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
        catch (Exception) { this.Status = "query-error"; }
    }

    internal bool OptionalWaitFinished(long now) => this.Status != "pending" || now - this.startedAt >= 3000;

    internal static Vector3? ContinuationTarget(IReadOnlyList<PathAction> actions, int index)
    {
        for (int i = index + 1; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action.Name is "TeleportTo" or "ModifyIndex") return null;
            if (action.Position != Vector3.Zero && action.Name is "MoveTo" or "Boss" or "Interactable" or "KillInRange" or "Wait")
                return action.Position;
        }
        return null;
    }

    internal static bool HasEndpoint(IReadOnlyList<Vector3>? points, Vector3 target)
    {
        if (points is not { Count: > 0 }) return false;
        foreach (var p in points)
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) return false;
        var end = points[^1];
        return MathF.Abs(end.Y - target.Y) <= 3
            && Vector2.DistanceSquared(new(end.X, end.Z), new(target.X, target.Z)) <= 4;
    }
}
