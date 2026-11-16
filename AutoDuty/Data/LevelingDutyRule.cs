namespace AutoDuty.Data;

public sealed class LevelingDutyRule
{
    public int MinimumLevel { get; set; } = 15;
    public int MaximumLevel { get; set; } = 99;
    public int Priority { get; set; }
    public bool Allows(int level) => level >= MinimumLevel && level <= MaximumLevel;
}
