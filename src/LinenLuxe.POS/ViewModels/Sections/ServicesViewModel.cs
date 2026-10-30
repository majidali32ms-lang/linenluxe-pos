using System.Collections.ObjectModel;
using LinenLuxe.POS.Models;
using LinenLuxe.POS.Services.Data;
using LinenLuxe.POS.Services.Search;
using Supabase.Postgrest;

namespace LinenLuxe.POS.ViewModels.Sections;

/// <summary>
/// Services &amp; Pricing — the shop's price list, and the only place
/// it can be changed.
///
/// Rates are edited in the list itself rather than one at a time in a side panel.
/// A price rise arrives as a page of new rates, not as one number, and a screen
/// that costs three clicks per item is a screen that gets left out of date — which
/// is worse than a plain list, because staff then trust a figure that is wrong.
///
/// Nothing is saved as it is typed. Changed rows are marked, counted, and written
/// together when Save is pressed, so a mistyped rate can be taken back before it
/// reaches a customer.
///
/// Two things a price change deliberately does NOT do:
///   * it does not touch orders already taken — each order line stores the price it
///     was sold at, so last month's invoice still adds up;
///   * it does not delete anything. An item that is no longer offered is switched
///     off, because an order taken last week names it.
/// </summary>
public sealed class ServicesViewModel : ViewModelBase
{
    private const string AllCategories = "All";

    public const string ShowActive = "In use";
    public const string ShowHidden = "Hidden";
    public const string ShowEverything = "All items";

    private List<ServiceItemViewModel> _all = [];

    public ServicesViewModel()
    {
        Categories = [AllCategories];

        // Each chip carries its own count. Without them "In use" and "All items"
        // show the same 163 rows and look broken, when in fact nothing is hidden —
        // a screen that cannot tell those two apart makes the owner hunt for a bug
        // that isn't there.
        Shown = [new(ShowActive), new(ShowHidden), new(ShowEverything)];
        _selectedShown = Shown[0];

        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        DiscardCommand = new RelayCommand(Discard);
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        NewItemCommand = new RelayCommand(StartNewItem);
        CancelNewItemCommand = new RelayCommand(CancelNewItem);
        AddItemCommand = new RelayCommand(async () => await AddItemAsync(), () => CanAddItem);

        _ = LoadAsync();
    }

    /// <summary>The filtered rows shown in the list.</summary>
    public ObservableCollection<ServiceItemViewModel> Items { get; } = [];

    /// <summary>"All" plus every category found in the price list.</summary>
    public ObservableCollection<string> Categories { get; }

    /// <summary>In use · Hidden · All items, each with how many it holds.</summary>
    public IReadOnlyList<ShownOption> Shown { get; }

    private ShownOption _selectedShown;
    public ShownOption SelectedShown
    {
        get => _selectedShown;
        set
        {
            if (value is null || !SetProperty(ref _selectedShown, value)) return;
            Showing = value.Key;
        }
    }

    // ----- Search -----
    private string _search = "";
    public string Search
    {
        get => _search;
        set
        {
            if (!SetProperty(ref _search, value)) return;
            OnPropertyChanged(nameof(IsSearchEmpty));
            ApplyFilter();
        }
    }

    public bool IsSearchEmpty => string.IsNullOrEmpty(_search);

    // ----- Filters -----
    private string _selectedCategory = AllCategories;
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value)) ApplyFilter(); }
    }

    private string _showing = ShowActive;
    /// <summary>
    /// Defaults to the items actually in use. A hidden item is hidden for a reason,
    /// and mixing the two by default would put a discontinued service back in front
    /// of whoever is pricing an order.
    /// </summary>
    public string Showing
    {
        get => _showing;
        set { if (SetProperty(ref _showing, value)) ApplyFilter(); }
    }

    // ----- Load / error state -----
    private bool _loading = true;
    public bool Loading
    {
        get => _loading;
        private set { if (SetProperty(ref _loading, value)) OnPropertyChanged(nameof(ShowList)); }
    }

    private string _error = "";
    public string Error
    {
        get => _error;
        private set
        {
            if (!SetProperty(ref _error, value)) return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowList));
        }
    }

    public bool HasError => Error.Length > 0;
    public bool ShowList => !Loading && !HasError;

    /// <summary>
    /// A write that failed, kept apart from <see cref="Error"/>. A load failure has
    /// no list to show; a save failure has one, and hiding it would take away the
    /// very rows the message is about.
    /// </summary>
    private string _saveError = "";
    public string SaveError
    {
        get => _saveError;
        private set { if (SetProperty(ref _saveError, value)) OnPropertyChanged(nameof(HasSaveError)); }
    }

    public bool HasSaveError => SaveError.Length > 0;

    private string _message = "";
    public string Message
    {
        get => _message;
        private set { if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage)); }
    }

    public bool HasMessage => Message.Length > 0;

    private string _countText = "Loading the price list…";
    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    // ================= Unsaved changes =================

    public int DirtyCount => _all.Count(i => i.IsDirty);

    public bool HasChanges => DirtyCount > 0;

    /// <summary>The first thing wrong, said plainly. Empty when everything is fine.</summary>
    public string ChangeProblem =>
        _all.FirstOrDefault(i => i.IsDirty && !i.IsValid)?.Problem ?? "";

    public bool HasChangeProblem => ChangeProblem.Length > 0;

    public bool CanSave => HasChanges && !HasChangeProblem && !Saving;

    public string SaveLabel => Saving
        ? "Saving…"
        : DirtyCount == 1 ? "Save 1 change" : $"Save {DirtyCount} changes";

    private bool _saving;
    public bool Saving
    {
        get => _saving;
        private set
        {
            if (!SetProperty(ref _saving, value)) return;
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(SaveLabel));
        }
    }

    private void RowChanged()
    {
        OnPropertyChanged(nameof(DirtyCount));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ChangeProblem));
        OnPropertyChanged(nameof(HasChangeProblem));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveLabel));
        Message = "";
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand NewItemCommand { get; }
    public RelayCommand CancelNewItemCommand { get; }
    public RelayCommand AddItemCommand { get; }

    // ================= Loading =================

    /// <summary>Reads the catalog from Supabase (staff session required by RLS).</summary>
    private async Task LoadAsync()
    {
        Loading = true;
        Error = "";
        Message = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            // Hidden items are loaded too — this is the screen where they are
            // brought back, so it is the one screen that has to be able to see them.
            var result = await db.Client
                .From<PosItem>()
                .Order("sort_order", Constants.Ordering.Ascending)
                .Limit(1000)
                .Get();

            _all = [.. result.Models.Select(m => new ServiceItemViewModel(m, RowChanged))];

            Categories.Clear();
            Categories.Add(AllCategories);
            foreach (var c in _all.Select(i => i.Category).Distinct().OrderBy(c => c))
                Categories.Add(c);

            Loading = false;
            RowChanged();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Loading = false;
            Error = "Could not load the price list. " + ex.Message;
        }
    }

    /// <summary>Apply the filters and search text to the full list.</summary>
    private void ApplyFilter()
    {
        IEnumerable<ServiceItemViewModel> list = Showing switch
        {
            ShowActive => _all.Where(i => i.IsActive),
            ShowHidden => _all.Where(i => !i.IsActive),
            _ => _all,
        };

        if (SelectedCategory != AllCategories)
            list = list.Where(i => i.Category == SelectedCategory);

        // Same relevance search as the New Order screen, run over the models.
        var pool = list.ToList();
        var matched = ItemSearch.Filter(pool.Select(i => i.Item), Search);
        var byItem = pool.ToDictionary(i => i.Item);

        Items.Clear();
        foreach (var item in matched)
            if (byItem.TryGetValue(item, out var row))
                Items.Add(row);

        var inUse = _all.Count(i => i.IsActive);
        var hidden = _all.Count - inUse;

        Shown[0].Count = inUse;
        Shown[1].Count = hidden;
        Shown[2].Count = _all.Count;

        CountText = Items.Count == _all.Count
            ? $"{_all.Count} items"
            : $"{Items.Count} of {_all.Count} items"
              + (hidden > 0 ? $"  ·  {hidden} hidden" : "");

        OnPropertyChanged(nameof(ShowNothingFound));
        OnPropertyChanged(nameof(NothingFoundTitle));
        OnPropertyChanged(nameof(NothingFoundDetail));
    }

    // ================= When the list comes back empty =================

    public bool ShowNothingFound => ShowList && Items.Count == 0;

    /// <summary>
    /// A blank page is the one answer a screen must never give: it reads as broken.
    /// These say which of the three reasons it is — nothing hidden yet, a filter
    /// that excludes everything, or a search that matched nothing.
    /// </summary>
    public string NothingFoundTitle => Showing switch
    {
        ShowHidden when _all.All(i => i.IsActive) => "Nothing is hidden",
        _ when Search.Trim().Length > 0 => $"Nothing matches “{Search.Trim()}”",
        _ => "No items here",
    };

    public string NothingFoundDetail => Showing switch
    {
        ShowHidden when _all.All(i => i.IsActive) =>
            "Every item on the price list is being offered. To stop offering one: go to "
            + "In use, find the item, click the ticked box in its OFFERED? column so it "
            + "reads Hidden — then press Save at the foot of the screen. It only moves "
            + "here once it is saved. It comes back the same way; nothing is ever deleted.",
        _ when Search.Trim().Length > 0 =>
            "Check the spelling, or clear the search with Esc. Remember this list is "
            + "showing " + Showing.ToLowerInvariant() + " only.",
        _ => "Try another category, or switch to All items.",
    };

    // ================= Saving =================

    private async Task SaveAsync()
    {
        if (!CanSave) return;

        var changed = _all.Where(i => i.IsDirty).ToList();
        if (changed.Count == 0) return;

        Saving = true;
        SaveError = "";
        Message = "";

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            var saved = 0;
            var refused = new List<string>();

            foreach (var row in changed)
            {
                row.ApplyTo(row.Item);

                // PostgREST answers a write that changed nothing with 200 and an
                // empty list, not with an error — a row the policy would not let
                // through comes back looking exactly like success. Saying "saved"
                // there is the worst thing this screen could do: the counter would
                // charge the old rate believing it had been put up.
                var response = await row.Item.Update<PosItem>();
                if (response.Models.Count == 0) refused.Add(row.Name);
                else saved++;
            }

            // Reloaded rather than marked clean in place: the row objects now have
            // to forget what they were loaded with, and reading it back is also the
            // only proof that what was sent is what the database kept.
            await LoadAsync();

            if (refused.Count > 0)
                SaveError = $"The database did not accept {Names(refused)} — nothing was changed for "
                            + (refused.Count == 1 ? "it" : "them")
                            + ". The list below shows what is actually stored. "
                            + "Sign out and back in, then try once more.";

            Message = saved switch
            {
                0 => "",
                1 => "1 price saved.",
                _ => $"{saved} prices saved.",
            };
        }
        catch (Exception ex)
        {
            SaveError = "Could not save the price list. " + ex.Message;
        }
        finally
        {
            Saving = false;
        }
    }

    /// <summary>"Kandura", "Kandura and Tie", "Kandura, Tie and 3 more".</summary>
    private static string Names(List<string> names) => names.Count switch
    {
        1 => $"\"{names[0]}\"",
        2 => $"\"{names[0]}\" and \"{names[1]}\"",
        _ => $"\"{names[0]}\", \"{names[1]}\" and {names.Count - 2} more",
    };

    private void Discard()
    {
        foreach (var row in _all) row.Revert();
        RowChanged();
        ApplyFilter();
        Message = "Changes thrown away — nothing was saved.";
    }

    // ================= Adding an item =================

    private bool _isAddingItem;
    public bool IsAddingItem
    {
        get => _isAddingItem;
        private set => SetProperty(ref _isAddingItem, value);
    }

    private string _newCategory = "";
    public string NewCategory
    {
        get => _newCategory;
        set { if (SetProperty(ref _newCategory, value)) RaiseNewItemChanged(); }
    }

    private string _newName = "";
    public string NewName
    {
        get => _newName;
        set { if (SetProperty(ref _newName, value)) RaiseNewItemChanged(); }
    }

    private string _newCleanPress = "";
    public string NewCleanPress
    {
        get => _newCleanPress;
        set { if (SetProperty(ref _newCleanPress, value)) RaiseNewItemChanged(); }
    }

    private string _newPressOnly = "";
    public string NewPressOnly
    {
        get => _newPressOnly;
        set { if (SetProperty(ref _newPressOnly, value)) RaiseNewItemChanged(); }
    }

    private string _newItemError = "";
    public string NewItemError
    {
        get => _newItemError;
        private set { if (SetProperty(ref _newItemError, value)) OnPropertyChanged(nameof(HasNewItemError)); }
    }

    public bool HasNewItemError => NewItemError.Length > 0;

    /// <summary>Existing categories, so a new item joins one rather than inventing a near-duplicate.</summary>
    public IEnumerable<string> CategoryChoices => Categories.Where(c => c != AllCategories);

    public bool CanAddItem =>
        NewName.Trim().Length >= 2
        && NewCategory.Trim().Length > 0
        && (ServiceItemViewModel.Parse(NewCleanPress) is not null
            || ServiceItemViewModel.Parse(NewPressOnly) is not null)
        && !Saving;

    private void RaiseNewItemChanged()
    {
        NewItemError = "";
        OnPropertyChanged(nameof(CanAddItem));
    }

    private void StartNewItem()
    {
        NewCategory = SelectedCategory == AllCategories
            ? Categories.Skip(1).FirstOrDefault() ?? ""
            : SelectedCategory;
        NewName = "";
        NewCleanPress = "";
        NewPressOnly = "";
        NewItemError = "";
        IsAddingItem = true;
    }

    private void CancelNewItem() => IsAddingItem = false;

    private async Task AddItemAsync()
    {
        if (!CanAddItem) return;

        var category = NewCategory.Trim();
        var name = NewName.Trim();

        // The table refuses two items with the same name in one category, and a
        // rejected insert reads like a database fault. Said here instead, in words.
        if (_all.Any(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewItemError = $"\"{name}\" is already in {category}. Change its price in the list instead.";
            return;
        }

        Saving = true;

        try
        {
            var db = SupabaseService.Instance;
            await db.InitializeAsync();

            // Added to the end of its own category, so the printed price-list order
            // the seed established is not disturbed by a late addition.
            var sameCategory = _all.Where(i => i.Category == category).ToList();
            var part = sameCategory.FirstOrDefault()?.Item.Part ?? "Special";
            var sort = sameCategory.Count > 0
                ? sameCategory.Max(i => i.Item.SortOrder) + 1
                : (_all.Count > 0 ? _all.Max(i => i.Item.SortOrder) + 1 : 1);

            var item = new PosItem
            {
                Part = part,
                Category = category,
                Name = name,
                CleanPressPrice = ServiceItemViewModel.Parse(NewCleanPress),
                PressOnlyPrice = ServiceItemViewModel.Parse(NewPressOnly),
                SortOrder = sort,
                IsActive = true,
            };

            var inserted = await db.Client.From<PosItem>().Insert(item);
            if (inserted.Models.FirstOrDefault() is null)
            {
                NewItemError = "The item did not save. Please try again.";
                return;
            }

            IsAddingItem = false;
            await LoadAsync();

            // Shown straight away, whichever filter was on, or it looks like nothing
            // happened when the new item lands outside the current category.
            SelectedCategory = category;
            Search = name;
            Message = $"\"{name}\" added to {category}.";
        }
        catch (Exception ex)
        {
            NewItemError = "Could not add the item. " + ex.Message;
        }
        finally
        {
            Saving = false;
        }
    }

    /// <summary>Esc on the screen clears the search box.</summary>
    public void ClearSearch() => Search = "";
}
