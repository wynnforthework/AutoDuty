namespace AutoDuty.Helpers;

internal static class RouteMovementPolicy
{
    // Pulling keeps rotation/instant attacks, but must not stop for cast bars.
    // Pauses, interactions and combat ownership restore normal casting.
    internal static bool PreferInstants(bool stopForCombat, Stage stage)
        => !stopForCombat && stage == Stage.Moving;
}
