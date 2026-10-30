using System.Windows;
using LinenLuxe.POS.Services.Configuration;
using LinenLuxe.POS.ViewModels;

namespace LinenLuxe.POS;

/// <summary>
/// Application shell window. Hosts the active screen (login/home/POS) via the
/// ShellViewModel and shows the bottom status bar.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel(App.Config);

        // The title bar belongs to Windows, so it is coloured through DWM rather
        // than XAML — otherwise the app is navy and gold with a grey strip on top.
        WindowChrome.ApplyBrand(this);
    }
}
