namespace AutoDuty.Helpers;

internal static class DutyActivationPolicy
{
    internal static bool CanStop(Stage stage, bool looping, bool navigating, bool other, bool tasks, bool helpers) =>
        stage != Stage.Stopped || looping || navigating || other || tasks || helpers;

    internal static Stage DisplayStage(Stage stage, bool looping) =>
        stage == Stage.Stopped && looping ? Stage.Looping : stage;

    internal static bool RouteActive(Stage stage, bool navigating) => navigating && stage != Stage.Stopped;
    internal static bool AutoStartFollower(bool enabled, bool host, bool isDuty, bool looping) =>
        enabled && !host && isDuty && looping;
}
