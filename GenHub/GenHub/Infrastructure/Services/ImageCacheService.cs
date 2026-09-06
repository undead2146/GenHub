using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using GenHub;
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
    private static readonly Uri ModDbReferrerUri = new(ImageCacheConstants.ModDbReferrerUrl);
    private static volatile IImageCacheService? instance;

    private readonly LruMemoryCache memoryCache = new(ImageCacheConstants.MaxMemoryCacheEntries);
    private readonly ConcurrentDictionary<string, Lazy<CoalescedDownloadOperation>> pendingDownloads = new(StringComparer.OrdinalIgnoreCase);
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
    /// <param name="configProvider">Configuration provider to resolve the application data directory.</param>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/>.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ImageCacheService(
        IConfigurationProviderService? configProvider,
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

        var normalizedIp = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        var bytes = normalizedIp.GetAddressBytes();

        return bytes.Length switch
        {
            4 => IsSafeIPv4(bytes),
            16 => IsSafeIPv6(normalizedIp, bytes),
            _ => false,
        };
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
            return addresses.Length > 0 && addresses.All(IsSafeIpAddress);
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

        if (IsAssetPath(url))
        {
            return TryLoadAssetImage(url);
        }

        if (IsLocalFilePath(url))
        {
            return await TryLoadLocalFileImageAsync(url, cancellationToken);
        }

        if (!IsSafeRemoteUrl(url, out _))
        {
            return null;
        }

        return await GetOrDownloadRemoteImageAsync(url, cancellationToken);
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

    private static bool IsSafeIPv4(byte[] b)
    {
        // 0.0.0.0/8, 10.0.0.0/8, 127.0.0.0/8, Multicast (224-239), Reserved (240+)
        if (b[0] is 0 or 10 or 127 || b[0] >= 224)
        {
            return false;
        }

        // 100.64.0.0/10 (Carrier-grade NAT)
        if (b[0] == 100 && b[1] is >= 64 and <= 127)
        {
            return false;
        }

        // 169.254.0.0/16 (Link-local)
        if (b[0] == 169 && b[1] == 254)
        {
            return false;
        }

        // 172.16.0.0/12 (Private network)
        if (b[0] == 172 && b[1] is >= 16 and <= 31)
        {
            return false;
        }

        return !IsBlocked192Or198Or203(b);
    }

    private static bool IsBlocked192Or198Or203(byte[] b)
    {
        // 192.168.0.0/16 (Private network)
        if (b[0] == 192 && b[1] == 168) return true;

        // 192.0.0.0/24 (IETF Protocol Assignments) & 192.0.2.0/24 (TEST-NET-1)
        if (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2) return true;

        // 198.18.0.0/15 (Benchmark testing)
        if (b[0] == 198 && b[1] is 18 or 19) return true;

        // 198.51.100.0/24 (TEST-NET-2)
        if (b[0] == 198 && b[1] == 51 && b[2] == 100) return true;

        // 203.0.113.0/24 (TEST-NET-3)
        if (b[0] == 203 && b[1] == 0 && b[2] == 113) return true;

        return false;
    }

    private static bool IsSafeIPv6(IPAddress ip, byte[] b)
    {
        if (ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any)) return false;
        if (b[0] == 0xff) return false; // Multicast (ff00::/8)
        if ((b[0] & 0xfe) == 0xfc) return false; // Unique local address (fc00::/7)
        if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) return false; // Link-local address (fe80::/10)
        return true;
    }

    private static bool IsAssetPath(string url) =>
        url.StartsWith("avares://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalFilePath(string url) =>
        url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
        (Path.IsPathRooted(url) && !url.StartsWith(@"\\", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal));

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "ModDB requires its own fixed referrer to serve images and prevent hotlink blocking.")]
    private static HttpRequestMessage CreateImageHttpRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
        if (url.Contains("moddb.com", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Referrer = ModDbReferrerUri;
        }

        return request;
    }

    private static string? ResolveRedirectUrl(Uri? redirectLocation, Uri targetUri)
    {
        if (redirectLocation == null)
        {
            return null;
        }

        return redirectLocation.IsAbsoluteUri
            ? redirectLocation.ToString()
            : new Uri(targetUri, redirectLocation).ToString();
    }

    private static async Task<byte[]?> ReadValidatedImageBytesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > ImageCacheConstants.MaxImageDownloadSizeBytes)
            {
                return null;
            }

            await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return ms.Length > 0 ? ms.ToArray() : null;
    }

    private static List<FileInfo> DeleteExpiredCacheFiles(FileInfo[] files)
    {
        var cutoff = DateTime.UtcNow.AddDays(-ImageCacheConstants.DiskCacheTtlDays);
        var activeFiles = new List<FileInfo>(files.Length);

        foreach (var file in files)
        {
            if (file.LastWriteTimeUtc < cutoff)
            {
                TryDeleteFile(file.FullName);
            }
            else
            {
                activeFiles.Add(file);
            }
        }

        return activeFiles;
    }

    private static void EnforceMaxDiskCacheSize(List<FileInfo> files)
    {
        long totalSize = files.Sum(f => f.Length);
        if (totalSize <= ImageCacheConstants.MaxDiskCacheSizeBytes)
        {
            return;
        }

        var sorted = files.OrderBy(f => f.LastWriteTimeUtc);
        var targetSize = (long)(ImageCacheConstants.MaxDiskCacheSizeBytes * 0.8);

        foreach (var file in sorted)
        {
            if (totalSize <= targetSize)
            {
                break;
            }

            var fileLen = file.Length;
            if (TryDeleteFile(file.FullName))
            {
                totalSize -= fileLen;
            }
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Bitmap? TryLoadAssetImage(string url)
    {
        try
        {
            var uri = url.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(url)
                : new Uri($"avares://GenHub/{url.TrimStart('/')}");

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
            logger?.LogWarning(ex, "Failed to load asset image '{Uri}'", url);
        }

        return null;
    }

    private async Task<Bitmap?> TryLoadLocalFileImageAsync(string url, CancellationToken cancellationToken)
    {
        var localPath = url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var fileUri)
            ? fileUri.LocalPath
            : url;

        if (!File.Exists(localPath))
        {
            return null;
        }

        try
        {
            using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            var localBitmap = new Bitmap(ms);
            memoryCache.AddOrUpdate(url, localBitmap);
            return localBitmap;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load local file image '{Path}'", localPath);
            return null;
        }
    }

    private async Task<Bitmap?> GetOrDownloadRemoteImageAsync(string url, CancellationToken cancellationToken)
    {
        var diskPath = GetDiskCachePath(url);
        var diskBitmap = await TryLoadFromDiskCacheAsync(diskPath, cancellationToken);
        if (diskBitmap != null)
        {
            memoryCache.AddOrUpdate(url, diskBitmap);
            return diskBitmap;
        }

        return await CoalesceDownloadAsync(url, diskPath, cancellationToken);
    }

    private async Task<Bitmap?> TryLoadFromDiskCacheAsync(string diskPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(diskPath) || !File.Exists(diskPath))
        {
            return null;
        }

        var fileInfo = new FileInfo(diskPath);
        var cutoff = DateTime.UtcNow.AddDays(-ImageCacheConstants.DiskCacheTtlDays);
        if (fileInfo.LastWriteTimeUtc < cutoff)
        {
            TryDeleteFile(diskPath);
            return null;
        }

        try
        {
            using var fs = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to read cached image from disk '{Path}'", diskPath);
            return null;
        }
    }

    private async Task<Bitmap?> CoalesceDownloadAsync(string url, string diskPath, CancellationToken cancellationToken)
    {
        IDisposable? waiterRegistration;
        CoalescedDownloadOperation? operation;

        while (true)
        {
            if (pendingDownloads.TryGetValue(url, out var existing))
            {
                if (!existing.Value.Task.IsCompleted &&
                    existing.Value.TryAddWaiter(cancellationToken, out waiterRegistration))
                {
                    operation = existing.Value;
                    break;
                }

                pendingDownloads.TryRemove(KeyValuePair.Create(url, existing));
            }

            Lazy<CoalescedDownloadOperation>? created = null;
            created = new Lazy<CoalescedDownloadOperation>(
                () => new CoalescedDownloadOperation(token => DownloadAndCacheAsync(url, diskPath, created!, token)),
                LazyThreadSafetyMode.ExecutionAndPublication);

            if (pendingDownloads.TryAdd(url, created))
            {
                if (created.Value.TryAddWaiter(cancellationToken, out waiterRegistration))
                {
                    operation = created.Value;
                    break;
                }

                pendingDownloads.TryRemove(KeyValuePair.Create(url, created));
            }
        }

        using (waiterRegistration)
        {
            try
            {
                return await operation.Task.WaitAsync(cancellationToken);
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
    }

    private async Task<Bitmap?> DownloadAndCacheAsync(
        string initialUrl,
        string diskPath,
        Lazy<CoalescedDownloadOperation> lazyOperation,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await ExecuteRequestWithRedirectsAsync(initialUrl, cancellationToken);
            if (response == null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            var imageBytes = await ReadValidatedImageBytesAsync(response, cancellationToken);
            if (imageBytes == null)
            {
                return null;
            }

            using var decodeStream = new MemoryStream(imageBytes);
            var bitmap = new Bitmap(decodeStream);

            memoryCache.AddOrUpdate(initialUrl, bitmap);
            SaveImageBytesToDiskAtomic(diskPath, imageBytes);
            TriggerDiskCleanupIfNeeded();

            return bitmap;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error downloading or decoding image '{Url}'", initialUrl);
            return null;
        }
        finally
        {
            pendingDownloads.TryRemove(KeyValuePair.Create(initialUrl, lazyOperation));
        }
    }

    private async Task<HttpResponseMessage?> ExecuteRequestWithRedirectsAsync(string initialUrl, CancellationToken cancellationToken)
    {
        var currentUrl = initialUrl;

        for (var redirectCount = 0; redirectCount <= ImageCacheConstants.MaxRedirects; redirectCount++)
        {
            if (!IsSafeRemoteUrl(currentUrl, out var targetUri) || targetUri == null)
            {
                return null;
            }

            var request = CreateImageHttpRequest(currentUrl);
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                var nextUrl = ResolveRedirectUrl(response.Headers.Location, targetUri);
                response.Dispose();
                if (nextUrl == null)
                {
                    return null;
                }

                currentUrl = nextUrl;
                continue;
            }

            return response;
        }

        return null;
    }

    private void SaveImageBytesToDiskAtomic(string diskPath, byte[] imageBytes)
    {
        if (string.IsNullOrEmpty(diskPath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(diskPath);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        string? tempPath = null;
        try
        {
            Directory.CreateDirectory(dir);
            tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(tempPath, imageBytes);
            File.Move(tempPath, diskPath, overwrite: true);
        }
        catch (Exception ex)
        {
            if (tempPath != null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore deletion failure of temp file
                }
            }

            logger?.LogWarning(ex, "Failed to write disk cache file '{Path}'", diskPath);
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

        _ = Task.Run(PerformDiskCleanup);
    }

    private void PerformDiskCleanup()
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
                var activeFiles = DeleteExpiredCacheFiles(di.GetFiles("*.img"));
                EnforceMaxDiskCacheSize(activeFiles);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Disk cache cleanup failed");
            }
        }
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

    /// <summary>
    /// Represents an in-flight coalesced image download with waiter count tracking and linked cancellation.
    /// </summary>
    private sealed class CoalescedDownloadOperation : IDisposable
    {
        private readonly object syncLock = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private int waiterCount;
        private bool cancelQueued;

        public CoalescedDownloadOperation(Func<CancellationToken, Task<Bitmap?>> downloadFactory)
        {
            Task = downloadFactory(cancellationTokenSource.Token);
            _ = Task.ContinueWith(
                _ => cancellationTokenSource.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        public Task<Bitmap?> Task { get; }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }

        public bool TryAddWaiter(CancellationToken callerToken, out IDisposable? registration)
        {
            lock (syncLock)
            {
                if (cancelQueued || Task.IsCompleted)
                {
                    registration = null;
                    return false;
                }

                waiterCount++;
                registration = new WaiterRegistration(this, callerToken);
                return true;
            }
        }

        private void RemoveWaiter()
        {
            var shouldCancel = false;
            lock (syncLock)
            {
                waiterCount--;
                if (waiterCount <= 0 && !Task.IsCompleted)
                {
                    cancelQueued = true;
                    shouldCancel = true;
                }
            }

            if (shouldCancel)
            {
                try
                {
                    cancellationTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore if CancellationTokenSource was already disposed concurrently.
                }
            }
        }

        private sealed class WaiterRegistration : IDisposable
        {
            private readonly CoalescedDownloadOperation operation;
            private readonly CancellationTokenRegistration registration;
            private int removed;

            public WaiterRegistration(CoalescedDownloadOperation owner, CancellationToken callerToken)
            {
                operation = owner;
                if (callerToken.CanBeCanceled)
                {
                    registration = callerToken.Register(OnCancelled);
                }
            }

            public void Dispose()
            {
                registration.Dispose();
                DecrementOnce();
            }

            private void OnCancelled()
            {
                DecrementOnce();
            }

            private void DecrementOnce()
            {
                if (Interlocked.Exchange(ref removed, 1) == 0)
                {
                    operation.RemoveWaiter();
                }
            }
        }
    }
}
