using LinenLuxe.POS.Models;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// Landing screen after login: a greeting plus quick-action cards that jump to
/// the main POS screens (each also reachable by its keyboard shortcut).
/// </summary>
public sealed class DashboardViewModel : ViewModelBase
{
    public DashboardViewModel(Profile profile, Action<string> navigate)
    {
        Profile = profile;
        NewOrderCommand = new RelayCommand(() => navigate("new-order"));
        OrdersCommand = new RelayCommand(() => navigate("orders"));
        CustomersCommand = new RelayCommand(() => navigate("customers"));
        ServicesCommand = new RelayCommand(() => navigate("services"));
    }

    public Profile Profile { get; }

    // Name is dropped for now — the signed-in user is already shown in the
    // sidebar footer and the status bar.
    public string Greeting => "Welcome back";

    public string SubText => "Choose an action to get started.";

    public RelayCommand NewOrderCommand { get; }
    public RelayCommand OrdersCommand { get; }
    public RelayCommand CustomersCommand { get; }
    public RelayCommand ServicesCommand { get; }
}
