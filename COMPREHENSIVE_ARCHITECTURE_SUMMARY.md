# GenHub Publisher/Provider Architecture - Executive Summary

**Generated**: 2026-03-15
**Purpose**: Comprehensive overview of GenHub's decentralized content distribution system

---

## System Overview

GenHub implements a **decentralized content distribution architecture** enabling content creators to publish mods, maps, and addons without centralized infrastructure. The system consists of:

### Core Components

1. **Publisher Studio** - Desktop tool for creators to build and publish catalogs
2. **3-Tier Hosting Model** - Definition → Catalog → Artifacts for URL stability
3. **Content Pipeline** - Discovery → Resolution → Manifest → Installation
4. **Subscription System** - `genhub://` protocol links for user subscriptions
5. **Dependency Resolution** - Cross-publisher dependency management

---

## Architecture Layers

### Layer 1: Publisher Creation (Publisher Studio)

**Purpose**: Enable creators to become publishers without writing JSON

**Workflow**:
```
Create Project → Configure Profile → Add Content → Add Releases →
Upload Artifacts → Publish Catalog → Share Subscription Link
```

**Key Features**:
- Multi-catalog support (separate catalogs for mods, maps, tools)
- Addon chain management (mod → addon → sub-addon)
- Cross-publisher dependencies
- Hosting provider integration (Google Drive, GitHub, Dropbox)
- Validation and circular dependency detection

**Files**:
- `PublisherStudioViewModel.cs` - Main orchestrator
- `ContentLibraryViewModel.cs` - Content management
- `PublishShareViewModel.cs` - Publishing workflow
- `HostingProviderFactory.cs` - Hosting abstraction

---

### Layer 2: Hosting Infrastructure

**3-Tier Model**:

```
Tier 1: Definition (publisher_definition.json)
  ├─ Publisher identity and branding
  ├─ Catalog URLs (primary + mirrors)
  └─ Self-update URL for catalog changes

Tier 2: Catalogs (catalog.json, catalog-maps.json)
  ├─ Content items (mods, maps, addons)
  ├─ Releases with versions
  ├─ Artifact metadata (filename, size, hash)
  └─ Dependencies and relationships

Tier 3: Artifacts (*.zip, *.big)
  ├─ Actual downloadable content
  └─ Referenced by download URLs in catalog
```

**Why 3 Tiers?**
- **URL Stability**: Users subscribe to definition URL, which can redirect to new catalog URLs
- **Flexibility**: Publishers can migrate hosting without breaking subscriptions
- **Redundancy**: Mirrors supported at each tier

**Hosting Providers**:
- **Google Drive** (Recommended): OAuth, 15GB free, stable URLs, in-place updates
- **GitHub**: Gists for catalogs, Releases for artifacts
- **Dropbox**: Similar to Google Drive
- **Manual**: User provides URLs

---

### Layer 3: User Subscription & Discovery

**Subscription Flow**:
```
User clicks genhub://subscribe?url=<definition-url>
  ↓
GenHub fetches definition
  ↓
Show confirmation dialog (publisher info, catalogs)
  ↓
User confirms → Save to subscriptions.json
  ↓
Publisher appears in Downloads sidebar
  ↓
Fetch catalog → Display content
```

**Downloads UI**:
- **Sidebar Navigation**: Core providers + subscribed publishers
- **Multi-Catalog Support**: Publishers can have multiple catalogs (tabs)
- **Filtering**: Provider-specific filters (ModDB sections, CNCLabs tags)
- **Search**: Within selected publisher

**Files**:
- `DownloadsBrowserViewModel.cs` - Main UI orchestrator
- `SubscriptionConfirmationViewModel.cs` - Subscription dialog
- `PublisherDefinitionService.cs` - Fetches definitions and catalogs

---

### Layer 4: Content Pipeline

**Discovery Phase**:
```
Discoverers (GenericCatalog, ModDB, CNCLabs, AODMaps, GitHub)
  ↓
ContentSearchResult[] (lightweight metadata)
  ↓
Display in Content Browser
```

**Resolution Phase**:
```
User clicks "Install"
  ↓
Resolver (GenericCatalogResolver, ModDBResolver, etc.)
  ↓
ContentManifest (complete installation blueprint)
```

**Installation Phase**:
```
Download artifacts → Extract archives → Store in CAS
  ↓
Generate manifest with file references
  ↓
Add to ManifestPool (available for game profiles)
```

**Key Concepts**:
- **Content-Addressable Storage (CAS)**: Files stored by SHA256 hash, deduplicated
- **Workspace Strategies**: Symlink (default), Copy, Hardlink
- **Manifest Factories**: Convert static providers (ModDB, CNCLabs) to manifests

**Files**:
- `GenericCatalogDiscoverer.cs` - Catalog-based discovery
- `GenericCatalogResolver.cs` - Catalog-based resolution
- `ModDBManifestFactory.cs`, `CNCLabsManifestFactory.cs` - Static provider factories

---

### Layer 5: Dependency Resolution

**Dependency Types**:
- **Catalog Dependencies** (Publisher-facing): Defined in catalogs, cross-publisher references
- **Manifest Dependencies** (Runtime): Used during game profile creation

**Resolution Flow**:
```
User installs content with dependencies
  ↓
Check each dependency (installed? version compatible?)
  ↓
If missing: Fetch from publisher catalog or prompt to subscribe
  ↓
Recursively resolve transitive dependencies
  ↓
Install all dependencies before main content
```

**Complex Structures**:
- **ModDB Addon Chains**: Mod → Addon → Sub-Addon
- **ControlBar Variants**: 4 resolution variants (Classic, Modern, Minimal, Extended)
- **Cross-Publisher**: Publisher A's mod depends on Publisher B's mod

**Files**:
- `CatalogDependency.cs` - Catalog dependency model
- `ContentDependency.cs` - Manifest dependency model
- `CrossPublisherDependencyResolver.cs` - Cross-publisher resolution

---

### Layer 6: Game Profile Integration

**Profile Creation**:
```
User creates game profile
  ↓
Select installed content (mods, maps, addons)
  ↓
Choose workspace strategy (symlink/copy/hardlink)
  ↓
Configure game settings (resolution, graphics, etc.)
  ↓
Save profile
```

**Profile Launch**:
```
User launches profile
  ↓
Resolve dependencies → Acquire files from CAS
  ↓
Apply workspace strategy (map files to game directory)
  ↓
Write Options.ini (game settings)
  ↓
Launch game executable
```

**Files**:
- `GameProfileSettingsViewModel.cs` - Profile creation
- `ProfileLauncherFacade.cs` - Profile launch orchestration
- `GameSettingsMapper.cs` - Settings conversion

---

## Data Models

### Core Models

**PublisherDefinition** (V2 Schema):
```json
{
  "$schemaVersion": 2,
  "publisher": { "id", "name", "description", "avatarUrl", "websiteUrl" },
  "catalogs": [
    { "id", "name", "url", "mirrors": [] }
  ],
  "definitionUrl": "self-reference",
  "referrals": [ { "publisherId", "definitionUrl" } ]
}
```

**PublisherCatalog**:
```json
{
  "$schemaVersion": 1,
  "publisher": { ... },
  "content": [
    {
      "id", "name", "description", "contentType", "targetGame",
      "releases": [
        {
          "version", "releaseDate", "changelog",
          "artifacts": [ { "filename", "downloadUrl", "sha256", "sizeBytes" } ],
          "dependencies": [ { "publisherId", "contentId", "versionConstraint" } ]
        }
      ],
      "metadata": { "author", "bannerUrl", "screenshotUrls" },
      "tags": [],
      "extendsContentId": "base-mod"
    }
  ],
  "referrals": []
}
```

**ContentManifest**:
```json
{
  "id": "1.0.publisher.contentType.contentId",
  "name", "description", "version",
  "contentType", "targetGame",
  "publisher": { "name", "website", "supportUrl", "publisherType" },
  "files": [
    { "relativePath", "sourceType", "installTarget", "size", "hash", "downloadUrl" }
  ],
  "dependencies": [
    { "id", "name", "dependencyType", "installBehavior", "minVersion" }
  ],
  "metadata": { "description", "tags", "iconUrl", "screenshotUrls" }
}
```

---

## Implementation Status

### ✅ Fully Implemented

- **Publisher Studio**: Project management, content library, validation, export
- **Hosting Integration**: Google Drive OAuth, file upload, state persistence
- **Subscription System**: `genhub://` protocol, definition fetching, catalog parsing
- **Downloads UI**: Sidebar navigation, multi-catalog support, provider-specific filters
- **Content Pipeline**: Discovery, resolution, manifest generation, CAS storage
- **Dependency Resolution**: Same-catalog and cross-publisher dependencies
- **Game Profile Integration**: Content selection, workspace strategies, launch workflow

### 🟡 Partially Implemented

- **Artifact Upload**: UI exists, but no bulk upload or automated workflow
- **Referrals System**: Data model exists, but no UI for managing referrals
- **Variant Support**: Documented but not implemented in factories

### ❌ Planned/Not Implemented

- **Automated Publishing Pipeline**: One-click publish with progress tracking
- **Catalog Diff/Update Detection**: Show what changed since last publish
- **Catalog Signing**: Digital signatures for integrity verification
- **Analytics/Metrics**: Download counts, subscription tracking

---

## Design Patterns

1. **Factory Pattern**: Manifest factories for static providers (ModDB, CNCLabs)
2. **Builder Pattern**: `IContentManifestBuilder` for fluent manifest construction
3. **Strategy Pattern**: Workspace strategies (symlink, copy, hardlink)
4. **Observer Pattern**: Subscription updates and catalog refresh
5. **Repository Pattern**: `PublisherSubscriptionStore`, `ContentManifestPool`

---

## Key Architectural Decisions

### Why Decentralized?

**Pros**:
- No single point of failure
- Publishers control their own content
- No hosting costs for GenHub maintainers
- Resilient to censorship

**Cons**:
- Publishers must manage hosting
- No centralized moderation
- Dependency resolution complexity

### Why JSON Catalogs?

**Pros**:
- Human-readable and editable
- Version-controllable (Git)
- Easy to host anywhere (static files)
- No database required

**Cons**:
- Size limits (5 MB per catalog)
- No query capabilities
- Must fetch entire catalog

### Why 3-Tier Hosting?

**Pros**:
- URL stability (definition URL never changes)
- Publishers can migrate hosting
- Mirrors supported at each tier

**Cons**:
- More complex than single-tier
- Additional HTTP requests

### Why Content-Addressable Storage?

**Pros**:
- Deduplication (same file used by multiple mods stored once)
- Integrity verification (SHA256 hash)
- Immutability (files never modified)

**Cons**:
- Requires symlink support (or copy/hardlink fallback)
- More complex than direct file installation

---

## Related Documentation

- **Detailed Reports**:
  - `DOWNLOADS_UI_REPORT.md` - Downloads UI refactor and sidebar
  - `CONTENT_PIPELINE_REPORT.md` - Content pipeline deep dive
  - `ARCHITECTURE_DISCOVERY_REPORT.md` - Provider/publisher model

- **Existing Docs**:
  - `docs/features/content/provider-configuration.md` - Provider JSON schema
  - `docs/features/content/provider-infrastructure.md` - Provider architecture
  - `docs/features/content/content-dependencies.md` - Dependency system
  - `docs/features/tools/publisher-studio.md` - Publisher Studio guide
  - `publisher_studio_plan.md` - Original implementation plan

---

## File Reference

### Core Models
- `GenHub.Core/Models/Providers/PublisherDefinition.cs`
- `GenHub.Core/Models/Providers/PublisherCatalog.cs`
- `GenHub.Core/Models/Providers/CatalogEntry.cs`
- `GenHub.Core/Models/Providers/CatalogContentItem.cs`
- `GenHub.Core/Models/Providers/CatalogDependency.cs`
- `GenHub.Core/Models/Manifest/ContentManifest.cs`
- `GenHub.Core/Models/Manifest/ContentDependency.cs`

### Publisher Studio
- `GenHub/Features/Tools/ViewModels/PublisherStudioViewModel.cs`
- `GenHub/Features/Tools/ViewModels/ContentLibraryViewModel.cs`
- `GenHub/Features/Tools/ViewModels/PublishShareViewModel.cs`
- `GenHub/Features/Tools/Services/PublisherStudioService.cs`
- `GenHub/Features/Tools/Services/Hosting/HostingProviderFactory.cs`
- `GenHub/Features/Tools/Services/Hosting/GoogleDriveHostingProvider.cs`

### Content Pipeline
- `GenHub/Features/Content/Services/ContentDiscoverers/GenericCatalogDiscoverer.cs`
- `GenHub/Features/Content/Services/ContentResolvers/GenericCatalogResolver.cs`
- `GenHub/Features/Content/Services/Publishers/ModDBManifestFactory.cs`
- `GenHub/Features/Content/Services/Publishers/CNCLabsManifestFactory.cs`

### Downloads UI
- `GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs`
- `GenHub/Features/Content/ViewModels/ContentBrowserViewModel.cs`
- `GenHub/Features/Content/ViewModels/Catalog/SubscriptionConfirmationViewModel.cs`

### Services
- `GenHub.Core/Services/Publishers/PublisherDefinitionService.cs`
- `GenHub/Features/Content/Services/Catalog/CrossPublisherDependencyResolver.cs`

---

**End of Summary** - For detailed ASCII diagrams and technical deep-dives, see the comprehensive reports generated by the investigation agents.
