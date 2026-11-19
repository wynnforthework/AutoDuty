using System;
using System.Collections.Generic;
using System.Reflection;
using ECommons.Reflection;

namespace AutoDuty.Helpers;

// Optional, read-only diagnostics for BMR's input arbitration. These internals
// are not an API: absent/changed members mean unknown, never "not blocked".
// Do not retain plugin instances across hot reload or use these to drive steps.
internal static class MovementControllerDiagnostics
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static object Capture()
    {
        var controllers = new List<object>();
        foreach (string name in new[] { "BossModReborn", "BossMod" })
        {
            try
            {
                if (!DalamudReflector.TryGetDalamudPlugin(name, out object plugin, false, true)) continue;
                object? movement = Member(plugin, "_movementOverride");
                object? actions = Member(plugin, "_amex");
                object? hints = Member(plugin, "_hints");
                controllers.Add(new
                {
                    plugin = name,
                    version = plugin.GetType().Assembly.GetName().Version?.ToString(),
                    movement = Values(movement, "MovementBlocked", "UserMove", "ActualMove", "DesiredDirection"),
                    actions = Values(actions, "PreventMovingWhileCasting", "MoveMightInterruptCast", "CastTimeRemaining"),
                    hints = Values(hints, "ForcedMovement", "HoldCasts", "MaxCastTime", "ForceCancelCastOther", "ForceCancelCastMechanic"),
                });
            }
            catch (Exception ex) { controllers.Add(new { plugin = name, unavailable = ex.GetType().Name }); }
        }
        return controllers;
    }

    private static object? Member(object? instance, string name)
    {
        if (instance == null) return null;
        try
        {
            Type type = instance.GetType();
            return type.GetProperty(name, Members)?.GetValue(instance)
                ?? type.GetField(name, Members)?.GetValue(instance);
        }
        catch { return null; }
    }

    private static Dictionary<string, object?> Values(object? instance, params string[] names)
    {
        var values = new Dictionary<string, object?> { ["available"] = instance != null };
        foreach (string name in names)
        {
            object? value = Member(instance, name);
            // Bound serialization: never walk another plugin's object graph.
            values[name] = value is null or bool or float or double or int ? value : value.ToString();
        }
        return values;
    }
}
