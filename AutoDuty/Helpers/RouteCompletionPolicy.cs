namespace AutoDuty.Helpers;

internal static class RouteCompletionPolicy
{
    internal static bool CompletedBoss(bool dutyComplete, bool navigating, bool paused, int index, string[] actions) =>
        dutyComplete && navigating && !paused && HasStep(index, actions.Length)
        && actions[index] == "Boss" && System.Array.TrueForAll(actions[(index + 1)..],
            name => name is "TreasureCoffer" or "<-- Comment -->");
    internal static bool DrainFinished(bool busy, long elapsedMs) => !busy || elapsedMs >= 10_000;

    internal static bool HasStep(int index, int count) => index >= 0 && index < count;
    internal static bool Finished(int index, int count, bool navigating, bool paused) =>
        navigating && !paused && count > 0 && index >= count;
}
