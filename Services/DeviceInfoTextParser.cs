using System;
using System.Text.RegularExpressions;

namespace Proxmark3GUI.Services;

/// <summary>
/// Parses common key/value layouts emitted by Proxmark3 client status commands.
/// Client output is not a stable wire schema, so the accepted layouts are tolerant.
/// </summary>
public static class DeviceInfoTextParser
{
    private static readonly Regex DottedSeparator = new(@"^(.*?)\.{2,}\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex WhitespaceSeparator = new(@"^(.+?)\s{2,}(.+)$", RegexOptions.Compiled);

    public static string[] SplitKeyValue(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0)
            return new[] { line[..colonIndex], line[(colonIndex + 1)..] };

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex >= 0)
            return new[] { line[..equalsIndex], line[(equalsIndex + 1)..] };

        var match = DottedSeparator.Match(line);
        if (match.Success)
            return new[] { match.Groups[1].Value, match.Groups[2].Value };

        match = WhitespaceSeparator.Match(line);
        if (match.Success)
            return new[] { match.Groups[1].Value, match.Groups[2].Value };

        return Array.Empty<string>();
    }
}
