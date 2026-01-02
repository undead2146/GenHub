# Map Manager

The Map Manager is a built-in tool in GenHub that allows you to manage, import, and share your Command & Conquer: Generals and Zero Hour custom maps with ease. It also features MapPacks for organizing maps into collections.

## Features

- **Unified View**: See all your maps for both Generals and Zero Hour in one place.
- **Easy Import**: Import maps directly from URLs or by dragging and dropping files.
- **Cloud Sharing**: Share your custom maps instantly via UploadThing.
- **Local Export**: Bundle multiple maps into a ZIP archive for local storage or manual sharing.
- **MapPacks**: Create named collections of maps for easy organization and profile management.

## Getting Started

To access the Map Manager:
1. Open GenHub.
2. Navigate to the **TOOLS** tab.
3. Select **Map Manager** from the sidebar.

## Importing Maps

### From URL
You can import maps from various sources by pasting the link into the import bar:
- **UploadThing**: Direct links from other GenHub users.
- **Direct Links**: Any URL ending in `.map` or `.zip`.

### Drag and Drop
Simply drag one or more `.map` or `.zip` files from your computer and drop them anywhere on the Map Manager window to import them.

## Sharing Maps

1. Select the maps you want to share from the list.
2. Click **Upload & Share**.
3. Once the upload is complete, the download link will be copied to your clipboard automatically.

> [!IMPORTANT]
> **Size Limit**: Each individual map file must be under **5 MB**.
> **Privacy**: Shared maps are maintained for up to 14 days or until storage is full.

## MapPacks

MapPacks allow you to organize maps into named collections. This is especially useful for managing different map sets for different profiles or game modes.

### Creating a MapPack

1. Select the maps you want to include in the pack.
2. Click the **📦 MapPacks** button.
3. Enter a name and optional description.
4. Click **Create MapPack**.

### Using MapPacks

MapPacks are stored as metadata and integrate with GenHub's userdata system. When you load a MapPack for a profile, the maps will be automatically activated by the userdata system when that profile is launched.

> [!NOTE]
> **Integration with Profiles**: MapPacks work seamlessly with GenHub's profile system. Maps from loaded MapPacks are managed by the userdata service, which handles file linking and cleanup automatically.

## Architecture

The Map Manager is built on a modular service architecture:

- **`IMapDirectoryService`**: Manages map directory operations and file system access.
- **`IMapImportService`**: Handles importing maps from URLs, local files, and ZIP archives.
- **`IMapExportService`**: Manages exporting and cloud sharing via UploadThing.
- **`IMapPackService`**: Manages MapPack creation, loading, and storage.

## Map Storage

Maps imported or shared via GenHub are stored in:
- **Generals**: `Documents\Command and Conquer Generals Data\Maps`
- **Zero Hour**: `Documents\Command and Conquer Generals Zero Hour Data\Maps`

These are the standard game directories, ensuring compatibility with the game and other tools.
