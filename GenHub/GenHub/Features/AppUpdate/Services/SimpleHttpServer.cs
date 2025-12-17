using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AppUpdate.Services;

/// <summary>
/// Simple HTTP server to serve local .nupkg files to Velopack.
/// </summary>
internal sealed class SimpleHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _nupkgPath;
    private readonly string _releasesPath;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleHttpServer"/> class.
    /// </summary>
    /// <param name="nupkgPath">The path to the .nupkg file to serve.</param>
    /// <param name="releasesPath">The path to the releases.json file (or releases.win.json).</param>
    /// <param name="port">The port to listen on.</param>
    /// <param name="logger">The logger instance.</param>
    public SimpleHttpServer(string nupkgPath, string releasesPath, int port, ILogger logger)
    {
        _nupkgPath = nupkgPath ?? throw new ArgumentNullException(nameof(nupkgPath));
        _releasesPath = releasesPath ?? throw new ArgumentNullException(nameof(releasesPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Port = port;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
    }

    /// <summary>
    /// Starts the HTTP server.
    /// </summary>
    public void Start()
    {
        if (_serverTask != null)
        {
            return; // Already running
        }

        _cts = new CancellationTokenSource();
        _listener.Start();
        _serverTask = Task.Run(() => HandleRequestsAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("HTTP server started on port {Port}", Port);
    }

    /// <summary>
    /// Stops the HTTP server.
    /// </summary>
    public void Stop()
    {
        if (_cts == null || _serverTask == null)
        {
            return;
        }

        _cts.Cancel();
        _listener.Stop();

        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping HTTP server");
        }

        _logger.LogInformation("HTTP server stopped");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _listener.Close();
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling HTTP request");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            _logger.LogDebug("HTTP request: {Method} {Url}", request.HttpMethod, request.Url);

            // Serve releases.win.json or releases.json (Velopack format)
            if (request.Url?.AbsolutePath.Contains("releases", StringComparison.OrdinalIgnoreCase) == true &&
                request.Url.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(_releasesPath))
                {
                    _logger.LogDebug("Serving releases JSON from {Path}", _releasesPath);
                    var content = await File.ReadAllBytesAsync(_releasesPath);
                    response.ContentType = "application/json";
                    response.ContentLength64 = content.Length;
                    await response.OutputStream.WriteAsync(content);
                }
                else
                {
                    _logger.LogWarning("Releases JSON file not found at {Path}", _releasesPath);
                    response.StatusCode = 404;
                }
            }

            // Serve .nupkg file
            else if (request.Url?.AbsolutePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (File.Exists(_nupkgPath))
                {
                    _logger.LogDebug("Serving nupkg from {Path}", _nupkgPath);
                    response.ContentType = "application/octet-stream";
                    response.ContentLength64 = new FileInfo(_nupkgPath).Length;

                    using var fileStream = File.OpenRead(_nupkgPath);
                    await fileStream.CopyToAsync(response.OutputStream);
                }
                else
                {
                    _logger.LogWarning("Nupkg file not found at {Path}", _nupkgPath);
                    response.StatusCode = 404;
                }
            }
            else
            {
                _logger.LogWarning("Unknown request path: {Path}", request.Url?.AbsolutePath);
                response.StatusCode = 404;
            }

            response.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HTTP request");
        }
    }
}
