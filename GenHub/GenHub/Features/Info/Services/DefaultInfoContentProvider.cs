using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Info;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Info;

namespace GenHub.Features.Info.Services;

/// <summary>
/// Default implementation of the info content provider, providing static but easily updatable content.
/// </summary>
public class DefaultInfoContentProvider : IInfoContentProvider
{
    private readonly List<InfoSection> _sections;

    /// <inheritdoc/>
    public Task<IEnumerable<InfoSection>> GetAllSectionsAsync()
    {
        return Task.FromResult(_sections.OrderBy(s => s.Order).AsEnumerable());
    }

    /// <inheritdoc/>
    public Task<InfoSection?> GetSectionAsync(string sectionId)
    {
        return Task.FromResult(_sections.FirstOrDefault(s => s.Id.Equals(sectionId, StringComparison.OrdinalIgnoreCase)));
    }

    private static InfoSection CreateProfileSettingsSection()
    {
        return new InfoSection
        {
            Id = "profile-settings",
            Title = "Profile Configuration",
            Description = "Deep dive into the powerful configuration options available for each profile.",
            IconKey = "Tune",
            Order = 1,
            Cards =
            [
                new InfoCard
                {
                    Title = "Independent Settings",
                    Content = "Each profile maintains its own completely isolated `Options.ini` configuration.",
                    Type = InfoCardType.Concept,
                    IconKey = "SourceCommit",
                    IsExpandable = true,
                    DetailedContent = "This allows you to have different resolutions, audio levels, and camera heights for different mods. You'll never have to manually swap `Options.ini` files again.",
                },
                new InfoCard
                {
                    Title = "Client Features",
                    Content = "Configure specialized features for community clients like GeneralsOnline and TheSuperHackers.",
                    Type = InfoCardType.Concept,
                    IconKey = "Creation",
                    IsExpandable = true,
                    DetailedContent = "Enable money-per-minute overlays, adjust font sizes, or toggle wide-screen fixes directly from the profile editor. These settings are injected into the game at launch.",
                },
                new InfoCard
                {
                    Title = "The Settings Editor",
                    Content = "Use the interactive demo above to explore the full range of configurable options.",
                    Type = InfoCardType.Tip,
                    IconKey = "GestureTap",
                    DetailedContent = "The editor supports live validation for resolution bounds and provides presets for common aspect ratios.",
                },
            ],
        };
    }

    private static InfoSection CreateGameProfilesSection()
    {
        return new InfoSection
        {
            Id = "game-profiles",
            Title = "Game Profiles",
            Description = "The heart of GenHub. Learn how to create and manage your own game instances.",
            IconKey = "GamepadVariant",
            Order = 0,
            Cards =
            [
                new InfoCard
                {
                    Title = "The Profile Concept",
                    Content = "A Game Profile is a virtual installation of Command & Conquer. It bundles a specific game version with any number of mods, patches, and custom settings.",
                    Type = InfoCardType.Concept,
                    IconKey = "InformationOutline",
                    IsExpandable = true,
                    DetailedContent = "Think of a profile as a 'Bookmark' that knows exactly how to build the game on the fly. When you launch a profile, GenHub prepares a temporary 'Workspace' with all selected files combined. This allows you to have multiple mods installed without them ever touching or breaking each other.",
                    Actions =
                    [
                        new InfoAction { Label = "Create Profile", ActionId = InfoActionConstants.NavigateToGameProfiles, IconKey = "Plus", IsPrimary = true },
                    ],
                },
                new InfoCard
                {
                    Title = "Game Client Selection",
                    Content = "GenHub supports official executables and community-enhanced clients.",
                    Type = InfoCardType.Concept,
                    IconKey = "Application",
                    IsExpandable = true,
                    DetailedContent = "• Base Game: The original 'generals.exe' from Steam or EA App.\n• GeneralsOnline: A modern client for online play with built-in networking fixes and frame rate options (30Hz/60Hz).\n• SuperHackers: Community binaries that fix widescreen issues and modern OS compatibility.\n\nTip: We recommend using GeneralsOnline for the best stability on Windows 10/11.",
                },
                new InfoCard
                {
                    Title = "Adding Content to Profiles",
                    Content = "Once a profile is created, you can enable specific mods, patches, or map packs from your library.",
                    Type = InfoCardType.HowTo,
                    IconKey = "PuzzleOutline",
                    IsExpandable = true,
                    DetailedContent = "1. Select a profile in the 'Game Profiles' tab.\n2. Click 'Edit Profile'.\n3. Browse the 'Content' section and check the items you want to include.\n4. GenHub will automatically resolve dependencies and order the files correctly for the game to load them.",
                },
                new InfoCard
                {
                    Title = "Performance & Quality Settings",
                    Content = "Profiles store their own video and audio settings, separate from your other installations.",
                    Type = InfoCardType.Tip,
                    IconKey = "SettingsOutline",
                    DetailedContent = "You can set 'Generals Evolution' to run at 4K resolution while keeping 'Vanilla Zero Hour' at 1080p for performance. Use the 'Settings' tab within the profile editor to fine-tune resolution, shadows, and client-specific features like 'Money per Minute' overlays.",
                },
            ],
        };
    }

    private static InfoSection CreateSteamIntegrationSection()
    {
        return new InfoSection
        {
            Id = "steam-integration",
            Title = "Steam & Game Platforms",
            Description = "How GenHub works seamlessly with Steam and the EA App.",
            IconKey = "Steam",
            Order = 2,
            Cards =
            [
                new InfoCard
                {
                    Title = "Non-Destructive Integration",
                    Content = "GenHub does NOT modify your official game installation files.",
                    Type = InfoCardType.Concept,
                    IconKey = "ShieldCheck",
                    IsExpandable = true,
                    DetailedContent = "Unlike traditional mod loaders that overwrite 'generals.exe' or 'INIZH.big', GenHub uses a 'Proxy Launcher'. Your original game files remain pristine and can be verified by Steam at any time without issues.",
                },
                new InfoCard
                {
                    Title = "The Proxy Launcher",
                    Content = "When you press 'Play' in Steam, you are actually launching GenHub's proxy.",
                    Type = InfoCardType.Concept,
                    IconKey = "RocketLaunch",
                    IsExpandable = true,
                    DetailedContent = "GenHub backs up the original executable to `.ghbak` and places a small `generals.exe` proxy in its place. This proxy reads `proxy_config.json` to know which GenHub profile is active, and then launches THAT profile's isolated workspace. This tricks Steam into tracking your hours and status while playing any mod!",
                },
                new InfoCard
                {
                    Title = "Workspace Isolation",
                    Content = "Each time you play, a temporary workspace is built using 'Junctions'.",
                    Type = InfoCardType.Concept,
                    IconKey = "FolderMultiple",
                    DetailedContent = "Junctions are like shortcuts that look like real files to the game. This allows us to compose a game folder from many different locations (base game + mod folder + patch folder) instantly, without copying gigabytes of data.",
                },
            ],
        };
    }

    private static InfoSection CreateModsAndMapsSection()
    {
        return new InfoSection
        {
            Id = "mods-maps",
            Title = "Mods & Maps",
            Description = "Managing your local content library.",
            IconKey = "MapMarkerRadius",
            Order = 3,
            Cards =
            [
                new InfoCard
                {
                    Title = "Local Content",
                    Content = "Import your own mods and maps using the 'Add Local Content' button.",
                    Type = InfoCardType.HowTo,
                    IconKey = "FolderUpload",
                    IsExpandable = true,
                    DetailedContent = "Got a mod from ModDB that isn't in our repo? No problem. Use the 'Add Local Content' feature in the Navigation bar to import a folder or ZIP file. GenHub will treat it just like any official download.",
                },
                new InfoCard
                {
                    Title = "Maps & Map Packs",
                    Content = "Maps are loaded per-profile, keeping your map list clean.",
                    Type = InfoCardType.Concept,
                    IconKey = "EarthBox",
                    IsExpandable = true,
                    DetailedContent = "Instead of dumping 500 maps into your Documents folder and slowing down the game menu, GenHub injects only the maps enabled for the active profile. You can also group maps into 'Map Packs' to organize them by tournament, style, or author.",
                },
                new InfoCard
                {
                    Title = "Automatic Clean-up",
                    Content = "When you close the game, GenHub cleans up.",
                    Type = InfoCardType.Tip,
                    IconKey = "Broom",
                    DetailedContent = "The temporary workspace and injected maps are removed when the game process ends. Your computer stays clean, and your next launch is fresh.",
                },
            ],
        };
    }

    private static InfoSection CreateToolsSection()
    {
        return new InfoSection
        {
            Id = "tools",
            Title = "Tools & Utilities",
            Description = "Setting up external tools like WorldBuilder and FinalBig.",
            IconKey = "Tools",
            Order = 4,
            Cards =
            [
                new InfoCard
                {
                    Title = "Tool Profiles",
                    Content = "You can create profiles for Tools, not just the Game.",
                    Type = InfoCardType.Concept,
                    IconKey = "ApplicationBraces",
                    IsExpandable = true,
                    DetailedContent = "Select 'Modding Tool' when creating a profile to set up WorldBuilder, FinalBig, or any other utility. These tools will run inside the context of a GenHub workspace, giving them access to the game files they need.",
                },
                new InfoCard
                {
                    Title = "WorldBuilder Setup",
                    Content = "Run WorldBuilder with specific mods loaded to edit mod maps.",
                    Type = InfoCardType.HowTo,
                    IconKey = "Terrain",
                    IsExpandable = true,
                    DetailedContent = "Create a profile for `WorldBuilder.exe` and enable the mod you want to map for (e.g., 'Rise of the Reds'). GenHub will ensure WorldBuilder sees all the mod's assets, preventing pink textures and missing object errors.",
                },
            ],
        };
    }

    private static InfoSection CreateSharingSection()
    {
        return new InfoSection
        {
            Id = "sharing",
            Title = "Sharing",
            Description = "Share your replays and battles with the community.",
            IconKey = "ShareVariant",
            Order = 5,
            Cards =
            [
                new InfoCard
                {
                    Title = "Replay Sharing",
                    Content = "Easily package and share your replays.",
                    Type = InfoCardType.Concept,
                    IconKey = "MovieOpen",
                    IsExpandable = true,
                    DetailedContent = "GenHub can zip up your latest replay with the associated `.map` file (if it's a custom map), ensuring the recipient has everything they need to watch it.",
                },
                new InfoCard
                {
                    Title = "Supported Platforms",
                    Content = "Direct integrations with community pillars.",
                    Type = InfoCardType.Concept,
                    IconKey = "Web",
                    IsExpandable = true,
                    DetailedContent = "We support direct upload/download for:\n• GenTool (Replay parsing)\n• GeneralsOnline (Ladder matches)\n• UploadThing (General file sharing)",
                },
            ],
        };
    }

    private static InfoSection CreateContentSystemSection()
    {
        return new InfoSection
        {
            Id = "content-system",
            Title = "Content System",
            Description = "How GenHub finds, downloads, and validates community content.",
            IconKey = "CloudDownloadOutline",
            Order = 6,
            Cards =
            [
                new InfoCard
                {
                    Title = "The Three-Tier Pipeline",
                    Content = "Discovery ➔ Acquisition ➔ Delivery. Every piece of content follows this strict path.",
                    Type = InfoCardType.Concept,
                    IconKey = "Pipe",
                    IsExpandable = true,
                    DetailedContent = "• Discovery: GenHub scans GitHub, ModDB, and CNC Labs for new versions.\n• Acquisition: Files are securely downloaded and hashed for integrity.\n• Delivery: Content is organized into the library, ready to be used by any profile.\n\nThis ensures you always have the latest versions and that they are safe to run.",
                    Actions =
                    [
                        new InfoAction { Label = "Browse Content", ActionId = InfoActionConstants.NavigateToDownloads, IconKey = "Compass", IsPrimary = true },
                    ],
                },
                new InfoCard
                {
                    Title = "Content Manifests",
                    Content = "Every mod in GenHub has a 'JSON Manifest' that describes its contents and requirements.",
                    Type = InfoCardType.Concept,
                    IconKey = "FileDocumentOutline",
                    IsExpandable = true,
                    DetailedContent = "Manifests tell GenHub which files are needed, where to find them, and if they depend on other mods. This metadata allows GenHub to handle complex installations automatically, including patches that require specific versions of a mod.",
                },
                new InfoCard
                {
                    Title = "Download Management",
                    Content = "Manage active downloads and see real-time progress for your mods.",
                    Type = InfoCardType.HowTo,
                    IconKey = "DownloadNetworkOutline",
                    Actions =
                    [
                        new InfoAction { Label = "Open Downloads", ActionId = InfoActionConstants.NavigateToDownloads, IconKey = "Download" },
                    ],
                },
            ],
        };
    }

    private static InfoSection CreateWorkspaceSection()
    {
        return new InfoSection
        {
            Id = "workspaces",
            Title = "Workspace Strategies",
            Description = "Advanced information on how game environments are built on disk.",
            IconKey = "Harddisk",
            Order = 7,
            Cards =
            [
                new InfoCard
                {
                    Title = "Isolation via Virtualization",
                    Content = "GenHub creates a 'shadow' copy of your game folders for each profile, so your original game files are NEVER modified.",
                    Type = InfoCardType.Concept,
                    IconKey = "ShieldCheckOutline",
                    IsExpandable = true,
                    DetailedContent = "This is done using 'Workspace Strategies'. Instead of copying 5GB of game data for every mod, GenHub uses advanced file system techniques to 'point' to the existing files.",
                },
                new InfoCard
                {
                    Title = "Strategy Comparison Matrix",
                    Content = "Choose the best strategy for your system and drive configuration.",
                    Type = InfoCardType.Example,
                    IconKey = "TableLarge",
                    IsExpandable = true,
                    DetailedContent = "• Symlink (Default): Best performance, 0 extra disk space. Requires admin rights or Developer Mode.\n• Full Copy: Most compatible, uses 2-5GB per profile. Best for external drives.\n• Hard Link: Efficient, but only works if the workspace is on the same drive as the game files.\n• Hybrid: Copies critical small files and links large assets. Good balance.",
                },
                new InfoCard
                {
                    Title = "Troubleshooting Workspaces",
                    Content = "If a game fails to launch or mods don't appear, try clearing the workspace.",
                    Type = InfoCardType.Warning,
                    IconKey = "AlertCircleOutline",
                    DetailedContent = "GenHub can re-build a workspace in seconds. If files become corrupted or locked, use the 'Clear Workspace' button in the profile settings to force a fresh assembly.",
                },
            ],
        };
    }

    private static InfoSection CreateUserDataSection()
    {
        return new InfoSection
        {
            Id = "user-data",
            Title = "User Data & Saves",
            Description = "How GenHub handles your replays, maps, and save games.",
            IconKey = "AccountDetailsOutline",
            Order = 8,
            Cards =
            [
                new InfoCard
                {
                    Title = "Isolated User Data",
                    Content = "Just like game files, your user data (saves, screenshots) is isolated per profile.",
                    Type = InfoCardType.Concept,
                    IconKey = "FolderAccountOutline",
                    DetailedContent = "This prevents 'Vanilla' Zero Hour from seeing your 'Evolution' save games, which would likely cause crashes. Each profile has its own 'Command and Conquer Generals Data' folder.",
                },
                new InfoCard
                {
                    Title = "Global vs. Local Data",
                    Content = "Some data can be shared across all profiles if desired.",
                    Type = InfoCardType.Tip,
                    IconKey = "Earth",
                    DetailedContent = "By default, GenHub keeps everything separate for safety. Advanced users can modify the 'UserData' settings in a profile to point to a shared location for maps or replays.",
                },
            ],
        };
    }

    private static InfoSection CreateAppUpdatesSection()
    {
        return new InfoSection
        {
            Id = "app-updates",
            Title = "App Updates",
            Description = "Stay up to date with the latest features and fixes.",
            IconKey = "Update",
            Order = 9,
            Cards =
            [
                new InfoCard
                {
                    Title = "Auto-Updates",
                    Content = "GenHub checks for updates automatically on startup.",
                    Type = InfoCardType.Concept,
                    IconKey = "Sync",
                    IsExpandable = true,
                    DetailedContent = "We use Velopack to deliver delta updates, meaning downloads are tiny and fast. You'll see a notification (like the demo above) when a new version is ready.",
                },
                new InfoCard
                {
                    Title = "Release Channels",
                    Content = "You can subscribe to Pull Requests or specific branches to test bleeding-edge features.",
                    Type = InfoCardType.HowTo,
                    IconKey = "SourceBranch",
                    IsExpandable = true,
                    DetailedContent = "1. Click 'Check for Updates' in the bottom left.\n2. Switch to the 'Browse Builds' tab.\n3. Expand 'Open Pull Requests' to see what's being worked on.\n4. Click 'Subscribe' to switch your installation to that version.",
                    Actions =
                    [
                        new InfoAction { Label = "Check Updates", ActionId = InfoActionConstants.NavigateToSettings, IconKey = "Update" },
                    ],
                },
            ],
        };
    }

    private static InfoSection CreateChangelogsSection()
    {
        return new InfoSection
        {
            Id = "changelogs",
            Title = "Changelogs",
            Description = "View recent updates and release notes.",
            IconKey = "History",
            Order = 10,
            Cards = [],
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultInfoContentProvider"/> class.
    /// </summary>
    public DefaultInfoContentProvider()
    {
        _sections = CreateContent();
    }


    private List<InfoSection> CreateContent()
    {
        return
        [
            CreateGameProfilesSection(),
            CreateProfileSettingsSection(),
            CreateSteamIntegrationSection(),
            CreateModsAndMapsSection(),
            CreateContentSystemSection(),
            CreateWorkspaceSection(),
            CreateUserDataSection(),
            CreateToolsSection(),
            CreateSharingSection(),
            CreateAppUpdatesSection(),
            CreateChangelogsSection(),
        ];
    }
}
