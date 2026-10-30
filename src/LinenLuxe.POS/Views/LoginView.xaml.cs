using System.Windows.Controls;
using LinenLuxe.POS.ViewModels;

namespace LinenLuxe.POS.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        Loaded += (_, _) => EmailBox.Focus(); // cursor ready in the email field
    }

    // WPF's PasswordBox deliberately blocks data binding for security, so we
    // forward its value to the view model here (kept only in memory).
    private void PasswordField_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }
}
