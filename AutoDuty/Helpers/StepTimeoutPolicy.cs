using System;

namespace AutoDuty.Helpers;

internal static class StepTimeoutPolicy
{
    internal static long Limit(PathAction action, int combatLimit, int waitLimit, int actionLimit)
        => action.Name switch
        {
            "Boss" or "KillInRange" => Math.Max(1000L, combatLimit),
            "WaitFor" => waitLimit <= 0 ? long.MaxValue : Math.Max(1000L, waitLimit),
            "Wait" or "AutoMoveFor" when action.Arguments.Count > 0
                && int.TryParse(action.Arguments[0], out int duration) => Math.Max(0L, duration) + 30_000L,
            _ => Math.Max(1000L, actionLimit),
        };
}
