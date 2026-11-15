using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AutoDuty.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoDuty.Helpers;

internal readonly record struct RouteMetricKey(uint Territory, string Hash, uint Job, string Mode, int PartySize, bool Multibox);

internal sealed class RouteMetricSummary
{
    internal required RouteMetricKey Key;
    internal required string Name;
    internal required List<RouteRunRecord> Runs;
    internal int Samples => this.Runs.Count;
    internal int Participants => this.Runs.Select(r => r.ParticipantId).Distinct().Count();
    internal int CleanRuns => this.Runs.Count(r => r.VerifiedComplete);
    internal double Reliability => this.Samples == 0 ? 0 : 100d * this.CleanRuns / this.Samples;
    internal double MeanCompletion => this.Runs.Average(r => r.CompletionPercent);
    internal double MeanSeconds => this.Runs.Average(r => r.Duration.TotalSeconds);
    internal long Experience => this.Runs.Sum(r => r.Experience);
    internal double ExperiencePerHour => this.Runs.Sum(r => r.Duration.TotalHours) is > 0 and var hours ? this.Experience / hours : 0;
}

internal static class AutomationReports
{
    internal const int MaxBytes = 4 * 1024 * 1024;
    internal static string Fingerprint(IReadOnlyList<PathAction> actions) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(actions, Formatting.None, PathJson.Write))));

    internal static List<RouteMetricSummary> Summarize(IEnumerable<RouteRunRecord> runs) => runs.Where(r => r.EndedAt != null)
        .GroupBy(r => new RouteMetricKey(r.TerritoryId, r.RouteHash, r.JobId, r.Mode, r.PartySize, r.Multibox))
        .Select(g => new RouteMetricSummary { Key = g.Key, Name = g.Last().RouteName, Runs = g.ToList() })
        .OrderByDescending(g => g.Reliability).ThenByDescending(g => g.ExperiencePerHour).ToList();

    internal static string Export(IEnumerable<RouteRunRecord> runs)
    {
        var report = new AutomationReport();
        foreach (var run in runs.Where(r => r.EndedAt != null))
        {
            var copy = JObject.FromObject(run);
            copy[nameof(RouteRunRecord.CharacterId)] = 0; // Keep a pseudonymous contributor ID, never character identity.
            report.Runs.Add(copy.ToObject<RouteRunRecord>()!);
        }
        string json = JsonConvert.SerializeObject(report, Formatting.None);
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes || report.Runs.Count > 10000) throw new ArgumentException("Report is too large; export fewer runs");
        return json;
    }

    internal static List<RouteRunRecord> Read(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new JsonSerializationException("Report exceeds size limit");
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 };
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var report = root.ToObject<AutomationReport>(JsonSerializer.Create(settings));
        if (report is not { Version: 1, Runs: not null } || report.Runs.Count > 10000) throw new JsonSerializationException("Unsupported or invalid report");
        foreach (var run in report.Runs)
        {
            if (run == null || run.Id == Guid.Empty || run.ParticipantId == Guid.Empty || run.EndedAt == null
                || run.StartedAt == default || run.EndedAt < run.StartedAt || run.EndedAt > DateTime.UtcNow.AddMinutes(5)
                || run.Duration > TimeSpan.FromDays(30) || run.TotalSteps is < 1 or > 10000
                || run.CompletedSteps < 0 || run.CompletedSteps > run.TotalSteps || run.AutonomousSteps < 0 || run.AutonomousSteps > run.CompletedSteps
                || run.ExecutedSteps < 0 || run.PolicySkippedSteps < 0 || (long)run.ExecutedSteps + run.PolicySkippedSteps != run.CompletedSteps
                || run.Failures is < 0 or > 100000 || run.Interventions is < 0 or > 100000 || run.Experience is < 0 or > 1000000000000
                || run.LevelsGained is < 0 or > 1000 || run.PartySize is < 1 or > 24 || run.JobId > 99 || run.TerritoryId == 0
                || run.RouteHash is not { Length: 64 } || run.RouteHash.Any(c => !Uri.IsHexDigit(c))
                || run.RouteName is not { Length: <= 512 } || run.Mode is not { Length: <= 100 }
                || run.FirstProblem is not { Length: <= 256 } || run.Outcome is not { Length: <= 100 }
                || (run.ProblemStep is { } step && (step < 0 || step >= run.TotalSteps)))
                throw new JsonSerializationException("Invalid run evidence in report");
            run.CharacterId = 0;
            run.Imported = true;
        }
        return report.Runs;
    }

    internal static int Merge(List<RouteRunRecord> destination, IEnumerable<RouteRunRecord> incoming)
    {
        var ids = destination.Select(r => r.Id).ToHashSet();
        int added = 0;
        foreach (var run in incoming)
            if (ids.Add(run.Id)) { destination.Add(run); added++; }
        return added;
    }
}
