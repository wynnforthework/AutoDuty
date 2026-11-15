using System;
using System.Collections.Generic;
using AutoDuty.Data;

namespace AutoDuty.Helpers;

internal sealed class RouteRunTracker(RouteRunRecord record)
{
    internal RouteRunRecord Record { get; } = record;
    private readonly HashSet<int> completed = [];

    internal void CompleteStep(int index, bool policySkip = false)
    {
        if (index < 0 || index >= this.Record.TotalSteps || !this.completed.Add(index)) return;
        this.Record.CompletedSteps++;
        if (policySkip) this.Record.PolicySkippedSteps++; else this.Record.ExecutedSteps++;
        // Freeze the uninterrupted prefix at the first failure/intervention. Recovery cannot erase it.
        if (this.Record.FirstProblem.Length == 0 && this.Record.StartedAtFirstStep && index == this.Record.AutonomousSteps)
            this.Record.AutonomousSteps++;
    }

    internal void Problem(string reason, int index, bool manual = false)
    {
        if (manual) this.Record.Interventions++; else this.Record.Failures++;
        if (this.Record.FirstProblem.Length != 0) return;
        this.Record.FirstProblem = reason;
        this.Record.ProblemStep = index >= 0 && index < this.Record.TotalSteps ? index : null;
    }

    internal void Finish(DateTime now, string outcome)
    {
        this.Record.UpdatedAt = now;
        this.Record.EndedAt = now;
        this.Record.Outcome = this.Record.VerifiedComplete ? "complete" : outcome;
    }
}

internal readonly record struct ExperienceReading(ulong CharacterId, int ExperienceIndex, int Level, long CurrentExperience);

internal sealed class ExperienceAccumulator
{
    private readonly Dictionary<(ulong, int), ExperienceReading> previous = [];

    // Uses unsynced job level/XP; two jobs sharing an experience index share a baseline.
    internal (long Experience, int Levels) Sample(ExperienceReading value, Func<int, long> requiredAtLevel)
    {
        var key = (value.CharacterId, value.ExperienceIndex);
        bool existed = this.previous.TryGetValue(key, out var last);
        this.previous[key] = value;
        if (!existed || value.Level < last.Level || value.Level > 100 || value.Level < 1 || value.CurrentExperience < 0) return default;
        long gained = value.CurrentExperience - last.CurrentExperience;
        for (int level = last.Level; level < value.Level; level++)
        {
            long needed = requiredAtLevel(level);
            if (needed <= 0) return default; // Missing data is not invented experience.
            gained += needed;
        }
        return (Math.Max(0, gained), Math.Max(0, value.Level - last.Level));
    }
}
