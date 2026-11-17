namespace AutoDuty.Helpers;

internal static class RouteCompletionPolicy
{
    internal static bool HasStep(int index, int count) => index >= 0 && index < count;
    internal static bool Finished(int index, int count, bool navigating, bool paused) =>
        navigating && !paused && count > 0 && index >= count;
}
