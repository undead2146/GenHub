using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Markdown.Avalonia.Utils;

namespace GenHub.Infrastructure.Markdown;

/// <summary>
/// A safe hyperlink command for Markdown viewers that only permits opening HTTP and HTTPS URLs,
/// preventing arbitrary scheme execution (e.g. file://, UNC paths, executable shell outs).
/// </summary>
public sealed class SafeMarkdownHyperlinkCommand : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return IsSafeUrl(parameter);
    }

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (!IsSafeUrl(parameter, out var safeUri))
        {
            return;
        }

        try
        {
            DefaultHyperlinkCommand.GoTo(safeUri.AbsoluteUri);
        }
        catch
        {
            // Silently ignore browser launch errors.
        }
    }

    private static bool IsSafeUrl(object? parameter, [NotNullWhen(true)] out Uri? safeUri)
    {
        safeUri = null;
        if (parameter is string urlText &&
            !string.IsNullOrWhiteSpace(urlText) &&
            Uri.TryCreate(urlText, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            safeUri = uri;
            return true;
        }

        return false;
    }

    private static bool IsSafeUrl(object? parameter) => IsSafeUrl(parameter, out _);
}
