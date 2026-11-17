using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AutoDuty.Data;

// Separate from the historical completion-only duty log: attempts and interruptions are samples too.
public sealed class RouteRunRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public ulong CharacterId { get; set; }
    public uint TerritoryId { get; set; }
    public string RouteName { get; set; } = "";
    public string RouteHash { get; set; } = "";
    public uint JobId { get; set; }
    public int StartingLevel { get; set; }
    public string Mode { get; set; } = "";
    public int PartySize { get; set; } = 1;
    public bool Multibox { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int ExecutedSteps { get; set; }
    public int PolicySkippedSteps { get; set; }
    public int AutonomousSteps { get; set; }
    public bool StartedAtFirstStep { get; set; }
    public bool RouteFinished { get; set; }
    public bool DutyCompleted { get; set; }
    public int Failures { get; set; }
    public int Interventions { get; set; }
    // Null identifies old records, which did not distinguish retries from interruptions.
    public bool? CompletionInterrupted { get; set; }
    public string FirstProblem { get; set; } = "";
    public int? ProblemStep { get; set; }
    public string Outcome { get; set; } = "running";
    public long Experience { get; set; }
    public int LevelsGained { get; set; }
    public bool Imported { get; set; }

    private bool LegacyRecoveredComplete => CompletionInterrupted == null && StartedAtFirstStep && TotalSteps > 0
        && CompletedSteps == TotalSteps && RouteFinished && DutyCompleted && Interventions == 0
        && FirstProblem is "stuck" or "navigation-stopped-short" or "move-timeout" or "no-path" or "pathfind-rejected"
            or "task-timeout" or "boss-timeout" or "action-timeout" or "interaction-transfer-timeout";
    [JsonIgnore] public bool HasCompletionInterruption => Interventions > 0
        || (CompletionInterrupted ?? (FirstProblem.Length != 0 && !LegacyRecoveredComplete));
    [JsonIgnore] public bool VerifiedComplete => StartedAtFirstStep && TotalSteps > 0
        && (AutonomousSteps == TotalSteps || LegacyRecoveredComplete)
        && CompletedSteps == TotalSteps && RouteFinished && DutyCompleted && !HasCompletionInterruption;
    [JsonIgnore] public double CompletionPercent => VerifiedComplete ? 100 : Math.Min(99.9, TotalSteps > 0 ? 100d * AutonomousSteps / TotalSteps : 0);
    [JsonIgnore] public TimeSpan Duration => (EndedAt ?? UpdatedAt) > StartedAt ? (EndedAt ?? UpdatedAt) - StartedAt : TimeSpan.Zero;
}

public sealed class AutomationSessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long Experience { get; set; }
    public int LevelsGained { get; set; }
    [JsonIgnore] public TimeSpan Duration => (EndedAt ?? UpdatedAt) > StartedAt ? (EndedAt ?? UpdatedAt) - StartedAt : TimeSpan.Zero;
}

public sealed class AutomationReport
{
    public int Version { get; set; } = 1;
    public List<RouteRunRecord> Runs { get; set; } = [];
}
