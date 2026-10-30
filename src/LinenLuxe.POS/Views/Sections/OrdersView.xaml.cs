using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.ViewModels.Sections;

namespace LinenLuxe.POS.Views.Sections;

public partial class OrdersView : UserControl
{
    private Window? _window;

    public OrdersView()
    {
        InitializeComponent();

        // Shortcuts live on the window, not on this control: a handler here only
        // sees keys while focus is already inside the list, so Ctrl+F did nothing
        // straight after clicking the sidebar. Unloaded detaches them again so the
        // next screen doesn't inherit them.
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
        if (DataContext is not OrdersViewModel vm) return;

        // HasFlag, not equality: a stray Shift or a sticky modifier shouldn't stop
        // a shortcut working in the middle of a shift.
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
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

            // Ctrl+Shift+P — the receipt. Checked before plain Ctrl+P, or holding
            // Shift would quietly print the A4 invoice instead of what was asked
            // for, which is worse than the shortcut not working at all.
            case Key.P when ctrl && shift:
                vm.PrintReceiptCommand.Execute(null);
                e.Handled = true;
                return;

            // Ctrl+P — the A4 invoice.
            case Key.P when ctrl:
                vm.PrintCommand.Execute(null);
                e.Handled = true;
                return;

            // Ctrl+R — the receipt again, on a key that is easy to reach with one
            // hand at a counter and that no browser or terminal claims.
            case Key.R when ctrl:
                vm.PrintReceiptCommand.Execute(null);
                e.Handled = true;
                return;

            // ↑ ↓ walk the list without leaving the search box
            case Key.Down or Key.Up when vm.Orders.Count > 0:
                Move(vm, e.Key == Key.Down ? +1 : -1);
                e.Handled = true;
                return;

            case Key.Enter when vm.SelectedOrder is not null:
                vm.OpenCommand.Execute(vm.SelectedOrder);
                e.Handled = true;
                return;

            // Ctrl+→ / Ctrl+← walk the order along its lifecycle, as the shortcut
            // shortcut scheme specifies.
            case Key.Right when ctrl:
                vm.AdvanceStatusCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Left when ctrl:
                vm.RewindStatusCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Escape:
                vm.Search = "";
                SearchBox.Focus();
                e.Handled = true;
                return;
        }
    }

    private void Move(OrdersViewModel vm, int delta)
    {
        if (vm.Orders.Count == 0) return;
        var current = vm.SelectedOrder is null ? -1 : vm.Orders.IndexOf(vm.SelectedOrder);
        var next = Math.Clamp(current + delta, 0, vm.Orders.Count - 1);
        vm.SelectedOrder = vm.Orders[next];
        OrderList.ScrollIntoView(vm.SelectedOrder);
    }

    /// <summary>
    /// Double-clicking an order opens it. The row under the pointer is resolved
    /// explicitly: ListBox raises this for the empty space below the last row too,
    /// and there a double-click would otherwise open whatever was highlighted.
    /// </summary>
    private void OnOrderDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not OrdersViewModel vm) return;

        if (ItemsControl.ContainerFromElement(OrderList, (DependencyObject)e.OriginalSource)
            is not ListBoxItem { DataContext: PosOrder order }) return;

        vm.OpenCommand.Execute(order);
    }
}
