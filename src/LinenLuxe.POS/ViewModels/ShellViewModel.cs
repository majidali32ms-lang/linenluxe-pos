using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Auth;
using LinenLuxe.POS.Services.Configuration;

namespace LinenLuxe.POS.ViewModels;

/// <summary>
/// Root view model: owns which screen is visible (login → home → later the full
/// POS shell with sidebar) and the bottom status bar.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly AuthService _auth = new();

    public ShellViewModel(AppConfig config)
    {
        _config = config;
        _current = new LoginViewModel(_auth, OnSignedIn);
    }

    private ViewModelBase _current;
    /// <summary>The active screen; MainWindow maps it to a view via DataTemplates.</summary>
    public ViewModelBase Current
    {
        get => _current;
        private set
        {
            if (SetProperty(ref _current, value)) OnPropertyChanged(nameof(UserStatus));
        }
    }

    private void OnSignedIn(Profile profile)
        => Current = new PosShellViewModel(profile, OnSignOutRequested);

    private async void OnSignOutRequested()
    {
        await _auth.SignOutAsync();
        Current = new LoginViewModel(_auth, OnSignedIn);
    }

    // ----- Status bar -----
    public bool IsConfigured => _config.IsConfigured;

    public string ConfigStatus => _config.IsConfigured
        ? "Database configured"
        : "Database not configured — check .env";

    public string ConfigDetail => _config.IsConfigured
        ? $"Connected to {ShortHost(_config.SupabaseUrl)}"
        : "Add SUPABASE_URL and SUPABASE_KEY to .env";

    /// <summary>
    /// Shows who is signed in (empty on the login screen). Same rule as the top bar:
    /// the owner signs in as admin and does not need his own name read back to him,
    /// but a staff name is the only way to tell who was on the till.
    /// </summary>
    public string UserStatus => _auth.CurrentProfile is { } p
        ? p.IsAdmin ? "Admin" : $"Staff  ·  {p.FullName ?? p.Email}"
        : "";

    public string Version => "v0.1";

    private static string ShortHost(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
}
