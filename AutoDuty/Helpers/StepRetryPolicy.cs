using System;
namespace AutoDuty.Helpers;

internal static class StepRetryPolicy
{
    // failures includes the initial execution failure; it is the ordinal of the next retry.
    internal static bool CanRetry(int failures, int maxRetries) => failures > 0 && failures <= Math.Max(0, maxRetries);
}
