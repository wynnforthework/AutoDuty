using System;
using System.Collections.Generic;
using System.IO;
using AutoDuty.Data;

namespace AutoDuty.Helpers;

internal static class PathFileStore
{
    internal static bool ShouldPersistEdit(string reason, bool recording) => !recording && reason is
        "add MoveTo" or "add action" or "replace action" or "reorder" or "delete" or "duplicate" or "insert after" or "strict conditions";

    // Read metadata from disk, not from a potentially stale path catalog entry.
    // Rename on the same volume keeps readers from observing a partially written JSON file.
    internal static void Save(string path, IReadOnlyList<PathAction> actions, string changelog = "", int version = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("No route file selected");
        path = Path.GetFullPath(path);
        var file = File.Exists(path) ? PathJson.ReadFile(File.ReadAllText(path)) : new PathFile();
        file.Actions = [.. actions];
        if (changelog.Length > 0)
            file.Meta.Changelog.Add(new PathFileChangelogEntry { Version = version, Change = changelog });
        PathValidator.ValidateStructure(file);
        string json = PathJson.WriteFile(file);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
