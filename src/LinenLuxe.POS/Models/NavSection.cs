namespace LinenLuxe.POS.Models;

/// <summary>One entry in the POS sidebar navigation.</summary>
public sealed class NavSection
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// Segoe MDL2 Assets icon code point (e.g. 0xE80F = Home). Stored as a number
    /// so the source stays plain ASCII; converted to the glyph for binding.
    /// </summary>
    public required int GlyphCode { get; init; }

    /// <summary>The icon character the UI binds to.</summary>
    public string Glyph => char.ConvertFromUtf32(GlyphCode);

    /// <summary>Shortcut shown on the right of the item (e.g. "Ctrl+1").</summary>
    public required string ShortcutHint { get; init; }
}
