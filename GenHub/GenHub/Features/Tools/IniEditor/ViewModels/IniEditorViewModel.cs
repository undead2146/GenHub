using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Interfaces.Tools.IniEditor;
using GenHub.Core.Models.Tools.IniEditor;
using GenHub.Features.Tools.IniEditor.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Tools.IniEditor.ViewModels;

/// <summary>
/// ViewModel for the INI editor tool. Edits Generals and Zero Hour INI data files with undo support.
/// </summary>
[method: SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Primary constructor injects required services for INI editor tool orchestrator.")]
[SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Primary constructor injects required services for INI editor tool orchestrator.")]
public sealed partial class IniEditorViewModel(
    IIniDocumentService iniDocumentService,
    IIniSchemaService schemaService,
    INotificationService notificationService,
    ILocalizationService localizationService,
    IDialogService dialogService,
    ILogger<IniEditorViewModel> logger) : ObservableObject, IDisposable
{
    private static readonly EnumerationOptions SafeDirectoryEnumerationOptions = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint | FileAttributes.System,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
    };

    private static readonly (string Key, string Value)[] UpgradeHookupTemplate =
    [
        (IniConstants.FieldKeys.Upgrade, "Upgrade_"),
        (IniConstants.FieldKeys.TriggeredBy, "Upgrade_"),
    ];

    private static readonly (string Key, string Value)[] DamageProfileTemplate =
    [
        (IniConstants.FieldKeys.DamageType, "EXPLOSION"),
        (IniConstants.FieldKeys.PrimaryDamage, "50.0"),
        (IniConstants.FieldKeys.PrimaryDamageRadius, "20.0"),
        (IniConstants.FieldKeys.DeathType, "EXPLODED"),
    ];

    private readonly Stack<IniEditAction> _undoStack = new();
    private readonly Stack<IniEditAction> _redoStack = new();
    private IniDocument? _document;
    private int _historyVersion;
    private int _savedHistoryVersion;
    private bool _disposed;
    private bool _cultureSubscribed;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _filterCts;

    /// <summary>
    /// Gets the root block nodes of the edited document.
    /// </summary>
    public ObservableCollection<IniTreeNodeViewModel> RootNodes { get; } = [];

    /// <summary>
    /// Gets the INI files listed in the explorer.
    /// </summary>
    public ObservableCollection<IniFileTreeNodeViewModel> Files { get; } = [];

    /// <summary>
    /// Gets the field rows of the selected block.
    /// </summary>
    public ObservableCollection<IniFieldRowViewModel> FieldRows { get; } = [];

    /// <summary>
    /// Gets the canvas summary lines for the selected block.
    /// </summary>
    public ObservableCollection<IniCanvasSummaryRow> CanvasSummary { get; } = [];

    /// <summary>
    /// Gets or sets the selected block node.
    /// </summary>
    [ObservableProperty]
    private IniTreeNodeViewModel? _selectedNode;

    /// <summary>
    /// Gets or sets the block filter text.
    /// </summary>
    [ObservableProperty]
    private string? _blockFilter;

    /// <summary>
    /// Gets or sets the current file path.
    /// </summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>
    /// Gets or sets the files directory listed in the explorer.
    /// </summary>
    [ObservableProperty]
    private string? _filesDirectory;

    /// <summary>
    /// Gets or sets the raw text preview of the document.
    /// </summary>
    [ObservableProperty]
    private string _rawText = string.Empty;

    /// <summary>
    /// Gets or sets the document title display.
    /// </summary>
    [ObservableProperty]
    private string _documentTitle = string.Empty;

    /// <summary>
    /// Gets or sets the new field key input.
    /// </summary>
    [ObservableProperty]
    private string _newFieldKey = string.Empty;

    /// <summary>
    /// Gets or sets the new field value input.
    /// </summary>
    [ObservableProperty]
    private string _newFieldValue = string.Empty;

    /// <summary>
    /// Gets or sets the new block type input.
    /// </summary>
    [ObservableProperty]
    private string _newBlockType = IniConstants.BlockTypes.Object;

    /// <summary>
    /// Gets or sets the new block name input.
    /// </summary>
    [ObservableProperty]
    private string _newBlockName = string.Empty;

    /// <summary>
    /// Gets a value indicating whether a document is open.
    /// </summary>
    public bool HasDocument => _document != null;

    /// <summary>
    /// Gets a value indicating whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets a value indicating whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the document has unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges => _historyVersion != _savedHistoryVersion;

    /// <summary>
    /// Gets the available block types for the add-block input.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Bound from XAML; instance member required for binding.")]
    public IReadOnlyList<string> AvailableBlockTypes => IniConstants.BlockTypes.All;

    /// <summary>
    /// Opens an INI file, asking to discard unsaved changes first.
    /// </summary>
    /// <param name="filePath">The full path of the file to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the file was opened successfully.</returns>
    public async Task<bool> OpenFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!await ConfirmDiscardUnsavedAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        filePath = NormalizeSourceFilePath(filePath);
        var result = await iniDocumentService.ParseFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data == null)
        {
            notificationService.ShowError(
                localizationService.GetString("Tools.IniEditor.Open.FailureTitle"),
                localizationService.GetString("Tools.IniEditor.Open.FailureMessage", result.FirstError ?? filePath),
                NotificationDurations.Long);
            return false;
        }

        await AdoptDocumentAsync(result.Data, filePath, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Opened INI file {Path}", filePath);
        return true;
    }

    /// <summary>
    /// Opens a folder in the file explorer and optionally loads the first INI file.
    /// </summary>
    /// <param name="folderPath">The path of the directory to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the folder was opened.</returns>
    public async Task<bool> OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        FilesDirectory = folderPath;
        await RefreshFilesAsync(cancellationToken).ConfigureAwait(false);

        var first = FindFirstIniFilePath(Files);
        if (!string.IsNullOrEmpty(first))
        {
            return await OpenFileAsync(first, cancellationToken).ConfigureAwait(false);
        }

        notificationService.ShowInfo(
            localizationService.GetString("Tools.IniEditor.Files.NoIniFilesTitle"),
            localizationService.GetString("Tools.IniEditor.Files.NoIniFilesMessage"),
            NotificationDurations.Medium);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_cultureSubscribed)
        {
            localizationService.PropertyChanged -= OnLocalizationPropertyChanged;
        }

        CancelDeferred(ref _refreshCts);
        CancelDeferred(ref _previewCts);
        CancelDeferred(ref _filterCts);
    }

    /// <summary>
    /// Normalizes a source file path for consistent handling.
    /// Falls back to the raw path when normalization fails so the open
    /// surfaces a normal parse failure instead of an unhandled exception.
    /// </summary>
    /// <param name="filePath">The raw file path.</param>
    /// <returns>The normalized absolute path.</returns>
    internal static string NormalizeSourceFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        try
        {
            return Path.GetFullPath(filePath.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return filePath;
        }
    }

    private static bool MatchesFilter(IniBlock block, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        if ($"{block.BlockType} {block.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return block.Children.Any(child => MatchesFilter(child, filter));
    }

    private static IniTreeNodeViewModel BuildTreeNode(IniBlock block, IniTreeNodeViewModel? parent)
    {
        var node = new IniTreeNodeViewModel(block, parent);
        foreach (var child in block.Children)
        {
            node.Children.Add(BuildTreeNode(child, node));
        }

        return node;
    }

    private static IniTreeNodeViewModel? FindNode(IEnumerable<IniTreeNodeViewModel> nodes, IniBlock block)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Block, block))
            {
                return node;
            }

            var child = FindNode(node.Children, block);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> CanvasHighlightKeys(string blockType)
    {
        if (string.Equals(blockType, IniConstants.BlockTypes.Object, StringComparison.OrdinalIgnoreCase))
        {
            return ["Health", IniConstants.FieldKeys.BuildCost, IniConstants.FieldKeys.BuildTime, "Side", IniConstants.FieldKeys.DisplayName, IniConstants.BlockTypes.ArmorSet, IniConstants.BlockTypes.WeaponSet, IniConstants.BlockTypes.CommandSet, "Icon", IniConstants.FieldKeys.ButtonImage];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            return [IniConstants.FieldKeys.PrimaryDamage, IniConstants.FieldKeys.PrimaryDamageRadius, "AttackRange", IniConstants.FieldKeys.DamageType, IniConstants.FieldKeys.DeathType, "WeaponSpeed"];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.CommandButton, StringComparison.OrdinalIgnoreCase))
        {
            return ["Command", "Object", IniConstants.FieldKeys.Upgrade, "TextLabel", IniConstants.FieldKeys.ButtonImage];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.Upgrade, StringComparison.OrdinalIgnoreCase))
        {
            return ["Type", IniConstants.FieldKeys.BuildCost, IniConstants.FieldKeys.BuildTime, IniConstants.FieldKeys.DisplayName, IniConstants.FieldKeys.ButtonImage];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.Locomotor, StringComparison.OrdinalIgnoreCase))
        {
            return ["Speed", "TurnRate", "Lift", "Appearance"];
        }

        return [IniConstants.FieldKeys.DisplayName, IniConstants.FieldKeys.ButtonImage, "Icon"];
    }

    private static string? FindFieldValue(IniBlock block, string key)
    {
        return block.Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static void SeedBlockTemplate(IniBlock block)
    {
        if (string.Equals(block.BlockType, IniConstants.BlockTypes.Object, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.Add(new IniField(IniConstants.FieldKeys.DisplayName, "OBJECT:Name"));
            block.Fields.Add(new IniField("Side", "USA"));
            block.Fields.Add(new IniField(IniConstants.FieldKeys.BuildCost, "100"));
            block.Fields.Add(new IniField(IniConstants.FieldKeys.BuildTime, "5.0"));
            block.Fields.Add(new IniField("Health", "100.0"));
        }
        else if (string.Equals(block.BlockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.Add(new IniField(IniConstants.FieldKeys.PrimaryDamage, "50.0"));
            block.Fields.Add(new IniField(IniConstants.FieldKeys.PrimaryDamageRadius, "20.0"));
            block.Fields.Add(new IniField("AttackRange", "200.0"));
            block.Fields.Add(new IniField(IniConstants.FieldKeys.DamageType, "EXPLOSION"));
        }
        else if (string.Equals(block.BlockType, IniConstants.BlockTypes.Upgrade, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.Add(new IniField("Type", "OBJECT"));
            block.Fields.Add(new IniField(IniConstants.FieldKeys.BuildCost, "500"));
            block.Fields.Add(new IniField(IniConstants.FieldKeys.BuildTime, "30.0"));
        }
    }

    private static string? FindFirstIniFilePath(IEnumerable<IniFileTreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsDirectory)
            {
                return node.FullPath;
            }

            var childFile = FindFirstIniFilePath(node.Children);
            if (childFile != null)
            {
                return childFile;
            }
        }

        return null;
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            return TopLevel.GetTopLevel(lifetime.MainWindow);
        }

        return null;
    }

    private static bool IsWithinDirectory(string directory, string? root)
    {
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        try
        {
            var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return (directory + Path.DirectorySeparatorChar).StartsWith(rootPath, comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static void ExpandAncestors(IniTreeNodeViewModel node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            current.IsExpanded = true;
        }

        node.IsExpanded = true;
    }

    private static void SetFieldValueByKey(IniBlock block, string key, string value)
    {
        var index = block.Fields.FindIndex(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            block.Fields[index] = block.Fields[index] with { Value = value };
        }
    }

    private static void RemoveAddedField(IniBlock block, int index, string key)
    {
        if (index >= 0 &&
            index < block.Fields.Count &&
            string.Equals(block.Fields[index].Key, key, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.RemoveAt(index);
            return;
        }

        RemoveFieldByKey(block, key);
    }

    private static void RemoveFieldByKey(IniBlock block, string key)
    {
        var match = block.Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            block.Fields.Remove(match);
        }
    }

    private static List<IniField> BuildMissingTemplateFields(IniBlock block, (string Key, string Value)[] desired)
    {
        var missing = new List<IniField>(desired.Length);
        foreach (var (key, value) in desired)
        {
            if (FindFieldValue(block, key) == null)
            {
                missing.Add(new IniField(key, value));
            }
        }

        return missing;
    }

    private static bool AllowsEmptyName(string blockType)
    {
        return string.Equals(blockType, IniConstants.BlockTypes.ExperienceLevels, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipDirectory(DirectoryInfo directoryInfo, IniFileTreeNodeViewModel? parent)
    {
        return parent != null &&
            (directoryInfo.Name.StartsWith('.') ||
            string.Equals(directoryInfo.Name, ModBuilderConstants.DefaultBuildDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(directoryInfo.Name, ModBuilderConstants.DefaultReleaseDir, StringComparison.OrdinalIgnoreCase));
    }

    private static void CancelDeferred(ref CancellationTokenSource? slot)
    {
        slot?.Cancel();
        slot?.Dispose();
        slot = null;
    }

    private static async Task InvokeOnUIThreadAsync(Action action)
    {
        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            await Task.CompletedTask.ConfigureAwait(false);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    private static void PostToUIThread(Action action)
    {
        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    /// <summary>
    /// Creates a new empty document.
    /// </summary>
    [RelayCommand]
    private async Task NewDocumentAsync(CancellationToken cancellationToken = default)
    {
        if (!await ConfirmDiscardUnsavedAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await AdoptDocumentAsync(new IniDocument(), null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens an INI file chosen with a file dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenFileWithDialogAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = localizationService.GetString("Tools.IniEditor.FileDialog.OpenTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(localizationService.GetString("Tools.IniEditor.FileDialog.FilterName"))
                {
                    Patterns = [ModBuilderConstants.FileNames.IniSearchPattern],
                },
            ],
        });
        if (files.Count > 0)
        {
            var localPath = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(localPath))
            {
                await OpenFileAsync(localPath, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Opens a folder chosen with a folder dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenFolderWithDialogAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = localizationService.GetString("Tools.IniEditor.FileDialog.FolderTitle"),
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }

        var localPath = folders[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(localPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await OpenFolderAsync(localPath, cancellationToken);
        }
    }

    /// <summary>
    /// Saves the open document to its current file.
    /// </summary>
    [RelayCommand]
    private async Task SaveFileAsync(CancellationToken cancellationToken = default)
    {
        if (_document == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(FilePath))
        {
            await SaveFileAsWithDialogAsync(cancellationToken);
            return;
        }

        await WriteDocumentToFileAsync(FilePath, cancellationToken);
    }

    /// <summary>
    /// Saves the open document to a path chosen with a dialog.
    /// </summary>
    [RelayCommand]
    private async Task SaveFileAsWithDialogAsync(CancellationToken cancellationToken = default)
    {
        if (_document == null)
        {
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = localizationService.GetString("Tools.IniEditor.FileDialog.SaveTitle"),
            SuggestedFileName = string.IsNullOrEmpty(FilePath) ? "Untitled.ini" : Path.GetFileName(FilePath),
        });
        if (file == null)
        {
            return;
        }

        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(localPath))
        {
            await WriteDocumentToFileAsync(localPath, cancellationToken);
        }
    }

    /// <summary>
    /// Validates the open document and reports issues via toast.
    /// </summary>
    [RelayCommand]
    private void ValidateDocument()
    {
        if (_document == null)
        {
            notificationService.ShowInfo(
                localizationService.GetString("Tools.IniEditor.Validate.NoDocumentTitle"),
                localizationService.GetString("Tools.IniEditor.Validate.NoDocumentMessage"),
                NotificationDurations.Short);
            return;
        }

        var result = iniDocumentService.ValidateDocument(_document, FilePath ?? DocumentTitle);
        if (result.IsValid)
        {
            notificationService.ShowSuccess(
                localizationService.GetString("Tools.IniEditor.Validate.SuccessTitle"),
                localizationService.GetString("Tools.IniEditor.Validate.SuccessMessage", _document.Blocks.Count),
                NotificationDurations.Medium);
            return;
        }

        var first = result.Issues.Count > 0 ? result.Issues[0].Message : result.FirstError;
        notificationService.ShowWarning(
            localizationService.GetString("Tools.IniEditor.Validate.IssuesTitle"),
            localizationService.GetString("Tools.IniEditor.Validate.IssuesMessage", result.Issues.Count, first ?? string.Empty),
            NotificationDurations.Long);
    }

    /// <summary>
    /// Formats the open file on disk in canonical form.
    /// </summary>
    [RelayCommand]
    private async Task FormatDocumentAsync(CancellationToken cancellationToken = default)
    {
        if (_document == null || string.IsNullOrEmpty(FilePath))
        {
            notificationService.ShowInfo(
                localizationService.GetString("Tools.IniEditor.Format.NoFileTitle"),
                localizationService.GetString("Tools.IniEditor.Format.NoFileMessage"),
                NotificationDurations.Short);
            return;
        }

        await WriteDocumentToFileAsync(FilePath, cancellationToken);
    }

    /// <summary>
    /// Undoes the last edit.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        _historyVersion++;
        RebuildAll();
    }

    /// <summary>
    /// Redoes the last undone edit.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        _historyVersion++;
        RebuildAll();
    }

    /// <summary>
    /// Adds a top-level block from the new block inputs.
    /// </summary>
    [RelayCommand]
    private void AddBlock()
    {
        if (_document == null || string.IsNullOrWhiteSpace(NewBlockType))
        {
            return;
        }

        var block = CreateValidatedBlock();
        if (block == null)
        {
            return;
        }

        SeedBlockTemplate(block);
        var index = _document.Blocks.Count;
        _document.Blocks.Add(block);
        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddBlock"),
            () =>
            {
                _document.Blocks.Insert(Math.Min(index, _document.Blocks.Count), block);
                RebuildAll();
            },
            () =>
            {
                _document.Blocks.Remove(block);
                RebuildAll();
            }));
        _historyVersion++;
        RebuildAll();
        SelectBlock(block);
        NewBlockName = string.Empty;
    }

    /// <summary>
    /// Deletes the selected block.
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedBlock()
    {
        if (_document == null || SelectedNode == null)
        {
            return;
        }

        var node = SelectedNode;
        var siblings = node.Parent == null ? _document.Blocks : node.Parent.Block.Children;
        var index = siblings.IndexOf(node.Block);
        if (index < 0)
        {
            SelectedNode = null;
            return;
        }

        siblings.RemoveAt(index);
        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.DeleteBlock"),
            () =>
            {
                siblings.Remove(node.Block);
                RebuildAll();
            },
            () =>
            {
                siblings.Insert(Math.Min(index, siblings.Count), node.Block);
                RebuildAll();
            }));
        _historyVersion++;
        RebuildAll();
    }

    /// <summary>
    /// Adds a field to the selected block from the new field inputs.
    /// </summary>
    [RelayCommand]
    private void AddField()
    {
        if (SelectedNode == null || string.IsNullOrWhiteSpace(NewFieldKey))
        {
            return;
        }

        var block = SelectedNode.Block;
        var field = new IniField(NewFieldKey.Trim(), NewFieldValue.Trim());
        var index = block.Fields.Count;
        block.Fields.Add(field);
        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddField"),
            () =>
            {
                block.Fields.Insert(Math.Min(index, block.Fields.Count), field);
                RebuildAll();
            },
            () =>
            {
                RemoveAddedField(block, index, field.Key);
                RebuildAll();
            }));
        _historyVersion++;
        RebuildAll();
        NewFieldKey = string.Empty;
        NewFieldValue = string.Empty;
    }

    /// <summary>
    /// Adds a standard upgrade hookup (Upgrade + TriggeredBy) to the selected Object block.
    /// </summary>
    [RelayCommand]
    private void AddUpgradeHookup()
    {
        if (SelectedNode == null)
        {
            return;
        }

        var block = SelectedNode.Block;
        if (!string.Equals(block.BlockType, IniConstants.BlockTypes.Object, StringComparison.OrdinalIgnoreCase))
        {
            notificationService.ShowInfo(
                localizationService.GetString("Tools.IniEditor.Upgrade.ObjectOnlyTitle"),
                localizationService.GetString("Tools.IniEditor.Upgrade.ObjectOnlyMessage"),
                NotificationDurations.Short);
            return;
        }

        var added = BuildMissingTemplateFields(block, UpgradeHookupTemplate);
        if (added.Count == 0)
        {
            notificationService.ShowInfo(
                localizationService.GetString("Tools.IniEditor.Template.SkippedTitle"),
                localizationService.GetString("Tools.IniEditor.Template.SkippedMessage"),
                NotificationDurations.Short);
            return;
        }

        var startIndex = block.Fields.Count;
        foreach (var field in added)
        {
            block.Fields.Add(field);
        }

        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddUpgrade"),
            () =>
            {
                for (var i = 0; i < added.Count; i++)
                {
                    block.Fields.Insert(Math.Min(startIndex + i, block.Fields.Count), added[i]);
                }

                RebuildAll();
            },
            () =>
            {
                for (var i = added.Count - 1; i >= 0; i--)
                {
                    RemoveAddedField(block, startIndex + i, added[i].Key);
                }

                RebuildAll();
            }));
        _historyVersion++;
        RebuildAll();
    }

    /// <summary>
    /// Adds a damage profile starter (DamageType + PrimaryDamage + DeathType) to the selected Weapon block.
    /// </summary>
    [RelayCommand]
    private void AddDamageProfile()
    {
        if (SelectedNode == null)
        {
            return;
        }

        var block = SelectedNode.Block;
        if (!string.Equals(block.BlockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            notificationService.ShowInfo(
                localizationService.GetString("Tools.IniEditor.Damage.WeaponOnlyTitle"),
                localizationService.GetString("Tools.IniEditor.Damage.WeaponOnlyMessage"),
                NotificationDurations.Short);
            return;
        }

        var added = BuildMissingTemplateFields(block, DamageProfileTemplate);
        if (added.Count == 0)
        {
            notificationService.ShowInfo(
                localizationService.GetString("Tools.IniEditor.Template.SkippedTitle"),
                localizationService.GetString("Tools.IniEditor.Template.SkippedMessage"),
                NotificationDurations.Short);
            return;
        }

        var startIndex = block.Fields.Count;
        foreach (var field in added)
        {
            block.Fields.Add(field);
        }

        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddDamage"),
            () =>
            {
                for (var i = 0; i < added.Count; i++)
                {
                    block.Fields.Insert(Math.Min(startIndex + i, block.Fields.Count), added[i]);
                }

                RebuildAll();
            },
            () =>
            {
                for (var i = added.Count - 1; i >= 0; i--)
                {
                    RemoveAddedField(block, startIndex + i, added[i].Key);
                }

                RebuildAll();
            }));
        _historyVersion++;
        RebuildAll();
    }

    /// <summary>
    /// Refreshes the file explorer listing.
    /// </summary>
    [RelayCommand]
    private async Task RefreshFilesAsync(CancellationToken cancellationToken = default)
    {
        CancelDeferred(ref _refreshCts);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _refreshCts = cts;

        var directory = FilesDirectory;
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            await InvokeOnUIThreadAsync(Files.Clear).ConfigureAwait(false);
            return;
        }

        try
        {
            var root = await Task.Run(() => BuildDirectoryNode(new DirectoryInfo(directory), null, 0, cts.Token), cts.Token).ConfigureAwait(false);
            cts.Token.ThrowIfCancellationRequested();
            await InvokeOnUIThreadAsync(() =>
            {
                Files.Clear();
                if (root != null)
                {
                    Files.Add(root);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer refresh or disposed.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to list INI files in {Directory}", directory);
        }
    }

    /// <summary>
    /// Opens a file chosen in the explorer, or expands a directory node.
    /// </summary>
    /// <param name="file">The file tree node to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenExplorerFileAsync(IniFileTreeNodeViewModel? file, CancellationToken cancellationToken = default)
    {
        if (file == null)
        {
            return;
        }

        if (file.IsDirectory)
        {
            file.IsExpanded = !file.IsExpanded;
            return;
        }

        await OpenFileAsync(file.FullPath, cancellationToken);
    }

    private async Task AdoptDocumentAsync(IniDocument document, string? filePath, CancellationToken cancellationToken)
    {
        EnsureCultureSubscription();
        _document = document;
        _undoStack.Clear();
        _redoStack.Clear();
        _historyVersion = 0;
        _savedHistoryVersion = 0;
        await InvokeOnUIThreadAsync(() =>
        {
            FilePath = filePath;
            UpdateDocumentTitle();
            UpdateFilesDirectoryFromFile(filePath);
            RebuildAll();
        }).ConfigureAwait(false);
        await RefreshFilesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void UpdateDocumentTitle()
    {
        DocumentTitle = string.IsNullOrEmpty(FilePath)
            ? localizationService.GetString("Tools.IniEditor.Document.Untitled")
            : Path.GetFileName(FilePath);
    }

    private void UpdateFilesDirectoryFromFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        if (IsWithinDirectory(directory, FilesDirectory))
        {
            return;
        }

        FilesDirectory = directory;
    }

    private void RebuildAll()
    {
        RebuildTree();
        RebuildFieldRows();
        RebuildCanvasSummary();
        RefreshRawText();
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RebuildTree()
    {
        var selectedBlock = SelectedNode?.Block;
        RootNodes.Clear();
        if (_document == null)
        {
            return;
        }

        var filter = BlockFilter?.Trim();
        foreach (var block in _document.Blocks)
        {
            if (!MatchesFilter(block, filter))
            {
                continue;
            }

            RootNodes.Add(BuildTreeNode(block, null));
        }

        if (selectedBlock == null)
        {
            return;
        }

        var match = FindNode(RootNodes, selectedBlock);
        if (match == null)
        {
            SelectedNode = null;
            return;
        }

        ExpandAncestors(match);
        SelectedNode = match;
    }

    private void RebuildFieldRows()
    {
        FieldRows.Clear();
        var block = SelectedNode?.Block;
        if (block == null)
        {
            return;
        }

        for (var i = 0; i < block.Fields.Count; i++)
        {
            var key = block.Fields[i].Key;
            var fieldIndex = i;
            var known = schemaService.TryGetField(block.BlockType, key, out var schema);
            FieldRows.Add(new IniFieldRowViewModel(
                block,
                fieldIndex,
                schema?.Description,
                known,
                MarkDirty,
                (oldValue, newValue) => PushFieldValueUndo(block, key, fieldIndex, oldValue, newValue)));
        }
    }

    private void RebuildCanvasSummary()
    {
        CanvasSummary.Clear();
        var block = SelectedNode?.Block;
        if (block == null || _document == null)
        {
            return;
        }

        CanvasSummary.Add(new IniCanvasSummaryRow(
            localizationService.GetString("Tools.IniEditor.Canvas.BlockType"),
            block.BlockType));
        if (!string.IsNullOrEmpty(block.Name))
        {
            CanvasSummary.Add(new IniCanvasSummaryRow(
                localizationService.GetString("Tools.IniEditor.Canvas.BlockName"),
                block.Name));
        }

        var schema = schemaService.GetBlockSchema(block.BlockType);
        if (schema != null)
        {
            CanvasSummary.Add(new IniCanvasSummaryRow(
                localizationService.GetString("Tools.IniEditor.Canvas.Schema"),
                schema.Description));
        }

        foreach (var key in CanvasHighlightKeys(block.BlockType))
        {
            var value = FindFieldValue(block, key);
            if (value != null)
            {
                CanvasSummary.Add(new IniCanvasSummaryRow(key, value));
            }
        }

        AddObjectCanvasLinks(block);
        AddWeaponCanvasDamage(block);
    }

    private void AddObjectCanvasLinks(IniBlock block)
    {
        if (!string.Equals(block.BlockType, IniConstants.BlockTypes.Object, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var weaponSet = FindFieldValue(block, IniConstants.BlockTypes.WeaponSet);
        if (weaponSet != null)
        {
            foreach (var weapon in FindBlocks(IniConstants.BlockTypes.WeaponSet, weaponSet))
            {
                CanvasSummary.Add(new IniCanvasSummaryRow(
                    localizationService.GetString("Tools.IniEditor.Canvas.LinkedWeapon"),
                    $"{weapon.Name} ({weapon.Fields.Count})"));
            }
        }

        var commandSet = FindFieldValue(block, IniConstants.BlockTypes.CommandSet);
        if (commandSet != null)
        {
            CanvasSummary.Add(new IniCanvasSummaryRow(
                localizationService.GetString("Tools.IniEditor.Canvas.LinkedCommandSet"),
                commandSet));
        }

        var upgrades = block.Fields.Where(field =>
            string.Equals(field.Key, IniConstants.FieldKeys.Upgrade, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Key, "Upgrades", StringComparison.OrdinalIgnoreCase)).ToList();
        if (upgrades.Count > 0)
        {
            CanvasSummary.Add(new IniCanvasSummaryRow(
                localizationService.GetString("Tools.IniEditor.Canvas.Upgrades"),
                string.Join(", ", upgrades.Select(field => field.Value))));
        }
    }

    private void AddWeaponCanvasDamage(IniBlock block)
    {
        if (!string.Equals(block.BlockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var damage = FindFieldValue(block, IniConstants.FieldKeys.PrimaryDamage);
        var damageType = FindFieldValue(block, IniConstants.FieldKeys.DamageType);
        if (damage != null || damageType != null)
        {
            CanvasSummary.Add(new IniCanvasSummaryRow(
                localizationService.GetString("Tools.IniEditor.Canvas.Damage"),
                $"{damage ?? "?"} ({damageType ?? "?"})"));
        }
    }

    private IEnumerable<IniBlock> FindBlocks(string blockType, string name)
    {
        if (_document == null)
        {
            return [];
        }

        return _document.Blocks.Where(block =>
            string.Equals(block.BlockType, blockType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(block.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRawText()
    {
        RawText = _document == null ? string.Empty : iniDocumentService.WriteDocument(_document);
    }

    private void RefreshPreviews()
    {
        RefreshRawText();
        RebuildCanvasSummary();
    }

    private void MarkDirty()
    {
        _historyVersion++;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        ScheduleDeferred(ref _previewCts, IniConstants.Editor.PreviewRefreshDebounceMs, RefreshPreviews);
    }

    private void PushUndo(IniEditAction action)
    {
        if (action.CoalesceKey != null &&
            _undoStack.TryPeek(out var top) &&
            Equals(top.CoalesceKey, action.CoalesceKey))
        {
            _undoStack.Pop();
        }

        _undoStack.Push(action);
        if (_undoStack.Count > IniConstants.Editor.MaxUndoHistory)
        {
            var kept = _undoStack.Take(IniConstants.Editor.MaxUndoHistory).Reverse().ToArray();
            _undoStack.Clear();
            foreach (var item in kept)
            {
                _undoStack.Push(item);
            }

            if (_savedHistoryVersion < _historyVersion - IniConstants.Editor.MaxUndoHistory)
            {
                _savedHistoryVersion = int.MinValue;
            }
        }

        if (_redoStack.Count > 0)
        {
            _savedHistoryVersion = int.MinValue;
            _redoStack.Clear();
        }
    }

    private void PushFieldValueUndo(IniBlock block, string key, int fieldIndex, string oldValue, string newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.EditField"),
            () =>
            {
                SetFieldValueByKey(block, key, newValue);
                RebuildAll();
            },
            () =>
            {
                SetFieldValueByKey(block, key, oldValue);
                RebuildAll();
            },
            (block, fieldIndex)));
    }

    private IniBlock? CreateValidatedBlock()
    {
        var blockType = NewBlockType.Trim();
        var name = NewBlockName.Trim();
        if (name.Length == 0 && !AllowsEmptyName(blockType))
        {
            notificationService.ShowWarning(
                localizationService.GetString("Tools.IniEditor.AddBlock.EmptyNameTitle"),
                localizationService.GetString("Tools.IniEditor.AddBlock.EmptyNameMessage", blockType),
                NotificationDurations.Long);
            return null;
        }

        if (name.Length > 0 && HasBlock(blockType, name))
        {
            notificationService.ShowWarning(
                localizationService.GetString("Tools.IniEditor.AddBlock.DuplicateTitle"),
                localizationService.GetString("Tools.IniEditor.AddBlock.DuplicateMessage", blockType, name),
                NotificationDurations.Long);
            return null;
        }

        return new IniBlock { BlockType = blockType, Name = name, LineNumber = 0 };
    }

    private bool HasBlock(string blockType, string name)
    {
        return _document != null && _document.Blocks.Any(block =>
            string.Equals(block.BlockType, blockType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(block.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectBlock(IniBlock block)
    {
        var match = FindNode(RootNodes, block);
        if (match != null)
        {
            ExpandAncestors(match);
            SelectedNode = match;
        }
    }

    private async Task<bool> ConfirmDiscardUnsavedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_document == null || !HasUnsavedChanges)
        {
            return true;
        }

        return await dialogService.ShowConfirmationAsync(
            localizationService.GetString("Tools.IniEditor.UnsavedChanges.Title"),
            localizationService.GetString("Tools.IniEditor.UnsavedChanges.Message"),
            localizationService.GetString("Tools.IniEditor.UnsavedChanges.Discard"),
            localizationService.GetString("Tools.IniEditor.UnsavedChanges.Cancel"));
    }

    private async Task WriteDocumentToFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (_document == null)
        {
            return;
        }

        string? tempPath = null;
        try
        {
            var canonical = iniDocumentService.WriteDocument(_document);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            tempPath = Path.Combine(directory ?? Path.GetTempPath(), Path.GetRandomFileName());
            await File.WriteAllTextAsync(tempPath, canonical, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            tempPath = null;
            await InvokeOnUIThreadAsync(() =>
            {
                FilePath = filePath;
                _savedHistoryVersion = _historyVersion;
                UpdateDocumentTitle();
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }).ConfigureAwait(false);
            notificationService.ShowSuccess(
                localizationService.GetString("Tools.IniEditor.Save.SuccessTitle"),
                localizationService.GetString("Tools.IniEditor.Save.SuccessMessage", DocumentTitle),
                NotificationDurations.Medium);
        }
        catch (OperationCanceledException)
        {
            if (tempPath != null)
            {
                IniDocumentService.DeleteTempFile(tempPath);
            }

            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (tempPath != null)
            {
                IniDocumentService.DeleteTempFile(tempPath);
            }

            logger.LogError(ex, "Failed to save INI file {Path}", filePath);
            notificationService.ShowError(
                localizationService.GetString("Tools.IniEditor.Save.FailureTitle"),
                localizationService.GetString("Tools.IniEditor.Save.FailureMessage", ex.Message),
                NotificationDurations.Long);
        }
    }

    private IniFileTreeNodeViewModel? BuildDirectoryNode(DirectoryInfo directoryInfo, IniFileTreeNodeViewModel? parent, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth > IniConstants.Editor.MaxExplorerDepth)
        {
            return null;
        }

        if (ShouldSkipDirectory(directoryInfo, parent))
        {
            return null;
        }

        var node = new IniFileTreeNodeViewModel(directoryInfo.Name, directoryInfo.FullName, isDirectory: true, parent: parent);
        try
        {
            AddChildNodes(node, directoryInfo, depth, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to enumerate INI directory {Directory}", directoryInfo.FullName);
        }

        node.HasVisibleDescendants = node.Children.Count > 0;
        if (parent == null)
        {
            node.IsExpanded = true;
            return node.Children.Count > 0 ? node : null;
        }

        return node.HasVisibleDescendants ? node : null;
    }

    private void AddChildNodes(IniFileTreeNodeViewModel node, DirectoryInfo directoryInfo, int depth, CancellationToken cancellationToken)
    {
        foreach (var directory in directoryInfo.EnumerateDirectories("*", SafeDirectoryEnumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var child = BuildDirectoryNode(directory, node, depth + 1, cancellationToken);
            if (child != null && child.HasVisibleDescendants)
            {
                node.Children.Add(child);
            }
        }

        foreach (var file in directoryInfo.EnumerateFiles(ModBuilderConstants.FileNames.IniSearchPattern, SafeDirectoryEnumerationOptions))
        {
            node.Children.Add(new IniFileTreeNodeViewModel(file.Name, file.FullName, isDirectory: false, parent: node));
        }
    }

    private void EnsureCultureSubscription()
    {
        if (_cultureSubscribed)
        {
            return;
        }

        _cultureSubscribed = true;
        localizationService.PropertyChanged += OnLocalizationPropertyChanged;
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(ILocalizationService.CurrentCulture))
        {
            return;
        }

        PostToUIThread(RefreshLocalizedStrings);
    }

    private void RefreshLocalizedStrings()
    {
        if (_disposed)
        {
            return;
        }

        UpdateDocumentTitle();
        RebuildCanvasSummary();
    }

    private void ScheduleDeferred(ref CancellationTokenSource? slot, int delayMs, Action refresh)
    {
        slot?.Cancel();
        var cts = new CancellationTokenSource();
        slot = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, cts.Token).ConfigureAwait(false);
                if (_disposed || cts.IsCancellationRequested)
                {
                    return;
                }

                PostToUIThread(refresh);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Superseded by a newer edit, or the view model was disposed.
            }
        });
    }

    partial void OnSelectedNodeChanged(IniTreeNodeViewModel? value)
    {
        RebuildFieldRows();
        RebuildCanvasSummary();
    }

    partial void OnBlockFilterChanged(string? value)
    {
        ScheduleDeferred(ref _filterCts, IniConstants.Editor.FilterDebounceMs, RebuildTree);
    }
}
