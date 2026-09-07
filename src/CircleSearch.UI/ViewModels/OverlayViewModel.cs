using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CircleSearch.UI.ViewModels;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private string _statusText = "✨ Обведите объект или кликните по нему";

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}