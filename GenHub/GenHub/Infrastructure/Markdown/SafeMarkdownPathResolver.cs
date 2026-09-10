using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Markdown.Avalonia.Utils;

namespace GenHub.Infrastructure.Markdown;

/// <summary>
/// A restricted path resolver for Markdown viewers rendering untrusted content.
/// Restricts image loading strictly to HTTP and HTTPS URLs, preventing unauthorized local file or asset access.
/// </summary>
public sealed class SafeMarkdownPathResolver : IPathResolver
{
    private const int MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB cap

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
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                if (response.Content.Headers.ContentLength > MaxImageSizeBytes)
                {
                    return null;
                }

                var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                var memoryStream = new MemoryStream();
                var buffer = new byte[81920];
                int bytesRead;
                long totalBytes = 0;
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > MaxImageSizeBytes)
                    {
                        return null;
                    }

                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token).ConfigureAwait(false);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
            {
                return null;
            }
        }

        return null;
    }
}
