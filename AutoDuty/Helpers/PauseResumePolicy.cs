namespace AutoDuty.Helpers;

internal static class PauseResumePolicy
{
    internal static Stage Target(Stage previous, bool navigating) => previous switch
    {
        Stage.Moving or Stage.Interactable => Stage.Reading_Path,
        Stage.Paused when navigating => Stage.Reading_Path,
        Stage.Paused => Stage.Idle,
        _ => previous,
    };

    internal static bool ShouldInvokeAction(Stage from) => from != Stage.Paused;
}
