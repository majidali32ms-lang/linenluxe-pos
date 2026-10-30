using System.Collections.ObjectModel;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.ViewModels.Sections;

namespace LinenLuxe.POS.ViewModels;

/// <summary>
/// The main POS shell shown after login: a sidebar of sections on the left and
/// the active screen on the right. Owns navigation (mouse + keyboard shortcuts).
/// </summary>
public sealed class PosShellViewModel : ViewModelBase
{
    // Segoe MDL2 Assets icon code points (kept as numbers so the source is ASCII).
    private const int IconHome = 0xE80F;
    private const int IconAdd = 0xE710;
    private const int IconList = 0xE8FD;
    private const int IconContact = 0xE77B;
    private const int IconShop = 0xE719;
    private const int IconSettings = 0xE713;

    private readonly Profile _profile;
    private readonly Action _onSignOut;

    public PosShellViewModel(Profile profile, Action onSignOut)
    {
        _profile = profile;
        _onSignOut = onSignOut;

        // Sidebar order also defines the Ctrl+1..6 jump numbers.
        Sections =
        [
            new() { Id = "dashboard", Title = "Dashboard", GlyphCode = IconHome,     ShortcutHint = "Ctrl+1" },
            new() { Id = "new-order", Title = "New Order", GlyphCode = IconAdd,      ShortcutHint = "Ctrl+N" },
            new() { Id = "orders",    Title = "Orders",    GlyphCode = IconList,     ShortcutHint = "Ctrl+3" },
            new() { Id = "customers", Title = "Customers", GlyphCode = IconContact,  ShortcutHint = "Ctrl+4" },
            new() { Id = "services",  Title = "Services",  GlyphCode = IconShop,     ShortcutHint = "Ctrl+5" },
            new() { Id = "settings",  Title = "Settings",  GlyphCode = IconSettings, ShortcutHint = "Ctrl+6" },
        ];

        NavigateCommand = new RelayCommand(p => Navigate(p as string));
        NavigateIndexCommand = new RelayCommand(NavigateIndex);
        SignOutCommand = new RelayCommand(() => _onSignOut());
        HelpCommand = new RelayCommand(() => Navigate("settings")); // F1 → shortcuts (full guide comes later)
        FocusSearchCommand = new RelayCommand(() => FocusSearchRequested?.Invoke());

        Navigate("dashboard");
    }

    public ObservableCollection<NavSection> Sections { get; }

    private NavSection? _selected;
    /// <summary>Two-way bound to the sidebar ListBox; setting it navigates.</summary>
    public NavSection? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value) && value is not null)
            {
                CurrentSection = CreateSection(value.Id);
                OnPropertyChanged(nameof(CurrentSectionTitle));
            }
        }
    }

    private ViewModelBase _currentSection = null!;
    public ViewModelBase CurrentSection
    {
        get => _currentSection;
        private set => SetProperty(ref _currentSection, value);
    }

    // ================= Top bar =================

    /// <summary>Which screen is showing, named in the bar above it.</summary>
    public string CurrentSectionTitle => Selected?.Title ?? "";

    /// <summary>Written out in full — a receipt date read back to a customer
    /// should never be ambiguous about which is the day and which the month.</summary>
    public string Today => DateTime.Now.ToString("dddd, d MMMM yyyy");

    private string _globalSearch = "";
    /// <summary>
    /// Ctrl+K — one box for "find this". An order number goes to the Orders
    /// screen, anything else is treated as a customer name or phone; staff have
    /// whichever the customer offers and shouldn't have to pick a screen first.
    /// </summary>
    public string GlobalSearch
    {
        get => _globalSearch;
        set
        {
            if (!SetProperty(ref _globalSearch, value)) return;
            OnPropertyChanged(nameof(IsGlobalSearchEmpty));
        }
    }

    public bool IsGlobalSearchEmpty => GlobalSearch.Length == 0;

    /// <summary>
    /// Set by the view so Ctrl+K can put the caret in a box the view model has no
    /// other way to reach. A command rather than a direct call keeps the shortcut
    /// declared with the others in XAML.
    /// </summary>
    public Action? FocusSearchRequested { get; set; }

    /// <summary>
    /// Run whatever is in the top-bar box. Everything lands on the Orders screen:
    /// it already searches order number, customer name and phone together, so
    /// there is no guess to get wrong here.
    /// </summary>
    public void RunGlobalSearch()
    {
        var q = GlobalSearch.Trim();
        if (q.Length == 0) return;

        _pendingSearch = q;
        Navigate("orders");
        GlobalSearch = "";
    }

    private string? _pendingSearch;

    private string? TakePendingSearch()
    {
        var q = _pendingSearch;
        _pendingSearch = null;
        return q;
    }

    // ----- Signed-in user (sidebar footer + top bar) -----

    /// <summary>
    /// Who is signed in. The owner signs in as admin on his own machine and does
    /// not need his name read back to him all day — ADMIN alone says what the
    /// screen needs to say, which is that this session can do everything. A staff
    /// name is the opposite: it is the only way to tell, at a glance or afterwards,
    /// who was on the till.
    /// </summary>
    public bool IsAdmin => _profile.IsAdmin;

    public string UserName =>
        string.IsNullOrWhiteSpace(_profile.FullName) ? (_profile.Email ?? "Staff") : _profile.FullName!;

    public string RoleBadge => IsAdmin ? "ADMIN" : "STAFF";

    /// <summary>Shown in the sidebar footer only when there is a name worth showing.</summary>
    public bool ShowUserName => !IsAdmin;

    /// <summary>The top-bar chip: "ADMIN", or "STAFF · Fatima".</summary>
    public string SignedInAs => IsAdmin ? RoleBadge : $"{RoleBadge}  ·  {UserName}";

    // ----- Commands -----
    public RelayCommand NavigateCommand { get; }
    public RelayCommand NavigateIndexCommand { get; }
    public RelayCommand SignOutCommand { get; }
    public RelayCommand HelpCommand { get; }
    public RelayCommand FocusSearchCommand { get; }

    public void Navigate(string? id)
    {
        var section = Sections.FirstOrDefault(s => s.Id == id);
        if (section is not null) Selected = section; // triggers CreateSection
    }

    private void NavigateIndex(object? param)
    {
        if (int.TryParse(param?.ToString(), out var oneBased)
            && oneBased >= 1 && oneBased <= Sections.Count)
            Navigate(Sections[oneBased - 1].Id);
    }

    /// <summary>
    /// Set for one navigation only, when Orders hands an order to New Order to be
    /// edited. Cleared as it is consumed, so pressing Ctrl+N afterwards starts a
    /// genuinely new order rather than reopening the last one edited.
    /// </summary>
    private Guid? _editOrderId;

    /// <summary>Open a placed order on the New Order screen, ready to change.</summary>
    public void EditOrder(Guid orderId)
    {
        _editOrderId = orderId;
        Navigate("new-order");
    }

    // Build the right-hand screen for a section id.
    private ViewModelBase CreateSection(string id) => id switch
    {
        "dashboard" => new DashboardViewModel(_profile, Navigate),
        "settings"  => new SettingsViewModel(),
        "new-order" => new NewOrderViewModel(_profile, TakeEditOrderId()),
        "orders" => new OrdersViewModel(EditOrder, TakePendingSearch()),
        "customers" => new CustomersViewModel(),
        "services"  => new ServicesViewModel(),
        _ => new PlaceholderViewModel("Coming soon", IconHome, "", ""),
    };

    private Guid? TakeEditOrderId()
    {
        var id = _editOrderId;
        _editOrderId = null;
        return id;
    }
}
