using LinenLuxe.POS.Models;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>Which rate from the price list a line uses.</summary>
public enum ServiceType
{
    CleanPress,
    PressOnly,
}

/// <summary>
/// One line in the order being built: an item, the chosen service, a quantity
/// and the rate it is being charged at.
/// </summary>
public sealed class OrderLineViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public OrderLineViewModel(PosItem item, ServiceType type, decimal unitPrice, Action onChanged)
    {
        Item = item;
        Type = type;
        ListPrice = unitPrice;
        _unitPrice = unitPrice;
        _onChanged = onChanged;
        _quantity = 1;
    }

    public PosItem Item { get; }
    public ServiceType Type { get; }

    /// <summary>
    /// What the price list said when this line was added. Kept so the screen can
    /// show that a rate was changed, and what it was changed from.
    /// </summary>
    public decimal ListPrice { get; }

    private decimal _unitPrice;
    /// <summary>
    /// The rate actually being charged. Editable because the price list is a
    /// starting point, not the last word — its own note says the final price
    /// follows inspection, so a heavier or badly stained item gets repriced at
    /// the counter.
    /// </summary>
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            // Never below zero: a negative rate would quietly subtract from the
            // order and read as a discount nobody agreed.
            if (!SetProperty(ref _unitPrice, Math.Max(0m, value))) return;
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(LineTotalDisplay));
            OnPropertyChanged(nameof(UnitPriceDisplay));
            OnPropertyChanged(nameof(IsRepriced));
            OnPropertyChanged(nameof(RepricedNote));
            _onChanged();
        }
    }

    /// <summary>True once the rate differs from the price list.</summary>
    public bool IsRepriced => UnitPrice != ListPrice;

    /// <summary>e.g. "was AED 60" — shown only when the rate was changed.</summary>
    public string RepricedNote => IsRepriced ? $"was AED {ListPrice:0.##}" : "";

    public string Name => Item.Name;
    public string TypeLabel => Type == ServiceType.CleanPress ? "Clean & Press" : "Press Only";

    private int _quantity;
    /// <summary>Always at least 1; the line is removed instead of dropping to 0.</summary>
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (!SetProperty(ref _quantity, Math.Max(1, value))) return;
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(LineTotalDisplay));
            _onChanged();
        }
    }

    public decimal LineTotal => UnitPrice * Quantity;

    // ----- Display -----
    public string UnitPriceDisplay => $"AED {UnitPrice:0.##}";
    public string LineTotalDisplay => $"AED {LineTotal:0.##}";
}
