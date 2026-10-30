namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>One keyboard shortcut row (action + key combo).</summary>
public sealed class ShortcutItem
{
    public required string Action { get; init; }
    public required string Keys { get; init; }
}

/// <summary>A titled group of related shortcuts.</summary>
public sealed class ShortcutGroup
{
    public required string Title { get; init; }
    public required IReadOnlyList<ShortcutItem> Items { get; init; }
}

/// <summary>
/// Settings screen. Shows the full keyboard-shortcut reference plus basic
/// app info. More settings (VAT, printing, staff) get added in later phases.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    public string AppVersion => "v0.1";

    public IReadOnlyList<ShortcutGroup> ShortcutGroups { get; } =
    [
        new ShortcutGroup
        {
            Title = "Global / Navigation",
            Items =
            [
                new() { Action = "Help / User Guide",              Keys = "F1" },
                new() { Action = "New Order",                      Keys = "Ctrl+N" },
                new() { Action = "Search (customer / order / tag)",Keys = "Ctrl+F" },
                new() { Action = "Top-bar search (order/customer)", Keys = "Ctrl+K" },
                new() { Action = "Find Customer (by phone)",       Keys = "F2" },
                new() { Action = "Scan / enter Tag or Barcode",    Keys = "F3" },
                new() { Action = "Jump to sidebar section",        Keys = "Ctrl+1 … 9" },
                new() { Action = "Close dialog / cancel",          Keys = "Esc" },
                new() { Action = "Save form",                      Keys = "Ctrl+S" },
                new() { Action = "Print invoice",                  Keys = "Ctrl+P" },
                new() { Action = "Lock / logout",                  Keys = "Ctrl+L" },
            ],
        },
        new ShortcutGroup
        {
            Title = "New Order screen",
            Items =
            [
                new() { Action = "Add highlighted item",           Keys = "Enter" },
                new() { Action = "Quantity up / down",             Keys = "+ / −" },
                new() { Action = "Next field",                     Keys = "Tab" },
                new() { Action = "Apply discount",                 Keys = "Ctrl+D" },
                new() { Action = "Note for staff",                 Keys = "Ctrl+M" },
                new() { Action = "Toggle VAT",                     Keys = "Ctrl+T" },
                new() { Action = "Save order & generate invoice",  Keys = "Ctrl+Enter" },
                new() { Action = "Add / select customer",          Keys = "Alt+C" },
                new() { Action = "Payment method",                 Keys = "Alt+P" },
                new() { Action = "Paid / pending",                 Keys = "Alt+S" },
                new() { Action = "Urgent / express mark",          Keys = "Alt+U" },
            ],
        },
        new ShortcutGroup
        {
            Title = "Customers screen",
            Items =
            [
                new() { Action = "Search name / phone / area",      Keys = "Ctrl+F" },
                new() { Action = "Move between customers",          Keys = "↑ / ↓" },
                new() { Action = "New walk-in customer",            Keys = "Alt+N" },
                new() { Action = "New business account",            Keys = "Alt+B" },
                new() { Action = "Save the customer",               Keys = "Ctrl+S" },
                new() { Action = "Export the list to a spreadsheet",Keys = "Ctrl+E" },
                new() { Action = "Reload the list",                 Keys = "F5" },
                new() { Action = "Clear the search",                Keys = "Esc" },
            ],
        },
        new ShortcutGroup
        {
            Title = "Select Customer dialog",
            Items =
            [
                new() { Action = "Search by phone or name",        Keys = "Enter" },
                new() { Action = "Back to the search box",         Keys = "Ctrl+F" },
                new() { Action = "Move through results",           Keys = "↑ / ↓" },
                new() { Action = "Use highlighted customer",       Keys = "Enter" },
                new() { Action = "Start a new customer",           Keys = "Alt+N" },
                new() { Action = "Save the new customer",          Keys = "Alt+S" },
                new() { Action = "Back out one step",              Keys = "Esc" },
            ],
        },
        new ShortcutGroup
        {
            Title = "Services & Pricing screen",
            Items =
            [
                new() { Action = "Search the price list",           Keys = "Ctrl+F" },
                new() { Action = "Add a new item",                  Keys = "Alt+N" },
                new() { Action = "Save the changed rates",          Keys = "Ctrl+S" },
                new() { Action = "Reload the price list",           Keys = "F5" },
                new() { Action = "Clear the search",                Keys = "Esc" },
            ],
        },
        new ShortcutGroup
        {
            Title = "Orders screen",
            Items =
            [
                new() { Action = "Search order no / name / phone", Keys = "Ctrl+F" },
                new() { Action = "Move between orders",            Keys = "↑ / ↓" },
                new() { Action = "Open order for editing",         Keys = "Enter" },
                new() { Action = "Advance status",                 Keys = "Ctrl+Right" },
                new() { Action = "Move status back",               Keys = "Ctrl+Left" },
                new() { Action = "Print A4 invoice",               Keys = "Ctrl+P" },
                new() { Action = "Print 80mm till receipt",        Keys = "Ctrl+R" },
                new() { Action = "Reload the list",                Keys = "F5" },
                new() { Action = "Clear the search",               Keys = "Esc" },
            ],
        },
    ];
}
