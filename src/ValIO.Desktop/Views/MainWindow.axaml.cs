using Avalonia.Controls;
using Avalonia.Interactivity;
using ValIO.Desktop.ViewModels;

namespace ValIO.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnShowGenerate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ShowGenerate();
        SetActive(GenerateNavButton, SettingsNavButton);
    }

    private void OnShowSettings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ShowSettings();
        SetActive(SettingsNavButton, GenerateNavButton);
    }

    private static void SetActive(Button active, Button inactive)
    {
        if (!active.Classes.Contains("active")) active.Classes.Add("active");
        inactive.Classes.Remove("active");
    }
}
