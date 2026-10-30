using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LinenLuxe.POS.ViewModels.Sections;

namespace LinenLuxe.POS.Views.Sections;

public partial class ServicesView : UserControl
{
    private Window? _window;

    public ServicesView()
    {
        InitializeComponent();

        // Shortcuts live on the window, not on this control: a handler here only
        // sees keys while focus is already inside the price list, so Ctrl+F did
        // nothing right after clicking the sidebar. Unloaded detaches again so
        // the next screen doesn't inherit them.
        Loaded += (_, _) =>
        {
            if (_window is not null) return;
            _window = Window.GetWindow(this);
            if (_window is not null) _window.PreviewKeyDown += OnPreviewKeyDown;
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
        if (DataContext is not ServicesViewModel vm) return;

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // With Alt held, WPF reports Key.System and puts the real letter in
        // SystemKey — so Alt+N never reaches a plain switch on e.Key at all.
        // Key.System is itself proof that Alt was down, and asking the event
        // rather than the keyboard means the shortcut can be tested.
        var isSystemKey = e.Key == Key.System;
        var key = isSystemKey ? e.SystemKey : e.Key;
        var alt = isSystemKey || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        // While the new-item card is open it owns the keyboard. Esc closes it and
        // Ctrl+S would otherwise save the list underneath, which is not what the
        // person typing into the card means by "save".
        if (vm.IsAddingItem)
        {
            switch (key)
            {
                case Key.Escape:
                    vm.CancelNewItemCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.Enter when vm.CanAddItem:
                case Key.S when ctrl && vm.CanAddItem:
                    vm.AddItemCommand.Execute(null);
                    e.Handled = true;
                    return;
            }

            return;
        }

        switch (key)
        {
            case Key.F when ctrl:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;

            case Key.S when ctrl && vm.CanSave:
                vm.SaveCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.N when alt:
                vm.NewItemCommand.Execute(null);
                FocusNewName();
                e.Handled = true;
                return;

            case Key.F5:
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Escape:
                vm.ClearSearch();
                e.Handled = true;
                return;
        }
    }

    // Queued behind the layout pass: the card is only just becoming visible, and
    // Focus() on a control that is still collapsed silently does nothing.
    private void FocusNewName() => Dispatcher.BeginInvoke(new Action(() =>
    {
        NewNameBox.Focus();
        NewNameBox.SelectAll();
    }), System.Windows.Threading.DispatcherPriority.Input);
}
