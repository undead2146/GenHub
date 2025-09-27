# Manifest ID System

## Overview

The Manifest ID system provides **deterministic, human-readable, and type-safe identifiers** for all content in the GenHub ecosystem. This system ensures consistent content identification across platforms, prevents ID collisions, and provides robust validation with proper error handling.

## Architecture

The system follows a **layered architecture** with clear separation of concerns:

### Core Layer: ManifestIdGenerator

Low-level utility for generating deterministic, cross-platform manifest IDs with advanced normalization and filesystem-safe output.

### Service Layer: ManifestIdService

Implements the **ResultBase pattern** for type-safe operations, wrapping the generator with proper error handling and returning `ContentOperationResult<ManifestId>`.

### Validation Layer: ManifestIdValidator

Comprehensive validation ensuring ID format compliance and security through regex-based rules and format verification.

### Type Safety Layer: ManifestId

Strongly-typed value object with compile-time validation, implicit conversions, and JSON serialization support.

### Integration Layer

Seamless integration into `ContentManifestBuilder`, `ManifestGenerationService`, and other components.

## ID Formats

### Game Installations (Primary focus for game profiles)

**Format**: `schemaVersion.userVersion.platform.gameType-suffix`

**Examples**:
- Steam ZeroHour Installation: `1.0.steam.zerohour-installation`
- EA App Generals Client: `1.0.eaapp.generals-client`
- Retail Generals: `1.0.retail.generals-installation`

**Publisher Attribution**: Platform name from GameInstallationType enum (lowercase).

### Content Manifests

**Format**: `schemaVersion.userVersion.publisher.contentName`

**Examples**:
- GenHub Mod: `1.0.genhub.custom-mod`
- Simple Test: `1.0.test.simple-content`

**Publisher Attribution**: Defaults to "genhub" for system-generated content.

### Simple IDs

**Format**: Alphanumeric with dashes and dots
**Example**: `test-mod`, `simple.id`
**Use Case**: Testing/simple scenarios.

## API Reference

### ManifestIdGenerator

```csharp
public static class ManifestIdGenerator
{
    // Generate content ID with publisher
    public static string GeneratePublisherContentId(
        string publisherId,
        string contentName,
        int userVersion = 0);

    // Generate game installation ID
    public static string GenerateGameInstallationId(
        GameInstallation installation,
        GameType gameType,
        int userVersion = 0);
}
```

### ManifestIdService

```csharp
public class ManifestIdService : IManifestIdService
{
    // Generate content ID with OperationResult pattern
    OperationResult<ManifestId> GeneratePublisherContentId(
        string publisherId,
        string contentName,
        int userVersion = 0);

    // Generate game installation ID with OperationResult pattern
    OperationResult<ManifestId> GenerateGameInstallationId(
        GameInstallation installation,
        GameType gameType,
        int userVersion = 0);

    // Validate and create ManifestId
    OperationResult<ManifestId> ValidateAndCreateManifestId(string manifestIdString);
}
```

### ManifestId Struct

```csharp
public readonly struct ManifestId : IEquatable<ManifestId>
{
    public string Value { get; }

    // Implicit conversions
    public static implicit operator ManifestId(string id);
    public static implicit operator string(ManifestId id);

    // Validation and creation
    public static ManifestId Create(string id);

    // Equality operations
    public static bool operator ==(ManifestId left, ManifestId right);
    public static bool operator !=(ManifestId left, ManifestId right);

    // Object methods
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public override string ToString();
}
```

## Usage Examples

### Game Installation IDs (Primary for game profiles)

```csharp
// Generate ID for detected game installation
var installation = new GameInstallation("C:\\Games\\Generals", GameInstallationType.Steam);
var gameType = GameType.Generals;

// Using service
var idResult = _manifestIdService.GenerateGameInstallationId(installation, gameType, 0);
if (idResult.Success)
{
    ManifestId id = idResult.Data; // 1.0.steam.generals-installation
}

// Using generator directly
string idString = ManifestIdGenerator.GenerateGameInstallationId(installation, gameType, 0);
```

### Content IDs

```csharp
// Generate ID for content with publisher
var idResult = _manifestIdService.GeneratePublisherContentId("genhub", "custom-profile", 0);
if (idResult.Success)
{
    ManifestId id = idResult.Data; // 1.0.genhub.custom-profile
}
```

### Validation

```csharp
// Validate existing ID
var validation = _manifestIdService.ValidateAndCreateManifestId("1.0.steam.generals-installation");
if (validation.Success)
{
    ManifestId id = validation.Data;
}
```

## Validation Rules

### Game Installation IDs

- Format: `schemaVersion.userVersion.platform.gameType-suffix`
- Platform: Must be valid GameInstallationType (steam, eaapp, retail, etc.)
- GameType: Must be valid (generals, zerohour)
- Suffix: "-installation" or "-client"

### Content IDs

- Format: `schemaVersion.userVersion.publisher.contentName`
- Publisher: Alphanumeric with dashes/underscores
- ContentName: Alphanumeric with dashes/underscores
- Minimum 4 segments

### Simple IDs

- Alphanumeric characters with dashes and dots
- Used for tests and simple scenarios

## Error Handling

Uses **ResultBase pattern** for robust error handling:

```csharp
var result = _manifestIdService.GeneratePublisherContentId("invalid", "content", 0);
if (!result.Success)
{
    _logger.LogError($"ID generation failed: {result.ErrorMessage}");
}
```

## Integration Points

### ContentManifestBuilder

- Automatically generates and validates IDs
- Uses ManifestIdService for consistency

### ManifestGenerationService

- Uses ManifestIdService for all manifest creation
- Ensures deterministic ID generation

### ManifestProvider

- Validates manifest IDs during loading
- Uses IManifestIdService for operations

## Testing

Comprehensive test coverage in ManifestIdGeneratorTests.cs and related test files.

### Running Tests

```bash
dotnet test --filter "ManifestId"
```

## Cross-Platform Determinism

Ensures identical ID generation across all platforms through normalization and consistent processing.
