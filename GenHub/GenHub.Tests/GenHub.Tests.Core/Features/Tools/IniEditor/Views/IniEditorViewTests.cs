using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Features.Tools.IniEditor.Services;
using GenHub.Features.Tools.IniEditor.ViewModels;
using GenHub.Features.Tools.IniEditor.Views;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;
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

    private static IniEditorViewModel CreateViewModel()
    {
        var mockLocalization = new Mock<ILocalizationService>();
        mockLocalization
            .Setup(service => service.GetString(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Returns((string key, object?[] args) => key);

        return new IniEditorViewModel(
            new IniDocumentService(Mock.Of<ILogger<IniDocumentService>>()),
            new IniSchemaService(),
            Mock.Of<INotificationService>(),
            mockLocalization.Object,
            Mock.Of<IDialogService>(),
            Mock.Of<ILogger<IniEditorViewModel>>());
    }
}
