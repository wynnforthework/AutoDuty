using System.Numerics;

namespace AutoDuty.Helpers;

// Combat pursuit owns a separate watchdog: the route movement timer is suspended
// during combat, but an unreachable enemy must not cause unbounded wall running.
internal sealed class CombatApproachProgress
{
    private readonly MovementProgress progress = new();
    private bool active;
    private bool requested;
    private Vector3 destination;

    internal void Reset() { this.active = false; this.requested = false; }

    internal bool IsStuck(Vector3 position, long now, long limit)
    {
        if (!this.active) { this.progress.Reset(position, now); this.active = true; }
        return this.progress.IsStuck(position, now, limit);
    }

    internal bool ShouldRequest(Vector3 target, bool navigationBusy)
    {
        if (this.requested && navigationBusy && Vector3.DistanceSquared(target, this.destination) <= 4) return false;
        this.destination = target;
        this.requested = true;
        return true;
    }
}
