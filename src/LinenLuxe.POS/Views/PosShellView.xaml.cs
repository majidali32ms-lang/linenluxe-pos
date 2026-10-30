using System.Windows.Controls;
using System.Windows.Input;
using LinenLuxe.POS.ViewModels;

namespace LinenLuxe.POS.Views;

/// <summary>
/// The POS shell: top bar, sidebar navigation and the active screen. Keyboard
/// shortcuts for navigation are declared in the XAML InputBindings.
/// </summary>
public partial class PosShellView : UserControl
{
    public PosShellView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            // Focus the shell so its keyboard shortcuts work as soon as it appears.
            Focus();
            if (DataContext is PosShellViewModel vm) vm.FocusSearchRequested = FocusGlobalSearch;
        };
    }

    /// <summary>
    /// Enter runs the top-bar search; Esc abandons it and hands the keyboard back
    /// to the screen underneath, which has shortcuts of its own waiting.
    /// </summary>
    private void OnGlobalSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PosShellViewModel vm) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.RunGlobalSearch();
                Focus();
                e.Handled = true;
                return;

            case Key.Escape:
                vm.GlobalSearch = "";
                Focus();
                e.Handled = true;
                return;
        }
    }

    /// <summary>Ctrl+K — put the caret in the top-bar search from anywhere.</summary>
    public void FocusGlobalSearch()
    {
        GlobalSearchBox.Focus();
        GlobalSearchBox.SelectAll();
    }
}
