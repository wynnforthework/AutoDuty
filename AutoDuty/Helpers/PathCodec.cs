using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AutoDuty.Data;
using Newtonsoft.Json;

namespace AutoDuty.Helpers;

internal static class PathCodec
{
    internal const string Prefix = "ADP1.";
    internal const int MaxBytes = 2 * 1024 * 1024;

    internal static string Encode(PathFile file)
    {
        PathValidator.ValidateStructure(file);
        byte[] json = Encoding.UTF8.GetBytes(PathJson.WriteFile(file, Formatting.None));
        if (json.Length > MaxBytes) throw new ArgumentException("Path exceeds sharing size limit");
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, true)) deflate.Write(json);
        byte[] compressed = output.ToArray();
        return Prefix + Convert.ToBase64String(compressed).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + Crc32(compressed).ToString("x8", CultureInfo.InvariantCulture);
    }

    internal static PathFile? Decode(string input, out string error)
    {
        error = "";
        try
        {
            string[] parts = input.Trim().Split('.');
            if (parts.Length != 3 || parts[0] != "ADP1" || parts[1].Length > MaxBytes * 2 || parts[2].Length != 8
                || parts[1].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw new FormatException("Invalid ADP1 envelope");
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            byte[] compressed = Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '='));
            if (!uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint crc) || Crc32(compressed) != crc) throw new FormatException("Checksum mismatch: incomplete or damaged path");
            using var source = new MemoryStream(compressed);
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = deflate.Read(buffer)) > 0)
            {
                if (output.Length + read > MaxBytes) throw new FormatException("Decoded path exceeds size limit");
                output.Write(buffer, 0, read);
            }
            return PathJson.ReadFile(new UTF8Encoding(false, true).GetString(output.ToArray()));
        }
        catch (Exception ex) when (ex is FormatException or IOException or JsonException or ArgumentException or OverflowException)
        {
            error = ex.Message;
            return null;
        }
    }

    internal static string[] Split(string encoded, int chunkSize = 450)
    {
        if (!encoded.StartsWith(Prefix, StringComparison.Ordinal)) throw new ArgumentException("Invalid path envelope");
        if (chunkSize < 80 || chunkSize > 450) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        int size = chunkSize - 40;
        int total = (encoded.Length + size - 1) / size;
        string id = encoded.Split('.')[^1];
        return Enumerable.Range(0, total).Select(i => $"ADP1C{i + 1}/{total}:{id} {encoded.Substring(i * size, Math.Min(size, encoded.Length - i * size))}").ToArray();
    }

    internal static bool TryAssemble(IEnumerable<string> chunks, out string encoded, out string error)
    {
        encoded = error = "";
        var parts = chunks.ToArray();
        if (parts.Length == 0 || parts.Length > 10000) { error = "No chunks or too many chunks"; return false; }
        int total = -1;
        string? id = null;
        var builder = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            var split = parts[i].Trim().Split(' ', 2);
            var header = split[0].StartsWith("ADP1C", StringComparison.Ordinal) ? split[0][5..].Split(['/', ':']) : [];
            if (split.Length != 2 || header.Length != 3 || !int.TryParse(header[0], out int n) || !int.TryParse(header[1], out int count)
                || n != i + 1 || count <= 0 || count > 10000 || (total != -1 && count != total) || (id != null && header[2] != id))
            { error = "Chunks missing, mixed, duplicated or out of order"; return false; }
            total = count; id = header[2]; builder.Append(split[1]);
            if (builder.Length > MaxBytes * 2) { error = "Chunks exceed size limit"; return false; }
        }
        if (parts.Length != total) { error = $"Waiting for chunks: {parts.Length}/{total}"; return false; }
        encoded = builder.ToString();
        if (Decode(encoded, out error) == null) { encoded = ""; return false; }
        return true;
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        return ~crc;
    }
}
