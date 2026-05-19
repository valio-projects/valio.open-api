using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ValIO.Application.Services;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Desktop.Services;
using ValIO.Output.Abstractions;

namespace ValIO.Desktop.ViewModels;

public enum DesktopOutputFormat { Folder, Zip }

public sealed partial class GenerationViewModel : ObservableObject
{
    private readonly IGenerationOrchestrator _orchestrator;
    private readonly IOutputSinkFactory _sinkFactory;
    private readonly IGeneratorRegistry _registry;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<GenerationViewModel> _logger;

    public ObservableCollection<IGeneratorPlugin> AvailablePlugins { get; }
    public ObservableCollection<OptionFieldViewModel> OptionFields { get; } = new();

    [ObservableProperty] private IGeneratorPlugin? _selectedPlugin;
    [ObservableProperty] private string? _schemaPath;
    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private string? _namespaceName;
    [ObservableProperty] private bool _generateModels = true;
    [ObservableProperty] private bool _generateClients = true;
    [ObservableProperty] private bool _generateEnums = true;
    [ObservableProperty] private DesktopOutputFormat _format = DesktopOutputFormat.Folder;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    public GenerationViewModel(
        IGenerationOrchestrator orchestrator,
        IOutputSinkFactory sinkFactory,
        IGeneratorRegistry registry,
        ISettingsStore settingsStore,
        ILogger<GenerationViewModel> logger)
    {
        _orchestrator = orchestrator;
        _sinkFactory = sinkFactory;
        _registry = registry;
        _settingsStore = settingsStore;
        _logger = logger;

        AvailablePlugins = new ObservableCollection<IGeneratorPlugin>(_registry.AllPlugins);

        var settings = settingsStore.Load();
        SchemaPath = settings.LastSchemaFile;
        OutputPath = settings.LastOutputFolder;
        if (!string.IsNullOrEmpty(settings.LastLanguageId))
            SelectedPlugin = _registry.FindPlugin(settings.LastLanguageId);
    }

    partial void OnSelectedPluginChanged(IGeneratorPlugin? value)
    {
        OptionFields.Clear();
        if (value is null) return;
        foreach (var desc in value.OptionsDescriptor.GetOptionDescriptions())
            OptionFields.Add(new OptionFieldViewModel(desc));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnSchemaPathChanged(string? value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnOutputPathChanged(string? value) => GenerateCommand.NotifyCanExecuteChanged();

    public bool CanGenerate =>
        !IsBusy && SelectedPlugin is not null &&
        !string.IsNullOrWhiteSpace(SchemaPath) &&
        !string.IsNullOrWhiteSpace(OutputPath);

    [RelayCommand]
    private async Task PickSchemaAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open OpenAPI schema",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("OpenAPI schema") { Patterns = ["*.json", "*.yaml", "*.yml"] }
            ]
        });
        if (files.Count > 0) SchemaPath = files[0].TryGetLocalPath();
    }

    [RelayCommand]
    private async Task PickOutputAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        if (Format == DesktopOutputFormat.Zip)
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save as zip archive",
                SuggestedFileName = $"valio-{SelectedPlugin?.LanguageId ?? "generated"}.zip",
                FileTypeChoices = [new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] }]
            });
            if (file is not null) OutputPath = file.TryGetLocalPath();
        }
        else
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose output folder",
                AllowMultiple = false
            });
            if (folders.Count > 0) OutputPath = folders[0].TryGetLocalPath();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync(CancellationToken ct)
    {
        IsBusy = true;
        StatusMessage = "Generating...";
        try
        {
            var config = new GenerationConfiguration
            {
                LanguageId = SelectedPlugin!.LanguageId,
                OutputPath = OutputPath!,
                Namespace = NamespaceName,
                GenerateModels = GenerateModels,
                GenerateClients = GenerateClients,
                GenerateEnums = GenerateEnums,
            };

            var result = await _orchestrator.GenerateAsync(config, SchemaPath!, ct);
            if (!result.Success)
            {
                StatusMessage = $"Failed: {result.ErrorMessage}";
                _logger.LogError("Generation failed: {Error}", result.ErrorMessage);
                return;
            }

            var target = Format == DesktopOutputFormat.Zip
                ? OutputTarget.ZipPath(OutputPath!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    ? OutputPath!
                    : OutputPath! + ".zip")
                : OutputTarget.Folder(OutputPath!);

            await using var sink = _sinkFactory.Create(target);
            await sink.WriteAsync(result.Files, ct);

            StatusMessage = $"Wrote {result.Files.Count} files to {target.Path}";

            var settings = _settingsStore.Load();
            settings.LastSchemaFile = SchemaPath;
            settings.LastOutputFolder = OutputPath;
            settings.LastLanguageId = SelectedPlugin.LanguageId;
            _settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Desktop generation threw");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
