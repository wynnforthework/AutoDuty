using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using AutoDuty.Data;
using AutoDuty.Helpers;
using AutoDuty.Windows;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace AutoDuty;

public sealed partial class AutoDuty
{
    private readonly List<string> importChunks = [];
    private PathImportWindow? importWindow;

    internal void CopyPath(string? range = null, bool chat = false)
    {
        int first = 0, last = this.Actions.Count - 1;
        if (!string.IsNullOrEmpty(range))
        {
            var values = range.Split('-');
            if (values.Length != 2 || !int.TryParse(values[0], out int a) || !int.TryParse(values[1], out int b) || a < 1 || b < a || b > this.Actions.Count)
                throw new ArgumentException("Use a 1-based range, e.g. 12-30");
            first = a - 1; last = b - 1;
        }
        if (last < first) throw new InvalidOperationException("No path steps to copy");
        PathFileMetaData meta = new() { CreatedAt = this.Version, Territory = this.CurrentTerritoryContent?.TerritoryType };
        if (ContentPathsManager.DictionaryPaths.TryGetValue(meta.Territory ?? 0, out var container))
            meta = container.Paths.FirstOrDefault(p => p.FilePath == this.pathFile)?.PathFile.Meta ?? meta;
        var file = new PathFile { Actions = this.Actions.Skip(first).Take(last - first + 1).ToList(), Meta = meta };
        string encoded = PathCodec.Encode(file);
        ImGui.SetClipboardText(chat ? string.Join(Environment.NewLine, PathCodec.Split(encoded)) : encoded);
        Svc.Chat.Print($"[AutoDuty] Copied {encoded.Length} characters{(chat ? " in chat chunks" : "")}.");
    }

    internal void ImportPath(string input)
    {
        PathFile file;
        input = input.Trim();
        if (input.StartsWith("ADP1C", StringComparison.Ordinal))
        {
            if (input.StartsWith("ADP1C1/", StringComparison.Ordinal)) this.importChunks.Clear();
            this.importChunks.AddRange(input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            if (!PathCodec.TryAssemble(this.importChunks, out input, out string error))
            {
                Svc.Chat.Print($"[AutoDuty] {error}");
                if (!error.StartsWith("Waiting", StringComparison.Ordinal)) this.importChunks.Clear();
                return;
            }
            this.importChunks.Clear();
        }
        file = PathCodec.Decode(input, out string reason) ?? throw new ArgumentException(reason);
        this.PreviewImport(file, "Clipboard / ADP1");
    }

    internal void PreviewImport(PathFile file, string source)
    {
        PathValidator.ValidateStructure(file);
        if (this.importWindow != null) this.windowSystem.RemoveWindow(this.importWindow);
        this.windowSystem.AddWindow(this.importWindow = new(file, source) { IsOpen = true });
    }

    private async void ImportUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new ArgumentException("Use an HTTPS path JSON URL");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var output = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                if (output.Length + read > PathCodec.MaxBytes) throw new IOException("Path JSON exceeds size limit");
                output.Write(buffer, 0, read);
            }
            var file = PathJson.ReadFile(System.Text.Encoding.UTF8.GetString(output.ToArray()));
            await Svc.Framework.RunOnTick(() => this.PreviewImport(file, url));
        }
        catch (Exception ex) { Svc.Chat.PrintError($"[AutoDuty] {ex.Message}"); }
    }

    internal void PathCommand(string[] args)
    {
        try
        {
            if (args.Length < 2) { Svc.Chat.Print("[AutoDuty] path copy [a-b] | copy-chat | import [ADP1] | import-file <file> | import-url <url>"); return; }
            switch (args[1])
            {
                case "copy": this.CopyPath(args.Length > 2 ? args[2] : null); break;
                case "copy-chat": this.CopyPath(chat: true); break;
                case "import": this.ImportPath(args.Length > 2 ? string.Join(" ", args[2..]) : ImGui.GetClipboardText()); break;
                case "import-file" when args.Length > 2:
                    string fileName = string.Join(" ", args[2..]).Trim('"');
                    if (new FileInfo(fileName).Length > PathCodec.MaxBytes) throw new IOException("Path JSON exceeds size limit");
                    this.PreviewImport(PathJson.ReadFile(File.ReadAllText(fileName)), fileName); break;
                case "import-url" when args.Length > 2: this.ImportUrl(args[2]); break;
            }
        }
        catch (Exception ex) { Svc.Chat.PrintError($"[AutoDuty] {ex.Message}"); }
    }
}
