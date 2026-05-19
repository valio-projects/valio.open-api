using Avalonia.Controls;
using Avalonia.Interactivity;
using ValIO.Desktop.ViewModels;

namespace ValIO.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnShowGenerate(object? sender, RoutedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ShowGenerate();

    private void OnShowSettings(object? sender, RoutedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ShowSettings();
}
