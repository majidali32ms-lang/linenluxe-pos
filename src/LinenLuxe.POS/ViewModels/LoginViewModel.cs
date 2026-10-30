using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Auth;

namespace LinenLuxe.POS.ViewModels;

/// <summary>
/// Staff sign-in screen. Uses the same Supabase Auth accounts as the web app;
/// only staff/admin roles are allowed through (enforced by AuthService).
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly Action<Profile> _onSignedIn;

    public LoginViewModel(AuthService auth, Action<Profile> onSignedIn)
    {
        _auth = auth;
        _onSignedIn = onSignedIn;
        SignInCommand = new RelayCommand(async () => await SignInAsync(), () => !Busy);
    }

    // ----- Brand text (login hero) -----
    public string ModuleName => "POINT OF SALE";
    public string Tagline => "Luxury Care for Every Fabric";

    // ----- Form fields -----
    private string _email = "";
    public string Email
    {
        get => _email;
        set { if (SetProperty(ref _email, value)) Error = ""; }
    }

    // Set from the view's PasswordBox (WPF PasswordBox can't data-bind securely).
    private string _password = "";
    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) Error = ""; }
    }

    private bool _busy;
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (SetProperty(ref _busy, value)) OnPropertyChanged(nameof(SignInLabel));
        }
    }

    public string SignInLabel => Busy ? "Signing in…" : "Sign In";

    private string _error = "";
    public string Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => Error.Length > 0;

    /// <summary>Bound to the Sign In button (and Enter via IsDefault).</summary>
    public RelayCommand SignInCommand { get; }

    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Enter your email and password.";
            return;
        }

        Busy = true;
        Error = "";
        try
        {
            var profile = await _auth.SignInStaffAsync(Email.Trim(), Password);
            _onSignedIn(profile);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
