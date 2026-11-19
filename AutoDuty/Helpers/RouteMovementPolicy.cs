namespace AutoDuty.Helpers;

internal static class RouteMovementPolicy
{
    // Each route entry owns its policy, including the approach to its coordinate.
    internal static bool RotationEnabled(PathAction step) => step.Name != "MoveTo";
    // Traversal and timing entries must not implicitly create combat checkpoints.
    // In particular, Wait must only wait its duration, not additionally wait OOC.
    internal static bool StopsForCombat(PathAction step) => step.Name switch
    {
        "MoveTo" or "Wait" or "WaitFor" or "Jump" or "JumpTo" or "AutoMoveFor" or "<-- Comment -->" => false,
        "StopForCombat" => CombatCheckpointProgress.IsSwitchOn(step),
        _ => true,
    };
    internal static bool PreferInstants(PathAction step, Stage stage)
        => step.Name == "StopForCombat" && !StopsForCombat(step) && stage == Stage.Moving;
    internal static bool ActionMovement(PathAction step) => StopsForCombat(step) && step.Name != "Interactable";
}
