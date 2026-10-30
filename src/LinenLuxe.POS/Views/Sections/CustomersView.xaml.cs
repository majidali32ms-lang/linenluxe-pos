using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LinenLuxe.POS.ViewModels.Sections;

namespace LinenLuxe.POS.Views.Sections;

public partial class CustomersView : UserControl
{
    private Window? _window;

    public CustomersView()
    {
        InitializeComponent();

        // Shortcuts on the window, as on every other screen: a handler here only
        // sees keys once focus is already inside this control, so Ctrl+F would do
        // nothing straight after clicking the sidebar. Unloaded detaches them so
        // the next screen doesn't inherit them.
        Loaded += (_, _) =>
        {
            if (_window is null)
            {
                _window = Window.GetWindow(this);
                if (_window is not null) _window.PreviewKeyDown += OnPreviewKeyDown;
            }
            SearchBox.Focus();
        };

        Unloaded += (_, _) =>
        {
            if (_window is null) return;
            _window.PreviewKeyDown -= OnPreviewKeyDown;
            _window = null;
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CustomersViewModel vm) return;

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // With Alt held, WPF reports Key.System and puts the real letter in
        // SystemKey — so Alt+N never reaches a plain switch on e.Key at all.
        // Key.System is itself proof that Alt was down, and asking the event
        // rather than the keyboard means the shortcut can be tested.
        var isSystemKey = e.Key == Key.System;
        var key = isSystemKey ? e.SystemKey : e.Key;
        var alt = isSystemKey || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        switch (key)
        {
            case Key.F when ctrl:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;

            case Key.F5:
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
                return;

            // Alt+N / Alt+B start a new customer, and put the caret straight in the
            // name box — the only field that is always needed.
            case Key.N when alt:
                vm.NewWalkInCommand.Execute(null);
                FocusName();
                e.Handled = true;
                return;

            case Key.B when alt:
                vm.NewBusinessCommand.Execute(null);
                FocusName();
                e.Handled = true;
                return;

            case Key.S when ctrl && vm.CanSave:
                vm.SaveCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.E when ctrl:
                Export(vm);
                e.Handled = true;
                return;

            // ↑ ↓ walk the list without leaving the search box
            case Key.Down or Key.Up when vm.Customers.Count > 0:
                Move(vm, key == Key.Down ? +1 : -1);
                e.Handled = true;
                return;

            case Key.Escape:
                vm.Search = "";
                SearchBox.Focus();
                e.Handled = true;
                return;
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel vm) Export(vm);
    }

    /// <summary>
    /// Writes the list to a CSV the owner chooses the place for — the same Save-As
    /// the invoice uses, rather than dropping a file somewhere and announcing it.
    /// The byte-order mark is what makes Excel read Arabic names and the AED figures
    /// correctly instead of showing them as mojibake.
    /// </summary>
    private void Export(CustomersViewModel vm)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the customer list",
            FileName = vm.SuggestedExportName,
            DefaultExt = ".csv",
            Filter = "Spreadsheet (*.csv)|*.csv|All files (*.*)|*.*",
            AddExtension = true,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            System.IO.File.WriteAllText(dialog.FileName, vm.BuildCsv(),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Could not save the file.\n\n" + ex.Message,
                "LinenLuxe POS", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Queued behind the layout pass: the box is only just being enabled, and
    // Focus() on a disabled control silently does nothing.
    private void FocusName() => Dispatcher.BeginInvoke(new Action(() =>
    {
        NameBox.Focus();
        NameBox.SelectAll();
    }), System.Windows.Threading.DispatcherPriority.Input);

    private void Move(CustomersViewModel vm, int delta)
    {
        if (vm.Customers.Count == 0) return;
        var current = vm.Selected is null ? -1 : vm.Customers.IndexOf(vm.Selected);
        var next = Math.Clamp(current + delta, 0, vm.Customers.Count - 1);
        vm.Selected = vm.Customers[next];
        CustomerList.ScrollIntoView(vm.Selected);
    }
}
