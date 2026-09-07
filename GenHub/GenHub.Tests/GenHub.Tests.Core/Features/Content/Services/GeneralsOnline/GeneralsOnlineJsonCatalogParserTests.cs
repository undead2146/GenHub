using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Models.GeneralsOnline;
using GenHub.Core.Models.Providers;
using GenHub.Features.Content.Services.GeneralsOnline;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GenHub.Tests.Core.Features.Content.Services.GeneralsOnline;

/// <summary>
/// Tests for <see cref="GeneralsOnlineJsonCatalogParser"/>.
/// </summary>
public class GeneralsOnlineJsonCatalogParserTests
{
    private readonly GeneralsOnlineJsonCatalogParser _parser;
    private readonly Mock<IProviderDefinitionLoader> _providerLoaderMock;
    private readonly ProviderDefinition _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralsOnlineJsonCatalogParserTests"/> class.
    /// </summary>
    public GeneralsOnlineJsonCatalogParserTests()
    {
        _parser = new GeneralsOnlineJsonCatalogParser(NullLogger<GeneralsOnlineJsonCatalogParser>.Instance);
        _providerLoaderMock = new Mock<IProviderDefinitionLoader>();

        _provider = new ProviderDefinition
        {
            PublisherType = GeneralsOnlineConstants.PublisherType,
            Endpoints = new ProviderEndpoints
            {
                Custom = new Dictionary<string, string>
                {
                    { "releasesUrl", "https://cdn.playgenerals.online/releases" },
                    { "downloadPageUrl", "https://www.playgenerals.online/download" },
                    { "iconUrl", "https://www.playgenerals.online/logo.png" },
                },
            },
        };
    }

    /// <summary>
    /// Tests that ParseAsync correctly parses PascalCase JSON.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ParseAsync_WithPascalCaseJson_ParsesCorrectlyAsync()
    {
        // Arrange
        var json = @"{
            ""Version"": ""111825_QFE2"",
            ""Download_Url"": ""https://example.com/download.zip"",
            ""Size"": 123456,
            ""Release_Notes"": ""Fixes stuff""
        }";

        var wrapper = $"{{\"source\":\"manifest\",\"data\":{json}}}";

        // Act
        var result = await _parser.ParseAsync(wrapper, _provider);

        // Assert
        Assert.True(result.Success);
        var item = result.Data.First();
        Assert.Equal("111825_QFE2", item.Version);
    }

    /// <summary>
    /// Tests that ParseAsync correctly parses camelCase JSON.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ParseAsync_WithCamelCaseJson_ParsesCorrectlyAsync()
    {
        // Arrange
        // Standard lowercase/camelCase that matches exact property names if attributes weren't there
        var json = @"{
            ""version"": ""111825_QFE2"",
            ""download_url"": ""https://example.com/download.zip"",
            ""size"": 123456,
            ""release_notes"": ""Fixes stuff""
        }";

        var wrapper = $"{{\"source\":\"manifest\",\"data\":{json}}}";

        // Act
        var result = await _parser.ParseAsync(wrapper, _provider);

        // Assert
        Assert.True(result.Success);
        var item = result.Data.First();
        Assert.Equal("111825_QFE2", item.Version);
    }

    /// <summary>
    /// Tests that ParseAsync correctly populates the SHA256 hash when present in the API response.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ParseAsync_WithSha256_PopulatesSha256OnReleaseAsync()
    {
        // Arrange
        const string expectedSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var json = $@"{{
            ""version"": ""111825_QFE2"",
            ""download_url"": ""https://example.com/download.zip"",
            ""size"": 123456,
            ""sha256"": ""{expectedSha256}"",
            ""release_notes"": ""Fixes stuff""
        }}";

        var wrapper = $"{{\"source\":\"manifest\",\"data\":{json}}}";

        // Act
        var result = await _parser.ParseAsync(wrapper, _provider);

        // Assert
        Assert.True(result.Success);
        var item = result.Data.First();
        var release = item.GetData<GeneralsOnlineRelease>();
        Assert.NotNull(release);
        Assert.Equal(expectedSha256, release.Sha256);
    }

    /// <summary>
    /// Tests that ParseAsync correctly parses day releases without QFE (e.g. 082826).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ParseAsync_WithDayReleaseWithoutQfe_ParsesCorrectlyAsync()
    {
        // Arrange
        var json = @"{
            ""version"": ""082826"",
            ""download_url"": ""https://example.com/GeneralsOnline_portable_082826.7z"",
            ""size"": 30108752,
            ""release_notes"": ""www.playgenerals.online""
        }";

        var wrapper = $"{{\"source\":\"manifest\",\"data\":{json}}}";

        // Act
        var result = await _parser.ParseAsync(wrapper, _provider);

        // Assert
        Assert.True(result.Success);
        var item = result.Data.First();
        Assert.Equal("082826", item.Version);
        Assert.Equal("Generals Online 082826", item.Description);
        var release = item.GetData<GeneralsOnlineRelease>();
        Assert.NotNull(release);
        Assert.Equal("082826", release.Version);
        Assert.Equal(new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc), release.VersionDate);
    }
}
