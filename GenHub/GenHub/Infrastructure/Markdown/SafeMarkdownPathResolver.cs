using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Markdown.Avalonia.Utils;

namespace GenHub.Infrastructure.Markdown;

/// <summary>
/// A restricted path resolver for Markdown viewers rendering untrusted content.
/// Restricts image loading strictly to HTTP and HTTPS URLs, preventing unauthorized local file or asset access.
/// </summary>
public sealed class SafeMarkdownPathResolver : IPathResolver
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    /// <inheritdoc/>
    public string? AssetPathRoot { get; set; }

    /// <inheritdoc/>
    public IEnumerable<string>? CallerAssemblyNames { get; set; }

    /// <inheritdoc/>
    public async Task<Stream?>? ResolveImageResource(string relativeOrAbsolutePath)
    {
        // Only permit http and https schemes for images in untrusted content.
        // Reject local files (file:), application assets (avares:), UNC, and relative paths.
        if (Uri.TryCreate(relativeOrAbsolutePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            try
            {
                var response = await HttpClient.GetAsync(uri).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
