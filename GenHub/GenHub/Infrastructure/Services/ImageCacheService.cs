using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Services;

/// <summary>
/// Thread-safe service for downloading and caching web and local images in memory and on disk.
/// </summary>
public sealed class ImageCacheService : IImageCacheService
{
    private static readonly object InstanceLock = new();
    private static IImageCacheService? instance;

    private readonly LruMemoryCache memoryCache = new(ImageCacheConstants.MaxMemoryCacheEntries);
    private readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> pendingDownloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient httpClient;
    private readonly string cacheDirectory;
    private readonly ILogger<ImageCacheService>? logger;
    private readonly object diskCleanupLock = new();
    private DateTime lastDiskCleanup = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the singleton instance of <see cref="IImageCacheService"/>.
    /// </summary>
    public static IImageCacheService Instance
    {
        get
        {
            if (instance == null)
            {
                lock (InstanceLock)
                {
                    if (instance == null)
                    {
                        var resolved = AppLocator.GetServiceOrDefault<IImageCacheService>();
                        instance = resolved ?? new ImageCacheService();
                    }
                }
            }

            return instance;
        }

        set
        {
            lock (InstanceLock)
            {
                instance = value;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCacheService"/> class with default settings,
    /// resolving optional dependencies from <see cref="AppLocator"/> if available.
    /// </summary>
    public ImageCacheService()
        : this(AppLocator.GetServiceOrDefault<IConfigurationProviderService>(), null, AppLocator.GetServiceOrDefault<ILogger<ImageCacheService>>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCacheService"/> class using the specified configuration provider,
    /// HTTP client, and logger.
    /// </summary>
    /// <param name="configProvider">Optional configuration provider to resolve the application data directory.</param>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/>.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ImageCacheService(
        IConfigurationProviderService? configProvider = null,
        HttpClient? httpClient = null,
        ILogger<ImageCacheService>? logger = null)
    {
        this.logger = logger;
        this.httpClient = httpClient ?? CreateDefaultHttpClient();

        try
        {
            var appData = configProvider != null
                ? configProvider.GetApplicationDataPath()
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            cacheDirectory = Path.Combine(appData, "GenHub", DirectoryNames.Cache, "Images");
            Directory.CreateDirectory(cacheDirectory);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to initialize image cache directory");
            cacheDirectory = string.Empty;
        }
    }

    /// <summary>
    /// Validates whether an IP address is a safe public IP address (not loopback, private, link-local, carrier-grade NAT, or reserved).
    /// </summary>
    /// <param name="ip">The IP address to evaluate.</param>
    /// <returns><see langword="true"/> if the IP address is safe; otherwise, <see langword="false"/>.</returns>
    public static bool IsSafeIpAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
        {
            return false;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // 0.0.0.0/8 (Current network)
            if (bytes[0] == 0) return false;

            // 10.0.0.0/8 (Private network)
            if (bytes[0] == 10) return false;

            // 100.64.0.0/10 (Carrier-grade NAT)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return false;

            // 127.0.0.0/8 (Loopback)
            if (bytes[0] == 127) return false;

            // 169.254.0.0/16 (Link-local)
            if (bytes[0] == 169 && bytes[1] == 254) return false;

            // 172.16.0.0/12 (Private network)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;

            // 192.0.0.0/24 (IETF Protocol Assignments)
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) return false;

            // 192.0.2.0/24 (TEST-NET-1)
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return false;

            // 192.168.0.0/16 (Private network)
            if (bytes[0] == 192 && bytes[1] == 168) return false;

            // 198.18.0.0/15 (Benchmark testing)
            if (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) return false;

            // 198.51.100.0/24 (TEST-NET-2)
            if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return false;

            // 203.0.113.0/24 (TEST-NET-3)
            if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return false;

            // 224.0.0.0/4 (Multicast)
            if (bytes[0] >= 224 && bytes[0] <= 239) return false;

            // 240.0.0.0/4 (Reserved / Future use / Limited broadcast 255.255.255.255)
            if (bytes[0] >= 240) return false;
        }
        else if (bytes.Length == 16)
        {
            // Unspecified address (::)
            if (ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any)) return false;

            // Multicast (ff00::/8)
            if (bytes[0] == 0xff) return false;

            // Unique local address (fc00::/7)
            if ((bytes[0] & 0xfe) == 0xfc) return false;

            // Link-local address (fe80::/10)
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return false;
        }

        return true;
    }

    /// <summary>
    /// Asynchronously validates that a host does not resolve to private or loopback IP addresses.
    /// </summary>
    /// <param name="host">The host name or IP string to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if all resolved IP addresses are safe; otherwise, <see langword="false"/>.</returns>
    public static async Task<bool> IsSafeHostAsync(string host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IsSafeIpAddress(ip);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            if (addresses.Length == 0)
            {
                return false;
            }

            foreach (var addr in addresses)
            {
                if (!IsSafeIpAddress(addr))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates whether a remote URL is a safe public HTTP or HTTPS endpoint.
    /// Rejects local paths, UNC shares, loopback addresses, link-local addresses, and private networks.
    /// </summary>
    /// <param name="url">The URL string to evaluate.</param>
    /// <param name="uri">When valid, receives the parsed <see cref="Uri"/>.</param>
    /// <returns><see langword="true"/> if the URL meets the security criteria; otherwise, <see langword="false"/>.</returns>
    public static bool IsSafeRemoteUrl(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        if (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (parsedUri.IsFile || parsedUri.IsUnc)
        {
            return false;
        }

        var host = parsedUri.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            parsedUri.IsLoopback ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ip) && !IsSafeIpAddress(ip))
        {
            return false;
        }

        uri = parsedUri;
        return true;
    }

    /// <summary>
    /// Synchronously checks if a bitmap is already cached in memory.
    /// </summary>
    /// <param name="url">The image URL or local file path.</param>
    /// <returns>The cached <see cref="Bitmap"/> if present; otherwise, <see langword="null"/>.</returns>
    public Bitmap? GetBitmapFromMemory(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return memoryCache.TryGet(url);
    }

    /// <summary>
    /// Asynchronously gets a bitmap from memory, disk cache, local file, or web.
    /// </summary>
    /// <param name="url">The image URL or local file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded <see cref="Bitmap"/>, or <see langword="null"/> if loading failed.</returns>
    public async Task<Bitmap?> GetBitmapAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var cached = memoryCache.TryGet(url);
        if (cached != null)
        {
            return cached;
        }

        // Handle avares:// URIs (embedded resources)
        if (url.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(url);
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    var bitmap = new Bitmap(stream);
                    memoryCache.AddOrUpdate(url, bitmap);
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load avares asset '{Uri}'", url);
            }

            return null;
        }

        // Handle relative asset paths (e.g., "/Assets/Logos/logo.png" or "Assets/Logos/logo.png")
        if (url.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var cleanPath = url.TrimStart('/');
                var uri = new Uri($"avares://GenHub/{cleanPath}");
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    var bitmap = new Bitmap(stream);
                    memoryCache.AddOrUpdate(url, bitmap);
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load relative asset '{Path}'", url);
            }

            return null;
        }

        // Handle local file paths (reject UNC shares)
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            (Path.IsPathRooted(url) && !url.StartsWith(@"\\", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)))
        {
            string localPath = url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var fileUri)
                ? fileUri.LocalPath
                : url;

            if (File.Exists(localPath))
            {
                try
                {
                    byte[] fileBytes;
                    using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        using var ms = new MemoryStream();
                        await fs.CopyToAsync(ms, cancellationToken);
                        fileBytes = ms.ToArray();
                    }

                    using var decodeStream = new MemoryStream(fileBytes);
                    var localBitmap = new Bitmap(decodeStream);
                    memoryCache.AddOrUpdate(url, localBitmap);
                    return localBitmap;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to load local file image '{Path}'", localPath);
                    return null;
                }
            }

            return null;
        }

        // Validate safe remote HTTP/HTTPS endpoint. Untrusted local paths and UNC shares are rejected.
        if (!IsSafeRemoteUrl(url, out _))
        {
            return null;
        }

        var diskPath = GetDiskCachePath(url);
        if (!string.IsNullOrEmpty(diskPath) && File.Exists(diskPath))
        {
            var fileInfo = new FileInfo(diskPath);
            var cutoff = DateTime.UtcNow.AddDays(-ImageCacheConstants.DiskCacheTtlDays);
            if (fileInfo.LastWriteTimeUtc < cutoff)
            {
                try
                {
                    File.Delete(diskPath);
                }
                catch
                {
                    // ignore file deletion failure
                }
            }
            else
            {
                try
                {
                    byte[] diskBytes;
                    using (var fs = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        using var ms = new MemoryStream();
                        await fs.CopyToAsync(ms, cancellationToken);
                        diskBytes = ms.ToArray();
                    }

                    using var decodeStream = new MemoryStream(diskBytes);
                    var diskBitmap = new Bitmap(decodeStream);
                    memoryCache.AddOrUpdate(url, diskBitmap);
                    return diskBitmap;
                }
                catch
                {
                    try
                    {
                        File.Delete(diskPath);
                    }
                    catch
                    {
                        // ignore file deletion failure
                    }
                }
            }
        }

        var lazyDownload = pendingDownloads.GetOrAdd(
            url,
            u => new Lazy<Task<Bitmap?>>(
                () => DownloadAndCacheAsync(u, diskPath),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyDownload.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the in-memory cache without disposing bitmaps that may still be referenced by UI controls.
    /// </summary>
    public void ClearMemoryCache() => memoryCache.Clear();

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken);
                if (entry.AddressList.Length == 0 || !entry.AddressList.All(IsSafeIpAddress))
                {
                    throw new HttpRequestException($"Host '{context.DnsEndPoint.Host}' resolved to an unsafe or invalid IP address.");
                }

                var safeIp = entry.AddressList[0];
                var socket = new System.Net.Sockets.Socket(safeIp.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(safeIp, context.DnsEndPoint.Port), cancellationToken);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(ImageCacheConstants.DefaultTimeoutSeconds),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        return client;
    }

    private async Task<Bitmap?> DownloadAndCacheAsync(string initialUrl, string diskPath)
    {
        try
        {
            var currentUrl = initialUrl;
            HttpResponseMessage? response = null;

            for (int redirectCount = 0; redirectCount <= ImageCacheConstants.MaxRedirects; redirectCount++)
            {
                if (!IsSafeRemoteUrl(currentUrl, out var targetUri) || targetUri == null)
                {
                    return null;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                request.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                if (currentUrl.Contains("moddb.com", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.Referrer = new Uri("https://www.moddb.com/");
                }

                response?.Dispose();
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
                {
                    var redirectLocation = response.Headers.Location;
                    if (redirectLocation == null)
                    {
                        return null;
                    }

                    var nextUri = redirectLocation.IsAbsoluteUri
                        ? redirectLocation
                        : new Uri(targetUri, redirectLocation);

                    currentUrl = nextUri.ToString();
                    continue;
                }

                break;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                response?.Dispose();
                return null;
            }

            using (response)
            {
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrEmpty(mediaType) &&
                    !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (response.Content.Headers.ContentLength is long len && len > ImageCacheConstants.MaxImageDownloadSizeBytes)
                {
                    return null;
                }

                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var ms = new MemoryStream();
                var buffer = new byte[81920];
                long totalRead = 0;
                int read = 0;

                while ((read = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalRead += read;
                    if (totalRead > ImageCacheConstants.MaxImageDownloadSizeBytes)
                    {
                        return null;
                    }

                    await ms.WriteAsync(buffer.AsMemory(0, read));
                }

                if (ms.Length == 0)
                {
                    return null;
                }

                var bytes = ms.ToArray();
                if (!string.IsNullOrEmpty(diskPath))
                {
                    var dir = Path.GetDirectoryName(diskPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        try
                        {
                            Directory.CreateDirectory(dir);
                            var tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
                            await File.WriteAllBytesAsync(tempPath, bytes);
                            File.Move(tempPath, diskPath, overwrite: true);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Failed to write disk cache file '{Path}'", diskPath);
                        }
                    }
                }

                using var decodeStream = new MemoryStream(bytes);
                var bitmap = new Bitmap(decodeStream);

                memoryCache.AddOrUpdate(initialUrl, bitmap);
                TriggerDiskCleanupIfNeeded();
                return bitmap;
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error downloading or decoding image '{Url}'", initialUrl);
            return null;
        }
        finally
        {
            pendingDownloads.TryRemove(initialUrl, out _);
        }
    }

    private string GetDiskCachePath(string url)
    {
        if (string.IsNullOrEmpty(cacheDirectory))
        {
            return string.Empty;
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return Path.Combine(cacheDirectory, sb.ToString() + ".img");
    }

    private void TriggerDiskCleanupIfNeeded()
    {
        if (string.IsNullOrEmpty(cacheDirectory) || DateTime.UtcNow - lastDiskCleanup < TimeSpan.FromHours(1))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            lock (diskCleanupLock)
            {
                if (DateTime.UtcNow - lastDiskCleanup < TimeSpan.FromHours(1))
                {
                    return;
                }

                lastDiskCleanup = DateTime.UtcNow;

                try
                {
                    if (!Directory.Exists(cacheDirectory))
                    {
                        return;
                    }

                    var di = new DirectoryInfo(cacheDirectory);
                    var files = di.GetFiles("*.img");
                    var cutoff = DateTime.UtcNow.AddDays(-ImageCacheConstants.DiskCacheTtlDays);
                    long totalSize = 0;

                    var fileList = new List<FileInfo>();
                    foreach (var file in files)
                    {
                        if (file.LastWriteTimeUtc < cutoff)
                        {
                            try
                            {
                                file.Delete();
                            }
                            catch
                            {
                                // ignore cleanup failure
                            }
                        }
                        else
                        {
                            fileList.Add(file);
                            totalSize += file.Length;
                        }
                    }

                    if (totalSize > ImageCacheConstants.MaxDiskCacheSizeBytes)
                    {
                        var sorted = fileList.OrderBy(f => f.LastWriteTimeUtc).ToList();
                        foreach (var file in sorted)
                        {
                            if (totalSize <= ImageCacheConstants.MaxDiskCacheSizeBytes * 0.8)
                            {
                                break;
                            }

                            try
                            {
                                var fileLen = file.Length;
                                file.Delete();
                                totalSize -= fileLen;
                            }
                            catch
                            {
                                // ignore cleanup failure
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Disk cache cleanup failed");
                }
            }
        });
    }

    /// <summary>
    /// Thread-safe bounded LRU memory cache for bitmaps.
    /// Eviction removes the reference from cache without calling Dispose,
    /// so any views or view models currently holding a reference to the Bitmap can continue safely.
    /// Avalonia's native bitmap memory is released by GC finalization when no longer referenced.
    /// </summary>
    private sealed class LruMemoryCache(int maxCapacity)
    {
        private readonly Dictionary<string, LinkedListNode<CacheItem>> cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<CacheItem> lruList = new();
        private readonly object syncLock = new();

        public Bitmap? TryGet(string key)
        {
            lock (syncLock)
            {
                if (cache.TryGetValue(key, out var node))
                {
                    lruList.Remove(node);
                    lruList.AddFirst(node);
                    return node.Value.Bitmap;
                }

                return null;
            }
        }

        public void AddOrUpdate(string key, Bitmap bitmap)
        {
            lock (syncLock)
            {
                if (cache.TryGetValue(key, out var existingNode))
                {
                    lruList.Remove(existingNode);
                    existingNode.Value = new CacheItem(key, bitmap);
                    lruList.AddFirst(existingNode);
                }
                else
                {
                    if (cache.Count >= maxCapacity)
                    {
                        var last = lruList.Last;
                        if (last != null)
                        {
                            lruList.RemoveLast();
                            cache.Remove(last.Value.Key);

                            // Do NOT dispose bitmap. Active controls may still render it.
                        }
                    }

                    var node = new LinkedListNode<CacheItem>(new CacheItem(key, bitmap));
                    lruList.AddFirst(node);
                    cache[key] = node;
                }
            }
        }

        public void Clear()
        {
            lock (syncLock)
            {
                // Do NOT dispose bitmaps. Active controls may still render them.
                cache.Clear();
                lruList.Clear();
            }
        }

        private readonly record struct CacheItem(string Key, Bitmap Bitmap);
    }
}
