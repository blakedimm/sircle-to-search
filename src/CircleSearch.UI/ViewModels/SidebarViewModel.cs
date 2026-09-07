using System.ComponentModel;
using System.Runtime.CompilerServices;
using CircleSearch.Core.Models;

namespace CircleSearch.UI.ViewModels;

public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private WindowDisplayMode _mode = WindowDisplayMode.Compact;
    private bool _isLoading = true;

    public WindowDisplayMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}