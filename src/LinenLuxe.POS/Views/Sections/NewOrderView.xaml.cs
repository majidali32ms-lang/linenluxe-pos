using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.ViewModels.Sections;

namespace LinenLuxe.POS.Views.Sections;

public partial class NewOrderView : UserControl
{
    private NewOrderViewModel? _vm;
    private Window? _window;

    public NewOrderView()
    {
        InitializeComponent();

        // Staff can start typing the item name the moment the screen opens.
        Loaded += (_, _) => { HookWindow(); FocusSearch(); };
        Unloaded += (_, _) => UnhookWindow();
        DataContextChanged += OnDataContextChanged;
    }

    // ===================== Keyboard reach =====================

    /// <summary>
    /// Screen shortcuts are handled on the WINDOW, not on this control.
    /// A handler here only ever sees keys while the focus sits inside this view,
    /// so after clicking the sidebar — or after the customer overlay closed and
    /// left focus on the window — Ctrl+F did nothing and staff had to click into
    /// the search box first. Listening at the window means the shortcuts work
    /// from anywhere while the New Order screen is the one on display; Unloaded
    /// detaches again so no other screen inherits them.
    /// </summary>
    private void HookWindow()
    {
        if (_window is not null) return;
        _window = Window.GetWindow(this);
        if (_window is not null) _window.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void UnhookWindow()
    {
        if (_window is null) return;
        _window.PreviewKeyDown -= OnPreviewKeyDown;
        _window = null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
        _vm = e.NewValue as NewOrderViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>
    /// Put the caret back in the search box at the two moments it would otherwise
    /// be stranded: when the customer overlay closes, and after an order saves.
    /// Both end with staff about to type the next item.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;

        var overlayClosed = e.PropertyName == nameof(NewOrderViewModel.IsCustomerPickerOpen)
                            && !_vm.IsCustomerPickerOpen;
        var orderSaved = e.PropertyName == nameof(NewOrderViewModel.SavedMessage)
                         && _vm.HasSavedMessage;

        if (overlayClosed || orderSaved) FocusSearch();
    }

    // Queued behind the layout pass — right after an overlay closes the box is
    // not yet focusable, and Focus() would silently do nothing.
    private void FocusSearch() => Dispatcher.BeginInvoke(new Action(() =>
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }), DispatcherPriority.Input);

    // ===================== Shortcuts =====================

    /// <summary>
    /// Handled here (rather than as KeyBindings) because these keys must behave
    /// differently while the staff member is typing in a field.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not NewOrderViewModel vm) return;

        // While the customer overlay is open it owns the keyboard — otherwise this
        // handler (which tunnels first) would steal its Esc/Enter/arrow keys.
        if (vm.IsCustomerPickerOpen) return;

        var typing = e.OriginalSource is TextBox;
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;

        // Enter and the arrow keys only drive the item picker while the cursor is
        // actually in it. Otherwise pressing Enter after touching the discount box,
        // Express or the cart would silently add the highlighted item again.
        var inPicker = SearchBox.IsKeyboardFocusWithin || PickerList.IsKeyboardFocusWithin;

        switch (e.Key)
        {
            // Ctrl+F — jump to the search box
            case Key.F when ctrl:
                FocusSearch();
                e.Handled = true;
                return;

            // Ctrl+D — jump to the discount box
            case Key.D when ctrl:
                DiscountBox.Focus();
                DiscountBox.SelectAll();
                e.Handled = true;
                return;

            // Ctrl+M — jump to the staff note
            case Key.M when ctrl:
                NotesBox.Focus();
                NotesBox.SelectAll();
                e.Handled = true;
                return;

            // ↑ ↓ — move the highlighted item (also while typing in search)
            case Key.Down when inPicker:
                vm.MovePickerSelection(+1);
                PickerList.ScrollIntoView(vm.SelectedPickerItem);
                e.Handled = true;
                return;

            case Key.Up when inPicker:
                vm.MovePickerSelection(-1);
                PickerList.ScrollIntoView(vm.SelectedPickerItem);
                e.Handled = true;
                return;

            // Enter — add the highlighted item. Only from the picker, and never
            // with Ctrl held (Ctrl+Enter is reserved for save & invoice).
            case Key.Enter when inPicker && Keyboard.Modifiers == ModifierKeys.None:
                vm.AddSelectedCommand.Execute(null);
                e.Handled = true;
                return;

            // Enter in the discount box: accept the amount and go back to searching.
            case Key.Enter when ReferenceEquals(e.OriginalSource, DiscountBox):
                FocusSearch();
                e.Handled = true;
                return;

            // + / − — adjust the selected order line (never while typing)
            case Key.OemPlus or Key.Add when !typing:
                vm.IncreaseQtyCommand.Execute(vm.SelectedLine);
                e.Handled = true;
                return;

            case Key.OemMinus or Key.Subtract when !typing:
                vm.DecreaseQtyCommand.Execute(vm.SelectedLine);
                e.Handled = true;
                return;

            // Esc — clear the search and return focus to it
            case Key.Escape:
                vm.ClearSearch();
                FocusSearch();
                e.Handled = true;
                return;
        }
    }

    /// <summary>
    /// Double-clicking a row adds it, exactly like pressing Enter on it — staff
    /// who reach for the mouse shouldn't have to switch to the keyboard.
    /// </summary>
    private void OnPickerDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NewOrderViewModel vm) return;

        // Resolve the row actually under the pointer. ListBox raises this event
        // for the empty space below the last item too, and there a double-click
        // would otherwise add whatever happened to still be highlighted.
        if (ItemsControl.ContainerFromElement(PickerList, (DependencyObject)e.OriginalSource)
            is not ListBoxItem { DataContext: PosItem item }) return;

        vm.SelectedPickerItem = item;
        vm.AddSelectedCommand.Execute(null);

        // Back to the search box so the next item can just be typed.
        FocusSearch();
    }
}
