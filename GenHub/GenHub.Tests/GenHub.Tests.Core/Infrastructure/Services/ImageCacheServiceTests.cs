using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Infrastructure.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace GenHub.Tests.Core.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="ImageCacheService"/> security, caching, and download logic.
/// </summary>
public class ImageCacheServiceTests
{
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    /// <summary>
    /// Verifies that private, loopback, multicast, and reserved IPv4/IPv6 addresses are rejected as unsafe.
    /// </summary>
    /// <param name="ipString">The IP string to test.</param>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("169.254.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.255")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    public void IsSafeIpAddress_UnsafeAddresses_ReturnsFalse(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.False(ImageCacheService.IsSafeIpAddress(ip));
    }

    /// <summary>
    /// Verifies that public IP addresses are accepted as safe.
    /// </summary>
    /// <param name="ipString">The IP string to test.</param>
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("208.67.222.222")]
    [InlineData("2606:4700:4700::1111")]
    public void IsSafeIpAddress_PublicAddresses_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.True(ImageCacheService.IsSafeIpAddress(ip));
    }

    /// <summary>
    /// Verifies that invalid or unsafe remote URLs are rejected by <see cref="ImageCacheService.IsSafeRemoteUrl"/>.
    /// </summary>
    /// <param name="url">The URL to test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("file:///C:/test.png")]
    [InlineData("ftp://example.com/test.png")]
    [InlineData("http://localhost/test.png")]
    [InlineData("http://127.0.0.1/test.png")]
    [InlineData("http://192.168.1.1/test.png")]
    [InlineData("http://10.0.0.1/test.png")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://224.0.0.1/test.png")]
    [InlineData("http://240.0.0.1/test.png")]
    [InlineData("http://[::1]/test.png")]
    [InlineData("http://[fc00::1]/test.png")]
    [InlineData("http://[fe80::1]/test.png")]
    public void IsSafeRemoteUrl_UnsafeUrls_ReturnsFalse(string? url)
    {
        Assert.False(ImageCacheService.IsSafeRemoteUrl(url, out _));
    }

    /// <summary>
    /// Verifies that public HTTP/HTTPS URLs are accepted by <see cref="ImageCacheService.IsSafeRemoteUrl"/>.
    /// </summary>
    /// <param name="url">The URL to test.</param>
    [Theory]
    [InlineData("https://media.moddb.com/images/members/1/1/1/profile/avatar.jpg")]
    [InlineData("http://example.com/test.png")]
    [InlineData("https://8.8.8.8/test.png")]
    public void IsSafeRemoteUrl_SafeUrls_ReturnsTrue(string url)
    {
        Assert.True(ImageCacheService.IsSafeRemoteUrl(url, out var uri));
        Assert.NotNull(uri);
    }

    /// <summary>
    /// Verifies that <see cref="ImageCacheService"/> initializes using the path from
    /// <see cref="IConfigurationProviderService.GetApplicationDataPath"/>.
    /// </summary>
    [Fact]
    public void Constructor_WithConfigurationProvider_UsesRelocatedPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GenHubTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configMock = new Mock<IConfigurationProviderService>();
            configMock.Setup(c => c.GetApplicationDataPath()).Returns(tempRoot);

            var service = new ImageCacheService(configMock.Object);
            var expectedCacheDir = Path.Combine(tempRoot, "GenHub", DirectoryNames.Cache, "Images");

            Assert.True(Directory.Exists(expectedCacheDir));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // ignore cleanup failure
                }
            }
        }
    }

    /// <summary>
    /// Verifies that local image files can be loaded via <see cref="ImageCacheService.GetBitmapAsync"/>
    /// without locking the file on disk.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [AvaloniaFact]
    public async Task GetBitmapAsync_LocalFile_LoadsSuccessfullyWithoutLockingAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_image_{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tempFile, ValidPngBytes);

        try
        {
            var service = new ImageCacheService();
            var bitmap = await service.GetBitmapAsync(tempFile);

            Assert.NotNull(bitmap);
            Assert.Equal(1, bitmap.PixelSize.Width);
            Assert.Equal(1, bitmap.PixelSize.Height);

            // Verify file is not locked on disk by modifying or deleting it immediately
            File.WriteAllBytes(tempFile, ValidPngBytes);
            File.Delete(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // ignore cleanup failure
                }
            }
        }
    }

    /// <summary>
    /// Verifies that memory cache eviction does not dispose bitmaps currently in use.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [AvaloniaFact]
    public async Task MemoryCache_Eviction_DoesNotDisposeBitmapAsync()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(ValidPngBytes),
                };
                res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return res;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new ImageCacheService(null, httpClient);

        // Fetch the first image which will be in memory cache
        var bitmap0 = await service.GetBitmapAsync($"https://example.com/first_image_{Guid.NewGuid():N}.png");
        Assert.NotNull(bitmap0);

        // Flood cache with 205 items to trigger LRU eviction (MaxMemoryCacheEntries = 200)
        for (int i = 0; i < 205; i++)
        {
            await service.GetBitmapAsync($"https://example.com/flood_{Guid.NewGuid():N}.png");
        }

        // Also test ClearMemoryCache
        service.ClearMemoryCache();

        // Bitmap0 should still be usable and not throw ObjectDisposedException
        Assert.Equal(1, bitmap0.PixelSize.Width);
    }

    /// <summary>
    /// Verifies that multiple concurrent calls to <see cref="ImageCacheService.GetBitmapAsync"/>
    /// for the same URL coalesce into a single HTTP download.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [AvaloniaFact]
    public async Task GetBitmapAsync_ConcurrentRequests_CoalesceToSingleDownloadAsync()
    {
        int requestCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                Interlocked.Increment(ref requestCount);
                await Task.Delay(50, ct); // simulate network latency
                var res = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(ValidPngBytes),
                };
                res.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return res;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new ImageCacheService(null, httpClient);

        var url = $"https://example.com/test_coalesce_{Guid.NewGuid():N}.png";

        // Launch 5 concurrent requests
        var task1 = service.GetBitmapAsync(url);
        var task2 = service.GetBitmapAsync(url);
        var task3 = service.GetBitmapAsync(url);
        var task4 = service.GetBitmapAsync(url);
        var task5 = service.GetBitmapAsync(url);

        var results = await Task.WhenAll(task1, task2, task3, task4, task5);

        foreach (var r in results)
        {
            Assert.NotNull(r);
        }

        // Only 1 actual HTTP request should have executed
        Assert.Equal(1, requestCount);
    }

    /// <summary>
    /// Verifies that <see cref="ImageCacheService.GetBitmapAsync"/> follows HTTP redirects.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [AvaloniaFact]
    public async Task GetBitmapAsync_FollowsRedirectAsync()
    {
        var redirectKey = Guid.NewGuid().ToString("N");
        var redirectUrl = $"https://example.com/redirect_{redirectKey}.png";
        var destUrl = $"https://example.com/dest_{redirectKey}.png";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == redirectUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = new Uri(destUrl);
                return redirect;
            });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == destUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var ok = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(ValidPngBytes),
                };
                ok.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return ok;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new ImageCacheService(null, httpClient);

        var bitmap = await service.GetBitmapAsync(redirectUrl);
        Assert.NotNull(bitmap);
        Assert.Equal(1, bitmap.PixelSize.Width);
    }

    /// <summary>
    /// Verifies that non-image content types (such as HTML error pages) are rejected and return null.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [AvaloniaFact]
    public async Task GetBitmapAsync_NonImageContentType_ReturnsNullAsync()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var res = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body>Not an image</body></html>"),
                };
                res.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return res;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new ImageCacheService(null, httpClient);

        var bitmap = await service.GetBitmapAsync($"https://example.com/error_{Guid.NewGuid():N}.png");
        Assert.Null(bitmap);
    }
}
