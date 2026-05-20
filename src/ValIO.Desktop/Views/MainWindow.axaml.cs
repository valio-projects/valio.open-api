using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ValIO.Desktop.ViewModels;

namespace ValIO.Desktop.Views;

public partial class MainWindow : Window
{
    private static readonly string[] AcceptedSchemaExtensions = [".json", ".yaml", ".yml"];

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

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

    /// <summary>
    /// Show the OS "copy here" cursor only when the user drags an actual openapi-shaped
    /// file (.json / .yaml / .yml). Anything else gets DragDropEffects.None.
    /// </summary>
    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = IsSchemaDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!IsSchemaDrop(e)) return;
        var path = GetFirstSchemaPath(e);
        if (string.IsNullOrEmpty(path)) return;

        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ShowGenerate();
            SetActive(GenerateNavButton, SettingsNavButton);
            mainVm.GenerationViewModel.SetSchemaPath(path);
        }
    }

    private static bool IsSchemaDrop(DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return false;
        var path = GetFirstSchemaPath(e);
        return !string.IsNullOrEmpty(path);
    }

    private static string? GetFirstSchemaPath(DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files is null) return null;
        foreach (var f in files)
        {
            var p = f.TryGetLocalPath();
            if (string.IsNullOrEmpty(p)) continue;
            var ext = Path.GetExtension(p).ToLowerInvariant();
            if (Array.IndexOf(AcceptedSchemaExtensions, ext) >= 0) return p;
        }
        return null;
    }
}
