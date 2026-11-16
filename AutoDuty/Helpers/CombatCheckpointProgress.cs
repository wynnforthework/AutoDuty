using System;
using System.Collections.Generic;
using System.Numerics;

namespace AutoDuty.Helpers;

// Evidence belongs to one specific switch step, never to a dungeon-wide combat flag.
internal sealed class CombatCheckpointProgress
{
    internal const float NearbyRadius = 6;
    internal const float SwitchArrivalRadius = 1.5f;
    private PathAction? checkpoint;
    private bool observedCombat;
    private bool combatEnded;

    internal static bool IsSwitchOn(PathAction step) => step.Name == "StopForCombat"
        && step.Arguments.Count > 0 && bool.TryParse(step.Arguments[0], out bool stop) && stop;

    internal void Reset() { this.checkpoint = null; this.observedCombat = this.combatEnded = false; }

    internal void Observe(IReadOnlyList<PathAction> actions, int index, Vector3 position, bool inCombat, bool alive)
    {
        if (!alive || index < 0 || index >= actions.Count) { this.Reset(); return; }
        PathAction? candidate = IsSwitchOn(actions[index]) ? actions[index] :
            actions[index].Name == "MoveTo" && index + 1 < actions.Count && IsSwitchOn(actions[index + 1])
            && Vector3.Distance(actions[index].Position, actions[index + 1].Position) <= NearbyRadius
                ? actions[index + 1] : null;
        if (!ReferenceEquals(candidate, this.checkpoint))
        {
            this.Reset();
            this.checkpoint = candidate;
        }
        if (candidate == null || candidate.Position == Vector3.Zero) return;
        if (inCombat)
        {
            this.combatEnded = false;
            if (Vector3.Distance(position, candidate.Position) <= NearbyRadius) this.observedCombat = true;
        }
        else if (this.observedCombat) this.combatEnded = true;
    }

    internal bool CanApply(PathAction step, Vector3 position, bool inCombat) => !inCombat
        && this.observedCombat && this.combatEnded && ReferenceEquals(step, this.checkpoint)
        && Vector3.Distance(position, step.Position) <= NearbyRadius;

    internal static float ArrivalRadius(PathAction step, int retries) => step.Name == "StopForCombat"
        ? SwitchArrivalRadius : step.Name == "Interactable" ? 2f : retries > 0 ? .5f : .25f;

    internal static bool HasArrived(PathAction step, Vector3 position, int retries)
    {
        float radius = ArrivalRadius(step, retries);
        Vector3 delta = position - step.Position;
        // A combat switch is a ground location, not a precise 3D waypoint.
        // Slopes must not leave navmesh stopped within its horizontal tolerance
        // while AutoDuty waits forever for a smaller 3D distance. Bound height
        // separately so another floor cannot satisfy this checkpoint.
        return step.Name == "StopForCombat"
            ? delta.X * delta.X + delta.Z * delta.Z <= radius * radius && MathF.Abs(delta.Y) <= 2f
            : delta.LengthSquared() <= radius * radius;
    }
}
