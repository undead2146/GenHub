# Publisher Studio Architecture (Corrected)

## Core Concepts Clarification

### Provider Definition vs Catalog

**Provider Definition** (`provider.json`):
- **Purpose**: Publisher metadata (static, rarely changes)
- **Contains**: Publisher ID, name, website, support URL, catalog endpoint, configuration
- **Location**: `GenHub/Providers/{publisherId}.provider.json`
- **Updates**: Rarely (only when publisher info or catalog URL changes)

**Catalog** (from endpoint):
- **Purpose**: Content listings (dynamic, updates frequently)
- **Contains**: Available releases, download URLs, version information
- **Location**: Remote endpoint (e.g., `https://cdn.playgenerals.online/manifest.json`)
- **Updates**: Frequently (every time publisher releases new content)

### Variants vs Addons

**Variants** (WRONG understanding):
- ❌ NOT multiple builds of same release
- ❌ NOT file pattern extraction

**Addons** (CORRECT understanding):
- ✅ Separate content that depends on base content
- ✅ Example: QuickMatch MapPack is an ADDON to 60Hz game client
- ✅ Has its own manifest, dependencies, and installation target

---

## GeneralsOnline Architecture (Current Reality)

### Flow Overview

```
1. Provider Definition (generalsonline.provider.json)
   ├── Publisher metadata (name, website, support)
   └── Catalog endpoint: https://cdn.playgenerals.online/manifest.json

2. Catalog (manifest.json from CDN)
   ├── ONE release entry
   ├── Version: 111825_QFE2
   ├── Download URL: https://cdn.playgenerals.online/releases/GeneralsOnline_portable_111825_QFE2.zip
   └── Contains: 60Hz executable + Maps directory

3. Catalog Parser (GeneralsOnlineJsonCatalogParser)
   └── Returns: ONE ContentSearchResult (the release)

4. Resolver (GeneralsOnlineResolver)
   └── Creates: ONE ContentManifest (for the ZIP download)

5. Deliverer (GeneralsOnlineDeliverer)
   ├── Downloads ZIP
   ├── Extracts to disk
   └── Calls ManifestFactory.CreateManifestsFromExtractedContentAsync()

6. Manifest Factory (GeneralsOnlineManifestFactory)
   ├── Analyzes extracted files
   ├── Creates 2 manifests:
   │   ├── 60Hz GameClient (all files EXCEPT Maps/)
   │   └── QuickMatch MapPack (ONLY Maps/ files)
   └── Registers both to CAS

7. Game Profile
   ├── Links to 60Hz GameClient manifest
   └── Creates workspace from manifest files

8. Workspace
   └── Physical installation directory with game files
```

### Key Insight: Post-Extraction Splitting

**The catalog returns ONE release**, but the ManifestFactory creates **TWO manifests** after extraction:

1. **60Hz GameClient** - Base game
2. **QuickMatch MapPack** - Addon (depends on 60Hz)

This splitting happens **AFTER download and extraction**, not during catalog parsing.

---

## Detailed Flow Analysis

### 1. Provider Definition

**File**: `GenHub/Providers/generalsonline.provider.json`

```json
{
  "providerId": "generalsonline",
  "publisherType": "generalsonline",
  "displayName": "Generals Online",
  "description": "Community-driven multiplayer service...",
  "catalogFormat": "generalsonline-json-api",
  "endpoints": {
    "catalogUrl": "https://cdn.playgenerals.online/manifest.json",
    "websiteUrl": "https://www.playgenerals.online/",
    "supportUrl": "https://discord.playgenerals.online/"
  },
  "targetGame": "ZeroHour",
  "defaultTags": ["multiplayer", "online", "community"],
  "enabled": true
}
```

**Purpose**:
- Tells GenHub WHERE to find the catalog (`catalogUrl`)
- Provides publisher metadata for UI display
- Specifies catalog format for parser selection

---

### 2. Catalog (API Response)

**Endpoint**: `https://cdn.playgenerals.online/manifest.json`

**Response**:
```json
{
  "version": "111825_QFE2",
  "download_url": "https://cdn.playgenerals.online/releases/GeneralsOnline_portable_111825_QFE2.zip",
  "size": 524288000,
  "release_notes": "Bug fixes and improvements",
  "sha256": "abc123..."
}
```

**Key Point**: Catalog returns **ONE release**, not multiple content items.

---

### 3. Catalog Parser

**File**: `GeneralsOnlineJsonCatalogParser.cs`

**Code** (lines 114-119):
```csharp
// Create search result from release
var searchResult = CreateSearchResult(release, provider);

return Task.FromResult(
    OperationResult<IEnumerable<ContentSearchResult>>.CreateSuccess(
        [searchResult]));  // ONE search result
```

**Output**: ONE `ContentSearchResult` representing the release.

---

### 4. Resolver

**File**: `GeneralsOnlineResolver.cs`

**Purpose**: Creates a manifest for downloading the ZIP.

**Output**: ONE `ContentManifest` with:
- Download URL for the ZIP
- Version information
- Publisher metadata

---

### 5. Deliverer

**File**: `GeneralsOnlineDeliverer.cs`

**Code** (lines 51-140):
```csharp
public async Task<OperationResult<ContentManifest>> DeliverContentAsync(
    ContentManifest packageManifest,
    string targetDirectory,
    IProgress<ContentAcquisitionProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    // Step 1: Download ZIP file
    var zipPath = Path.Combine(targetDirectory, "GeneralsOnline.zip");
    var downloadResult = await downloadService.DownloadFileAsync(...);

    // Step 2: Extract ZIP
    var extractPath = Path.Combine(targetDirectory, "extracted");
    ZipFile.ExtractToDirectory(zipPath, extractPath);

    // Step 3: Create manifests from extracted content
    var manifests = await manifestFactory.CreateManifestsFromExtractedContentAsync(
        packageManifest,
        extractPath,
        cancellationToken);

    // Step 4: Register manifests to CAS
    foreach (var manifest in manifests)
    {
        await manifestPool.RegisterManifestAsync(manifest, cancellationToken);
    }

    return OperationResult<ContentManifest>.CreateSuccess(manifests[0]);
}
```

**Key Point**: Deliverer calls `ManifestFactory.CreateManifestsFromExtractedContentAsync()` which returns **2 manifests**.

---

### 6. Manifest Factory (The Splitting Logic)

**File**: `GeneralsOnlineManifestFactory.cs`

**Code** (lines 137-149):
```csharp
public async Task<List<ContentManifest>> CreateManifestsFromExtractedContentAsync(
    ContentManifest originalManifest,
    string extractedDirectory,
    CancellationToken cancellationToken = default)
{
    // Create all variant manifests (60Hz and QuickMatch MapPack) from extracted files
    var manifests = CreateVariantManifestsFromOriginal(originalManifest);

    // Update manifests with extracted files (compute hashes, set file entries)
    return await UpdateManifestsWithExtractedFiles(manifests, extractedDirectory, cancellationToken);
}
```

**Step 1: Create Empty Manifests** (lines 177-226):
```csharp
private List<ContentManifest> CreateVariantManifestsFromOriginal(ContentManifest original)
{
    var manifests = new List<ContentManifest>();

    // Create 60Hz GameClient manifest
    manifests.Add(CreateVariantManifest(
        release,
        GeneralsOnlineConstants.Variant60HzSuffix,  // "60hz"
        GameClientConstants.GeneralsOnline60HzDisplayName));

    // Create QuickMatch MapPack manifest
    manifests.Add(CreateQuickMatchMapPackManifest(release));

    return manifests;
}
```

**Step 2: Filter Files Per Manifest** (lines 401-519):
```csharp
private async Task<List<ContentManifest>> UpdateManifestsWithExtractedFiles(
    List<ContentManifest> manifests,
    string extractPath,
    CancellationToken cancellationToken)
{
    // Detect Maps directory
    var mapsDirectory = Directory.GetDirectories(extractPath, "*", SearchOption.TopDirectoryOnly)
        .FirstOrDefault(d => Path.GetFileName(d).Equals("Maps", StringComparison.OrdinalIgnoreCase));

    // Compute hashes for all files
    var filesWithHashes = new List<(string relativePath, FileInfo fileInfo, string hash, bool isMap)>();
    foreach (var filePath in allFiles)
    {
        var isMap = mapsDirectory != null && filePath.StartsWith(mapsDirectory, StringComparison.OrdinalIgnoreCase);
        var hash = await ComputeFileHashAsync(filePath, cancellationToken);
        filesWithHashes.Add((relativePath, fileInfo, hash, isMap));
    }

    // Assign files to manifests
    foreach (var manifest in manifests)
    {
        var isMapPackManifest = manifest.Id.Contains("mappack", StringComparison.OrdinalIgnoreCase);

        if (isMapPackManifest)
        {
            // MapPack: only include map files
            foreach (var (relativePath, fileInfo, hash, isMap) in filesWithHashes)
            {
                if (!isMap) continue;
                manifestFiles.Add(CreateMapManifestFile(relativePath, fileInfo, hash));
            }
        }
        else
        {
            // GameClient: include everything EXCEPT maps
            foreach (var (relativePath, fileInfo, hash, isMap) in filesWithHashes)
            {
                if (isMap) continue;
                manifestFiles.Add(new ManifestFile { ... });
            }
        }

        manifest.Files = manifestFiles;
    }

    return manifests;
}
```

**Output**: 2 manifests:
1. **60Hz GameClient** - All files except Maps/
2. **QuickMatch MapPack** - Only Maps/ files

---

### 7. Dependencies

**File**: `GeneralsOnlineDependencyBuilder.cs`

**60Hz GameClient Dependencies**:
```csharp
public static List<ContentDependency> GetDependenciesFor60Hz(int mapPackVersion = 0)
{
    return new List<ContentDependency>
    {
        CreateZeroHourDependencyForGeneralsOnline(),  // Requires Zero Hour 1.04
        CreateQuickMatchMapPackDependency(mapPackVersion),  // Requires MapPack
    };
}
```

**QuickMatch MapPack Dependencies**:
```csharp
// MapPack has NO dependencies (it's installed separately)
// But 60Hz GameClient depends on MapPack
```

**Key Point**: MapPack is an **ADDON** that the 60Hz client depends on, not a variant.

---

### 8. Game Profile & Workspace

**Game Profile**:
- Links to a specific manifest (e.g., 60Hz GameClient)
- Contains launch configuration (executable path, arguments)
- References workspace directory

**Workspace**:
- Physical directory where game files are installed
- Created from manifest files
- Example: `C:\Users\{User}\Documents\GenHub\Workspaces\GeneralsOnline_60Hz\`

**Flow**:
```
User creates game profile
  ↓
Profile links to 60Hz GameClient manifest
  ↓
Workspace created from manifest files
  ↓
Files copied/linked to workspace directory
  ↓
User launches game from profile
```

---

## Publisher Studio Architecture (Aligned with GeneralsOnline)

### Core Principles

1. **Provider Definition** = Publisher metadata (static)
2. **Catalog** = Content listings (dynamic)
3. **Catalog returns releases** (not individual content items)
4. **Post-extraction splitting** creates multiple manifests from one release
5. **Addons** are separate manifests with dependencies

### Publisher Studio Catalog Format

**Definition.json** (Publisher metadata):
```json
{
  "publisherId": "rotr-team",
  "displayName": "Rise of the Reds Team",
  "description": "Official releases for Rise of the Reds mod",
  "website": "https://www.riseoftheredsmod.com",
  "supportUrl": "https://discord.gg/rotr",
  "catalogFormat": "publisher-studio-v1",
  "endpoints": {
    "catalogUrl": "https://drive.google.com/uc?id=ABC123/catalog.json"
  },
  "targetGame": "ZeroHour",
  "defaultTags": ["mod", "rotr", "community"],
  "enabled": true
}
```

**Catalog.json** (Content listings):
```json
{
  "catalogVersion": "1.0",
  "lastUpdated": "2026-03-15T00:00:00Z",
  "releases": [
    {
      "releaseId": "rotr-2.0.0",
      "version": "2.0.0",
      "releaseDate": "2026-01-15T00:00:00Z",
      "displayName": "Rise of the Reds 2.0",
      "changelog": "Major update with new factions...",
      "downloadUrl": "https://drive.google.com/uc?id=XYZ789",
      "size": 2147483648,
      "checksum": "sha256:abc123...",
      "content": [
        {
          "contentId": "rotr-mod",
          "displayName": "Rise of the Reds",
          "contentType": "Mod",
          "fileFilter": {
            "exclude": ["Addons/**"]
          },
          "dependencies": [
            {
              "contentId": "zerohour",
              "minVersion": "1.04"
            }
          ]
        },
        {
          "contentId": "rotr-controlbar",
          "displayName": "ControlBar Addon",
          "contentType": "Addon",
          "fileFilter": {
            "include": ["Addons/ControlBar/**"]
          },
          "dependencies": [
            {
              "contentId": "rotr-mod",
              "minVersion": "2.0.0"
            }
          ],
          "variants": [
            {
              "variantId": "1080p",
              "displayName": "1080p Resolution",
              "fileFilter": {
                "include": ["**/1080p/**"]
              }
            },
            {
              "variantId": "1440p",
              "displayName": "1440p Resolution",
              "fileFilter": {
                "include": ["**/1440p/**"]
              }
            },
            {
              "variantId": "4k",
              "displayName": "4K Resolution",
              "fileFilter": {
                "include": ["**/4k/**"]
              }
            },
            {
              "variantId": "8k",
              "displayName": "8K Resolution",
              "fileFilter": {
                "include": ["**/8k/**"]
              }
            }
          ]
        }
      ]
    },
    {
      "releaseId": "rotr-1.9.5",
      "version": "1.9.5",
      "releaseDate": "2025-11-20T00:00:00Z",
      "displayName": "Rise of the Reds 1.9.5",
      "downloadUrl": "https://drive.google.com/uc?id=DEF456",
      "size": 2000000000,
      "checksum": "sha256:def456...",
      "content": [
        {
          "contentId": "rotr-mod",
          "displayName": "Rise of the Reds",
          "contentType": "Mod",
          "fileFilter": {
            "exclude": ["Addons/**"]
          },
          "dependencies": [
            {
              "contentId": "zerohour",
              "minVersion": "1.04"
            }
          ]
        }
      ]
    }
  ]
}
```

### Key Differences from GeneralsOnline

| Aspect | GeneralsOnline | Publisher Studio |
|--------|---------------|------------------|
| **Catalog entries** | ONE release | MULTIPLE releases |
| **Content per release** | Implicit (60Hz + MapPack) | Explicit (content array) |
| **File filtering** | Hardcoded (Maps/ directory) | Data-driven (fileFilter) |
| **Variants** | None (MapPack is addon) | Supported (ControlBar resolutions) |
| **Dependencies** | Hardcoded in DependencyBuilder | Defined in catalog |

---

## Publisher Studio Flow

### 1. Provider Definition

**File**: `GenHub/Providers/rotr-team.provider.json`

```json
{
  "providerId": "rotr-team",
  "catalogFormat": "publisher-studio-v1",
  "endpoints": {
    "catalogUrl": "https://drive.google.com/uc?id=ABC123/catalog.json"
  }
}
```

### 2. Catalog Discovery

**PublisherStudioDiscoverer** fetches catalog from Google Drive:

```csharp
public async Task<OperationResult<IEnumerable<ContentSearchResult>>> DiscoverAsync(
    ProviderDefinition provider,
    ContentSearchQuery query,
    CancellationToken cancellationToken)
{
    var catalogContent = await FetchCatalogFromGoogleDrive(provider.Endpoints.CatalogUrl);
    var parser = _catalogParserFactory.GetParser(provider.CatalogFormat);
    return await parser.ParseAsync(catalogContent, provider, cancellationToken);
}
```

### 3. Catalog Parser

**PublisherStudioCatalogParser** creates search results:

```csharp
public async Task<OperationResult<IEnumerable<ContentSearchResult>>> ParseAsync(
    string catalogContent,
    ProviderDefinition provider,
    CancellationToken cancellationToken)
{
    var catalog = JsonSerializer.Deserialize<PublisherStudioCatalog>(catalogContent);
    var results = new List<ContentSearchResult>();

    foreach (var release in catalog.Releases)
    {
        // Create ONE search result per release
        results.Add(new ContentSearchResult
        {
            Id = $"{provider.ProviderId}.{release.ReleaseId}",
            Name = release.DisplayName,
            Version = release.Version,
            SourceUrl = release.DownloadUrl,
            // Store content definitions for post-extraction splitting
            ResolverMetadata = new Dictionary<string, string>
            {
                ["releaseId"] = release.ReleaseId,
                ["content"] = JsonSerializer.Serialize(release.Content)
            }
        });
    }

    return OperationResult.CreateSuccess(results);
}
```

**Output**: ONE `ContentSearchResult` per release (same as GeneralsOnline).

### 4. Resolver

**PublisherStudioResolver** creates download manifest:

```csharp
public async Task<OperationResult<ContentManifest>> ResolveAsync(
    ProviderDefinition provider,
    ContentSearchResult searchResult,
    CancellationToken cancellationToken)
{
    var manifest = _manifestBuilder
        .WithId($"{searchResult.Version}.{provider.PublisherType}.{searchResult.Id}")
        .WithVersion(searchResult.Version)
        .WithFiles([new ContentFile
        {
            DownloadUrl = searchResult.SourceUrl,
            // Store content definitions for deliverer
            Metadata = new Dictionary<string, string>
            {
                ["content"] = searchResult.ResolverMetadata["content"]
            }
        }])
        .Build();

    return OperationResult.CreateSuccess(manifest);
}
```

### 5. Deliverer

**PublisherStudioDeliverer** downloads and splits:

```csharp
public async Task<OperationResult<ContentManifest>> DeliverContentAsync(
    ContentManifest packageManifest,
    string targetDirectory,
    IProgress<ContentAcquisitionProgress>? progress,
    CancellationToken cancellationToken)
{
    // Step 1: Download ZIP
    var zipPath = await DownloadAsync(packageManifest.Files.First().DownloadUrl);

    // Step 2: Extract ZIP
    var extractPath = Path.Combine(targetDirectory, "extracted");
    ZipFile.ExtractToDirectory(zipPath, extractPath);

    // Step 3: Get content definitions from metadata
    var contentJson = packageManifest.Files.First().Metadata["content"];
    var contentDefinitions = JsonSerializer.Deserialize<List<ContentDefinition>>(contentJson);

    // Step 4: Create manifests from extracted content
    var manifests = await _manifestFactory.CreateManifestsFromExtractedContentAsync(
        packageManifest,
        extractPath,
        contentDefinitions,  // Pass content definitions
        cancellationToken);

    // Step 5: Register manifests to CAS
    foreach (var manifest in manifests)
    {
        await _manifestPool.RegisterManifestAsync(manifest, cancellationToken);
    }

    return OperationResult.CreateSuccess(manifests[0]);
}
```

### 6. Manifest Factory

**PublisherStudioManifestFactory** creates manifests per content definition:

```csharp
public async Task<List<ContentManifest>> CreateManifestsFromExtractedContentAsync(
    ContentManifest originalManifest,
    string extractedDirectory,
    List<ContentDefinition> contentDefinitions,
    CancellationToken cancellationToken)
{
    var manifests = new List<ContentManifest>();

    // Compute hashes for all files
    var allFiles = Directory.GetFiles(extractedDirectory, "*", SearchOption.AllDirectories);
    var filesWithHashes = await ComputeHashesAsync(allFiles, cancellationToken);

    foreach (var content in contentDefinitions)
    {
        if (content.Variants != null && content.Variants.Any())
        {
            // Create manifest per variant
            foreach (var variant in content.Variants)
            {
                var manifest = CreateManifestForVariant(
                    originalManifest,
                    content,
                    variant,
                    filesWithHashes);
                manifests.Add(manifest);
            }
        }
        else
        {
            // Create single manifest for content
            var manifest = CreateManifestForContent(
                originalManifest,
                content,
                filesWithHashes);
            manifests.Add(manifest);
        }
    }

    return manifests;
}

private ContentManifest CreateManifestForContent(
    ContentManifest original,
    ContentDefinition content,
    List<(string path, string hash)> filesWithHashes)
{
    // Filter files based on content.FileFilter
    var filteredFiles = ApplyFileFilter(filesWithHashes, content.FileFilter);

    return new ContentManifest
    {
        Id = $"{original.Version}.{content.ContentId}",
        Name = content.DisplayName,
        ContentType = content.ContentType,
        Dependencies = content.Dependencies,
        Files = filteredFiles.Select(f => new ManifestFile
        {
            RelativePath = f.path,
            Hash = f.hash
        }).ToList()
    };
}
```

**Output**: Multiple manifests based on content definitions:
- ROTR Mod manifest
- ControlBar 1080p manifest
- ControlBar 1440p manifest
- ControlBar 4K manifest
- ControlBar 8K manifest

---

## Comparison: GeneralsOnline vs Publisher Studio

### Similarities

1. **Provider Definition** - Publisher metadata (static)
2. **Catalog** - Content listings (dynamic)
3. **ONE search result per release** - Catalog parser returns releases, not content items
4. **Post-extraction splitting** - Deliverer creates multiple manifests after extraction
5. **File filtering** - Different files go to different manifests
6. **Dependencies** - Addons depend on base content

### Differences

| Aspect | GeneralsOnline | Publisher Studio |
|--------|---------------|------------------|
| **Catalog format** | Simple (version, download URL) | Rich (releases, content, variants) |
| **Content definitions** | Hardcoded in ManifestFactory | Data-driven from catalog |
| **File filtering** | Hardcoded (Maps/ directory) | Configurable (fileFilter) |
| **Variants** | Not supported | Supported (ControlBar resolutions) |
| **Multiple releases** | Not supported (only latest) | Supported (v1.0, v1.1, v1.2) |

---

## Implementation Roadmap

### Phase 1: Extend GeneralsOnline Catalog Format

**Current**:
```json
{
  "version": "111825_QFE2",
  "download_url": "https://...",
  "size": 524288000
}
```

**Extended**:
```json
{
  "version": "111825_QFE2",
  "download_url": "https://...",
  "size": 524288000,
  "content": [
    {
      "contentId": "60hz-client",
      "displayName": "60Hz Game Client",
      "contentType": "GameClient",
      "fileFilter": {
        "exclude": ["Maps/**"]
      },
      "dependencies": [
        { "contentId": "zerohour", "minVersion": "1.04" },
        { "contentId": "quickmatch-mappack", "minVersion": "1.0" }
      ]
    },
    {
      "contentId": "quickmatch-mappack",
      "displayName": "QuickMatch MapPack",
      "contentType": "Addon",
      "fileFilter": {
        "include": ["Maps/**"]
      }
    }
  ]
}
```

**Benefits**:
- Makes content definitions explicit
- Enables data-driven file filtering
- Prepares for Publisher Studio format

### Phase 2: Refactor ManifestFactory

**Current**: Hardcoded splitting logic

**Proposed**: Data-driven splitting

```csharp
public async Task<List<ContentManifest>> CreateManifestsFromExtractedContentAsync(
    ContentManifest originalManifest,
    string extractedDirectory,
    List<ContentDefinition> contentDefinitions,  // From catalog
    CancellationToken cancellationToken)
{
    // Generic splitting logic based on content definitions
}
```

### Phase 3: Implement Publisher Studio Catalog Format

**New catalog format** with:
- Multiple releases
- Content definitions per release
- Variant support
- Dependency specifications

### Phase 4: Build Publisher Studio UI

**Features**:
- Publisher registration (Google Drive, GitHub, ModDB)
- Catalog editor (add releases, define content, set dependencies)
- Variant configuration (resolution variants, language variants)
- Dependency management (version constraints)

---

## Conclusion

### Key Insights

1. **Provider Definition ≠ Catalog**
   - Provider Definition: Publisher metadata (static)
   - Catalog: Content listings (dynamic)

2. **MapPack is an ADDON, not a variant**
   - It's separate content with its own manifest
   - It depends on the 60Hz game client

3. **Post-extraction splitting is the pattern**
   - Catalog returns ONE release
   - Deliverer downloads ONE ZIP
   - ManifestFactory creates MULTIPLE manifests

4. **GeneralsOnline is already aligned with Publisher Studio**
   - Same flow: Provider → Catalog → Release → Manifests
   - Just needs data-driven content definitions

### Next Steps

1. **Extend GeneralsOnline catalog format** to include content definitions
2. **Refactor ManifestFactory** to use data-driven splitting
3. **Implement Publisher Studio catalog format** with multiple releases
4. **Build Publisher Studio UI** for catalog management

This architecture is **extensible** and **future-proof** for Publisher Studio.
