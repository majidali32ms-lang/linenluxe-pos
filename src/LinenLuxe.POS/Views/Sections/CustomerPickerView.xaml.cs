using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LinenLuxe.POS.ViewModels.Sections;

namespace LinenLuxe.POS.Views.Sections;

public partial class CustomerPickerView : UserControl
{
    private CustomerPickerViewModel? _vm;
    private Window? _window;

    public CustomerPickerView()
    {
        InitializeComponent();

        Loaded += (_, _) => { HookWindow(); Focus(SearchBox); };
        Unloaded += (_, _) => UnhookWindow();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Shortcuts are handled on the WINDOW, not on this control.
    ///
    /// A handler here only ever sees keys while focus sits inside the overlay, and
    /// as soon as focus moved — into the new-customer form, or onto a button —
    /// Esc stopped closing the dialog and staff had to reach for the mouse. The
    /// New Order screen listens at the window too, but it stands aside whenever
    /// this overlay is open, so the two never both act on a key.
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
        _vm = e.NewValue as CustomerPickerViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelChanged;
    }

    // Opening the form should put the caret in the first field staff will type in.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null || e.PropertyName != nameof(CustomerPickerViewModel.ShowWalkInForm)) return;
        Focus(_vm.ShowWalkInForm ? NewNameBox : SearchBox);
    }

    // Queued behind the layout pass — a box that has only just become visible is
    // not focusable yet, and Focus() would silently do nothing.
    private void Focus(TextBox box) => Dispatcher.BeginInvoke(new Action(() =>
    {
        box.Focus();
        box.SelectAll();
    }), DispatcherPriority.Input);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CustomerPickerViewModel vm) return;

        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        var alt = e.Key == Key.System || Keyboard.Modifiers == ModifierKeys.Alt;
        var inNewCustomer = ReferenceEquals(e.OriginalSource, NewNameBox)
                            || ReferenceEquals(e.OriginalSource, NewPhoneBox);

        // With Alt held, WPF reports Key.System and puts the real letter in
        // SystemKey — so Alt+N never reaches a plain switch on e.Key at all.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            // Esc backs out one step at a time: out of the new-customer form
            // first, then out of the dialog. Closing everything at once would
            // throw away a half-typed customer on a stray key press.
            case Key.Escape when vm.ShowNewForm:
                vm.BackToSearchCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Escape:
                vm.CancelCommand.Execute(null);
                e.Handled = true;
                return;

            // Alt+N — start a new customer
            case Key.N when alt:
                vm.NewCustomerCommand.Execute(null);
                e.Handled = true;
                return;

            // Alt+S — save the new customer
            case Key.S when alt && vm.ShowWalkInForm:
                vm.AddWalkInCommand.Execute(null);
                e.Handled = true;
                return;

            // Ctrl+F — back to the search box
            case Key.F when ctrl:
                if (vm.ShowNewForm) vm.BackToSearchCommand.Execute(null);
                Focus(SearchBox);
                e.Handled = true;
                return;

            // ↑ ↓ — move through the results without leaving the search box
            case Key.Down or Key.Up when vm.ShowResults && vm.Results.Count > 0:
                MoveResult(vm, key == Key.Down ? +1 : -1);
                e.Handled = true;
                return;

            // Enter inside the new-customer form saves it. Checked before the
            // result case below, or Enter would attach whichever customer was
            // still highlighted from an earlier search instead.
            case Key.Enter when inNewCustomer:
                vm.AddWalkInCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Enter when ReferenceEquals(e.OriginalSource, SearchBox):
                vm.SearchCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.Enter when vm.SelectedResult is not null:
                vm.ChooseCommand.Execute(vm.SelectedResult);
                e.Handled = true;
                return;
        }
    }

    private static void MoveResult(CustomerPickerViewModel vm, int delta)
    {
        if (vm.Results.Count == 0) return;
        var current = vm.SelectedResult is null ? -1 : vm.Results.IndexOf(vm.SelectedResult);
        var next = Math.Clamp(current + delta, 0, vm.Results.Count - 1);
        vm.SelectedResult = vm.Results[next];
    }

    // Double-clicking a customer picks them straight away
    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CustomerPickerViewModel { SelectedResult: not null } vm)
            vm.ChooseCommand.Execute(vm.SelectedResult);
    }
}
