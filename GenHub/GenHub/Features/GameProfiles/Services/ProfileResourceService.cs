using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;  
using Avalonia.Platform;  
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.GameProfiles.Services
{
    /// <summary>
    /// Service for managing profile resources like icons and covers
    /// </summary>
    public class ProfileResourceService
    {
        private readonly ILogger<ProfileResourceService> _logger;
        private readonly List<ProfileResourceItem> _icons = new();
        private readonly List<ProfileResourceItem> _covers = new();
        
        // Default resource paths - Using standard /Assets paths for consistency with correct capitalization
        private const string DefaultIconPath = "/Assets/Icons/genhub-logo.png";
        private const string GeneralsIconPath = "/Assets/Icons/generals-icon.png";
        private const string ZeroHourIconPath = "/Assets/Icons/zerohour-icon.png";
        private const string GenHubIconPath = "/Assets/Icons/genhub-logo.png";
        
        private const string DefaultCoverPath = "/Assets/Covers/generals-cover-2.png";
        private const string GeneralsCoverPath = "/Assets/Covers/generals-cover.png";
        private const string GeneralsCover2Path = "/Assets/Covers/generals-cover-2.png";
        private const string ZeroHourCoverPath = "/Assets/Covers/zerohour-cover.png";
        
        // Storage paths
        private readonly string _customIconsPath;
        private readonly string _customCoversPath;
        private readonly string _appAssetsPath;
        
        // Track initialization state
        private bool _initialized = false;
        private object _initLock = new object();
        
        public ProfileResourceService(ILogger<ProfileResourceService> logger)
        {
            _logger = logger;
            
            // Initialize storage directories
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GenHub"
            );
            
            // Get the executable location for asset fallbacks
            string? exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _appAssetsPath = exeDir != null ? Path.Combine(exeDir, "Assets") : string.Empty;
            
            _customIconsPath = Path.Combine(appDataPath, "Assets", "CustomIcons");
            _customCoversPath = Path.Combine(appDataPath, "Assets", "CustomCovers");
            
            // Create directories if they don't exist
            Directory.CreateDirectory(_customIconsPath);
            Directory.CreateDirectory(_customCoversPath);
            
            _logger.LogInformation("ProfileResourceService initialized with custom paths: Icons={IconsPath}, Covers={CoversPath}, AppAssets={AppAssets}",
                _customIconsPath, _customCoversPath, _appAssetsPath);
                
            // Initialize resources
            EnsureInitialized();
        }
        
        /// <summary>
        /// Ensures resources are initialized (thread-safe)
        /// </summary>
        protected virtual void EnsureInitialized()
        {
            if (!_initialized)
            {
                lock (_initLock)
                {
                    if (!_initialized)
                    {
                        // Initialize all built-in resources first
                        InitializeBuiltInResources();
                        
                        // Then scan for additional resources
                        ScanForResources();
                        
                        _initialized = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Initializes built-in resources with proper path construction
        /// </summary>
        protected virtual void InitializeBuiltInResources()
        {
            try
            {
                _logger.LogDebug("Initializing built-in resources");
                
                // Icons - Using consistent paths for all built-in resources
                AddBuiltInIcon("default", DefaultIconPath, "Default Icon", "Default");
                AddBuiltInIcon("generals", GeneralsIconPath, "Generals", "Generals");
                AddBuiltInIcon("zerohour", ZeroHourIconPath, "Zero Hour", "Zero Hour");
                AddBuiltInIcon("genhub", GenHubIconPath, "GenHub", "Default");
                
                // Covers
                AddBuiltInCover("default", DefaultCoverPath, "Default Cover", "Default");
                AddBuiltInCover("generals", GeneralsCoverPath, "Generals", "Generals");
                AddBuiltInCover("generals-2", GeneralsCover2Path, "Generals Alt", "Generals");
                AddBuiltInCover("zerohour", ZeroHourCoverPath, "Zero Hour", "Zero Hour");
                
                _logger.LogInformation("Initialized built-in resources: {IconCount} icons, {CoverCount} covers", 
                    _icons.Count, _covers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing built-in resources");
            }
        }
        
        /// <summary>
        /// Adds a built-in icon with proper path resolution
        /// </summary>
        private void AddBuiltInIcon(string id, string relativePath, string displayName, string gameType)
        {
            try
            {
                string normalizedPath = NormalizePath(relativePath);
                
                _icons.Add(new ProfileResourceItem 
                { 
                    Id = id,
                    Path = normalizedPath,
                    DisplayName = displayName,
                    IsBuiltIn = true,
                    GameType = gameType
                });
                
                _logger.LogDebug("Added built-in icon: {Id}, {Path}, {DisplayName}", id, normalizedPath, displayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add built-in icon: {Id}, {Path}", id, relativePath);
            }
        }
        
        /// <summary>
        /// Adds a built-in cover with proper path resolution
        /// </summary>
        private void AddBuiltInCover(string id, string relativePath, string displayName, string gameType)
        {
            try
            {
                string normalizedPath = NormalizePath(relativePath);
                
                _covers.Add(new ProfileResourceItem 
                { 
                    Id = id,
                    Path = normalizedPath,
                    DisplayName = displayName,
                    IsBuiltIn = true,
                    GameType = gameType
                });
                
                _logger.LogDebug("Added built-in cover: {Id}, {Path}, {DisplayName}", id, normalizedPath, displayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add built-in cover: {Id}, {Path}", id, relativePath);
            }
        }
        
        /// <summary>
        /// Normalizes a path to ensure it works with Avalonia's resource system
        /// </summary>
        protected virtual string NormalizePath(string path)
        {
            // Skip if it's already an avalonia resource URI
            if (path.StartsWith("avares://"))
                return path;
                
            // Ensure path starts with / for consistent processing
            if (!path.StartsWith("/"))
                path = "/" + path;
                
            // Return in the format that Avalonia's StringToImageConverter expects
            return path;
        }
        
        /// <summary>
        /// Scans for all available resources in both embedded and file system locations
        /// </summary>
        protected virtual void ScanForResources()
        {
            try
            {
                _logger.LogDebug("Scanning for additional resources");
                
                // Scan for resources in the Assets directory - both Icons and Covers
                ScanEmbeddedResources();
                
                // Scan for files in the custom directories
                ScanFileSystemResources();
                
                _logger.LogInformation("Resource scanning complete: {IconCount} icons, {CoverCount} covers", 
                    _icons.Count, _covers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for resources");
            }
        }
        
        /// <summary>
        /// Scans for embedded resources using AssetLoader
        /// </summary>
        protected virtual void ScanEmbeddedResources()
        {
            try
            {
                _logger.LogDebug("Scanning embedded resources");
                
                // Scan for icon resources
                ScanResourceDirectory("Assets/Icons", true);
                
                // Scan for cover resources
                ScanResourceDirectory("Assets/Covers", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning embedded resources: {Message}", ex.Message);
            }
        }
        
        /// <summary>
        /// Scans a specific resource directory using AssetLoader
        /// </summary>
        private void ScanResourceDirectory(string directoryPath, bool isIcon)
        {
            try
            {
                _logger.LogDebug("Scanning resource directory: {Directory}", directoryPath);
                
                // Create safe empty list for assets
                var assetPaths = new List<string>();
                
                try
                {
                    var testUri = new Uri($"avares://GenHub/{directoryPath}");
                    if (!AssetLoader.Exists(testUri))
                    {
                        _logger.LogWarning("Resource directory does not exist: {Directory}", directoryPath);
                        return;
                    }
                    
                    var assets = AssetLoader.GetAssets(testUri, null);
                    
                    // Extract just the path strings from the assets to avoid using IAssetDescriptor
                    foreach (var asset in assets)
                    {
                        try
                        {
                            // Use reflection to extract the path since we can't access IAssetDescriptor directly
                            if (asset != null)
                            {
                                var absolutePath = asset.GetType().GetProperty("AbsolutePath")?.GetValue(asset) as string;
                                if (!string.IsNullOrEmpty(absolutePath))
                                {
                                    assetPaths.Add(absolutePath);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error extracting asset path");
                        }
                    }
                    
                    _logger.LogDebug("Found {Count} assets in {Directory}", assetPaths.Count, directoryPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accessing assets in directory {Directory}", directoryPath);
                    return;
                }
                
                // Proceed with loading resources from the found asset paths
                foreach (var assetPath in assetPaths)
                {
                    try
                    {
                        // Extract just the filename from the path
                        string fileName = Path.GetFileName(assetPath);
                        
                        if (!IsImageFile(fileName))
                            continue;
                            
                        // Create a normalized path that works with StringToImageConverter
                        string normalizedPath = "/" + assetPath.TrimStart('/');
                        
                        // Extract ID and display name
                        string id = Path.GetFileNameWithoutExtension(fileName);
                        
                        // This prevents the built-in resources from being added twice
                        if (isIcon && (_icons.Any(i => i.Id == id) || _icons.Any(i => i.Path == normalizedPath)))
                            continue;
                            
                        if (!isIcon && (_covers.Any(c => c.Id == id) || _covers.Any(c => c.Path == normalizedPath)))
                            continue;
                            
                        // Add the resource to the appropriate collection
                        string displayName = FormatDisplayName(id);
                        string gameType = DetermineGameType(id);
                        
                        if (isIcon)
                        {
                            _icons.Add(new ProfileResourceItem
                            {
                                Id = id,
                                Path = normalizedPath,
                                DisplayName = displayName,
                                IsBuiltIn = true,
                                GameType = gameType
                            });
                            _logger.LogDebug("Added embedded icon: {Id}, {Path}", id, normalizedPath);
                        }
                        else
                        {
                            _covers.Add(new ProfileResourceItem
                            {
                                Id = id,
                                Path = normalizedPath,
                                DisplayName = displayName,
                                IsBuiltIn = true,
                                GameType = gameType
                            });
                            _logger.LogDebug("Added embedded cover: {Id}, {Path}", id, normalizedPath);
                        }
                    }
                    catch (Exception assetEx)
                    {
                        _logger.LogError(assetEx, "Error processing asset {Path}", assetPath);
                        // Continue with next asset
                    }
                }
                
                // Add default resources if none were found
                if (isIcon && !_icons.Any())
                {
                    _logger.LogWarning("No icons found in {Directory}, adding fallback icon", directoryPath);
                    AddBuiltInIcon("default", DefaultIconPath, "Default Icon", "Default");
                }
                
                if (!isIcon && !_covers.Any())
                {
                    _logger.LogWarning("No covers found in {Directory}, adding fallback cover", directoryPath);
                    AddBuiltInCover("default", DefaultCoverPath, "Default Cover", "Default");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning resource directory {Directory}: {Message}", directoryPath, ex.Message);
            }
        }
        
        /// <summary>
        /// Scans for file-system resources in the custom directories
        /// </summary>
        protected virtual void ScanFileSystemResources()
        {
            try
            {
                _logger.LogDebug("Scanning file system resources");
                
                // Scan custom icons directory
                if (Directory.Exists(_customIconsPath))
                {
                    foreach (var filePath in Directory.GetFiles(_customIconsPath))
                    {
                        // Only add image files
                        if (IsImageFile(filePath))
                        {
                            string fileName = Path.GetFileName(filePath);
                            string id = Path.GetFileNameWithoutExtension(fileName);
                            
                            // Skip if we already have this ID
                            if (_icons.Any(i => i.Id == id))
                                continue;
                                
                            _icons.Add(new ProfileResourceItem
                            {
                                Id = id,
                                Path = filePath,
                                DisplayName = $"Custom - {FormatDisplayName(id)}",
                                IsBuiltIn = false,
                                GameType = "Custom"
                            });
                            _logger.LogDebug("Added custom icon: {Id}, {Path}", id, filePath);
                        }
                    }
                }
                
                // Scan custom covers directory
                if (Directory.Exists(_customCoversPath))
                {
                    foreach (var filePath in Directory.GetFiles(_customCoversPath))
                    {
                        // Only add image files
                        if (IsImageFile(filePath))
                        {
                            string fileName = Path.GetFileName(filePath);
                            string id = Path.GetFileNameWithoutExtension(fileName);
                            
                            // Skip if we already have this ID
                            if (_covers.Any(c => c.Id == id))
                                continue;
                                
                            _covers.Add(new ProfileResourceItem
                            {
                                Id = id,
                                Path = filePath,
                                DisplayName = $"Custom - {FormatDisplayName(id)}",
                                IsBuiltIn = false,
                                GameType = "Custom"
                            });
                            _logger.LogDebug("Added custom cover: {Id}, {Path}", id, filePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning file system resources");
            }
        }
        
        /// <summary>
        /// Checks if a file is an image based on extension
        /// </summary>
        protected virtual bool IsImageFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".ico" || ext == ".bmp";
        }
        
        /// <summary>
        /// Formats a display name from an ID (e.g. "generals-icon" -> "Generals Icon")
        /// </summary>
        protected virtual string FormatDisplayName(string id)
        {
            // Handle special cases
            if (string.IsNullOrEmpty(id))
                return "Unknown";
                
            if (id.Equals("genhub-logo", StringComparison.OrdinalIgnoreCase) || 
                id.Equals("genhub-icon", StringComparison.OrdinalIgnoreCase))
                return "GenHub";
                
            if (id.Contains("generals-cover-2", StringComparison.OrdinalIgnoreCase))
                return "Generals Alt";
                
            // Standard formatting: Convert kebab-case to Title Case
            var parts = id.Split('-', '_', ' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
                }
            }
            return string.Join(" ", parts);
        }
        
        /// <summary>
        /// Determines game type from a resource ID
        /// </summary>
        protected virtual string DetermineGameType(string id)
        {
            if (id.Contains("zerohour", StringComparison.OrdinalIgnoreCase) || 
                id.Contains("zero-hour", StringComparison.OrdinalIgnoreCase))
                return "Zero Hour";
                
            if (id.Contains("generals", StringComparison.OrdinalIgnoreCase))
                return "Generals";
                
            if (id.Contains("genhub", StringComparison.OrdinalIgnoreCase))
                return "Default";
                
            return "Default";
        }
        
        /// <summary>
        /// Gets all available profile icons
        /// </summary>
        public List<ProfileResourceItem> GetAvailableIcons()
        {
            // Ensure resources are initialized
            EnsureInitialized();
            
            try
            {
                // Return a deep copy to avoid external modification
                return _icons.Select(icon => new ProfileResourceItem
                {
                    Id = icon.Id,
                    Path = icon.Path,
                    DisplayName = icon.DisplayName,
                    IsBuiltIn = icon.IsBuiltIn,
                    GameType = icon.GameType
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available icons");
                return new List<ProfileResourceItem>
                {
                    new ProfileResourceItem { 
                        Id = "default", 
                        DisplayName = "Default", 
                        Path = NormalizePath(DefaultIconPath),
                        IsBuiltIn = true,
                        GameType = "Default"
                    }
                };
            }
        }
        
        /// <summary>
        /// Gets all available cover images
        /// </summary>
        public List<ProfileResourceItem> GetAvailableCovers()
        {
            // Ensure resources are initialized
            EnsureInitialized();
            
            try
            {
                // Return a deep copy to avoid external modification
                return _covers.Select(cover => new ProfileResourceItem
                {
                    Id = cover.Id,
                    Path = cover.Path,
                    DisplayName = cover.DisplayName,
                    IsBuiltIn = cover.IsBuiltIn,
                    GameType = cover.GameType
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available covers");
                return new List<ProfileResourceItem>
                {
                    new ProfileResourceItem { 
                        Id = "default", 
                        DisplayName = "Default", 
                        Path = NormalizePath(DefaultCoverPath),
                        IsBuiltIn = true,
                        GameType = "Default"
                    }
                };
            }
        }
        
        /// <summary>
        /// Finds an appropriate icon for the given game type
        /// </summary>
        public ProfileResourceItem FindIconForGameType(string gameType)
        {
            // Ensure resources are initialized
            EnsureInitialized();
            
            if (string.IsNullOrEmpty(gameType)) 
                return GetDefaultIcon();
            
            var allIcons = GetAvailableIcons();
            
            // Case-insensitive check for Zero Hour
            if (gameType.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Finding icon for Zero Hour game type");
                return allIcons.FirstOrDefault(i => i.GameType == "Zero Hour") ?? 
                       allIcons.FirstOrDefault(i => i.Id.Contains("zerohour", StringComparison.OrdinalIgnoreCase)) ??
                       GetDefaultIcon();
            }
            // For Generals (default)
            else
            {
                _logger.LogDebug("Finding icon for Generals game type");
                return allIcons.FirstOrDefault(i => i.GameType == "Generals" && 
                                                  !i.DisplayName.Contains("Zero", StringComparison.OrdinalIgnoreCase)) ??
                       allIcons.FirstOrDefault(i => i.Id.Contains("generals", StringComparison.OrdinalIgnoreCase) &&
                                                  !i.Id.Contains("zero", StringComparison.OrdinalIgnoreCase)) ??
                       GetDefaultIcon();
            }
        }
        
        /// <summary>
        /// Gets the default icon (fallback)
        /// </summary>
        private ProfileResourceItem GetDefaultIcon()
        {
            // If we have icons, return the first one tagged as Default
            var defaultIcon = _icons.FirstOrDefault(i => i.GameType == "Default") ?? 
                              _icons.FirstOrDefault(i => i.Id == "default") ??
                              _icons.FirstOrDefault();
                              
            // If we somehow have no icons, create a fallback
            if (defaultIcon == null)
            {
                defaultIcon = new ProfileResourceItem 
                {
                    Id = "default",
                    Path = NormalizePath(DefaultIconPath),
                    DisplayName = "Default Icon",
                    IsBuiltIn = true,
                    GameType = "Default"
                };
                _icons.Add(defaultIcon);
            }
            
            return defaultIcon;
        }
        
        /// <summary>
        /// Finds an appropriate cover for the given game type
        /// </summary>
        public ProfileResourceItem FindCoverForGameType(string gameType)
        {
            // Ensure resources are initialized
            EnsureInitialized();
            
            if (string.IsNullOrEmpty(gameType)) 
                return GetDefaultCover();
            
            var allCovers = GetAvailableCovers();
            
            // Case-insensitive check for Zero Hour
            if (gameType.Contains("Zero Hour", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Finding cover for Zero Hour game type");
                return allCovers.FirstOrDefault(c => c.GameType == "Zero Hour") ??
                       allCovers.FirstOrDefault(c => c.Id.Contains("zerohour", StringComparison.OrdinalIgnoreCase)) ??
                       GetDefaultCover();
            }
            // For Generals (default)
            else
            {
                _logger.LogDebug("Finding cover for Generals game type");
                return allCovers.FirstOrDefault(c => c.GameType == "Generals" && 
                                                   !c.DisplayName.Contains("Zero", StringComparison.OrdinalIgnoreCase)) ??
                       allCovers.FirstOrDefault(c => c.Id.Contains("generals", StringComparison.OrdinalIgnoreCase) &&
                                                   !c.Id.Contains("zero", StringComparison.OrdinalIgnoreCase)) ??
                       GetDefaultCover();
            }
        }
        
        /// <summary>
        /// Gets the default cover (fallback)
        /// </summary>
        private ProfileResourceItem GetDefaultCover()
        {
            // If we have covers, return the first one tagged as Default
            var defaultCover = _covers.FirstOrDefault(c => c.GameType == "Default") ?? 
                               _covers.FirstOrDefault(c => c.Id == "default") ??
                               _covers.FirstOrDefault();
                               
            // If we somehow have no covers, create a fallback
            if (defaultCover == null)
            {
                defaultCover = new ProfileResourceItem 
                {
                    Id = "default",
                    Path = NormalizePath(DefaultCoverPath),
                    DisplayName = "Default Cover",
                    IsBuiltIn = true,
                    GameType = "Default"
                };
                _covers.Add(defaultCover);
            }
            
            return defaultCover;
        }
        
        // For backward compatibility - transitional methods that convert domain models to view models
        
        /// <summary>
        /// Gets all available icons as view models (for backward compatibility)
        /// </summary>
        public List<ProfileIconViewModel> GetAvailableIconViewModels()
        {
            return GetAvailableIcons()
                .Select(item => new ProfileIconViewModel { 
                    DisplayName = item.DisplayName,
                    Path = item.Path 
                })
                .ToList();
        }
        
        /// <summary>
        /// Gets all available covers as view models (for backward compatibility)
        /// </summary>
        public List<ProfileIconViewModel> GetAvailableCoverViewModels()
        {
            return GetAvailableCovers()
                .Select(item => new ProfileIconViewModel { 
                    DisplayName = item.DisplayName,
                    Path = item.Path 
                })
                .ToList();
        }
        
        /// <summary>
        /// Finds icon for game type and returns as view model (for backward compatibility)
        /// </summary>
        public ProfileIconViewModel FindIconViewModelForGameType(string gameType) 
        {
            var resourceItem = FindIconForGameType(gameType);
            
            return new ProfileIconViewModel {
                DisplayName = resourceItem.DisplayName,
                Path = resourceItem.Path
            };
        }
        
        /// <summary>
        /// Finds cover for game type and returns as view model (for backward compatibility)
        /// </summary>
        public ProfileIconViewModel FindCoverViewModelForGameType(string gameType)
        {
            var resourceItem = FindCoverForGameType(gameType);
            
            return new ProfileIconViewModel {
                DisplayName = resourceItem.DisplayName,
                Path = resourceItem.Path
            };
        }
        
        /// <summary>
        /// Gets an appropriate color for the given game type
        /// </summary>
        public string GetColorForGameType(string gameType)
        {
            // Use centralized theme color logic
            return ProfileThemeColor.GetColorForGameType(gameType) ?? "#2A2A2A";
        }
        
        /// <summary>
        /// Adds an icon from a file to the custom icons directory
        /// </summary>
        public virtual string AddIconFromFile(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string targetPath = Path.Combine(_customIconsPath, fileName);
                
                // Copy the file
                File.Copy(filePath, targetPath, true);
                
                // Add to our collection
                string id = Path.GetFileNameWithoutExtension(fileName);
                
                // Remove any existing item with this ID
                _icons.RemoveAll(i => i.Id == id);
                
                // Add the new item
                _icons.Add(new ProfileResourceItem
                {
                    Id = id,
                    Path = targetPath,
                    DisplayName = $"Custom - {FormatDisplayName(id)}",
                    IsBuiltIn = false,
                    GameType = "Custom"
                });
                
                _logger.LogInformation("Added custom icon: {SourcePath} -> {TargetPath}", filePath, targetPath);
                return targetPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding custom icon: {Path}", filePath);
                return NormalizePath(DefaultIconPath); // Return default on error
            }
        }
        
        /// <summary>
        /// Adds a cover from a file to the custom covers directory
        /// </summary>
        public virtual string AddCoverFromFile(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string targetPath = Path.Combine(_customCoversPath, fileName);
                
                // Copy the file
                File.Copy(filePath, targetPath, true);
                
                // Add to our collection
                string id = Path.GetFileNameWithoutExtension(fileName);
                
                // Remove any existing item with this ID
                _covers.RemoveAll(c => c.Id == id);
                
                // Add the new item
                _covers.Add(new ProfileResourceItem
                {
                    Id = id,
                    Path = targetPath,
                    DisplayName = $"Custom - {FormatDisplayName(id)}",
                    IsBuiltIn = false,
                    GameType = "Custom"
                });
                
                _logger.LogInformation("Added custom cover: {SourcePath} -> {TargetPath}", filePath, targetPath);
                return targetPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding custom cover: {Path}", filePath);
                return NormalizePath(DefaultCoverPath); // Return default on error
            }
        }
        
        /// <summary>
        /// Fixes resource paths in a profile to ensure they're valid
        /// </summary>
        public void FixResourcePaths(IGameProfile profile)
        {
            // Ensure resources are initialized
            EnsureInitialized();
            
            try
            {
                // Fix icon path
                if (string.IsNullOrEmpty(profile.IconPath) || 
                    (!profile.IconPath.StartsWith("avares://") && !profile.IconPath.StartsWith("/Assets") && !FileExists(profile.IconPath)))
                {
                    _logger.LogWarning("Profile {ProfileId} has invalid icon path: {IconPath}, setting default", 
                        profile.Id, profile.IconPath);
                        
                    var defaultIcon = GetDefaultIcon();
                    profile.IconPath = defaultIcon.Path;
                }
                
                // Fix cover path
                if (string.IsNullOrEmpty(profile.CoverImagePath) || 
                    (!profile.CoverImagePath.StartsWith("avares://") && !profile.CoverImagePath.StartsWith("/Assets") && !FileExists(profile.CoverImagePath)))
                {
                    _logger.LogWarning("Profile {ProfileId} has invalid cover path: {CoverPath}, setting default", 
                        profile.Id, profile.CoverImagePath);
                        
                    var defaultCover = GetDefaultCover();
                    profile.CoverImagePath = defaultCover.Path;
                }
                
                // Log the updated paths
                _logger.LogDebug("Fixed resource paths for profile {ProfileId}: Icon={IconPath}, Cover={CoverPath}",
                    profile.Id, profile.IconPath, profile.CoverImagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing resource paths for profile {ProfileId}", profile.Id);
            }
        }
        
        /// <summary>
        /// Checks if a file exists (abstracted for testing)
        /// </summary>
        protected virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }
        
        /// <summary>
        /// Refreshes the available resources (scanning for new ones)
        /// </summary>
        public void RefreshResources()
        {
            try
            {
                _logger.LogInformation("Refreshing resources");
                
                // Clear existing lists but keep built-ins
                var builtInIcons = _icons.Where(i => i.IsBuiltIn).ToList();
                var builtInCovers = _covers.Where(c => c.IsBuiltIn).ToList();
                
                _icons.Clear();
                _covers.Clear();
                
                // Re-add built-ins
                foreach (var icon in builtInIcons)
                {
                    _icons.Add(icon);
                }
                
                foreach (var cover in builtInCovers)
                {
                    _covers.Add(cover);
                }
                
                // Scan for new resources
                ScanFileSystemResources();
                
                _logger.LogInformation("Resources refreshed: {IconCount} icons, {CoverCount} covers", 
                    _icons.Count, _covers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing resources");
            }
        }
        
        // Add this method to provide better fallback behavior
        private string CreateFallbackIconPath(string originalPath)
        {
            try
            {
                // Try different capitalizations
                string[] attempts = {
                    originalPath,
                    originalPath.ToLower(),
                    "/Assets/Icons/genhub-logo.png",
                    "/Assets/Icons/icon-default.png"
                };
                
                foreach (var attempt in attempts)
                {
                    try
                    {
                        var testUri = new Uri($"avares://GenHub{attempt}");
                        var asset = AssetLoader.Exists(testUri);
                        if (asset)
                        {
                            _logger.LogDebug("Found fallback icon at {Path}", attempt);
                            return attempt;
                        }
                    }
                    catch
                    {
                        // Ignore errors in fallback attempts
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fallback path for {Path}", originalPath);
            }
            
            return originalPath; // Return original as last resort
        }
        
        // Add these methods to help diagnose resource issues

        /// <summary>
        /// List all available resources in a given directory path
        /// </summary>
        private void ListAvailableResources(string directoryPath)
        {
            try
            {
                _logger.LogDebug("Listing resources in directory: {Directory}", directoryPath);
                var resourceUri = new Uri($"avares://GenHub/{directoryPath}");
                
                if (!AssetLoader.Exists(resourceUri))
                {
                    _logger.LogWarning("Directory does not exist in resources: {Directory}", directoryPath);
                    return;
                }
                
                var assets = AssetLoader.GetAssets(resourceUri, null);
                
                foreach (var asset in assets)
                {
                    _logger.LogDebug("Found resource: {Path}", asset.AbsolutePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing resources in {Directory}", directoryPath);
            }
        }

        /// <summary>
        /// Attempts to verify a specific resource exists
        /// </summary>
        private bool VerifyResourceExists(string path)
        {
            try
            {
                string normalizedPath = path;
                if (!path.StartsWith("avares://"))
                {
                    normalizedPath = $"avares://GenHub{path}";
                }
                
                var uri = new Uri(normalizedPath);
                bool exists = AssetLoader.Exists(uri);
                
                _logger.LogDebug("Resource {Path} exists: {Exists}", path, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying resource {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// Gets a profile resource item with game type metadata
        /// </summary>
        public ProfileResourceItem GetResourceWithGameType(string path, string displayName, string gameType)
        {
            return new ProfileResourceItem
            {
                Path = path,
                DisplayName = displayName,
                GameType = gameType
            };
        }

        /// <summary>
        /// Gets all available icons asynchronously
        /// </summary>
        public async Task<IEnumerable<ProfileResourceItem>> GetAvailableIconsAsync()
        {
            return await Task.FromResult(GetAvailableIcons());
        }

        /// <summary>
        /// Gets all available cover images asynchronously
        /// </summary>
        public async Task<IEnumerable<ProfileResourceItem>> GetAvailableCoverImagesAsync()
        {
            return await Task.FromResult(GetAvailableCovers());
        }

        /// <summary>
        /// Adds a custom icon asynchronously
        /// </summary>
        public async Task<string> AddCustomIconAsync(string filePath)
        {
            return await Task.FromResult(AddIconFromFile(filePath));
        }

        /// <summary>
        /// Adds a custom cover asynchronously
        /// </summary>
        public async Task<string> AddCustomCoverAsync(string filePath)
        {
            return await Task.FromResult(AddCoverFromFile(filePath));
        }

        /// <summary>
        /// Gets all available icons with associated game types
        /// </summary>
        public async Task<IEnumerable<ProfileResourceItem>> GetAvailableIconsWithGameTypesAsync()
        {
            var icons = await GetAvailableIconsAsync();
            
            // Add game type metadata to standard icons
            var result = new List<ProfileResourceItem>();
            foreach (var icon in icons)
            {
                // Clone the item
                var enhancedIcon = new ProfileResourceItem
                {
                    Path = icon.Path,
                    DisplayName = icon.DisplayName,
                    Id = icon.Id,
                    IsBuiltIn = icon.IsBuiltIn,
                    GameType = icon.GameType
                };
                
                // Add game type based on filename/path
                if (string.IsNullOrEmpty(enhancedIcon.GameType))
                {
                    if (icon.Path.Contains("generals", StringComparison.OrdinalIgnoreCase) && 
                        !icon.Path.Contains("zerohour", StringComparison.OrdinalIgnoreCase))
                    {
                        enhancedIcon.GameType = "Generals";
                    }
                    else if (icon.Path.Contains("zerohour", StringComparison.OrdinalIgnoreCase) || 
                            icon.Path.Contains("zero_hour", StringComparison.OrdinalIgnoreCase) ||
                            icon.Path.Contains("zero-hour", StringComparison.OrdinalIgnoreCase))
                    {
                        enhancedIcon.GameType = "Zero Hour";
                    }
                }
                
                result.Add(enhancedIcon);
            }
            
            return result;
        }
    }


}
