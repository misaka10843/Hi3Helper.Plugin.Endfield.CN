using System.IO;

namespace Hi3Helper.Hypergryph.Core.Utils;

/// <summary>
///     Parser for Hg config.ini values.
/// </summary>
public static class ConfigTool
{
    public static string ReadConfig(string filePath)
    {
        return HgCrypto.DecryptFileToString(filePath);
    }

    public static string? ParseVersion(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("version=")) return trimmed.Split('=')[1].Trim();
        }

        return null;
    }
}
