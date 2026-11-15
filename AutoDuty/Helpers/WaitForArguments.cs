using System;
using System.Globalization;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using ECommons;

namespace AutoDuty.Helpers;

internal static class WaitForArguments
{
    internal static string[] Normalize(string argument)
    {
        var parts = argument.Split(';').Select(p => p.Trim()).ToArray();
        // Historical paths used these aliases and omitted DistanceTo before coordinates.
        if (parts.Length == 2 && parts[0].Contains(',')) return ["DistanceTo", parts[0], parts[1]];
        parts[0] = parts[0].ToLowerInvariant() switch
        {
            "ready" or "isready" => "IsReady",
            "isocupied" or "isoccupied" => "IsOccupied",
            "isvalid" => "IsValid", "combat" => "Combat", "ooc" => "OOC",
            "distanceto" => "DistanceTo", "conditionflag" => "ConditionFlag",
            "bnpcinradius" => "BNpcInRadius", "addon" => "Addon",
            _ => parts[0],
        };
        return parts;
    }

    internal static string? Error(string argument)
    {
        var parts = Normalize(argument);
        bool Number(string value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && float.IsFinite(n) && n >= 0;
        bool valid = parts[0] switch
        {
            "IsReady" or "IsOccupied" or "IsValid" or "Combat" or "OOC" => parts.Length == 1,
            "DistanceTo" => parts.Length == 3 && parts[1].TryGetVector3(out var position)
                && float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) && Number(parts[2]),
            "ConditionFlag" => parts.Length == 3 && Enum.TryParse<ConditionFlag>(parts[1], out var flag) && flag != ConditionFlag.None && bool.TryParse(parts[2], out _),
            "BNpcInRadius" => parts.Length == 2 && int.TryParse(parts[1], out int radius) && radius >= 0,
            "Addon" => parts.Length == 2 && parts[1].Length > 0,
            _ => false,
        };
        return valid ? null : "WaitFor: unknown condition or invalid arguments";
    }
}
