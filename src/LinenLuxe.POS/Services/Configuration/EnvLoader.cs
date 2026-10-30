using System.IO;

namespace LinenLuxe.POS.Services.Configuration;

/// <summary>
/// Minimal ".env" file reader (KEY=VALUE per line, "#" comments). No external
/// dependency. It searches UPWARD from the running app folder so it also works
/// during development, where the .env lives at the solution root while the app
/// runs from bin/Debug/net10.0-windows.
/// </summary>
public static class EnvLoader
{
    public static IReadOnlyDictionary<string, string> Load(string? startDir = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = FindEnvFile(startDir ?? AppContext.BaseDirectory);
        if (path is null) return result;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');
            result[key] = value;
        }
        return result;
    }

    // Walk up the directory tree (a few levels) looking for a .env file.
    private static string? FindEnvFile(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
