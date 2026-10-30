namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// Generic "coming soon" section shown for screens not yet built.
/// Each real screen replaces its placeholder in a later micro-step.
/// </summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    private readonly int _glyphCode;

    public PlaceholderViewModel(string title, int glyphCode, string description, string shortcut)
    {
        Title = title;
        _glyphCode = glyphCode;
        Description = description;
        Shortcut = shortcut;
    }

    public string Title { get; }
    /// <summary>Segoe MDL2 Assets icon character.</summary>
    public string Glyph => char.ConvertFromUtf32(_glyphCode);
    public string Description { get; }
    public string Shortcut { get; }
    public bool HasShortcut => Shortcut.Length > 0;
    public string Note => "This screen is on the way.";
}
