using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Interfaces.Tools.IniEditor;
using GenHub.Core.Models.Tools.IniEditor;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private readonly Stack<IniEditAction> _undoStack = new();
    private readonly Stack<IniEditAction> _redoStack = new();
    private IniDocument? _document;
    private int _historyVersion;
    private int _savedHistoryVersion;
    private bool _disposed;

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

        AdoptDocument(result.Data, filePath);
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
        RefreshFiles();

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
    }

    /// <summary>
    /// Normalizes a source file path for consistent handling.
    /// </summary>
    /// <param name="filePath">The raw file path.</param>
    /// <returns>The normalized absolute path.</returns>
    internal static string NormalizeSourceFilePath(string filePath)
    {
        return Path.GetFullPath(filePath.Trim().Trim('"'));
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
            return ["Health", "BuildCost", "BuildTime", "Side", "DisplayName", "ArmorSet", "WeaponSet", "CommandSet", "Icon", "ButtonImage"];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            return ["PrimaryDamage", "PrimaryDamageRadius", "AttackRange", "DamageType", "DeathType", "WeaponSpeed"];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.CommandButton, StringComparison.OrdinalIgnoreCase))
        {
            return ["Command", "Object", "Upgrade", "TextLabel", "ButtonImage"];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.Upgrade, StringComparison.OrdinalIgnoreCase))
        {
            return ["Type", "BuildCost", "BuildTime", "DisplayName", "ButtonImage"];
        }

        if (string.Equals(blockType, IniConstants.BlockTypes.Locomotor, StringComparison.OrdinalIgnoreCase))
        {
            return ["Speed", "TurnRate", "Lift", "Appearance"];
        }

        return ["DisplayName", "ButtonImage", "Icon"];
    }

    private static string? FindFieldValue(IniBlock block, string key)
    {
        foreach (var field in block.Fields)
        {
            if (string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return field.Value;
            }
        }

        return null;
    }

    private static void SeedBlockTemplate(IniBlock block)
    {
        if (string.Equals(block.BlockType, IniConstants.BlockTypes.Object, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.Add(new IniField("DisplayName", "OBJECT:Name"));
            block.Fields.Add(new IniField("Side", "USA"));
            block.Fields.Add(new IniField("BuildCost", "100"));
            block.Fields.Add(new IniField("BuildTime", "5.0"));
            block.Fields.Add(new IniField("Health", "100.0"));
        }
        else if (string.Equals(block.BlockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.Add(new IniField("PrimaryDamage", "50.0"));
            block.Fields.Add(new IniField("PrimaryDamageRadius", "20.0"));
            block.Fields.Add(new IniField("AttackRange", "200.0"));
            block.Fields.Add(new IniField("DamageType", "EXPLOSION"));
        }
        else if (string.Equals(block.BlockType, IniConstants.BlockTypes.Upgrade, StringComparison.OrdinalIgnoreCase))
        {
            block.Fields.Add(new IniField("Type", "OBJECT"));
            block.Fields.Add(new IniField("BuildCost", "500"));
            block.Fields.Add(new IniField("BuildTime", "30.0"));
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

        AdoptDocument(new IniDocument(), null);
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

        var block = new IniBlock
        {
            BlockType = NewBlockType.Trim(),
            Name = NewBlockName.Trim(),
            LineNumber = 0,
        };
        SeedBlockTemplate(block);
        _document.Blocks.Add(block);
        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddBlock"),
            () =>
            {
                _document.Blocks.Add(block);
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
        siblings.Remove(node.Block);
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
        block.Fields.Add(field);
        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddField"),
            () =>
            {
                block.Fields.Add(field);
                RebuildAll();
            },
            () =>
            {
                block.Fields.Remove(field);
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

        var added = new List<IniField>
        {
            new("Upgrade", "Upgrade_"),
            new("TriggeredBy", "Upgrade_"),
        };
        foreach (var field in added)
        {
            block.Fields.Add(field);
        }

        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddUpgrade"),
            () =>
            {
                foreach (var field in added)
                {
                    block.Fields.Add(field);
                }

                RebuildAll();
            },
            () =>
            {
                foreach (var field in added)
                {
                    block.Fields.Remove(field);
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

        var added = new List<IniField>
        {
            new("DamageType", "EXPLOSION"),
            new("PrimaryDamage", "50.0"),
            new("PrimaryDamageRadius", "20.0"),
            new("DeathType", "EXPLODED"),
        };
        foreach (var field in added)
        {
            block.Fields.Add(field);
        }

        PushUndo(new IniEditAction(
            localizationService.GetString("Tools.IniEditor.History.AddDamage"),
            () =>
            {
                foreach (var field in added)
                {
                    block.Fields.Add(field);
                }

                RebuildAll();
            },
            () =>
            {
                foreach (var field in added)
                {
                    block.Fields.Remove(field);
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
    private void RefreshFiles()
    {
        Files.Clear();
        if (string.IsNullOrEmpty(FilesDirectory) || !Directory.Exists(FilesDirectory))
        {
            return;
        }

        try
        {
            var rootDirInfo = new DirectoryInfo(FilesDirectory);
            var rootNode = BuildDirectoryNode(rootDirInfo, null, 0);
            if (rootNode != null)
            {
                Files.Add(rootNode);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to list INI files in {Directory}", FilesDirectory);
        }
    }

    /// <summary>
    /// Opens a file chosen in the explorer.
    /// </summary>
    /// <param name="file">The file tree node to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenExplorerFileAsync(IniFileTreeNodeViewModel? file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.IsDirectory)
        {
            return;
        }

        await OpenFileAsync(file.FullPath, cancellationToken);
    }

    private void AdoptDocument(IniDocument document, string? filePath)
    {
        _document = document;
        FilePath = filePath;
        _undoStack.Clear();
        _redoStack.Clear();
        _historyVersion = 0;
        _savedHistoryVersion = 0;
        DocumentTitle = string.IsNullOrEmpty(filePath)
            ? localizationService.GetString("Tools.IniEditor.Document.Untitled")
            : Path.GetFileName(filePath);
        UpdateFilesDirectoryFromFile(filePath);
        RefreshFiles();
        RebuildAll();
    }

    private void UpdateFilesDirectoryFromFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            FilesDirectory = directory;
        }
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

        if (selectedBlock != null)
        {
            var match = FindNode(RootNodes, selectedBlock);
            if (match != null)
            {
                match.IsExpanded = true;
                SelectedNode = match;
            }
        }
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
            var known = schemaService.TryGetField(block.BlockType, key, out var schema);
            FieldRows.Add(new IniFieldRowViewModel(block, i, schema?.Description, known, MarkDirty));
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

        if (string.Equals(block.BlockType, IniConstants.BlockTypes.Object, StringComparison.OrdinalIgnoreCase))
        {
            var weaponSet = FindFieldValue(block, "WeaponSet");
            if (weaponSet != null)
            {
                foreach (var weapon in FindBlocks(IniConstants.BlockTypes.WeaponSet, weaponSet))
                {
                    CanvasSummary.Add(new IniCanvasSummaryRow(
                        localizationService.GetString("Tools.IniEditor.Canvas.LinkedWeapon"),
                        $"{weapon.Name} ({weapon.Fields.Count})"));
                }
            }

            var commandSet = FindFieldValue(block, "CommandSet");
            if (commandSet != null)
            {
                CanvasSummary.Add(new IniCanvasSummaryRow(
                    localizationService.GetString("Tools.IniEditor.Canvas.LinkedCommandSet"),
                    commandSet));
            }

            var upgrades = block.Fields.Where(field =>
                string.Equals(field.Key, "Upgrade", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Key, "Upgrades", StringComparison.OrdinalIgnoreCase)).ToList();
            if (upgrades.Count > 0)
            {
                CanvasSummary.Add(new IniCanvasSummaryRow(
                    localizationService.GetString("Tools.IniEditor.Canvas.Upgrades"),
                    string.Join(", ", upgrades.Select(field => field.Value))));
            }
        }

        if (string.Equals(block.BlockType, IniConstants.BlockTypes.Weapon, StringComparison.OrdinalIgnoreCase))
        {
            var damage = FindFieldValue(block, "PrimaryDamage");
            var damageType = FindFieldValue(block, "DamageType");
            if (damage != null || damageType != null)
            {
                CanvasSummary.Add(new IniCanvasSummaryRow(
                    localizationService.GetString("Tools.IniEditor.Canvas.Damage"),
                    $"{damage ?? "?"} ({damageType ?? "?"})"));
            }
        }
    }

    private IEnumerable<IniBlock> FindBlocks(string blockType, string name)
    {
        if (_document == null)
        {
            yield break;
        }

        foreach (var block in _document.Blocks)
        {
            if (string.Equals(block.BlockType, blockType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(block.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                yield return block;
            }
        }
    }

    private void RefreshRawText()
    {
        RawText = _document == null ? string.Empty : iniDocumentService.WriteDocument(_document);
    }

    private void MarkDirty()
    {
        _historyVersion++;
        RefreshRawText();
        RebuildCanvasSummary();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void PushUndo(IniEditAction action)
    {
        _undoStack.Push(action);
        if (_undoStack.Count > IniConstants.Editor.MaxUndoHistory)
        {
            var kept = _undoStack.Reverse().Skip(1).Reverse().ToArray();
            _undoStack.Clear();
            foreach (var item in kept.Reverse())
            {
                _undoStack.Push(item);
            }
        }

        _redoStack.Clear();
    }

    private void SelectBlock(IniBlock block)
    {
        var match = FindNode(RootNodes, block);
        if (match != null)
        {
            match.IsExpanded = true;
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

        try
        {
            var canonical = iniDocumentService.WriteDocument(_document);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = Path.Combine(directory ?? Path.GetTempPath(), Path.GetRandomFileName());
            await File.WriteAllTextAsync(tempPath, canonical, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            FilePath = filePath;
            _savedHistoryVersion = _historyVersion;
            DocumentTitle = Path.GetFileName(filePath);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            notificationService.ShowSuccess(
                localizationService.GetString("Tools.IniEditor.Save.SuccessTitle"),
                localizationService.GetString("Tools.IniEditor.Save.SuccessMessage", DocumentTitle),
                NotificationDurations.Medium);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to save INI file {Path}", filePath);
            notificationService.ShowError(
                localizationService.GetString("Tools.IniEditor.Save.FailureTitle"),
                localizationService.GetString("Tools.IniEditor.Save.FailureMessage", ex.Message),
                NotificationDurations.Long);
        }
    }

    private IniFileTreeNodeViewModel? BuildDirectoryNode(DirectoryInfo directoryInfo, IniFileTreeNodeViewModel? parent, int depth)
    {
        if (depth > 20)
        {
            return null;
        }

        if (parent != null &&
            (directoryInfo.Name.StartsWith('.') ||
             string.Equals(directoryInfo.Name, ModBuilderConstants.DefaultBuildDir, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directoryInfo.Name, ModBuilderConstants.DefaultReleaseDir, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var node = new IniFileTreeNodeViewModel(directoryInfo.Name, directoryInfo.FullName, isDirectory: true, parent: parent);
        try
        {
            foreach (var directory in directoryInfo.EnumerateDirectories("*", SafeDirectoryEnumerationOptions))
            {
                var child = BuildDirectoryNode(directory, node, depth + 1);
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

    partial void OnSelectedNodeChanged(IniTreeNodeViewModel? value)
    {
        RebuildFieldRows();
        RebuildCanvasSummary();
    }

    partial void OnBlockFilterChanged(string? value)
    {
        RebuildTree();
    }
}
