namespace AutoDuty.Helpers;

internal static class RouteMovementPolicy
{
    // Each route entry owns its policy, including the approach to its coordinate.
    internal static bool RotationEnabled(PathAction step) => step.Name != "MoveTo";
    internal static bool StopsForCombat(PathAction step) => step.Name != "MoveTo"
        && (step.Name != "StopForCombat" || CombatCheckpointProgress.IsSwitchOn(step));
    internal static bool PreferInstants(PathAction step, Stage stage)
        => step.Name == "StopForCombat" && !StopsForCombat(step) && stage == Stage.Moving;
    internal static bool ActionMovement(PathAction step) => StopsForCombat(step) && step.Name != "Interactable";
}
