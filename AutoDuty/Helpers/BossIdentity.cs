using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Helpers;

internal static class BossIdentity
{
    // Shared by the real object-table lookup and service-free selection tests.
    // No route notes or object names enter this selection API.
    internal static IEnumerable<T> Candidates<T>(IEnumerable<T> objects, Vector3 origin,
        Func<T, Vector3> position, Func<T, bool> boss, int radius = 100) =>
        objects.Where(o => Vector3.DistanceSquared(origin, position(o)) <= radius * radius
            && MathF.Abs(origin.Y - position(o).Y) <= 10 && boss(o))
            .OrderBy(o => Vector3.DistanceSquared(origin, position(o)));

    internal static T? SelectLive<T>(IEnumerable<T> objects, Vector3 origin, Func<T, Vector3> position,
        Func<T, bool> boss, Func<T, bool> liveTargetable, Func<T, bool> tracked, int radius = 100) where T : class =>
        Candidates(objects, origin, position, o => liveTargetable(o) && (tracked(o) || boss(o)), radius)
            .OrderByDescending(tracked).FirstOrDefault();

    internal static bool OwnedBy<T>(T step, IEnumerable<T> steps, Vector3 bossPosition, Func<T, Vector3> position) where T : class =>
        ReferenceEquals(step, steps.Where(s => position(s) != Vector3.Zero)
            .OrderBy(s => Vector3.DistanceSquared(position(s), bossPosition)).FirstOrDefault());
}
