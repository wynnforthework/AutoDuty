namespace AutoDuty.Helpers;

internal static class RouteMovementPolicy
{
    // Each route entry owns its policy, including the approach to its coordinate.
    internal static bool RotationEnabled(PathAction step) => step.Name != "MoveTo";
    private enum CombatPolicy { Neutral, Fight, Switch }

    // Explicitly reviewed catalogue. New/unknown actions cannot silently become
    // combat checkpoints; the catalogue coverage test requires a review entry.
    private static CombatPolicy? Policy(string name) => name switch
    {
        "Boss" or "KillInRange" => CombatPolicy.Fight,
        "StopForCombat" => CombatPolicy.Switch,
        "MoveTo" or "TeleportTo" or "<-- Comment -->" or "Wait" or "WaitFor"
            or "Interactable" or "TreasureCoffer" or "SelectYesno" or "SelectString"
            or "SelectJournalResult" or "MoveToObject" or "DutySpecificCode"
            or "BossMod" or "Rotation" or "Target" or "AutoMoveFor" or "ChatCommand"
            or "Revival" or "ForceAttack" or "Jump" or "JumpTo" or "CameraFacing"
            or "ClickTalk" or "ConditionAction" or "ModifyIndex" or "Action"
            or "BLULoad" or "VariantVote" or "DisableBMModule" or "Collision"
            or "Follow" or "SetBMSettings" or "ExitDuty" or "PausePandora" => CombatPolicy.Neutral,
        _ => null,
    };
    internal static bool IsReviewed(string name) => Policy(name).HasValue;
    internal static bool StopsForCombat(PathAction step) => Policy(step.Name) switch
    {
        CombatPolicy.Fight => true,
        CombatPolicy.Switch => CombatCheckpointProgress.IsSwitchOn(step),
        _ => false,
    };
    // A true switch owns combat only after its coordinate has been reached.
    internal static bool InterruptsApproach(PathAction step) => step.Name != "StopForCombat" && StopsForCombat(step);
    internal static bool PreferInstants(PathAction step, Stage stage)
        => step.Name == "StopForCombat" && stage == Stage.Moving;
    internal static bool ActionMovement(PathAction step) => StopsForCombat(step) && step.Name != "Interactable";
}
