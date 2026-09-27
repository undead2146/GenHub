using FluentAssertions;
using GenHub.Core.Models.Validation;
using GenHub.Features.Tools.IniEditor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Tests.Core.Features.Tools.IniEditor.Services;

/// <summary>
/// Unit tests for <see cref="IniDocumentService"/>.
/// </summary>
public sealed class IniDocumentServiceTests : IDisposable
{
    private const string SampleDocument =
        "; Generals object definition\n" +
        "Object AmericaVehicleHumvee\n" +
        "  DisplayName = OBJECT:Humvee\n" +
        "  Side = USA\n" +
        "  BuildCost = 800\n" +
        "  Health = 300.0\n" +
        "  WeaponSet\n" +
        "    Conditions = None\n" +
        "    PRIMARY = HumveeMissileWeapon\n" +
        "  End\n" +
        "End\n" +
        "\n" +
        "Weapon HumveeMissileWeapon\n" +
        "  PrimaryDamage = 50.0\n" +
        "  DamageType = EXPLOSION\n" +
        "End\n";

    private readonly Mock<ILogger<IniDocumentService>> _mockLogger;
    private readonly IniDocumentService _service;
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="IniDocumentServiceTests"/> class.
    /// </summary>
    public IniDocumentServiceTests()
    {
        _mockLogger = new Mock<ILogger<IniDocumentService>>();
        _service = new IniDocumentService(_mockLogger.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Cleans up the temporary directory.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that a representative object and weapon document parses with nesting and comments.
    /// </summary>
    [Fact]
    public void ParseText_ValidDocument_ReturnsBlocksWithChildren()
    {
        var result = _service.ParseText(SampleDocument);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Blocks.Should().HaveCount(2);
        var gameObject = result.Data.Blocks[0];
        gameObject.BlockType.Should().Be("Object");
        gameObject.Name.Should().Be("AmericaVehicleHumvee");
        gameObject.Fields.Should().HaveCount(4);
        gameObject.Children.Should().HaveCount(1);
        gameObject.Children[0].BlockType.Should().Be("WeaponSet");
        result.Data.Blocks[1].BlockType.Should().Be("Weapon");
    }

    /// <summary>
    /// Verifies that a block missing End fails parsing.
    /// </summary>
    [Fact]
    public void ParseText_MissingEnd_ReturnsFailure()
    {
        var result = _service.ParseText("Object MissingEnd\n  Health = 100.0\n");

        result.Success.Should().BeFalse();
        result.FirstError.Should().Contain("missing 'End'");
    }

    /// <summary>
    /// Verifies that tab characters are rejected following engine INI rules.
    /// </summary>
    [Fact]
    public void ParseText_TabCharacter_ReturnsFailure()
    {
        var result = _service.ParseText("Object Tabbed\n\tHealth = 100.0\nEnd\n");

        result.Success.Should().BeFalse();
        result.FirstError.Should().Contain("Tab");
    }

    /// <summary>
    /// Verifies that fields outside of a block fail parsing.
    /// </summary>
    [Fact]
    public void ParseText_FieldOutsideBlock_ReturnsFailure()
    {
        var result = _service.ParseText("Health = 100.0\n");

        result.Success.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that an unexpected End without an open block fails parsing.
    /// </summary>
    [Fact]
    public void ParseText_UnexpectedEnd_ReturnsFailure()
    {
        var result = _service.ParseText("End\n");

        result.Success.Should().BeFalse();
        result.FirstError.Should().Contain("Unexpected 'End'");
    }

    /// <summary>
    /// Verifies that semicolon comments are stripped and do not affect parsing.
    /// </summary>
    [Fact]
    public void ParseText_InlineComments_AreStripped()
    {
        var result = _service.ParseText("Object Commented ; trailing comment\n  Health = 100.0 ; hit points\nEnd\n");

        result.Success.Should().BeTrue();
        result.Data!.Blocks[0].Fields[0].Value.Should().Be("100.0");
    }

    /// <summary>
    /// Verifies that writing a parsed document round-trips through the parser.
    /// </summary>
    [Fact]
    public void WriteDocument_ParsedDocument_RoundTrips()
    {
        var parsed = _service.ParseText(SampleDocument);
        parsed.Success.Should().BeTrue();

        var canonical = _service.WriteDocument(parsed.Data!);
        var reparsed = _service.ParseText(canonical);

        reparsed.Success.Should().BeTrue();
        reparsed.Data!.Blocks.Should().HaveCount(2);
        reparsed.Data.Blocks[0].Fields.Should().HaveCount(4);
    }

    /// <summary>
    /// Verifies that validation reports duplicate blocks as warnings without failing.
    /// </summary>
    [Fact]
    public void ValidateDocument_DuplicateBlocks_ReportsWarning()
    {
        var parsed = _service.ParseText("Object Same\n  Health = 1.0\nEnd\nObject Same\n  Health = 2.0\nEnd\n");
        parsed.Success.Should().BeTrue();

        var validation = _service.ValidateDocument(parsed.Data!, "test");

        validation.Issues.Should().Contain(issue => issue.Severity == ValidationSeverity.Warning);
    }

    /// <summary>
    /// Verifies that validating a missing file reports a missing file issue.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ValidateFileAsync_MissingFile_ReportsMissingFile()
    {
        var missing = Path.Combine(_tempDirectory, "Missing.ini");

        var validation = await _service.ValidateFileAsync(missing, CancellationToken.None);

        validation.IsValid.Should().BeFalse();
        validation.MissingFilesCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that formatting a file rewrites it in canonical form.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task FormatFileAsync_ValidFile_RewritesCanonically()
    {
        var filePath = Path.Combine(_tempDirectory, "GameData.ini");
        await File.WriteAllTextAsync(filePath, SampleDocument);

        var result = await _service.FormatFileAsync(filePath, CancellationToken.None);

        result.Success.Should().BeTrue();
        var reparsed = await _service.ParseFileAsync(filePath, CancellationToken.None);
        reparsed.Success.Should().BeTrue();
        reparsed.Data!.Blocks.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that parsing a missing file fails with a not found error.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ParseFileAsync_MissingFile_ReturnsFailure()
    {
        var result = await _service.ParseFileAsync(Path.Combine(_tempDirectory, "Missing.ini"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FirstError.Should().Contain("not found");
    }
}
