using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Interfaces.Tools.IniEditor;
using GenHub.Core.Interfaces.Tools.TextureEditor;
using GenHub.Core.Interfaces.Tools.WndEditor;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Tools.IniEditor;
using GenHub.Features.Tools.IniEditor.Services;
using GenHub.Features.Tools.IniEditor.ViewModels;
using GenHub.Features.Tools.IniEditor.Views;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Tests.Core.Features.Tools.IniEditor.Views;

/// <summary>
/// Headless layout tests for <see cref="IniEditorView"/>.
/// </summary>
public class IniEditorViewTests
{
    /// <summary>
    /// Verifies that the editor renders its three panes and populates the block tree from a document.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [AvaloniaFact]
    public async Task EditorView_WithDocument_RendersPanesAndBlockTreeAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = "TestObject";
        viewModel.AddBlockCommand.Execute(null);

        Assert.Single(viewModel.RootNodes);

        var view = new IniEditorView { DataContext = viewModel };
        var window = new Window { Width = 1600, Height = 900, Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs(null);

            var treeViews = view.GetVisualDescendants().OfType<TreeView>().ToList();
            Assert.NotEmpty(treeViews);

            var textBoxes = view.GetVisualDescendants().OfType<TextBox>().ToList();
            Assert.NotEmpty(textBoxes);

            var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
            Assert.NotEmpty(buttons);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Verifies that the empty state renders open actions when no document is loaded.
    /// </summary>
    [AvaloniaFact]
    public void EditorView_WithoutDocument_RendersEmptyState()
    {
        using var viewModel = CreateViewModel();

        var view = new IniEditorView { DataContext = viewModel };
        var window = new Window { Width = 1600, Height = 900, Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs(null);

            var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
            Assert.NotEmpty(buttons);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Verifies that deleting the selected block clears the selection and a second delete does not throw.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DeleteCommand_ClearsSelectionAndFieldRowsAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = "Victim";
        viewModel.AddBlockCommand.Execute(null);
        Assert.NotNull(viewModel.SelectedNode);

        viewModel.DeleteCommand.Execute(null);

        Assert.Null(viewModel.SelectedNode);
        Assert.Empty(viewModel.FieldRows);
        Assert.Empty(viewModel.RootNodes);

        var exception = Record.Exception(() => viewModel.DeleteCommand.Execute(null));
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that undo removes added blocks and redo re-inserts them in position.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task UndoRedo_AddBlock_RestoresAndReappliesInPositionAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = "First";
        viewModel.AddBlockCommand.Execute(null);
        viewModel.NewBlockName = "Second";
        viewModel.AddBlockCommand.Execute(null);
        Assert.Equal(2, viewModel.RootNodes.Count);

        viewModel.UndoCommand.Execute(null);

        Assert.Single(viewModel.RootNodes);
        Assert.Equal("First", viewModel.RootNodes[0].Block.Name);

        viewModel.RedoCommand.Execute(null);

        Assert.Equal(2, viewModel.RootNodes.Count);
        Assert.Equal("First", viewModel.RootNodes[0].Block.Name);
        Assert.Equal("Second", viewModel.RootNodes[1].Block.Name);
    }

    /// <summary>
    /// Verifies that editing a field value is undoable and redoable.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task FieldEdit_UndoRedo_RestoresValueAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = "Edited";
        viewModel.AddBlockCommand.Execute(null);

        viewModel.FieldRows.First(row => row.Key == "Health").Value = "42.0";
        Assert.Equal("42.0", viewModel.FieldRows.First(row => row.Key == "Health").Value);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal("100.0", viewModel.FieldRows.First(row => row.Key == "Health").Value);

        viewModel.RedoCommand.Execute(null);
        Assert.Equal("42.0", viewModel.FieldRows.First(row => row.Key == "Health").Value);
    }

    /// <summary>
    /// Verifies that duplicate block names are rejected.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AddBlock_DuplicateName_IsRejectedAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = "Duplicate";
        viewModel.AddBlockCommand.Execute(null);
        viewModel.NewBlockName = "Duplicate";
        viewModel.AddBlockCommand.Execute(null);

        Assert.Single(viewModel.RootNodes);
    }

    /// <summary>
    /// Verifies that empty block names are rejected except for blocks that allow them.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AddBlock_EmptyName_IsRejectedExceptWhenAllowedAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = string.Empty;
        viewModel.AddBlockCommand.Execute(null);

        Assert.Empty(viewModel.RootNodes);

        viewModel.NewBlockType = "ExperienceLevels";
        viewModel.AddBlockCommand.Execute(null);

        Assert.Single(viewModel.RootNodes);
    }

    /// <summary>
    /// Verifies that template insertions skip fields the block already defines.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AddUpgradeHookup_ExistingFields_AreSkippedAsync()
    {
        using var viewModel = CreateViewModel();
        await viewModel.NewDocumentCommand.ExecuteAsync(null);
        viewModel.NewBlockType = "Object";
        viewModel.NewBlockName = "Hooked";
        viewModel.AddBlockCommand.Execute(null);
        viewModel.AddUpgradeHookupCommand.Execute(null);
        var fieldCount = viewModel.SelectedNode!.Block.Fields.Count;

        viewModel.AddUpgradeHookupCommand.Execute(null);

        Assert.Equal(fieldCount, viewModel.SelectedNode!.Block.Fields.Count);
        Assert.Single(viewModel.SelectedNode.Block.Fields, field => field.Key == "Upgrade");
    }

    /// <summary>
    /// Verifies that opening a flat settings file populates editable file-settings rows
    /// and that edits round-trip through save.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task OpenFile_FlatSettings_PopulatesGlobalFieldRowsAsync()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"GenHubIniGlobals{Guid.NewGuid():N}.ini");
        await File.WriteAllTextAsync(filePath, "ReallyLowMHz = 600\nLODPreset = LOW P3 1400 GF3 128\n");
        try
        {
            using var viewModel = CreateViewModel();
            var opened = await viewModel.OpenFileAsync(filePath);

            Assert.True(opened);
            Assert.True(viewModel.HasGlobalFields);
            Assert.Equal(2, viewModel.GlobalFieldRows.Count);
            Assert.Equal("ReallyLowMHz", viewModel.GlobalFieldRows[0].Key);

            viewModel.GlobalFieldRows[0].Value = "700";
            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = await File.ReadAllTextAsync(filePath);
            Assert.Contains("ReallyLowMHz = 700", saved, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static IniEditorViewModel CreateViewModel()
    {
        var mockLocalization = new Mock<ILocalizationService>();
        mockLocalization
            .Setup(service => service.GetString(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Returns((string key, object?[] args) => key);

        var mockReferenceService = new Mock<IIniReferenceService>();
        mockReferenceService
            .Setup(service => service.RebuildIndexAsync(
                It.IsAny<IniDocument?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<int>.CreateSuccess(0, TimeSpan.Zero));
        mockReferenceService
            .Setup(service => service.Entries)
            .Returns(new List<IniReferenceEntry>());
        mockReferenceService
            .Setup(service => service.GetNames(It.IsAny<string>()))
            .Returns(new List<string>());

        return new IniEditorViewModel(
            new IniDocumentService(Mock.Of<ILogger<IniDocumentService>>()),
            new IniSchemaService(mockLocalization.Object),
            mockReferenceService.Object,
            Mock.Of<ISageMappedImageParser>(),
            Mock.Of<IWndImageAssetService>(),
            Mock.Of<IGameInstallationService>(),
            Mock.Of<INotificationService>(),
            mockLocalization.Object,
            Mock.Of<IDialogService>(),
            Mock.Of<ILogger<IniEditorViewModel>>());
    }
}
