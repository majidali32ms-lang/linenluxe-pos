using System.Collections.ObjectModel;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Data;
using LinenLuxe.POS.Services.Invoicing;
using Supabase.Postgrest;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// Orders screen — every order taken at the counter, newest first.
///
/// Its job is to get staff back to an order a customer has returned to: opening
/// one hands it to the New Order screen in edit mode, where it keeps its P-number
/// so the reprinted invoice replaces the original.
/// </summary>
public sealed class OrdersViewModel : ViewModelBase
{
    /// <summary>A day's counter work is well inside this; the queue is not an archive.</summary>
    private const int MaxOrders = 200;

    private readonly Action<Guid> _openOrder;
    private List<PosOrder> _all = [];

    /// <param name="search">
    /// Pre-filled when the screen was opened from the top bar, so what was typed
    /// there is already applied by the time the list appears.
    /// </param>
    public OrdersViewModel(Action<Guid> openOrder, string? search = null)
    {
        _openOrder = openOrder;
        _search = search ?? "";

        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        OpenCommand = new RelayCommand(p => Open(p as PosOrder ?? SelectedOrder));
        AdvanceStatusCommand = new RelayCommand(async () => await MoveStatusAsync(+1), () => CanAdvance);
        RewindStatusCommand = new RelayCommand(async () => await MoveStatusAsync(-1), () => CanRewind);
        SetFilterCommand = new RelayCommand(p => StatusFilter = p?.ToString() ?? AllStatuses);
        PrintCommand = new RelayCommand(async () => await PrintAsync(receipt: false), () => CanOpen);
        PrintReceiptCommand = new RelayCommand(async () => await PrintAsync(receipt: true), () => CanOpen);

        StatusFilters = [AllStatuses, .. OrderStatus.Steps.Select(s => s.Label)];

        _ = LoadAsync();
    }

    // ================= Status =================

    public const string AllStatuses = "All";

    /// <summary>"All" plus every step, as labels — the chips along the top.</summary>
    public IReadOnlyList<string> StatusFilters { get; }

    private string _statusFilter = AllStatuses;
    public string StatusFilter
    {
        get => _statusFilter;
        set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); }
    }

    public bool CanAdvance => SelectedOrder is not null
                              && OrderStatus.Next(SelectedOrder.Status) is not null;

    public bool CanRewind => SelectedOrder is not null
                             && OrderStatus.Previous(SelectedOrder.Status) is not null;

    /// <summary>
    /// The buttons name the step they will move to — "Mark as Ready", not
    /// "Advance". Staff should not have to remember the order of the lifecycle to
    /// know what a button is about to do.
    /// </summary>
    public string AdvanceLabel =>
        SelectedOrder is not null && OrderStatus.Next(SelectedOrder.Status) is { } next
            ? $"Mark as {OrderStatus.Label(next)}   →"
            : "Next step   →";

    public string RewindLabel =>
        SelectedOrder is not null && OrderStatus.Previous(SelectedOrder.Status) is { } back
            ? $"←   Back to {OrderStatus.Label(back)}"
            : "←   Back a step";

    public RelayCommand AdvanceStatusCommand { get; }
    public RelayCommand RewindStatusCommand { get; }
    public RelayCommand SetFilterCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand PrintReceiptCommand { get; }

    /// <summary>
    /// Reprint the highlighted order — the A4 invoice (Ctrl+P) or the 80 mm till
    /// receipt (Ctrl+R). Two documents, not two sizes of one: the invoice is
    /// what gets emailed and filed, the receipt is what goes across the counter.
    ///
    /// The lines are fetched fresh rather than cached with the list: the queue
    /// shows dozens of orders and almost none of them will ever be printed, so
    /// loading every order's items up front would be a lot of work for nothing.
    /// </summary>
    private async Task PrintAsync(bool receipt)
    {
        var order = SelectedOrder;
        if (order is null) return;

        Error = "";
        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var id = order.Id;
            var rows = await db.Client.From<PosOrderItem>().Where(i => i.OrderId == id).Get();
            var items = rows.Models.OrderBy(i => i.CreatedAt).ToList();

            if (items.Count == 0)
            {
                Error = $"Order {order.OrderNumber} has no items to print.";
                return;
            }

            if (receipt) ReceiptService.Print(order, items);
            else InvoiceService.Print(order, items);
        }
        catch (Exception ex)
        {
            Error = $"Could not print the {(receipt ? "receipt" : "invoice")}. " + ex.Message;
        }
    }

    /// <summary>
    /// Move the highlighted order one step along the lifecycle, or one step back
    /// when someone taps too far.
    ///
    /// The status column alone is written — not the whole row — so advancing an
    /// order can never disturb its money or its customer.
    /// </summary>
    private async Task MoveStatusAsync(int direction)
    {
        var order = SelectedOrder;
        if (order is null) return;

        var next = direction > 0 ? OrderStatus.Next(order.Status) : OrderStatus.Previous(order.Status);
        if (next is null) return;

        var previous = order.Status;
        Error = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var id = order.Id;
            await db.Client.From<PosOrder>()
                .Where(o => o.Id == id)
                .Set(o => o.Status, next)
                .Update();

            order.Status = next;
            RefreshRow(order);
        }
        catch (Exception ex)
        {
            order.Status = previous;   // the screen must not claim a move that didn't happen
            RefreshRow(order);
            Error = "Could not change the status. " + ex.Message;
        }
    }

    /// <summary>
    /// Re-seat the row so its bindings pick up the new status. PosOrder is a plain
    /// database model with no change notification of its own, and giving it any
    /// would mean more properties the Supabase client tries to send as columns.
    /// </summary>
    private void RefreshRow(PosOrder order)
    {
        var index = Orders.IndexOf(order);
        if (index >= 0)
        {
            Orders.RemoveAt(index);
            Orders.Insert(index, order);
            SelectedOrder = order;
        }

        RaiseStatusButtonsChanged();

        // A row that no longer matches the chip in use should leave the list.
        if (StatusFilter != AllStatuses) ApplyFilter();
    }

    private void RaiseStatusButtonsChanged()
    {
        OnPropertyChanged(nameof(CanAdvance));
        OnPropertyChanged(nameof(CanRewind));
        OnPropertyChanged(nameof(AdvanceLabel));
        OnPropertyChanged(nameof(RewindLabel));
    }

    public ObservableCollection<PosOrder> Orders { get; } = [];

    private PosOrder? _selectedOrder;
    public PosOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (!SetProperty(ref _selectedOrder, value)) return;
            OnPropertyChanged(nameof(CanOpen));
            RaiseStatusButtonsChanged();
        }
    }

    public bool CanOpen => SelectedOrder is not null;

    private string _search = "";
    /// <summary>Order number, customer name or phone. Filters as you type.</summary>
    public string Search
    {
        get => _search;
        set
        {
            if (!SetProperty(ref _search, value)) return;
            OnPropertyChanged(nameof(IsSearchEmpty));
            ApplyFilter();
        }
    }

    public bool IsSearchEmpty => string.IsNullOrEmpty(_search);

    private bool _loading = true;
    public bool Loading
    {
        get => _loading;
        private set
        {
            if (!SetProperty(ref _loading, value)) return;
            OnPropertyChanged(nameof(ShowList));
            OnPropertyChanged(nameof(ShowEmpty));
        }
    }

    private string _error = "";
    public string Error
    {
        get => _error;
        private set
        {
            if (!SetProperty(ref _error, value)) return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowList));
            OnPropertyChanged(nameof(ShowEmpty));
        }
    }

    public bool HasError => Error.Length > 0;
    public bool ShowList => !Loading && !HasError && Orders.Count > 0;
    public bool ShowEmpty => !Loading && !HasError && Orders.Count == 0;

    public string CountText => Loading
        ? "Loading orders…"
        : Orders.Count == _all.Count
            ? $"{_all.Count} order{(_all.Count == 1 ? "" : "s")}"
            : $"{Orders.Count} of {_all.Count} orders";

    public RelayCommand RefreshCommand { get; }
    public RelayCommand OpenCommand { get; }

    private void Open(PosOrder? order)
    {
        if (order is not null) _openOrder(order.Id);
    }

    private async Task LoadAsync()
    {
        Loading = true;
        Error = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var result = await db.Client
                .From<PosOrder>()
                .Order("created_at", Constants.Ordering.Descending)
                .Limit(MaxOrders)
                .Get();

            _all = result.Models;
            Loading = false;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Loading = false;
            Error = "Could not load the orders. " + ex.Message;
        }
    }

    private void ApplyFilter()
    {
        var q = Search.Trim();

        // Matching the number, the name and the phone together is what makes one
        // box enough: staff have whichever of the three the customer offers.
        IEnumerable<PosOrder> list = q.Length == 0
            ? _all
            : _all.Where(o =>
                o.OrderNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                || o.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.CustomerPhone ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));

        if (StatusFilter != AllStatuses)
            list = list.Where(o => o.StatusLabel == StatusFilter);

        Orders.Clear();
        foreach (var order in list) Orders.Add(order);

        SelectedOrder = Orders.FirstOrDefault();

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowEmpty));
    }
}
