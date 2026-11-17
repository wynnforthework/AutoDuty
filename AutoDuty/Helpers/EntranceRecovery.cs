using System.Numerics;

namespace AutoDuty.Helpers;

internal static class EntranceRecovery
{
    internal static bool IsEntrance(uint recordedTerritory, uint currentTerritory, Vector3 entrance, Vector3 position)
        => recordedTerritory == currentTerritory
        && Vector2.DistanceSquared(new(entrance.X, entrance.Z), new(position.X, position.Z)) <= 64
        && System.MathF.Abs(entrance.Y - position.Y) <= 3;
}
