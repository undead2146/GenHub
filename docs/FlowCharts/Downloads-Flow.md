---
title: Downloads Flow
description: Complete user flow for downloading and installing content in GenHub
---

# Flowchart: Downloads User Flow

This flowchart details the complete user journey from browsing publishers to downloading and installing content.

## User Browsing Flow

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'primaryColor': '#e2e8f0',
    'primaryTextColor': '#1a202c',
    'primaryBorderColor': '#4a5568',
    'lineColor': '#2d3748',
    'background': '#ffffff'
  }
}}%%

flowchart TD
    subgraph User["👤 User Actions"]
        A["Open Downloads Tab"]
        B["Select Publisher<br/>(ModDB, CNC Labs, etc.)"]
        C["Browse/Search Content"]
        D["Click Content Card"]
        E["View Details"]
        F["Click Download"]
    end

    subgraph ViewModel["📱 DownloadsBrowserViewModel"]
        V1["LoadPublishersAsync()"]
        V2["SetSelectedPublisher()"]
        V3["DiscoverContentAsync()"]
        V4["OpenContentDetail()"]
        V5["DownloadContentCommand"]
    end

    subgraph Pipeline["🔧 Content Pipeline"]
        P1["IContentDiscoverer"]
        P2["ContentDiscoveryResult"]
        P3["IContentResolver"]
        P4["IContentManifestFactory"]
    end

    subgraph Storage["💾 Storage"]
        S1["CAS Service"]
        S2["Manifest Pool"]
        S3["Profile Integration"]
    end

    A --> V1
    V1 --> B
    B --> V2
    V2 --> V3
    V3 --> P1
    P1 --> P2
    P2 --> C
    C --> D
    D --> V4
    V4 --> E
    E --> F
    F --> V5
    V5 --> P3
    P3 --> P4
    P4 --> S1
    P4 --> S2
    S2 --> S3

    classDef user fill:#3182ce,stroke:#2c5282,stroke-width:2px,color:#ffffff
    classDef viewmodel fill:#805ad5,stroke:#6b46c1,stroke-width:2px,color:#ffffff
    classDef pipeline fill:#38a169,stroke:#2f855a,stroke-width:2px,color:#ffffff
    classDef storage fill:#e53e3e,stroke:#c53030,stroke-width:2px,color:#ffffff

    class A,B,C,D,E,F user
    class V1,V2,V3,V4,V5 viewmodel
    class P1,P2,P3,P4 pipeline
    class S1,S2,S3 storage
```

## Content Acquisition Flow

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'primaryColor': '#e2e8f0',
    'primaryTextColor': '#1a202c',
    'primaryBorderColor': '#4a5568',
    'lineColor': '#2d3748',
    'background': '#ffffff'
  }
}}%%

sequenceDiagram
    actor User
    participant UI as DownloadsBrowserView
    participant VM as DownloadsBrowserViewModel
    participant CO as ContentOrchestrator
    participant R as Resolver
    participant F as ManifestFactory
    participant MB as ContentManifestBuilder
    participant CAS as CAS Service
    participant Pool as ManifestPool
    participant Profile as GameProfile

    User->>UI: Click "Download"
    UI->>VM: DownloadContentCommand
    VM->>CO: AcquireContentAsync(searchResult)

    Note over CO: Resolve full details
    CO->>R: ResolveAsync(searchResult)
    R-->>CO: Full metadata + download URL

    Note over CO: Create manifest
    CO->>F: CreateManifestAsync(details)
    F->>MB: WithBasicInfo(), WithMetadata()...

    Note over MB: Download & process
    F->>MB: AddDownloadedFileAsync(url)
    MB->>MB: Download to temp
    MB->>MB: Detect archive type

    alt Is Archive (ZIP/RAR/7z)
        MB->>MB: Extract all files
        loop For each extracted file
            MB->>CAS: StoreContentAsync(file, hash)
            MB->>MB: Add ManifestFile entry
        end
        MB->>MB: Delete archive
    else Not Archive
        MB->>CAS: StoreContentAsync(file, hash)
        MB->>MB: Add single ManifestFile
    end

    MB-->>F: Builder with CAS refs
    F-->>CO: ContentManifest

    Note over CO: Store manifest
    CO->>Pool: AddManifest(manifest)
    Pool-->>CO: Success

    CO-->>VM: OperationResult.Success
    VM-->>UI: Update download status

    Note over User: Content available for profiles
    User->>Profile: Add to profile
```

## Publisher Selection

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'primaryColor': '#e2e8f0',
    'primaryTextColor': '#1a202c',
    'primaryBorderColor': '#4a5568',
    'lineColor': '#2d3748',
    'background': '#ffffff'
  }
}}%%

flowchart LR
    subgraph Sidebar["Publisher Sidebar"]
        P1["🎮 ModDB"]
        P2["🗺️ CNC Labs"]
        P3["🗺️ AOD Maps"]
        P4["🔧 Community Outpost"]
        P5["🐙 GitHub"]
        P6["🌐 Generals Online"]
    end

    subgraph Filter["Filter Panel"]
        F1["Content Type"]
        F2["Game (Generals/ZH)"]
        F3["Search Term"]
        F4["Sort Order"]
    end

    subgraph Grid["Content Grid"]
        G1["ContentCardView 1"]
        G2["ContentCardView 2"]
        G3["ContentCardView n..."]
    end

    P1 & P2 & P3 & P4 & P5 & P6 --> Filter
    Filter --> Grid
```

## Key Components

### DownloadsBrowserViewModel

**Location**: `GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs`

| Property/Command | Type | Purpose |
| :--- | :--- | :--- |
| `Publishers` | `ObservableCollection<PublisherCardViewModel>` | Available content sources |
| `SelectedPublisher` | `PublisherCardViewModel` | Currently selected publisher |
| `ContentItems` | `ObservableCollection<ContentGridItemViewModel>` | Discovered content |
| `FilterViewModel` | `IFilterPanelViewModel` | Publisher-specific filters |
| `DownloadContentCommand` | `IAsyncRelayCommand` | Initiates download |

### ContentGridItemViewModel

**Location**: `GenHub/Features/Downloads/ViewModels/ContentGridItemViewModel.cs`

Represents a single content item in the grid with:

- Title, description, preview image
- Publisher info and tags
- Download URL and content type
- Installation status tracking

### Filter ViewModels

Each publisher has a specialized filter ViewModel:

| Publisher | Filter ViewModel | Special Filters |
| :--- | :--- | :--- |
| ModDB | `ModDBFilterViewModel` | Category, release date |
| CNC Labs | `CNCLabsFilterViewModel` | Map size, player count |
| AOD Maps | `AODMapsFilterViewModel` | Map type |
| Community Outpost | `CommunityOutpostFilterViewModel` | Tool vs patch |
| GitHub | `GitHubFilterViewModel` | Repository, release type |

## Error Handling

```mermaid
flowchart TD
    D["Download Attempt"] --> N{Network OK?}
    N -->|No| E1["Show network error<br/>+ retry option"]
    N -->|Yes| A{Auth Required?}
    A -->|Yes| E2["Prompt for auth<br/>(ModDB WAF)"]
    A -->|No| DL["Download File"]
    DL --> V{Valid File?}
    V -->|No| E3["Show validation error"]
    V -->|Yes| EX{Extract OK?}
    EX -->|No| E4["Show extraction error<br/>fallback to single file"]
    EX -->|Yes| S["Store in CAS"]
    S --> M["Create Manifest"]
```

## Related Documentation

- [Content Pipeline](./content-pipeline.md) - Detailed pipeline architecture
- [Discovery Flow](../FlowCharts/Discovery-Flow.md) - Discovery process
- [Acquisition Flow](../FlowCharts/Acquisition-Flow.md) - Content acquisition
