using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace AutoDuty.Helpers;

internal static class PathValidator
{
    internal static void ValidateStructure(PathFile file)
    {
        if (file.Actions == null || file.Meta == null || file.Meta.Changelog == null || file.Meta.Notes == null)
            throw new JsonSerializationException("Missing path actions or metadata");
        if (file.Actions.Count > 10000) throw new JsonSerializationException("Path exceeds 10000 steps");
        foreach (var action in file.Actions)
            if (action == null || action.Name == null || action.Arguments == null || action.Arguments.Any(a => a == null)
                || action.Conditions == null || action.ConditionsWhile == null || action.ConditionsUntil == null
                || action.Conditions.Concat(action.ConditionsWhile).Concat(action.ConditionsUntil).Any(c => c == null)
                || !float.IsFinite(action.Position.X) || !float.IsFinite(action.Position.Y) || !float.IsFinite(action.Position.Z)
                || (action.Transition != null && (!action.Transition.Valid || action.Name is not ("MoveTo" or "Interactable"))))
                throw new JsonSerializationException("Invalid step structure or position");
    }

    internal static string? Error(PathAction action)
    {
        if (action.Transition is { } transition && (!transition.Valid || action.Name is not ("MoveTo" or "Interactable")))
            return "Invalid transition region (MoveTo/Interactable only)";
        bool Missing(int count) => action.Arguments.Count < count || action.Arguments.Take(count).Any(string.IsNullOrWhiteSpace);
        bool Integer(int i) => int.TryParse(action.Arguments[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        int required = action.Name switch
        {
            "Wait" or "WaitFor" or "Interactable" or "SelectString" or "SelectYesno" or "SelectJournalResult" or "MoveToObject" or "Target" or "KillInRange" or "AutoMoveFor" or "ChatCommand" or "StopForCombat" or "ModifyIndex" or "DutySpecificCode" => 1,
            "Action" or "BLULoad" => 2,
            _ => 0,
        };
        if (Missing(required)) return $"{action.Name}: expected {required} arguments";
        if (action.Name == "WaitFor") return WaitForArguments.Error(action.Arguments[0]);
        if (action.Name is "Wait" or "SelectString" or "DutySpecificCode" or "AutoMoveFor" or "ModifyIndex")
            if (!Integer(0)) return $"{action.Name}: invalid integer";
        if (action.Name is "Wait" or "AutoMoveFor")
            if (int.Parse(action.Arguments[0], CultureInfo.InvariantCulture) < 0) return $"{action.Name}: negative duration";
        if (action.Name == "KillInRange" && (!uint.TryParse(action.Arguments[0], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out uint range) || range == 0)) return "KillInRange: expected positive range";
        if (action.Name == "Interactable" && !action.Arguments.Any(argument =>
                uint.TryParse(RegexHelper.ObjectIdRegex().Match(argument).Value, out uint id) && id != 0))
            return "Interactable: expected at least one valid object id";
        if (action.Name == "MoveTo" && action.Arguments.Count > 0 && action.Arguments[0].Length > 0 && !bool.TryParse(action.Arguments[0], out _)) return "MoveTo: invalid mesh boolean";
        if (action.Name is "StopForCombat" or "SelectJournalResult" or "BLULoad")
            if (!bool.TryParse(action.Arguments[0], out _)) return $"{action.Name}: invalid boolean";
        if (action.Name == "Action" && (!Enum.TryParse<FFXIVClientStructs.FFXIV.Client.Game.ActionType>(action.Arguments[0], out _) || !Integer(1))) return "Action: invalid action type or id";
        return null;
    }
}
