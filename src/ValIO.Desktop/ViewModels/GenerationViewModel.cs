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
    private readonly ISchemaPreviewer _previewer;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<GenerationViewModel> _logger;

    private const int RecentLimit = 8;

    public ObservableCollection<IGeneratorPlugin> AvailablePlugins { get; }
    public ObservableCollection<OptionFieldViewModel> OptionFields { get; } = new();
    public ObservableCollection<string> RecentSchemas { get; } = new();

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
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private int _hiddenOptionsCount;

    // Schema preview properties
    [ObservableProperty] private bool _hasSchemaPreview;
    [ObservableProperty] private string? _schemaTitle;
    [ObservableProperty] private string? _schemaVersion;
    [ObservableProperty] private int _schemaPathCount;
    [ObservableProperty] private int _schemaOperationCount;
    [ObservableProperty] private int _schemaModelCount;
    [ObservableProperty] private string? _schemaError;

    private CancellationTokenSource? _previewCts;

    public GenerationViewModel(
        IGenerationOrchestrator orchestrator,
        IOutputSinkFactory sinkFactory,
        IGeneratorRegistry registry,
        ISchemaPreviewer previewer,
        ISettingsStore settingsStore,
        ILogger<GenerationViewModel> logger)
    {
        _orchestrator = orchestrator;
        _sinkFactory = sinkFactory;
        _registry = registry;
        _previewer = previewer;
        _settingsStore = settingsStore;
        _logger = logger;

        AvailablePlugins = new ObservableCollection<IGeneratorPlugin>(_registry.AllPlugins);

        var settings = settingsStore.Load();
        SchemaPath = settings.LastSchemaFile;
        OutputPath = settings.LastOutputFolder;
        foreach (var recent in settings.RecentSchemas) RecentSchemas.Add(recent);
        if (!string.IsNullOrEmpty(settings.LastLanguageId))
            SelectedPlugin = _registry.FindPlugin(settings.LastLanguageId);
    }

    partial void OnSelectedPluginChanged(IGeneratorPlugin? value)
    {
        foreach (var f in OptionFields)
            f.ValueChanged -= OnAnyOptionChanged;
        OptionFields.Clear();

        if (value is not null)
        {
            foreach (var desc in value.OptionsDescriptor.GetOptionDescriptions())
            {
                var vm = new OptionFieldViewModel(desc);
                vm.ValueChanged += OnAnyOptionChanged;
                OptionFields.Add(vm);
            }
            RecomputeDependsOn();
        }

        GenerateCommand.NotifyCanExecuteChanged();
    }

    private void OnAnyOptionChanged(object? sender, EventArgs e) => RecomputeDependsOn();

    private void RecomputeDependsOn()
    {
        var hidden = 0;
        foreach (var field in OptionFields)
        {
            if (string.IsNullOrEmpty(field.Description.DependsOn))
            {
                field.IsVisible = true;
                continue;
            }
            var dep = OptionFields.FirstOrDefault(
                f => string.Equals(f.Description.Name, field.Description.DependsOn, StringComparison.OrdinalIgnoreCase));
            field.IsVisible = dep is not null && dep.HasValue;
            if (!field.IsVisible) hidden++;
        }
        HiddenOptionsCount = hidden;
    }

    partial void OnSchemaPathChanged(string? value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        _ = RefreshSchemaPreviewAsync(value);
    }

    partial void OnOutputPathChanged(string? value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => GenerateCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Parse the schema in the background and expose a quick summary so users see
    /// "this is the Petstore API, v1.0.21, 14 paths, 8 models" before hitting Generate.
    /// </summary>
    private async Task RefreshSchemaPreviewAsync(string? path)
    {
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HasSchemaPreview = false;
            SchemaError = null;
            return;
        }
        try
        {
            await Task.Delay(150, ct); // debounce schema swaps during typing
            var preview = await _previewer.PreviewAsync(path, ct);
            if (ct.IsCancellationRequested) return;
            if (preview is null)
            {
                HasSchemaPreview = false;
                SchemaError = "Could not parse schema";
                return;
            }
            SchemaTitle = preview.Title;
            SchemaVersion = preview.Version;
            SchemaPathCount = preview.Paths;
            SchemaOperationCount = preview.Operations;
            SchemaModelCount = preview.Components;
            SchemaError = null;
            HasSchemaPreview = true;
        }
        catch (TaskCanceledException) { /* superseded by newer change */ }
        catch (Exception ex)
        {
            HasSchemaPreview = false;
            SchemaError = ex.Message;
        }
    }

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
        if (files.Count > 0) SetSchemaPath(files[0].TryGetLocalPath());
    }

    /// <summary>
    /// Called from drag&drop and the file picker. Updates <see cref="SchemaPath"/>
    /// and bubbles it into <see cref="RecentSchemas"/> (LRU, capped to 8).
    /// </summary>
    public void SetSchemaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        SchemaPath = path;
        RecentSchemas.Remove(path);
        RecentSchemas.Insert(0, path);
        while (RecentSchemas.Count > RecentLimit) RecentSchemas.RemoveAt(RecentSchemas.Count - 1);

        var settings = _settingsStore.Load();
        settings.RecentSchemas = RecentSchemas.ToList();
        _settingsStore.Save(settings);
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
        HasError = false;
        StatusMessage = "Generating...";
        try
        {
            var pluginOptionsJson = new Dictionary<string, System.Text.Json.JsonElement>();
            foreach (var field in OptionFields)
            {
                if (!field.IsVisible) continue;
                if (!field.HasValue) continue;
                pluginOptionsJson[field.Description.Name] = ToJsonElement(field.CurrentValue);
            }

            object? pluginOptions = null;
            if (SelectedPlugin is not null && pluginOptionsJson.Count > 0)
            {
                pluginOptions = SelectedPlugin.OptionsDescriptor.ParseOptions(pluginOptionsJson, out var optErrors);
                if (optErrors.Count > 0)
                {
                    HasError = true;
                    StatusMessage = "Invalid options: " + string.Join("; ", optErrors);
                    return;
                }
            }

            var config = new GenerationConfiguration
            {
                LanguageId = SelectedPlugin!.LanguageId,
                OutputPath = OutputPath!,
                Namespace = NamespaceName,
                GenerateModels = GenerateModels,
                GenerateClients = GenerateClients,
                GenerateEnums = GenerateEnums,
                PluginOptions = pluginOptions,
            };

            var result = await _orchestrator.GenerateAsync(config, SchemaPath!, ct);
            if (!result.Success)
            {
                HasError = true;
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
            HasError = true;
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Desktop generation threw");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static System.Text.Json.JsonElement ToJsonElement(object? value)
        => System.Text.Json.JsonSerializer.SerializeToElement(value);

    private static TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
