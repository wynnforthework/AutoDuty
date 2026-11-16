using System.Collections.Generic;
using System.Linq;

namespace AutoDuty.Helpers;

internal readonly record struct LevelingCandidate(uint Territory, int RequiredLevel);

internal static class LevelingPlanRanking
{
    // Eligibility (unlock, entry level, mode and route) is checked by the caller.
    internal static uint? Choose(IEnumerable<LevelingCandidate> candidates) => candidates
        .OrderByDescending(c => c.RequiredLevel).ThenBy(c => c.Territory)
        .Select(c => (uint?)c.Territory).FirstOrDefault();
}
