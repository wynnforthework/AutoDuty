namespace AutoDuty.Helpers;

internal static class StepRecoveryPolicy
{
    internal static bool MustPause(string reason) => reason is "stuck" or "navigation-stopped-short"
        or "move-timeout" or "no-path" or "pathfind-rejected" or "unexpected-position-transition";

    internal static bool UnexpectedTransition(Stage stage) => stage is Stage.Reading_Path
        or Stage.Moving or Stage.Waiting_For_Combat or Stage.Interactable;
}
