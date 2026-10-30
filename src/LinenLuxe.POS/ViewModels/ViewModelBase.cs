using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LinenLuxe.POS.ViewModels;

/// <summary>
/// Base class for all view models. Implements INotifyPropertyChanged so the UI
/// updates automatically when a bound property changes.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raise the change notification for the given (or calling) property.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Set a backing field and notify the UI only if the value actually changed.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
