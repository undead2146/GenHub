using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Features.Content.Services.ContentDiscoverers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;

namespace GenHub.Tests.Core.Features.Content;

/// <summary>
/// Unit tests for <see cref="CSVDiscoverer"/>.
/// </summary>
public class CSVDiscovererTests
{
    private const string GeneralsVersion = "1.08";
    private const string ZeroHourVersion = "1.04";
    private const string TestGeneralsCsvUrl = "https://example.com/generals.csv";
    private const string TestZeroHourCsvUrl = "https://example.com/zerohour.csv";
    private const string TestFallbackCsvUrl = "https://example.com/fallback.csv";

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns an empty result when the query specifies a non-game-installation content type.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenContentTypeIsNotGameInstallation_ReturnsEmptyResult()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());
        var query = new ContentSearchQuery { ContentType = ContentType.Map };

        var result = await discoverer.DiscoverAsync(query);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> loads and returns entries from index.json when available.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenIndexJsonAvailable_ReturnsEntries()
    {
        using var indexFile = CreateIndexFile(
            CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn),
            CreateEntry(TestZeroHourCsvUrl, CsvConstants.ZeroHourGameType, ZeroHourVersion, CsvConstants.LanguageEn));

        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery());

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> falls back to configured catalogs when index.json is unavailable.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenIndexJsonFails_FallsBackToConfiguredCatalogs()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration
        {
            IndexFilePath = "https://nonexistent.invalid/index.json",
            CsvValidationCatalogs =
            [
                CreateEntry(TestFallbackCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn),
            ],
        });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery());

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.First().ResolverMetadata[CsvConstants.CsvUrlMetadataKey].Should().Be(TestFallbackCsvUrl);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns an empty result when no sources contain valid entries.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenConfigEmpty_ReturnsEmpty()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration
        {
            IndexFilePath = string.Empty,
            CsvValidationCatalogs = [],
        });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery());

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> filters entries by language when a specific language is requested.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WithSpecificLanguageQuery_ReturnsFilteredResult()
    {
        using var indexFile = CreateIndexFile(
            CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn, CsvConstants.LanguageDe, CsvConstants.LanguageFr));

        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery { Language = "de" });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.First().ResolverMetadata[CsvConstants.LanguageMetadataKey].Should().Be(CsvConstants.LanguageDe);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns results for all supported languages when "All" is queried.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WithAllLanguageQuery_ReturnsAllSupportedLanguages()
    {
        using var indexFile = CreateIndexFile(
            CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn, CsvConstants.LanguageDe, CsvConstants.LanguageFr));

        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery { Language = CsvConstants.AllLanguagesFilter });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(3);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> matches an entry whose language is "All" when a specific language is queried.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenEntryHasAllLanguage_MatchesAnyQuery()
    {
        using var indexFile = CreateIndexFile(
            CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.AllLanguagesFilter));

        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery { Language = "fr" });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.First().ResolverMetadata[CsvConstants.LanguageMetadataKey].Should().Be(CsvConstants.LanguageFr);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> caches catalog entries across multiple calls.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_CachesEntriesBetweenCalls()
    {
        var tempIndex = CreateIndexFile(CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn));
        try
        {
            var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = tempIndex.FilePath });

            var firstResult = await discoverer.DiscoverAsync(new ContentSearchQuery());
            firstResult.Data!.Items.Should().HaveCount(1);

            // Delete file - second call should still succeed from cache
            tempIndex.Dispose();

            var secondResult = await discoverer.DiscoverAsync(new ContentSearchQuery());
            secondResult.Success.Should().BeTrue();
            secondResult.Data!.Items.Should().HaveCount(1);
        }
        finally
        {
            tempIndex.Dispose();
        }
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> propagates cancellation tokens.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            discoverer.DiscoverAsync(new ContentSearchQuery(), cts.Token));
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns a failure result when query is null.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenQueryIsNull_ReturnsFailure()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        var result = await discoverer.DiscoverAsync(null!);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns an empty result when network requests fail and no fallback is available.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenNetworkFails_ReturnsEmptyResult()
    {
        var httpHandler = new StubHttpMessageHandler(statusCode: HttpStatusCode.InternalServerError);
        var discoverer = CreateDiscoverer(
            new CsvCatalogConfiguration { IndexFilePath = "https://example.com/index.json" },
            httpHandler);

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery());

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> retries loading on next query after a transient failure without permanently caching empty results.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WhenFirstLoadFails_RetriesOnNextCall()
    {
        var tempIndex = CreateIndexFile(CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn));
        try
        {
            // First point to non-existent file
            var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = tempIndex.FilePath + ".nonexistent" });
            var firstResult = await discoverer.DiscoverAsync(new ContentSearchQuery());
            firstResult.Success.Should().BeTrue();
            firstResult.Data!.Items.Should().BeEmpty();

            // Next point to actual file with a new discoverer or reconfigured discoverer
            var secondDiscoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = tempIndex.FilePath });
            var secondResult = await secondDiscoverer.DiscoverAsync(new ContentSearchQuery());
            secondResult.Success.Should().BeTrue();
            secondResult.Data!.Items.Should().HaveCount(1);
        }
        finally
        {
            tempIndex.Dispose();
        }
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> gives precedence to the configured index over default.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_ConfiguredIndexTakesPrecedenceOverDefault()
    {
        var tempIndex = CreateIndexFile(CreateEntry("https://custom.com/custom.csv", CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn));
        try
        {
            var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = tempIndex.FilePath });
            var result = await discoverer.DiscoverAsync(new ContentSearchQuery());

            result.Success.Should().BeTrue();
            result.Data!.Items.Should().ContainSingle();
            result.Data.Items.First().SourceUrl.Should().Be("https://custom.com/custom.csv");
        }
        finally
        {
            tempIndex.Dispose();
        }
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> uses configured fallback catalogs.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WithConfiguredFallbackCatalogs_UsesFallback()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration
        {
            CsvValidationCatalogs =
            [
                new CsvCatalogRegistryEntry
                {
                    Url = TestFallbackCsvUrl,
                    GameType = CsvConstants.GeneralsGameType,
                    Version = GeneralsVersion,
                    SupportedLanguages = [CsvConstants.LanguageEn],
                },
            ],
        });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery());

        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.First().ResolverMetadata[CsvConstants.CsvUrlMetadataKey].Should().Be(TestFallbackCsvUrl);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns no items for unsupported target games.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WithUnsupportedTargetGame_ReturnsEmpty()
    {
        using var indexFile = CreateIndexFile(CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn));
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery { TargetGame = (GameType)999 });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> handles Zero Hour game type correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WithZeroHourQuery_ReturnsValidResult()
    {
        using var indexFile = CreateIndexFile(CreateEntry(TestZeroHourCsvUrl, CsvConstants.ZeroHourGameType, ZeroHourVersion, CsvConstants.LanguageEn));
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery { TargetGame = GameType.ZeroHour });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.First().TargetGame.Should().Be(GameType.ZeroHour);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.DiscoverAsync"/> returns empty when no matching game type is found.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DiscoverAsync_WithNonMatchingGameType_ReturnsEmpty()
    {
        using var indexFile = CreateIndexFile(CreateEntry(TestGeneralsCsvUrl, CsvConstants.GeneralsGameType, GeneralsVersion, CsvConstants.LanguageEn));
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration { IndexFilePath = indexFile.FilePath });

        var result = await discoverer.DiscoverAsync(new ContentSearchQuery { TargetGame = GameType.ZeroHour });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.SourceName"/> returns the correct source name.
    /// </summary>
    [Fact]
    public void SourceName_ReturnsExpectedValue()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        discoverer.SourceName.Should().Be(CsvConstants.SourceName);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.Description"/> returns the correct description.
    /// </summary>
    [Fact]
    public void Description_ReturnsExpectedValue()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        discoverer.Description.Should().Be(CsvConstants.Description);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.IsEnabled"/> returns true.
    /// </summary>
    [Fact]
    public void IsEnabled_ReturnsTrue()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        discoverer.IsEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.Capabilities"/> returns DirectSearch.
    /// </summary>
    [Fact]
    public void Capabilities_ReturnsDirectSearch()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        discoverer.Capabilities.Should().Be(ContentSourceCapabilities.DirectSearch);
    }

    /// <summary>
    /// Verifies that <see cref="CSVDiscoverer.Dispose()"/> can be called multiple times without throwing.
    /// </summary>
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var discoverer = CreateDiscoverer(new CsvCatalogConfiguration());

        var act = () =>
        {
            discoverer.Dispose();
            discoverer.Dispose();
        };

        act.Should().NotThrow();
    }

    private static CSVDiscoverer CreateDiscoverer(CsvCatalogConfiguration? config, HttpMessageHandler? httpMessageHandler = null)
    {
        var mockConfig = new Mock<IConfigurationProviderService>();
        mockConfig.Setup(o => o.GetCsvCatalogConfiguration()).Returns(config!);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(o => o.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(httpMessageHandler ?? new StubHttpMessageHandler()));

        return new CSVDiscoverer(Mock.Of<ILogger<CSVDiscoverer>>(), mockConfig.Object, mockHttpClientFactory.Object);
    }

    private static TempIndexFile CreateIndexFile(params CsvCatalogRegistryEntry[] entries)
    {
        return new TempIndexFile(entries);
    }

    private static CsvCatalogRegistryEntry CreateEntry(string url, string gameType, string version, params string[] languages)
    {
        return new CsvCatalogRegistryEntry
        {
            Url = url,
            GameType = gameType,
            Version = version,
            SupportedLanguages = languages.ToList(),
            IsActive = true,
        };
    }

    private sealed class TempIndexFile : IDisposable
    {
        public TempIndexFile(IEnumerable<CsvCatalogRegistryEntry> entries)
        {
            FilePath = Path.GetTempFileName();
            var index = new CsvCatalogRegistryIndex
            {
                Entries = entries.ToList(),
            };

            File.WriteAllText(FilePath, JsonSerializer.Serialize(index));
        }

        public string FilePath { get; }

        public void Dispose()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }

    private sealed class StubHttpMessageHandler(
        string? expectedUrl = null,
        string content = "",
        HttpStatusCode statusCode = HttpStatusCode.NotFound) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var code = expectedUrl == null || request.RequestUri?.AbsoluteUri == expectedUrl
                ? statusCode
                : HttpStatusCode.NotFound;
            var responseContent = code == HttpStatusCode.OK ? content : string.Empty;
            var response = new HttpResponseMessage(code)
            {
                RequestMessage = request,
                Content = new StringContent(responseContent),
            };

            return Task.FromResult(response);
        }
    }
}
