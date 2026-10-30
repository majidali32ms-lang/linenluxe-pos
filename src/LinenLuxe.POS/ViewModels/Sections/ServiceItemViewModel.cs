using LinenLuxe.POS.Models;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// One editable row of the price list.
///
/// The rates are held as text, not numbers, because a rate has three states and a
/// decimal only has two: a price, no price at all (the service is not offered for
/// this item — a dash on the printed list), and half-typed. Parsing on every
/// keystroke would turn an empty box into 0.00, which is a very different thing to
/// tell a customer than "we don't press that".
///
/// Nothing here writes to the database. The row remembers what it was loaded with
/// so the screen can show what has changed, save only those rows, and put the rest
/// back if the edit is abandoned.
/// </summary>
public sealed class ServiceItemViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public ServiceItemViewModel(PosItem item, Action onChanged)
    {
        Item = item;
        _onChanged = onChanged;

        _cleanPress = Text(item.CleanPressPrice);
        _pressOnly = Text(item.PressOnlyPrice);
        _isActive = item.IsActive;

        _originalCleanPress = _cleanPress;
        _originalPressOnly = _pressOnly;
        _originalActive = _isActive;
    }

    public PosItem Item { get; }

    public string Name => Item.Name;
    public string Category => Item.Category;

    private readonly string _originalCleanPress;
    private readonly string _originalPressOnly;
    private readonly bool _originalActive;

    private string _cleanPress;
    public string CleanPress
    {
        get => _cleanPress;
        set { if (SetProperty(ref _cleanPress, value)) Changed(); }
    }

    private string _pressOnly;
    public string PressOnly
    {
        get => _pressOnly;
        set { if (SetProperty(ref _pressOnly, value)) Changed(); }
    }

    private bool _isActive;
    /// <summary>
    /// Off means the item stops appearing on the New Order screen. It is never
    /// deleted: orders already taken name the item they were taken for, and a price
    /// list that forgets an item makes last month's invoice unexplainable.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set { if (SetProperty(ref _isActive, value)) Changed(); }
    }

    public bool IsDirty =>
        _cleanPress != _originalCleanPress
        || _pressOnly != _originalPressOnly
        || _isActive != _originalActive;

    /// <summary>
    /// Each box must be a price or empty, and an item must have at least one of the
    /// two: a row with neither cannot be sold, and would sit in the list waiting to
    /// be added to an order for nothing.
    /// </summary>
    public bool IsValid => Readable(_cleanPress) && Readable(_pressOnly)
                           && (Parse(_cleanPress) is not null || Parse(_pressOnly) is not null);

    /// <summary>Says which of the two problems it is, so the fix is obvious.</summary>
    public string Problem
    {
        get
        {
            if (!Readable(_cleanPress)) return $"{Name}: \"{_cleanPress.Trim()}\" is not a price.";
            if (!Readable(_pressOnly)) return $"{Name}: \"{_pressOnly.Trim()}\" is not a price.";
            if (Parse(_cleanPress) is null && Parse(_pressOnly) is null)
                return $"{Name} has no price at all — it needs at least one of the two.";
            return "";
        }
    }

    private static bool Readable(string text) => text.Trim().Length == 0 || Parse(text) is not null;

    /// <summary>Greyed out in the list, so a hidden item is obvious at a glance.</summary>
    public double RowOpacity => IsActive ? 1.0 : 0.45;

    /// <summary>Copies the edited values onto the model, ready to be saved.</summary>
    public void ApplyTo(PosItem target)
    {
        target.CleanPressPrice = Parse(_cleanPress);
        target.PressOnlyPrice = Parse(_pressOnly);
        target.IsActive = _isActive;
    }

    /// <summary>Puts the row back to what the database holds.</summary>
    public void Revert()
    {
        CleanPress = _originalCleanPress;
        PressOnly = _originalPressOnly;
        IsActive = _originalActive;
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(RowOpacity));
        _onChanged();
    }

    /// <summary>
    /// Blank means the service is not offered — a dash, not a zero.
    ///
    /// Digits and one dot, nothing else. The default parse accepts a comma as a
    /// thousands separator, which quietly turned "12,5" — how a good many people
    /// write twelve-fifty — into 125.00. A rate ten times too high looks like a
    /// price rise, not like a typo, and would be charged. Refusing it puts the
    /// mistake on screen in red instead.
    /// </summary>
    public static decimal? Parse(string text)
    {
        var t = text.Trim();
        if (t.Length == 0) return null;

        return decimal.TryParse(t,
                   System.Globalization.NumberStyles.AllowDecimalPoint,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var value)
               && value >= 0
            ? Math.Round(value, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string Text(decimal? value) => value is { } v ? v.ToString("0.##") : "";
}
