using System.Collections.ObjectModel;
using System.IO;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Configuration;
using LinenLuxe.POS.Services.Data;
using LinenLuxe.POS.Services.Invoicing;
using LinenLuxe.POS.Services.Search;
using Supabase.Postgrest;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// New Order screen — the heart of the POS. Left: pick items from the price list.
/// Right: the order being built, with live totals.
///
/// The same screen edits a placed order. A customer who comes back to add a shirt
/// wants their existing order changed, not a second one, so editing reuses this
/// whole screen and updates the order in place — keeping its P-number, so the new
/// invoice replaces the old one instead of competing with it.
/// </summary>
public sealed class NewOrderViewModel : ViewModelBase
{
    private const string AllCategories = "All";
    private const decimal VatRate = 0.05m;      // UAE VAT, matches the web invoice
    private const decimal ExpressRate = 0.50m;  // Express service adds 50% of the subtotal

    private List<PosItem> _all = [];

    /// <summary>The signed-in staff member — stored on the order as created_by.</summary>
    private readonly Profile _staff;

    /// <summary>The order being edited, or null when building a new one.</summary>
    private PosOrder? _editing;

    /// <summary>
    /// The lines as they were when the edit began. Kept so a failed update can put
    /// them back: updating replaces the lines wholesale, and without this a failure
    /// half-way would leave the order with no items at all.
    /// </summary>
    private List<PosOrderItem> _editingOriginalLines = [];

    public NewOrderViewModel(Profile staff, Guid? editOrderId = null)
    {
        _staff = staff;
        Categories = [AllCategories];

        AddSelectedCommand = new RelayCommand(_ => AddSelected());
        SaveOrderCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        IncreaseQtyCommand = new RelayCommand(p => ChangeQty(p as OrderLineViewModel, +1));
        DecreaseQtyCommand = new RelayCommand(p => ChangeQty(p as OrderLineViewModel, -1));
        RemoveLineCommand = new RelayCommand(p => RemoveLine(p as OrderLineViewModel));
        ClearOrderCommand = new RelayCommand(ClearOrder);
        ToggleVatCommand = new RelayCommand(() => VatEnabled = !VatEnabled);
        ToggleExpressCommand = new RelayCommand(() => IsExpress = !IsExpress);
        SetPaymentCommand = new RelayCommand(p => PaymentMethod = p?.ToString() ?? "cod");
        SetPaidCommand = new RelayCommand(p => IsPaid = p?.ToString() == "paid");
        TogglePaidCommand = new RelayCommand(() => IsPaid = !IsPaid);
        SetServiceTypeCommand = new RelayCommand(p =>
            ServiceType = Equals(p?.ToString(), "press") ? ServiceType.PressOnly : ServiceType.CleanPress);
        OpenCustomerPickerCommand = new RelayCommand(OpenCustomerPicker);
        ClearCustomerCommand = new RelayCommand(() => Customer = null);

        CancelEditCommand = new RelayCommand(StartNextOrder);

        Lines.CollectionChanged += (_, _) => RaiseOrderChanged();

        _ = LoadAsync(editOrderId);
    }

    // ================= Editing a placed order =================

    public bool IsEditing => _editing is not null;

    /// <summary>"New Order", or "Edit Order P00007" once one is loaded.</summary>
    public string ScreenTitle => IsEditing ? $"Edit Order {_editing!.OrderNumber}" : "New Order";

    /// <summary>Reassures staff that changing an order keeps its number.</summary>
    public string EditNote => IsEditing
        ? $"Editing a placed order — it keeps invoice number {_editing!.OrderNumber}."
        : "";

    public RelayCommand CancelEditCommand { get; }

    private void RaiseEditingChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(ScreenTitle));
        OnPropertyChanged(nameof(EditNote));
        OnPropertyChanged(nameof(SaveLabel));
    }

    /// <summary>
    /// Fill the screen from a placed order so it can be changed.
    ///
    /// The customer is rebuilt from the snapshot stored on the order rather than
    /// looked up again: that snapshot is what the original invoice printed, and a
    /// customer who has since changed their phone number must not silently change
    /// an order that was already agreed.
    /// </summary>
    private async Task LoadOrderAsync(Guid orderId)
    {
        var db = SupabaseService.Instance;
        await db.InitializeAsync();

        var orderRows = await db.Client.From<PosOrder>().Where(o => o.Id == orderId).Get();
        var order = orderRows.Models.FirstOrDefault();
        if (order is null)
        {
            Error = "That order could not be opened. It may have been removed.";
            return;
        }

        var itemRows = await db.Client.From<PosOrderItem>().Where(i => i.OrderId == orderId).Get();
        var items = itemRows.Models.OrderBy(i => i.CreatedAt).ToList();

        _editing = order;
        _editingOriginalLines = items;

        Customer = new OrderCustomer
        {
            ProfileId = order.ProfileId,
            PosCustomerId = order.PosCustomerId,
            Name = order.CustomerName,
            Phone = order.CustomerPhone,
        };

        IsExpress = order.IsExpress;
        PaymentMethod = order.PaymentMethod;
        IsPaid = order.IsPaid;
        VatEnabled = order.VatEnabled;
        Discount = order.Discount;
        Notes = order.Notes ?? "";

        Lines.Clear();
        foreach (var item in items) Lines.Add(ToLine(item));

        RaiseOrderChanged();
        RaiseEditingChanged();
    }

    /// <summary>
    /// Rebuild an editable line from a stored one. The price list is searched for
    /// the original item so quantity changes keep working, but the stored snapshot
    /// wins for name and rate — a price rise since the order was taken must not
    /// quietly re-price what the customer already agreed.
    /// </summary>
    private OrderLineViewModel ToLine(PosOrderItem stored)
    {
        var item = _all.FirstOrDefault(i => i.Id == stored.PosItemId)
                   ?? new PosItem
                   {
                       Id = stored.PosItemId ?? Guid.Empty,
                       Name = stored.ItemName,
                       Category = stored.Category ?? "",
                   };

        var type = stored.ServiceType == "press_only" ? ServiceType.PressOnly : ServiceType.CleanPress;

        // Rebuilt at its ORIGINAL rate, then repriced, so the line remembers it was
        // changed and still shows "was AED 60" after the order is reopened.
        var line = new OrderLineViewModel(item, type, stored.EstUnitPrice ?? stored.UnitPrice, RaiseOrderChanged)
        {
            Quantity = stored.Quantity,
        };
        if (stored.EstUnitPrice is not null) line.UnitPrice = stored.UnitPrice;
        return line;
    }

    // ================= Customer (Alt+C) =================

    private OrderCustomer? _customer;
    /// <summary>Registered website customer or walk-in; null until chosen.</summary>
    public OrderCustomer? Customer
    {
        get => _customer;
        private set
        {
            if (!SetProperty(ref _customer, value)) return;
            OnPropertyChanged(nameof(HasCustomer));
            OnPropertyChanged(nameof(NoCustomer));
            OnPropertyChanged(nameof(CanSave));

            ClearFirstOrderDiscount();
            if (value is not null && _editing is null) _ = CheckFirstOrderAsync(value);
        }
    }

    public bool HasCustomer => Customer is not null;
    public bool NoCustomer => Customer is null;

    // ----- First order, first discount -----

    private bool _firstOrderDiscount;
    /// <summary>
    /// On when the chosen customer has never ordered here. The whole point of a
    /// first-order discount is the second visit, so the till applies it rather than
    /// leaving whoever is at the counter to remember and offer it — the busier the
    /// shop, the less often it would be offered, which is exactly backwards.
    /// </summary>
    public bool FirstOrderDiscount
    {
        get => _firstOrderDiscount;
        private set
        {
            if (!SetProperty(ref _firstOrderDiscount, value)) return;
            OnPropertyChanged(nameof(FirstOrderDiscountNote));
            RaiseOrderChanged();
        }
    }

    public string FirstOrderDiscountNote =>
        $"First order — {ShopPolicy.FirstOrderDiscountPercent} off";

    /// <summary>Kept so the discount can be taken off without the check running again.</summary>
    private void ClearFirstOrderDiscount()
    {
        if (!_firstOrderDiscount) return;
        _firstOrderDiscount = false;
        OnPropertyChanged(nameof(FirstOrderDiscount));
        Discount = 0;
    }

    /// <summary>
    /// Asks the database, not the list on screen: the Customers screen holds the
    /// most recent 500 orders, and an old customer whose orders have scrolled past
    /// that would otherwise be handed a first-order discount by mistake.
    /// </summary>
    private async Task CheckFirstOrderAsync(OrderCustomer customer)
    {
        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var query = db.Client.From<PosOrder>();
            var existing = customer.PosCustomerId is { } posId
                ? await query.Where(o => o.PosCustomerId == posId).Limit(1).Get()
                : customer.ProfileId is { } profileId
                    ? await query.Where(o => o.ProfileId == profileId).Limit(1).Get()
                    : null;

            // A customer with neither id is typed in for this order alone and has no
            // history to look up; no discount rather than a guessed one.
            if (existing is null) return;

            // They may have chosen someone else while this was in flight.
            if (!ReferenceEquals(Customer, customer)) return;

            if (existing.Models.Count == 0) FirstOrderDiscount = true;
        }
        catch
        {
            // A discount is not worth an error message in the middle of an order.
            // Left off, which is the same as today's behaviour.
        }
    }

    private CustomerPickerViewModel? _customerPicker;
    /// <summary>Non-null while the Alt+C overlay is open.</summary>
    public CustomerPickerViewModel? CustomerPicker
    {
        get => _customerPicker;
        private set
        {
            if (!SetProperty(ref _customerPicker, value)) return;
            OnPropertyChanged(nameof(IsCustomerPickerOpen));
        }
    }

    public bool IsCustomerPickerOpen => CustomerPicker is not null;

    public void OpenCustomerPicker()
        => CustomerPicker = new CustomerPickerViewModel(
            onChosen: c => { Customer = c; CustomerPicker = null; },
            onCancel: () => CustomerPicker = null);

    public void CloseCustomerPicker() => CustomerPicker = null;

    // ================= Item picker (left) =================

    public ObservableCollection<PosItem> PickerItems { get; } = [];
    public ObservableCollection<string> Categories { get; }

    private string _search = "";
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

    private string _selectedCategory = AllCategories;
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value)) ApplyFilter(); }
    }

    private PosItem? _selectedPickerItem;
    /// <summary>The highlighted row; Enter adds it to the order.</summary>
    public PosItem? SelectedPickerItem
    {
        get => _selectedPickerItem;
        set => SetProperty(ref _selectedPickerItem, value);
    }

    private ServiceType _serviceType = ServiceType.CleanPress;
    /// <summary>Which rate is used when an item is added.</summary>
    public ServiceType ServiceType
    {
        get => _serviceType;
        set
        {
            if (!SetProperty(ref _serviceType, value)) return;
            OnPropertyChanged(nameof(IsCleanPress));
            OnPropertyChanged(nameof(IsPressOnly));
        }
    }

    public bool IsCleanPress => ServiceType == ServiceType.CleanPress;
    public bool IsPressOnly => ServiceType == ServiceType.PressOnly;

    private string _hint = "";
    /// <summary>Short feedback line under the picker (e.g. why an item couldn't be added).</summary>
    public string Hint
    {
        get => _hint;
        private set { if (SetProperty(ref _hint, value)) OnPropertyChanged(nameof(HasHint)); }
    }

    public bool HasHint => Hint.Length > 0;

    private bool _loading = true;
    public bool Loading
    {
        get => _loading;
        private set { if (SetProperty(ref _loading, value)) OnPropertyChanged(nameof(ShowPicker)); }
    }

    private string _error = "";
    public string Error
    {
        get => _error;
        private set
        {
            if (!SetProperty(ref _error, value)) return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowPicker));
        }
    }

    public bool HasError => Error.Length > 0;
    public bool ShowPicker => !Loading && !HasError;

    // ================= The order (right) =================

    public ObservableCollection<OrderLineViewModel> Lines { get; } = [];

    private OrderLineViewModel? _selectedLine;
    /// <summary>The cart line that + / - adjust.</summary>
    public OrderLineViewModel? SelectedLine
    {
        get => _selectedLine;
        set => SetProperty(ref _selectedLine, value);
    }

    public bool HasLines => Lines.Count > 0;
    public bool IsEmpty => Lines.Count == 0;

    public string ItemCountText
    {
        get
        {
            var pieces = Lines.Sum(l => l.Quantity);
            return pieces == 1 ? "1 piece" : $"{pieces} pieces";
        }
    }

    // ----- Order options -----
    private bool _vatEnabled;
    /// <summary>Ctrl+T. 5% UAE VAT on this order.</summary>
    public bool VatEnabled
    {
        get => _vatEnabled;
        set { if (SetProperty(ref _vatEnabled, value)) RaiseOrderChanged(); }
    }

    private bool _isExpress;
    /// <summary>Alt+U — Express service: rush turnaround, adds 50% of the subtotal.</summary>
    public bool IsExpress
    {
        get => _isExpress;
        set { if (SetProperty(ref _isExpress, value)) RaiseOrderChanged(); }
    }

    private string _paymentMethod = "cod";
    /// <summary>Alt+P — 'cod', 'online' or 'card'.</summary>
    public string PaymentMethod
    {
        get => _paymentMethod;
        set
        {
            if (!SetProperty(ref _paymentMethod, value)) return;
            OnPropertyChanged(nameof(IsCod));
            OnPropertyChanged(nameof(IsOnline));
            OnPropertyChanged(nameof(IsCard));
        }
    }

    public bool IsCod => PaymentMethod == "cod";
    public bool IsOnline => PaymentMethod == "online";
    public bool IsCard => PaymentMethod == "card";

    private string _notes = "";
    /// <summary>
    /// Ctrl+M — anything about this order the next person needs to know: "stain on
    /// collar", "no starch", "customer collecting Friday". Stored on the order, not
    /// printed on the invoice; it is a note between staff, not a promise to the
    /// customer.
    /// </summary>
    public string Notes
    {
        get => _notes;
        set
        {
            if (!SetProperty(ref _notes, value)) return;
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(IsNotesEmpty));
        }
    }

    public bool HasNotes => Notes.Trim().Length > 0;

    /// <summary>Drives the placeholder, since the box is empty far more often than not.</summary>
    public bool IsNotesEmpty => Notes.Length == 0;

    private bool _isPaid;
    /// <summary>
    /// Alt+S — has the customer paid yet? Separate from the payment method: a COD
    /// order can be settled at the counter, and an online order can still be
    /// awaiting the transfer. The invoice prints this as Paid / Payment Pending.
    /// </summary>
    public bool IsPaid
    {
        get => _isPaid;
        set
        {
            if (!SetProperty(ref _isPaid, value)) return;
            OnPropertyChanged(nameof(IsPending));
        }
    }

    public bool IsPending => !IsPaid;

    private decimal _discount;
    /// <summary>
    /// Ctrl+D — flat AED discount, never more than the subtotal. Typing one by hand
    /// takes over from the automatic first-order discount: the figure on screen has
    /// to be the one the counter chose, not one that quietly rewrites itself when
    /// the next item goes in the basket.
    /// </summary>
    public decimal Discount
    {
        get => _discount;
        set
        {
            var wanted = Math.Max(0, value);
            if (_firstOrderDiscount && wanted != _discount)
            {
                _firstOrderDiscount = false;
                OnPropertyChanged(nameof(FirstOrderDiscount));
            }
            if (SetProperty(ref _discount, wanted)) RaiseOrderChanged();
        }
    }

    // ----- Totals -----
    // Order of the maths: subtotal → + express (50%) → − discount → + VAT.
    //
    // Each figure is rounded to fils as it is produced, not just when displayed,
    // because all five are stored on the order and reprinted on the invoice. If
    // only the display were rounded, a saved order could show a total that its
    // own parts don't add up to.
    private static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public decimal Subtotal => Money(Lines.Sum(l => l.LineTotal));
    public decimal ExpressSurcharge => IsExpress ? Money(Subtotal * ExpressRate) : 0m;
    public decimal GrossBeforeDiscount => Subtotal + ExpressSurcharge;
    public decimal DiscountAmount => Money(Math.Min(Discount, GrossBeforeDiscount));
    public decimal TaxableBase => GrossBeforeDiscount - DiscountAmount;
    public decimal VatAmount => VatEnabled ? Money(TaxableBase * VatRate) : 0m;
    public decimal Total => TaxableBase + VatAmount;

    // ----- Saving -----
    private bool _saving;
    /// <summary>True while the order is being written to the database.</summary>
    public bool Saving
    {
        get => _saving;
        private set
        {
            if (!SetProperty(ref _saving, value)) return;
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(SaveLabel));
        }
    }

    /// <summary>An order needs at least one item and a customer — the database
    /// rejects a row with neither a profile nor a walk-in attached.</summary>
    public bool CanSave => HasLines && HasCustomer && !Saving;

    public string SaveLabel => Saving
        ? "Saving…"
        : IsEditing ? "Update Order & Invoice   ·   Ctrl+Enter"
                    : "Save Order & Invoice   ·   Ctrl+Enter";

    private string _savedMessage = "";
    /// <summary>Confirmation of the last saved order, e.g. "Order P00007 saved".</summary>
    public string SavedMessage
    {
        get => _savedMessage;
        private set { if (SetProperty(ref _savedMessage, value)) OnPropertyChanged(nameof(HasSavedMessage)); }
    }

    public bool HasSavedMessage => SavedMessage.Length > 0;

    private string _saveError = "";
    public string SaveError
    {
        get => _saveError;
        private set { if (SetProperty(ref _saveError, value)) OnPropertyChanged(nameof(HasSaveError)); }
    }

    public bool HasSaveError => SaveError.Length > 0;

    /// <summary>e.g. " Express service — 50% of AED 210.00".</summary>
    public string ExpressLabel => $" Express service — 50% of AED {Subtotal:0.00}";

    public string SubtotalDisplay => $"AED {Subtotal:0.00}";
    public string ExpressDisplay => $"+ AED {ExpressSurcharge:0.00}";
    public string DiscountDisplay => $"− AED {DiscountAmount:0.00}";
    public string VatDisplay => $"AED {VatAmount:0.00}";
    public string TotalDisplay => $"AED {Total:0.00}";
    public bool HasDiscount => DiscountAmount > 0;

    // ================= Commands =================
    public RelayCommand AddSelectedCommand { get; }
    public RelayCommand IncreaseQtyCommand { get; }
    public RelayCommand DecreaseQtyCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand ClearOrderCommand { get; }
    public RelayCommand ToggleVatCommand { get; }
    public RelayCommand ToggleExpressCommand { get; }
    public RelayCommand SetPaymentCommand { get; }
    public RelayCommand SetPaidCommand { get; }
    public RelayCommand TogglePaidCommand { get; }
    public RelayCommand SetServiceTypeCommand { get; }
    public RelayCommand OpenCustomerPickerCommand { get; }
    public RelayCommand ClearCustomerCommand { get; }
    public RelayCommand SaveOrderCommand { get; }

    // ================= Behaviour =================

    private async Task LoadAsync(Guid? editOrderId)
    {
        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var result = await db.Client
                .From<PosItem>()
                .Where(i => i.IsActive == true)
                .Order("sort_order", Constants.Ordering.Ascending)
                .Limit(1000)
                .Get();

            _all = result.Models;
            foreach (var c in _all.Select(i => i.Category).Distinct())
                Categories.Add(c);

            Loading = false;
            ApplyFilter();

            // After the price list, so an edited order's lines can be matched back
            // to their price-list items.
            if (editOrderId is { } id) await LoadOrderAsync(id);
        }
        catch (Exception ex)
        {
            Loading = false;
            Error = "Could not load the price list. " + ex.Message;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<PosItem> list = _all;

        if (SelectedCategory != AllCategories)
            list = list.Where(i => i.Category == SelectedCategory);

        // Relevance search — "bra" must not pull in "Shoe Cleaning - Branded".
        var results = ItemSearch.Filter(list, Search);

        PickerItems.Clear();
        foreach (var i in results) PickerItems.Add(i);

        // Keep a row highlighted so Enter always has a target.
        SelectedPickerItem = PickerItems.FirstOrDefault();
    }

    /// <summary>Add the highlighted item at the selected rate (Enter).</summary>
    private void AddSelected()
    {
        var item = SelectedPickerItem;
        if (item is null) return;

        // Starting the next order — the previous confirmation has served its purpose.
        SavedMessage = "";
        SaveError = "";

        var price = ServiceType == ServiceType.CleanPress ? item.CleanPressPrice : item.PressOnlyPrice;
        if (price is null)
        {
            Hint = $"“Press Only” isn't offered for {item.Name}. Switch to Clean & Press.";
            return;
        }

        Hint = "";

        // Same item + same service already in the order? Just bump the quantity.
        var existing = Lines.FirstOrDefault(l => l.Item.Id == item.Id && l.Type == ServiceType);
        if (existing is not null)
        {
            existing.Quantity++;
            SelectedLine = existing;
            return;
        }

        var line = new OrderLineViewModel(item, ServiceType, price.Value, RaiseOrderChanged);
        Lines.Add(line);
        SelectedLine = line;
    }

    // + / - on the selected line. Dropping below 1 removes the line.
    private void ChangeQty(OrderLineViewModel? line, int delta)
    {
        line ??= SelectedLine;
        if (line is null) return;

        if (delta < 0 && line.Quantity <= 1)
        {
            RemoveLine(line);
            return;
        }
        line.Quantity += delta;
    }

    private void RemoveLine(OrderLineViewModel? line)
    {
        if (line is null) return;
        var index = Lines.IndexOf(line);
        Lines.Remove(line);
        SelectedLine = Lines.ElementAtOrDefault(Math.Min(index, Lines.Count - 1));
    }

    private void ClearOrder()
    {
        Lines.Clear();
        Discount = 0;
        SelectedLine = null;
        Hint = "";
    }

    public void ClearSearch() => Search = "";

    /// <summary>Move the picker highlight with the arrow keys (works from the search box).</summary>
    public void MovePickerSelection(int delta)
    {
        if (PickerItems.Count == 0) return;
        var i = SelectedPickerItem is null ? -1 : PickerItems.IndexOf(SelectedPickerItem);
        SelectedPickerItem = PickerItems[Math.Clamp(i + delta, 0, PickerItems.Count - 1)];
    }

    /// <summary>Ctrl+Enter — write a new order, or update the one being edited.</summary>
    private async Task SaveAsync()
    {
        if (Saving) return;

        SaveError = "";
        SavedMessage = "";

        if (!HasLines) { SaveError = "Add at least one item before saving."; return; }
        if (Customer is null) { SaveError = "Choose a customer first — Alt+C."; return; }

        Saving = true;
        try
        {
            if (_editing is not null) await UpdateExistingAsync(Customer);
            else await InsertNewAsync(Customer);
        }
        finally
        {
            Saving = false;
        }
    }

    /// <summary>The order's lines as they stand on screen, ready to store.</summary>
    private List<PosOrderItem> BuildLines(Guid orderId) => Lines.Select(l => new PosOrderItem
    {
        OrderId = orderId,
        PosItemId = l.Item.Id == Guid.Empty ? null : l.Item.Id,
        ItemName = l.Item.Name,
        Category = l.Item.Category,
        ServiceType = l.Type == Sections.ServiceType.CleanPress ? "clean_press" : "press_only",
        UnitPrice = l.UnitPrice,
        // Only when it moved. Storing it always would make every line look
        // repriced on the invoice, which is exactly the signal we're trying to
        // keep meaningful.
        EstUnitPrice = l.IsRepriced ? l.ListPrice : null,
        Quantity = l.Quantity,
        LineTotal = l.LineTotal,
    }).ToList();

    /// <summary>
    /// Write a brand-new order and its lines.
    ///
    /// Two inserts, not one: the lines need the order's id, which the database
    /// generates. If the lines fail after the order row has landed, the order is
    /// deleted again — an order with no items would otherwise sit in the queue
    /// forever as a phantom that nobody can fulfil.
    /// </summary>
    private async Task InsertNewAsync(OrderCustomer customer)
    {
        var db = SupabaseService.Instance;
        var total = Total;          // captured before the order is cleared for the next customer
        PosOrder? order = null;
        var linesSaved = false;

        try
        {
            await db.InitializeAsync();

            var inserted = await db.Client.From<PosOrder>().Insert(new PosOrder
            {
                ProfileId = customer.ProfileId,
                PosCustomerId = customer.PosCustomerId,
                CustomerName = customer.Name,
                CustomerPhone = customer.Phone,
                Status = "booked",
                IsExpress = IsExpress,
                PaymentMethod = PaymentMethod,
                IsPaid = IsPaid,
                VatEnabled = VatEnabled,
                Notes = HasNotes ? Notes.Trim() : null,
                Subtotal = Subtotal,
                ExpressSurcharge = ExpressSurcharge,
                Discount = DiscountAmount,
                VatAmount = VatAmount,
                Total = Total,
                CreatedBy = Guid.TryParse(_staff.Id, out var staffId) ? staffId : null,
            });

            order = inserted.Models.FirstOrDefault();
            if (order is null)
            {
                SaveError = "The order did not save. Please try again.";
                return;
            }

            var lines = BuildLines(order.Id);
            await db.Client.From<PosOrderItem>().Insert(lines);
            linesSaved = true;

            // From here the order is safely in the database. Everything below is
            // presentation, and is not allowed to undo it — hence its own catch.
            var saved = await ReadSavedOrderAsync(db, order.Id);
            var number = saved?.OrderNumber ?? "";

            SavedMessage = number.Length > 0
                ? $"Order {number} saved  ·  AED {total:0.00}"
                : $"Order saved  ·  AED {total:0.00}";

            if (saved is not null) WriteInvoice(saved, lines);

            StartNextOrder();
        }
        catch (Exception ex)
        {
            // Only roll back a half-written order: the row exists but its lines
            // never landed. Once the lines are in, nothing here may delete it.
            if (order is not null && !linesSaved)
            {
                var orphanId = order.Id;
                try { await db.Client.From<PosOrder>().Where(o => o.Id == orphanId).Delete(); }
                catch { /* the failure is reported below; nothing more to do */ }
            }
            SaveError = "Could not save the order. " + ex.Message;
        }
    }

    /// <summary>
    /// Update a placed order in place, keeping its P-number so the reprinted
    /// invoice replaces the original rather than competing with it.
    ///
    /// The lines are replaced wholesale — deleted, then re-inserted — because
    /// working out which of them the staff member added, removed or re-counted
    /// would be far more code and far more ways to be wrong. If the re-insert
    /// fails the originals go back, so a broken update never costs the order its
    /// contents.
    /// </summary>
    private async Task UpdateExistingAsync(OrderCustomer customer)
    {
        var db = SupabaseService.Instance;
        var order = _editing!;
        var total = Total;
        var linesDeleted = false;

        try
        {
            await db.InitializeAsync();

            order.ProfileId = customer.ProfileId;
            order.PosCustomerId = customer.PosCustomerId;
            order.CustomerName = customer.Name;
            order.CustomerPhone = customer.Phone;
            order.IsExpress = IsExpress;
            order.PaymentMethod = PaymentMethod;
            order.IsPaid = IsPaid;
            order.VatEnabled = VatEnabled;
            order.Notes = HasNotes ? Notes.Trim() : null;
            order.Subtotal = Subtotal;
            order.ExpressSurcharge = ExpressSurcharge;
            order.Discount = DiscountAmount;
            order.VatAmount = VatAmount;
            order.Total = Total;

            await order.Update<PosOrder>();

            var orderId = order.Id;
            await db.Client.From<PosOrderItem>().Where(i => i.OrderId == orderId).Delete();
            linesDeleted = true;

            var lines = BuildLines(orderId);
            await db.Client.From<PosOrderItem>().Insert(lines);

            SavedMessage = $"Order {order.OrderNumber} updated  ·  AED {total:0.00}";
            WriteInvoice(order, lines);

            StartNextOrder();
        }
        catch (Exception ex)
        {
            if (linesDeleted) await RestoreLinesAsync(db);
            SaveError = "Could not update the order. " + ex.Message;
        }
    }

    /// <summary>Put the original lines back after a failed update.</summary>
    private async Task RestoreLinesAsync(SupabaseService db)
    {
        if (_editingOriginalLines.Count == 0) return;
        try
        {
            // Fresh rows: the originals carry ids the database has already released.
            var restored = _editingOriginalLines.Select(o => new PosOrderItem
            {
                OrderId = o.OrderId,
                PosItemId = o.PosItemId,
                ItemName = o.ItemName,
                Category = o.Category,
                ServiceType = o.ServiceType,
                UnitPrice = o.UnitPrice,
                Quantity = o.Quantity,
                LineTotal = o.LineTotal,
            }).ToList();

            await db.Client.From<PosOrderItem>().Insert(restored);
        }
        catch
        {
            SaveError = "The order's items could not be restored. Please check order "
                        + _editing?.OrderNumber + " before handing it over.";
        }
    }

    /// <summary>
    /// Re-read the order the database actually stored, for its P-number and its
    /// timestamp — both of which the invoice needs.
    ///
    /// It cannot come from the insert itself. order_number is marked
    /// ignoreOnInsert so the database's P-sequence produces it — but the client
    /// library applies that flag in BOTH directions during an insert, so the row
    /// the insert hands back always has the number blank. A plain select is the
    /// only way to see it.
    ///
    /// Returns null rather than throwing: the order is saved either way, and a
    /// failed read must not look like a failed save.
    /// </summary>
    private static async Task<PosOrder?> ReadSavedOrderAsync(SupabaseService db, Guid orderId)
    {
        try
        {
            var row = await db.Client.From<PosOrder>().Where(o => o.Id == orderId).Get();
            return row.Models.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Build the A4 PDF invoice and let the staff member choose where it goes.
    ///
    /// The PDF is rendered first and the dialog opened second, so a failure while
    /// building it never leaves an empty file behind at the chosen path.
    ///
    /// Failing here is a nuisance, not a disaster — the order is already saved and
    /// the invoice can be reprinted from the order queue later — so it reports
    /// itself in the confirmation line instead of throwing the save away.
    /// </summary>
    private void WriteInvoice(PosOrder saved, IReadOnlyList<PosOrderItem> lines)
    {
        try
        {
            var pdf = InvoiceService.Render(saved, lines);

            var path = InvoiceService.AskWhereToSave(saved);
            if (path is null)
            {
                // Cancelling is a real choice, not an error: the order stands and
                // the invoice can be produced again from the order queue.
                SavedMessage += "  ·  invoice not saved";
                return;
            }

            File.WriteAllBytes(path, pdf);
            SavedMessage += "  ·  invoice saved";
        }
        catch (Exception ex)
        {
            SavedMessage += "  ·  invoice could not be created";
            SaveError = "The order is saved. Only the invoice failed: " + ex.Message;
        }
    }

    /// <summary>
    /// Clear the counter for the next customer. Per-order choices reset; the VAT
    /// switch and the Clean &amp; Press / Press Only mode stay, because those track
    /// how this shop is working today rather than anything about one order.
    /// </summary>
    private void StartNextOrder()
    {
        Lines.Clear();
        Discount = 0;
        Customer = null;
        IsExpress = false;
        PaymentMethod = "cod";
        IsPaid = false;            // the next customer hasn't paid yet either
        Notes = "";                // notes belong to one order, never the next

        // Leaving edit mode: what follows is a new order, not more changes to the
        // one just updated.
        _editing = null;
        _editingOriginalLines = [];
        RaiseEditingChanged();
        SelectedLine = null;
        Hint = "";
    }

    // Recompute everything that depends on the lines/options.
    private void RaiseOrderChanged()
    {
        // A percentage has to follow the basket. Written straight to the field: the
        // setter would read it as the counter overriding the discount by hand and
        // switch the automatic one off.
        if (_firstOrderDiscount)
        {
            _discount = Money(GrossBeforeDiscount * ShopPolicy.FirstOrderDiscountRate);
            OnPropertyChanged(nameof(Discount));
        }

        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(ExpressSurcharge));
        OnPropertyChanged(nameof(ExpressDisplay));
        OnPropertyChanged(nameof(ExpressLabel));
        OnPropertyChanged(nameof(GrossBeforeDiscount));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(VatAmount));
        OnPropertyChanged(nameof(VatDisplay));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ItemCountText));
        OnPropertyChanged(nameof(CanSave));
    }
}
