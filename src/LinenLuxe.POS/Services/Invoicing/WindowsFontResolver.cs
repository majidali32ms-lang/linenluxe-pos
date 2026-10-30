using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using PdfSharp.Fonts;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// Supplies the invoice's fonts to PDFsharp.
///
/// PDFsharp 6 ships no font handling of its own — without a resolver it throws
/// "No appropriate font found" the moment it lays out the first line, which for
/// us would have been at the counter with a customer waiting. Setting
/// GlobalFontSettings.UseWindowsFontsUnderWindows is NOT enough on its own.
///
/// Two sources, in order:
///   * Great Vibes ships inside the app, because it is not installed on Windows
///     and the "Thank You!" flourish has to match the web invoice everywhere.
///   * Arial comes from the Windows font folder. jsPDF draws the web invoice in
///     Helvetica, and Arial carries the same metrics, so the two documents set
///     identically.
/// </summary>
public sealed class WindowsFontResolver : IFontResolver
{
    public const string ScriptFamily = "Great Vibes";
    private const string ScriptFace = "GreatVibes.ttf";

    private static readonly string FontsFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    // family (lower-case) -> regular, bold, italic, bold-italic
    private static readonly Dictionary<string, string[]> Families = new()
    {
        ["arial"] = ["arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"],
        ["helvetica"] = ["arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"],
        ["georgia"] = ["georgia.ttf", "georgiab.ttf", "georgiai.ttf", "georgiaz.ttf"],
        ["segoe ui"] = ["segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"],
    };

    private const string Fallback = "arial";

    private static readonly ConcurrentDictionary<string, byte[]> Cache = new();

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var key = familyName.ToLowerInvariant().Trim();

        // The script face has one cut; bold/italic requests still get that file.
        if (key == ScriptFamily.ToLowerInvariant()) return new FontResolverInfo(ScriptFace);

        if (!Families.TryGetValue(key, out var faces)) faces = Families[Fallback];

        var index = (isBold, isItalic) switch
        {
            (true, true) => 3,
            (true, false) => 1,
            (false, true) => 2,
            _ => 0,
        };

        var file = faces[index];

        // A style that isn't installed drops to that family's regular cut rather
        // than losing the whole invoice.
        if (!File.Exists(Path.Combine(FontsFolder, file))) file = faces[0];
        if (!File.Exists(Path.Combine(FontsFolder, file)))
        {
            file = Families[Fallback][0];
            if (!File.Exists(Path.Combine(FontsFolder, file))) return null;
        }

        return new FontResolverInfo(file);
    }

    public byte[]? GetFont(string faceName)
        => Cache.GetOrAdd(faceName, Load) is { Length: > 0 } bytes ? bytes : null;

    private static byte[] Load(string faceName)
    {
        if (faceName == ScriptFace) return LoadEmbedded() ?? [];

        var path = Path.Combine(FontsFolder, faceName);
        return File.Exists(path) ? File.ReadAllBytes(path) : [];
    }

    private static byte[]? LoadEmbedded()
    {
        try
        {
            // Named assembly, not the short "pack://application:,,,/…" form: that
            // resolves against whichever assembly is the entry point, so it would
            // find the font in the running POS but not when an invoice is rendered
            // from anywhere else.
            Application.ResourceAssembly ??= typeof(WindowsFontResolver).Assembly;

            var uri = new Uri("pack://application:,,,/LinenLuxe.POS;component/Assets/GreatVibes.ttf");
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is null) return null;

            using (stream)
            {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }
        catch
        {
            return null;   // the invoice falls back to italic Arial for the flourish
        }
    }
}
