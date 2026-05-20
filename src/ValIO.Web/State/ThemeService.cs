using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ValIO.Web.State;

/// <summary>
/// Per-circuit dark/light mode toggle. <see cref="MainLayout"/> binds the
/// <c>MudThemeProvider.IsDarkMode</c> to <see cref="IsDark"/>, and the cookie
/// persistence layer (JS interop, see Home.razor on first load) hydrates this
/// from <c>valio-theme</c>.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    private bool _isDark;

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark == value) return;
            _isDark = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
