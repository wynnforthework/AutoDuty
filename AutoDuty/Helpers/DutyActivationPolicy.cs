namespace AutoDuty.Helpers;

internal static class DutyActivationPolicy
{
    internal static bool RouteActive(Stage stage, bool navigating) => navigating && stage != Stage.Stopped;
    internal static bool AutoStartFollower(bool enabled, bool host, bool isDuty, bool looping) =>
        enabled && !host && isDuty && looping;
}
