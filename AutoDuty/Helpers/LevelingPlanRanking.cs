using System.Collections.Generic;
using System.Linq;

namespace AutoDuty.Helpers;

internal readonly record struct LevelingCandidate(uint Territory, int RequiredLevel, int Priority,
    int Samples = 0, int CleanRuns = 0, double ExperiencePerHour = 0);

internal static class LevelingPlanRanking
{
    // Caller supplies only eligible duties and evidence matching the current level and route/cohort.
    internal static uint? Choose(IEnumerable<LevelingCandidate> candidates, bool measured, int minimumSamples)
    {
        var list = candidates.ToArray();
        if (list.Length == 0) return null;
        var preferred = list.Where(c => c.Priority == list.Max(c => c.Priority)).ToArray();
        if (measured)
        {
            var proven = preferred.Where(c => c.Samples >= System.Math.Max(1, minimumSamples)
                && c.CleanRuns == c.Samples && c.ExperiencePerHour > 0).ToArray();
            if (proven.Length > 0)
                return proven.OrderByDescending(c => c.ExperiencePerHour).ThenByDescending(c => c.RequiredLevel)
                    .ThenBy(c => c.Territory).First().Territory;
        }
        return preferred.OrderByDescending(c => c.RequiredLevel).ThenBy(c => c.Territory).First().Territory;
    }
}
