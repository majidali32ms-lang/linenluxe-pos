namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// One of the In use / Hidden / All items chips, carrying how many items it holds.
///
/// The count is the whole point. With nothing hidden, "In use" and "All items"
/// show an identical list and the screen looks stuck; "In use 163 · Hidden 0"
/// answers that before it is asked.
/// </summary>
public sealed class ShownOption(string key) : ViewModelBase
{
    public string Key { get; } = key;

    private int _count;
    public int Count
    {
        get => _count;
        set { if (SetProperty(ref _count, value)) OnPropertyChanged(nameof(Label)); }
    }

    public string Label => $"{Key}  {Count}";
}
