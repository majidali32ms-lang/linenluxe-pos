using System.Collections.ObjectModel;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Data;
using Supabase.Postgrest;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// Customers screen — everyone the shop deals with, in one list.
///
/// Two kinds live here together: the POS's own customers, which include walk-ins
/// and the hotel/salon business accounts, and people who registered on the
/// website. The website ones are shown but not editable — that table belongs to
/// the live web app.
///
/// Selecting anyone shows what they have spent and every order they have placed,
/// which is the question staff are actually asked on the phone: "what did I leave
/// with you?"
/// </summary>
public sealed class CustomersViewModel : ViewModelBase
{
    /// <summary>The shop's own customers are loaded whole; there won't be many.</summary>
    private const int MaxPosCustomers = 1000;

    /// <summary>Website accounts could be endless, so the list is capped and search does the work.</summary>
    private const int MaxWebsiteCustomers = 300;

    /// <summary>Enough history to answer any question a customer actually asks.</summary>
    private const int MaxOrders = 500;

    public const string FilterAll = "All";
    public const string FilterWalkIn = "Walk-in";
    public const string FilterBusiness = "Business";
    public const string FilterWebsite = "Website";

    private List<CustomerListItem> _all = [];
    private List<PosOrder> _orders = [];

    public CustomersViewModel()
    {
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        NewWalkInCommand = new RelayCommand(() => StartNew(PosCustomer.WalkIn));
        NewBusinessCommand = new RelayCommand(() => StartNew(PosCustomer.Business));
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelEditCommand = new RelayCommand(CancelEdit);
        SetFilterCommand = new RelayCommand(p => Filter = p?.ToString() ?? FilterAll);
        SetTypeCommand = new RelayCommand(p => FormIsBusiness = p?.ToString() == PosCustomer.Business);
        ArmMergeCommand = new RelayCommand(ArmMerge);
        CancelMergeCommand = new RelayCommand(CancelMerge);
        ConfirmMergeCommand = new RelayCommand(async () => await MergeAsync());

        Filters = [FilterAll, FilterWalkIn, FilterBusiness, FilterWebsite];

        _ = LoadAsync();
    }

    public ObservableCollection<CustomerListItem> Customers { get; } = [];
    public ObservableCollection<PosOrder> History { get; } = [];
    public IReadOnlyList<string> Filters { get; }

    // ================= Selection =================

    private CustomerListItem? _selected;
    public CustomerListItem? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            // Moving to another customer abandons a half-finished edit rather than
            // carrying it across and saving it onto the wrong person.
            _draft = null;
            LoadForm();
            RaiseSelectionChanged();
            ApplyHistory();
            ApplyDuplicates();
        }
    }

    public bool HasSelection => Selected is not null;
    public bool CanEditSelected => IsCreating || Selected?.IsEditable == true;
    public bool SelectedIsWebsite => Selected?.IsWebsite == true;

    /// <summary>Shown instead of the form's Save button for a website account.</summary>
    public string ReadOnlyReason =>
        "This customer registered on the website. Their details belong to their own "
        + "account, so they are shown here but changed from the website — not from the POS.";

    // ================= Search and filter =================

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

    public bool IsSearchEmpty => Search.Length == 0;

    private string _filter = FilterAll;
    public string Filter
    {
        get => _filter;
        set { if (SetProperty(ref _filter, value)) ApplyFilter(); }
    }

    // ================= The form =================

    /// <summary>A customer being created, before it has been saved.</summary>
    private PosCustomer? _draft;

    public bool IsCreating => _draft is not null;

    private string _formName = "";
    public string FormName
    {
        get => _formName;
        set { if (SetProperty(ref _formName, value)) OnPropertyChanged(nameof(CanSave)); }
    }

    private string _formPhone = "";
    public string FormPhone { get => _formPhone; set => SetProperty(ref _formPhone, value); }

    private string _formEmail = "";
    public string FormEmail { get => _formEmail; set => SetProperty(ref _formEmail, value); }

    private string _formContact = "";
    public string FormContact { get => _formContact; set => SetProperty(ref _formContact, value); }

    private string _formAddress = "";
    public string FormAddress { get => _formAddress; set => SetProperty(ref _formAddress, value); }

    private string _formArea = "";
    public string FormArea { get => _formArea; set => SetProperty(ref _formArea, value); }

    private string _formTrn = "";
    public string FormTrn { get => _formTrn; set => SetProperty(ref _formTrn, value); }

    private string _formCreditDays = "0";
    public string FormCreditDays { get => _formCreditDays; set => SetProperty(ref _formCreditDays, value); }

    private string _formNotes = "";
    public string FormNotes { get => _formNotes; set => SetProperty(ref _formNotes, value); }

    private bool _formIsBusiness;
    /// <summary>Drives which half of the form matters — a walk-in has no contact
    /// person, TRN or credit terms.</summary>
    public bool FormIsBusiness
    {
        get => _formIsBusiness;
        set
        {
            if (!SetProperty(ref _formIsBusiness, value)) return;
            OnPropertyChanged(nameof(FormIsWalkIn));
            OnPropertyChanged(nameof(NameLabel));
            OnPropertyChanged(nameof(FormTitle));
        }
    }

    public bool FormIsWalkIn => !FormIsBusiness;

    /// <summary>A business has a company name; a person has a name.</summary>
    public string NameLabel => FormIsBusiness ? "COMPANY NAME" : "NAME";

    public string FormTitle => IsCreating
        ? (FormIsBusiness ? "New business account" : "New walk-in customer")
        : Selected?.Name ?? "";

    public bool CanSave => CanEditSelected && FormName.Trim().Length >= 2 && !Saving;

    private bool _saving;
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

    public string SaveLabel => Saving ? "Saving…" : IsCreating ? "Create customer" : "Save changes";

    private string _message = "";
    public string Message
    {
        get => _message;
        private set { if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage)); }
    }

    public bool HasMessage => Message.Length > 0;

    private string _error = "";
    public string Error
    {
        get => _error;
        private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => Error.Length > 0;

    /// <summary>
    /// Kept apart from <see cref="Error"/>, which belongs to the form. A list that
    /// failed to load and a list that is genuinely empty look identical otherwise,
    /// and "no customers" is the more believable of the two — so staff would go on
    /// adding someone who is already in the database.
    /// </summary>
    private string _loadError = "";
    public string LoadError
    {
        get => _loadError;
        private set
        {
            if (!SetProperty(ref _loadError, value)) return;
            OnPropertyChanged(nameof(HasLoadError));
            OnPropertyChanged(nameof(ShowEmpty));
        }
    }

    public bool HasLoadError => LoadError.Length > 0;

    // ================= History =================

    public bool HasHistory => History.Count > 0;

    public string HistorySummary
    {
        get
        {
            if (Selected is null) return "";
            if (History.Count == 0) return "No orders yet";

            var spent = History.Sum(o => o.Total);
            var unpaid = History.Where(o => !o.IsPaid).Sum(o => o.Total);
            var text = $"{History.Count} order{(History.Count == 1 ? "" : "s")}  ·  AED {spent:0.00} total";
            return unpaid > 0 ? text + $"  ·  AED {unpaid:0.00} unpaid" : text;
        }
    }

    // ================= Loading =================

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

    public bool ShowList => !Loading && Customers.Count > 0;
    public bool ShowEmpty => !Loading && Customers.Count == 0 && !HasLoadError;

    public string CountText => Loading
        ? "Loading customers…"
        : Customers.Count == _all.Count
            ? $"{_all.Count} customer{(_all.Count == 1 ? "" : "s")}"
            : $"{Customers.Count} of {_all.Count} customers";

    public RelayCommand RefreshCommand { get; }
    public RelayCommand NewWalkInCommand { get; }
    public RelayCommand NewBusinessCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand SetFilterCommand { get; }
    public RelayCommand SetTypeCommand { get; }
    public RelayCommand ArmMergeCommand { get; }
    public RelayCommand CancelMergeCommand { get; }
    public RelayCommand ConfirmMergeCommand { get; }

    private async Task LoadAsync()
    {
        Loading = true;
        LoadError = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var pos = await db.Client.From<PosCustomer>()
                .Order("full_name", Constants.Ordering.Ascending)
                .Limit(MaxPosCustomers)
                .Get();

            var website = await db.Client.From<Profile>()
                .Where(p => p.Role == "customer")
                .Limit(MaxWebsiteCustomers)
                .Get();

            // Every counter order, so a customer's history is a filter rather than
            // a query per person — staff click through several while on the phone.
            var orders = await db.Client.From<PosOrder>()
                .Order("created_at", Constants.Ordering.Descending)
                .Limit(MaxOrders)
                .Get();

            _orders = orders.Models;

            _all =
            [
                .. pos.Models.Select(CustomerListItem.FromPos),
                .. website.Models.Select(CustomerListItem.FromWebsite),
            ];

            ComputeStats();

            Loading = false;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Loading = false;
            LoadError = "Could not load the customers. " + ex.Message;
        }
    }

    private void ApplyFilter()
    {
        var q = Search.Trim();

        IEnumerable<CustomerListItem> list = _all;

        list = Filter switch
        {
            FilterWalkIn => list.Where(c => c.Pos?.IsWalkIn == true),
            FilterBusiness => list.Where(c => c.IsBusiness),
            FilterWebsite => list.Where(c => c.IsWebsite),
            _ => list,
        };

        // Name, phone and area together: staff have whichever the caller offers.
        if (q.Length > 0)
        {
            list = list.Where(c =>
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (c.Phone ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (c.Pos?.Area ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var keep = Selected;

        Customers.Clear();
        foreach (var c in list.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
            Customers.Add(c);

        // Keep the person on screen selected if they survived the filter, so
        // typing in the search box doesn't yank the detail pane away.
        Selected = keep is not null && Customers.Contains(keep) ? keep : Customers.FirstOrDefault();

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private void ApplyHistory()
    {
        History.Clear();
        if (Selected is not null)
            foreach (var order in _orders.Where(Selected.Owns))
                History.Add(order);

        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HistorySummary));
        OnPropertyChanged(nameof(StandingLine));
    }

    /// <summary>
    /// How many orders each customer has, what they have spent and what they still
    /// owe — worked out once against the orders already in hand, so the list can
    /// show it on every row without a query per person.
    /// </summary>
    private void ComputeStats()
    {
        foreach (var c in _all)
        {
            var mine = _orders.Where(c.Owns).ToList();
            c.OrderCount = mine.Count;
            c.TotalSpent = mine.Sum(o => o.Total);
            c.Unpaid = mine.Where(o => !o.IsPaid).Sum(o => o.Total);
        }
    }

    /// <summary>"Regular customer — 7 orders", said in full above the history.</summary>
    public string StandingLine =>
        Selected is null ? "" : CustomerStanding.Describe(Selected.OrderCount);

    // ================= Duplicates =================

    /// <summary>Other records with the same phone number as the one on screen.</summary>
    public ObservableCollection<CustomerListItem> Duplicates { get; } = [];

    public bool HasDuplicates => Duplicates.Count > 0;

    /// <summary>
    /// Whether anything can be done about them here. Two website accounts with one
    /// phone are the website's to sort out — <c>profiles</c> belongs to the live web
    /// app — so the panel says what it sees and stops there.
    /// </summary>
    public bool CanMergeDuplicates => Selected?.IsEditable == true && Duplicates.Any(d => d.IsEditable);

    /// <summary>
    /// Two website accounts sharing a phone are not a duplicate to be cleaned up —
    /// they are two logins, each with its own email, orders and password. Calling
    /// that a duplicate invites someone to try to fix what is not broken, so the
    /// panel only says "duplicate" when there is genuinely one record too many.
    /// </summary>
    public string DuplicateHeading => CanMergeDuplicates ? "POSSIBLE DUPLICATE" : "SAME PHONE NUMBER";

    public string DuplicateSummary
    {
        get
        {
            if (Duplicates.Count == 0) return "";

            var n = Duplicates.Count;
            var others = $"{n} other record{(n == 1 ? "" : "s")} share{(n == 1 ? "s" : "")} this phone number.";

            if (CanMergeDuplicates)
                return others + " Merging moves that record's orders onto this one and deletes it.";

            // Nothing here is the POS's to change, so say what they are instead of
            // offering something that cannot be done.
            return others + " They are separate website accounts — a household or a "
                   + "business often signs up more than once on one number, and each "
                   + "login has its own email and its own orders. Nothing to merge: "
                   + "website accounts belong to the website.";
        }
    }

    private void ApplyDuplicates()
    {
        Duplicates.Clear();
        CancelMerge();

        var key = Selected?.PhoneKey ?? "";
        if (Selected is not null && key.Length >= 7)
            foreach (var other in _all.Where(c => !ReferenceEquals(c, Selected) && c.PhoneKey == key))
                Duplicates.Add(other);

        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(CanMergeDuplicates));
        OnPropertyChanged(nameof(DuplicateHeading));
        OnPropertyChanged(nameof(DuplicateSummary));
    }

    /// <summary>
    /// The record a merge is armed against. Merging deletes a customer, so it asks
    /// once and says exactly what will happen first — the same click cannot both
    /// propose and carry out something irreversible.
    /// </summary>
    private CustomerListItem? _pendingMerge;

    public bool IsMergePending => _pendingMerge is not null;

    public string MergeQuestion => _pendingMerge is null || Selected is null
        ? ""
        : $"Move {_pendingMerge.OrderCount} order{(_pendingMerge.OrderCount == 1 ? "" : "s")} from "
          + $"\"{_pendingMerge.Name}\" onto \"{Selected.Name}\" and delete \"{_pendingMerge.Name}\"? "
          + "Anything blank on this record is filled in from the other one. This cannot be undone.";

    private void ArmMerge(object? param)
    {
        if (param is not CustomerListItem other || !other.IsEditable) return;
        _pendingMerge = other;
        Message = "";
        Error = "";
        RaiseMergeChanged();
    }

    private void CancelMerge()
    {
        if (_pendingMerge is null) return;
        _pendingMerge = null;
        RaiseMergeChanged();
    }

    private void RaiseMergeChanged()
    {
        OnPropertyChanged(nameof(IsMergePending));
        OnPropertyChanged(nameof(MergeQuestion));
    }

    private async Task MergeAsync()
    {
        var loser = _pendingMerge;
        var winner = Selected;
        if (loser?.Pos is null || winner?.Pos is null || ReferenceEquals(loser, winner)) return;

        Saving = true;
        Message = "";
        Error = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            // The orders move first. If the delete then fails, the shop is left with
            // an empty duplicate rather than orders pointing at a customer that is
            // no longer there — the harmless half of the two.
            var moving = _orders.Where(o => o.PosCustomerId == loser.Pos.Id).ToList();
            foreach (var order in moving)
            {
                order.PosCustomerId = winner.Pos.Id;

                // Stop at the first order that does not move. Carrying on would
                // delete the record those orders still belong to.
                var reassigned = await order.Update<PosOrder>();
                if (reassigned.Models.Count == 0)
                {
                    Error = $"Order {order.OrderNumber} would not move, so nothing was merged "
                            + "and nothing was deleted. Sign out and back in, then try again.";
                    await LoadAsync();
                    return;
                }
            }

            // Only blanks are filled. Overwriting what is already on the record you
            // chose to keep would lose the very details you kept it for.
            FillBlanks(winner.Pos, loser.Pos);
            await winner.Pos.Update<PosCustomer>();

            await db.Client.From<PosCustomer>().Where(c => c.Id == loser.Pos.Id).Delete();

            var name = loser.Name;
            var moved = moving.Count;
            CancelMerge();
            await LoadAsync();

            // The kept record is found again by id: LoadAsync rebuilt every row.
            Selected = Customers.FirstOrDefault(c => c.Pos?.Id == winner.Pos.Id);
            Message = moved == 0
                ? $"\"{name}\" merged in and removed."
                : $"\"{name}\" merged in — {moved} order{(moved == 1 ? "" : "s")} moved across.";
        }
        catch (Exception ex)
        {
            Error = "Could not merge the two records. " + ex.Message;
        }
        finally
        {
            Saving = false;
        }
    }

    private static void FillBlanks(PosCustomer keep, PosCustomer from)
    {
        keep.Phone ??= from.Phone;
        keep.Email ??= from.Email;
        keep.ContactPerson ??= from.ContactPerson;
        keep.Address ??= from.Address;
        keep.Area ??= from.Area;
        keep.Trn ??= from.Trn;
        if (keep.CreditDays == 0) keep.CreditDays = from.CreditDays;

        // Notes are the one field where both sides may be worth keeping.
        if (!string.IsNullOrWhiteSpace(from.Notes))
            keep.Notes = string.IsNullOrWhiteSpace(keep.Notes)
                ? from.Notes
                : keep.Notes + Environment.NewLine + from.Notes;
    }

    // ================= Export =================

    /// <summary>
    /// The list as it stands on screen — same filter, same search — as CSV. What is
    /// on screen is what someone means by "the list"; exporting all of it regardless
    /// of the filter would be a different answer to the one they are looking at.
    /// </summary>
    public string BuildCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Type,Name,Phone,Email,Contact person,Area,Address,TRN,Days to pay,"
                      + "Orders,Total spent (AED),Unpaid (AED),Standing");

        foreach (var c in Customers)
        {
            var p = c.Pos;
            sb.AppendLine(string.Join(",",
                Csv(c.Badge),
                Csv(c.Name),
                Digits(c.Phone),
                Csv(p?.Email ?? c.Website?.Email),
                Csv(p?.ContactPerson),
                Csv(p?.Area),
                Csv(p?.Address),
                Digits(p?.Trn),
                Csv(p is null || p.CreditDays == 0 ? "" : p.CreditDays.ToString()),
                Csv(c.OrderCount.ToString()),
                Csv(c.TotalSpent.ToString("0.00")),
                Csv(c.Unpaid.ToString("0.00")),
                Csv(c.StandingBadge)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Quoted always, so an address with a comma in it does not slide the rest of
    /// the row a column to the left.
    /// </summary>
    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    /// <summary>
    /// A phone or a TRN is a string of digits, not a quantity, and Excel does not
    /// agree: it reads 050000 as fifty thousand and +971561839671 as 9.72E+11.
    /// Quoting does not stop it — Excel treats CSV quotes as punctuation and
    /// converts what is inside anyway. Writing the cell as ="050000" does stop it,
    /// in Excel, Google Sheets and LibreOffice alike. An exported phone nobody can
    /// dial is not an export.
    /// </summary>
    private static string Digits(string? value)
    {
        var v = value ?? "";
        return v.Length == 0 ? "\"\"" : "\"=\"\"" + v.Replace("\"", "\"\"") + "\"\"\"";
    }

    public string SuggestedExportName =>
        $"LinenLuxe customers {DateTime.Now:yyyy-MM-dd}"
        + (Filter == FilterAll ? "" : $" ({Filter})") + ".csv";

    // ================= Editing =================

    private void StartNew(string type)
    {
        Message = "";
        Error = "";

        _draft = new PosCustomer { CustomerType = type };
        Selected = null;                  // nothing in the list is being edited

        FormIsBusiness = type == PosCustomer.Business;
        FormName = "";
        FormPhone = "";
        FormEmail = "";
        FormContact = "";
        FormAddress = "";
        FormArea = "";
        FormTrn = "";
        FormCreditDays = type == PosCustomer.Business ? "30" : "0";
        FormNotes = "";

        RaiseSelectionChanged();
    }

    private void CancelEdit()
    {
        _draft = null;
        Message = "";
        Error = "";
        LoadForm();
        RaiseSelectionChanged();
    }

    /// <summary>Fill the form from whatever is selected.</summary>
    private void LoadForm()
    {
        var c = Selected?.Pos;

        FormIsBusiness = c?.IsBusiness ?? false;
        FormName = Selected?.Name ?? "";
        FormPhone = Selected?.Phone ?? "";
        FormEmail = c?.Email ?? Selected?.Website?.Email ?? "";
        FormContact = c?.ContactPerson ?? "";
        FormAddress = c?.Address ?? "";
        FormArea = c?.Area ?? "";
        FormTrn = c?.Trn ?? "";
        FormCreditDays = (c?.CreditDays ?? 0).ToString();
        FormNotes = c?.Notes ?? "";
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;

        var target = _draft ?? Selected?.Pos;
        if (target is null) return;

        Saving = true;
        Message = "";
        Error = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            target.FullName = FormName.Trim();
            target.Phone = Blank(FormPhone);
            target.Email = Blank(FormEmail);
            target.Notes = Blank(FormNotes);
            target.CustomerType = FormIsBusiness ? PosCustomer.Business : PosCustomer.WalkIn;
            target.Address = Blank(FormAddress);
            target.Area = Blank(FormArea);

            // A walk-in has no contact person, tax number or credit terms. Clearing
            // them on the way out means a record switched from business to walk-in
            // doesn't keep details that no longer apply.
            target.ContactPerson = FormIsBusiness ? Blank(FormContact) : null;
            target.Trn = FormIsBusiness ? Blank(FormTrn) : null;
            target.CreditDays = FormIsBusiness && int.TryParse(FormCreditDays, out var days) && days > 0
                ? days
                : 0;

            if (_draft is not null)
            {
                var inserted = await db.Client.From<PosCustomer>().Insert(target);
                var created = inserted.Models.FirstOrDefault();
                if (created is null)
                {
                    Error = "The customer did not save. Please try again.";
                    return;
                }

                _draft = null;
                var row = CustomerListItem.FromPos(created);
                _all.Add(row);
                ApplyFilter();
                Selected = row;
                Message = $"{created.FullName} added.";
            }
            else
            {
                // A write the policy refuses comes back as 200 with an empty list,
                // not as an error. Reporting "saved" then would send staff away
                // believing a phone number had been corrected when it had not.
                var updated = await target.Update<PosCustomer>();
                if (updated.Models.Count == 0)
                {
                    Error = "The database did not accept the change — nothing was saved. "
                            + "Sign out and back in, then try once more.";
                    return;
                }

                ApplyFilter();
                Message = "Changes saved.";
            }
        }
        catch (Exception ex)
        {
            Error = "Could not save the customer. " + ex.Message;
        }
        finally
        {
            Saving = false;
        }
    }

    /// <summary>Empty boxes store as null, so "has a phone" means one thing everywhere.</summary>
    private static string? Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(SelectedIsWebsite));
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveLabel));
    }
}
