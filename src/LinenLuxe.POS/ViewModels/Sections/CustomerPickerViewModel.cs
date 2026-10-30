using System.Collections.ObjectModel;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Data;
using Supabase.Postgrest;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// Alt+C overlay: find the customer for an order. Searches BOTH the website's
/// registered customers and saved walk-ins by phone or name, and can create a
/// new walk-in on the spot (name + phone, no account needed).
/// </summary>
public sealed class CustomerPickerViewModel : ViewModelBase
{
    private readonly Action<OrderCustomer> _onChosen;
    private readonly Action _onCancel;

    public CustomerPickerViewModel(Action<OrderCustomer> onChosen, Action onCancel)
    {
        _onChosen = onChosen;
        _onCancel = onCancel;

        SearchCommand = new RelayCommand(async () => await SearchAsync());
        ChooseCommand = new RelayCommand(p => Choose(p as OrderCustomer ?? SelectedResult));
        AddWalkInCommand = new RelayCommand(async () => await AddWalkInAsync());
        NewCustomerCommand = new RelayCommand(OpenNewForm);
        BackToSearchCommand = new RelayCommand(() => { ShowNewForm = false; Error = ""; });
        CancelCommand = new RelayCommand(() => _onCancel());
    }

    public ObservableCollection<OrderCustomer> Results { get; } = [];

    private string _search = "";
    /// <summary>Phone or name. Searching is triggered explicitly (Enter / button).</summary>
    public string Search
    {
        get => _search;
        set
        {
            if (!SetProperty(ref _search, value)) return;
            OnPropertyChanged(nameof(IsSearchEmpty));
            Error = "";
        }
    }

    public bool IsSearchEmpty => string.IsNullOrEmpty(_search);

    private OrderCustomer? _selectedResult;
    public OrderCustomer? SelectedResult
    {
        get => _selectedResult;
        set => SetProperty(ref _selectedResult, value);
    }

    private bool _busy;
    public bool Busy
    {
        get => _busy;
        private set { if (SetProperty(ref _busy, value)) RaisePanelsChanged(); }
    }

    private bool _searched;
    /// <summary>True once a search has run, so "no results" only shows afterwards.</summary>
    public bool Searched
    {
        get => _searched;
        private set { if (SetProperty(ref _searched, value)) RaisePanelsChanged(); }
    }

    private bool _showNewForm;
    /// <summary>True once staff explicitly asked to add someone new.</summary>
    public bool ShowNewForm
    {
        get => _showNewForm;
        private set { if (SetProperty(ref _showNewForm, value)) RaisePanelsChanged(); }
    }

    public bool ShowNoResults => Searched && Results.Count == 0 && !Busy;

    /// <summary>
    /// The walk-in form. It opens two ways: on demand from the "New customer"
    /// button — a first-time visitor shouldn't have to run a search that fails
    /// before they can be added — or automatically when a search finds nobody.
    /// </summary>
    public bool ShowWalkInForm => ShowNewForm || ShowNoResults;

    /// <summary>Results step aside while the form is up so the two can't overlap.</summary>
    public bool ShowResults => !ShowWalkInForm && !Busy;

    public string WalkInHeading =>
        ShowNewForm ? "New customer" : "No customer found — add as a walk-in";

    private void RaisePanelsChanged()
    {
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ShowWalkInForm));
        OnPropertyChanged(nameof(ShowResults));
        OnPropertyChanged(nameof(WalkInHeading));
    }

    private string _error = "";
    public string Error
    {
        get => _error;
        private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => Error.Length > 0;

    // ----- New walk-in form -----
    private string _newName = "";
    public string NewName
    {
        get => _newName;
        set { if (SetProperty(ref _newName, value)) Error = ""; }
    }

    private string _newPhone = "";
    public string NewPhone
    {
        get => _newPhone;
        set { if (SetProperty(ref _newPhone, value)) Error = ""; }
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ChooseCommand { get; }
    public RelayCommand AddWalkInCommand { get; }
    public RelayCommand NewCustomerCommand { get; }
    public RelayCommand BackToSearchCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Open the walk-in form for a first-time visitor.</summary>
    private void OpenNewForm()
    {
        Error = "";

        // Carry whatever is already typed into the matching field, so staff who
        // searched first don't type the name or number a second time.
        var q = Search.Trim();
        if (q.Length >= 2)
        {
            if (q.Any(char.IsDigit) && !q.Any(char.IsLetter)) NewPhone = q;
            else NewName = q;
        }

        ShowNewForm = true;
    }

    /// <summary>Search registered customers and saved walk-ins together.</summary>
    private async Task SearchAsync()
    {
        var q = Search.Trim();
        if (q.Length < 2)
        {
            Error = "Type at least 2 characters of the phone or name.";
            return;
        }

        Busy = true;
        Error = "";
        Results.Clear();
        ShowNewForm = false;   // searching means they're back to looking, not adding

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var pattern = $"%{q}%";

            // --- Registered website customers (profiles) ---
            var byPhone = await db.Client.From<Profile>()
                .Filter("phone", Constants.Operator.ILike, pattern).Limit(20).Get();
            var byName = await db.Client.From<Profile>()
                .Filter("full_name", Constants.Operator.ILike, pattern).Limit(20).Get();

            var profiles = byPhone.Models.Concat(byName.Models)
                .GroupBy(p => p.Id).Select(g => g.First())
                .Where(p => p.Role == "customer");   // staff accounts aren't customers

            foreach (var p in profiles) Results.Add(OrderCustomer.FromProfile(p));

            // --- Saved walk-ins (pos_customers) ---
            var wPhone = await db.Client.From<PosCustomer>()
                .Filter("phone", Constants.Operator.ILike, pattern).Limit(20).Get();
            var wName = await db.Client.From<PosCustomer>()
                .Filter("full_name", Constants.Operator.ILike, pattern).Limit(20).Get();

            var walkIns = wPhone.Models.Concat(wName.Models)
                .GroupBy(c => c.Id).Select(g => g.First());

            foreach (var c in walkIns) Results.Add(OrderCustomer.FromWalkIn(c));

            SelectedResult = Results.FirstOrDefault();

            // Pre-fill the walk-in form so "not found" flows straight into "add".
            if (Results.Count == 0)
            {
                if (q.Any(char.IsDigit) && !q.Any(char.IsLetter)) NewPhone = q;
                else NewName = q;
            }
        }
        catch (Exception ex)
        {
            Error = "Search failed. " + ex.Message;
        }
        finally
        {
            Busy = false;
            Searched = true;
            RaisePanelsChanged();
        }
    }

    /// <summary>Create a walk-in customer and use it for this order.</summary>
    private async Task AddWalkInAsync()
    {
        var name = NewName.Trim();
        if (name.Length < 2)
        {
            Error = "Enter the customer's name.";
            return;
        }

        Busy = true;
        Error = "";
        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var created = await db.Client.From<PosCustomer>().Insert(new PosCustomer
            {
                FullName = name,
                Phone = string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(),
            });

            var row = created.Models.FirstOrDefault();
            if (row is null)
            {
                Error = "Could not save the customer. Please try again.";
                return;
            }

            Choose(OrderCustomer.FromWalkIn(row));
        }
        catch (Exception ex)
        {
            Error = "Could not save the customer. " + ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    private void Choose(OrderCustomer? customer)
    {
        if (customer is null) return;
        _onChosen(customer);
    }
}
