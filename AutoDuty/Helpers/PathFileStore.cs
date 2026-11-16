using System;
using System.Collections.Generic;
using System.IO;
using AutoDuty.Data;

namespace AutoDuty.Helpers;

internal static class PathFileStore
{
    internal static bool IsOriginal(PathFile file) => !file.Meta.IsUserVersion && !file.Meta.IsDraft;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Ticks, long Length, bool ReadOnly)> protectionCache = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsReadOnly(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        var info = new FileInfo(path);
        string key = info.FullName;
        if (protectionCache.TryGetValue(key, out var entry) && entry.Ticks == info.LastWriteTimeUtc.Ticks && entry.Length == info.Length)
            return entry.ReadOnly;
        bool readOnly = IsOriginal(PathJson.ReadFile(File.ReadAllText(path)));
        protectionCache[key] = (info.LastWriteTimeUtc.Ticks, info.Length, readOnly);
        return readOnly;
    }

    internal static void EnsureEditable(string path)
    {
        if (IsReadOnly(path)) throw new InvalidOperationException("原始脚本只读，请在构建页创建副本后编辑。");
    }
    internal static bool CanAutoSelect(PathFile? file) => file is { Meta.IsDraft: false, Actions.Count: > 0 };

    internal static bool ShouldPersistEdit(string reason, bool recording) => !recording && reason is
        "add MoveTo" or "add action" or "replace action" or "reorder" or "delete" or "duplicate" or "insert after" or "strict conditions";

    internal static string CopyAs(string source, string directory, uint territory, string name, IReadOnlyList<PathAction> actions)
    {
        name = PathRecorder.SafeName(name);
        string destination = Path.Combine(directory, $"({territory}) {name}.json");
        var file = File.Exists(source) ? PathJson.ReadFile(File.ReadAllText(source)) : new PathFile();
        file.Actions = [.. actions];
        file.Meta.Territory = territory;
        file.Meta.IsDraft = true;
        file.Meta.IsUserVersion = true;
        PathValidator.ValidateStructure(file);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, PathJson.WriteFile(file));
            // Refuse collisions, including copying over the source file.
            File.Move(temporary, destination, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return destination;
    }

    // Read metadata from disk, not from a potentially stale path catalog entry.
    // Rename on the same volume keeps readers from observing a partially written JSON file.
    internal static void Save(string path, IReadOnlyList<PathAction> actions, string changelog = "", int version = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("No route file selected");
        path = Path.GetFullPath(path);
        EnsureEditable(path);
        var file = File.Exists(path) ? PathJson.ReadFile(File.ReadAllText(path)) : new PathFile();
        file.Meta.IsUserVersion = true;
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
