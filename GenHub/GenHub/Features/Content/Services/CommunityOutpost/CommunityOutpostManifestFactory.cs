using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.CommunityOutpost;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Features.Content.Services.Common;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GenHub.Features.Content.Services.CommunityOutpost;

/// <summary>
/// Manifest factory for Community Outpost publisher.
/// Handles single-content releases (patches, addons, maps, etc.) from the GenPatcher catalog.
/// Creates manifests with proper file entries and install targets.
/// </summary>
public class CommunityOutpostManifestFactory(
    ILogger<CommunityOutpostManifestFactory> logger,
    IFileHashProvider hashProvider,
    IArchivePayloadProcessor archivePayloadProcessor,
    IControlBarPackageProcessor? controlBarProcessor = null) : IPublisherManifestFactory
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    private static Regex GetCachedRegex(string pattern)
    {
        var normalized = pattern.ToLowerInvariant();
        return RegexCache.GetOrAdd(normalized, p => new Regex(
            "^" + Regex.Escape(p).Replace("\\*", ".*") + "$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));
    }

    /// <inheritdoc />
    public string PublisherId => CommunityOutpostConstants.PublisherId;

    /// <inheritdoc />
    public bool CanHandle(ContentManifest manifest)
    {
        var publisherMatches = manifest.Publisher?.PublisherType?.Equals(
            CommunityOutpostConstants.PublisherType,
            StringComparison.OrdinalIgnoreCase) == true ||
            manifest.OriginalProviderName?.Equals(
            CommunityOutpostConstants.PublisherType,
            StringComparison.OrdinalIgnoreCase) == true;

        logger.LogDebug(
            "CanHandle check for manifest {ManifestId}: Publisher={Publisher}, Type={PublisherType}, OriginalProvider={OriginalProvider}, Result={Result}",
            manifest.Id,
            manifest.Publisher?.Name,
            manifest.Publisher?.PublisherType,
            manifest.OriginalProviderName,
            publisherMatches);

        return publisherMatches;
    }

    /// <inheritdoc />
    public async Task<List<ContentManifest>> CreateManifestsFromExtractedContentAsync(
        ContentManifest originalManifest,
        string extractedDirectory,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Creating Community Outpost manifest from extracted content in: {Directory}",
            extractedDirectory);

        if (!Directory.Exists(extractedDirectory))
        {
            logger.LogError("Extracted directory does not exist: {Directory}", extractedDirectory);
            return [];
        }

        await archivePayloadProcessor.ProcessPayloadAsync(
            extractedDirectory,
            originalManifest.ContentType,
            originalManifest.TargetGame,
            cancellationToken);

        // Get the content code and install target from the original manifest metadata
        var contentCode = GetContentCodeFromManifest(originalManifest);
        var contentMetadata = GenPatcherContentRegistry.GetMetadata(contentCode);
        var requestedVariantId = GetRequestedVariantIdFromManifest(originalManifest);

        logger.LogInformation(
            "Processing content: {Name} ({ContentType}) with content code {Code}, InstallTarget={InstallTarget}, SupportsVariants={SupportsVariants}, RequestedVariant={RequestedVariant}",
            originalManifest.Name,
            originalManifest.ContentType,
            contentCode,
            contentMetadata.InstallTarget,
            contentMetadata.SupportsVariants,
            requestedVariantId ?? "none");

        // If content supports variants (e.g., resolution options), create separate manifests for each variant
        if (contentMetadata.SupportsVariants && contentMetadata.Variants != null && contentMetadata.Variants.Count > 0)
        {
            var variantsToBuild = contentMetadata.Variants;
            if (!string.IsNullOrEmpty(requestedVariantId))
            {
                var specificVariant = contentMetadata.Variants.FirstOrDefault(v => string.Equals(v.Id, requestedVariantId, StringComparison.OrdinalIgnoreCase));
                if (specificVariant != null)
                {
                    variantsToBuild = [specificVariant];
                }
            }

            logger.LogInformation(
                "Creating {VariantCount} variant manifests for {Name}",
                variantsToBuild.Count,
                originalManifest.Name);

            var variantManifests = new List<ContentManifest>();

            foreach (var variant in variantsToBuild)
            {
                var variantManifest = await BuildManifestWithFilesAsync(
                    originalManifest,
                    extractedDirectory,
                    contentMetadata,
                    variant,
                    cancellationToken);

                if (variantManifest != null)
                {
                    variantManifests.Add(variantManifest);
                    logger.LogInformation(
                        "Created variant manifest {ManifestId} for {VariantName} with {FileCount} files",
                        variantManifest.Id,
                        variant.Name,
                        variantManifest.Files.Count);
                }
            }

            return variantManifests;
        }

        // Build the manifest with file entries (single manifest, no variants)
        var manifest = await BuildManifestWithFilesAsync(
            originalManifest,
            extractedDirectory,
            contentMetadata,
            null,
            cancellationToken);

        if (manifest == null)
        {
            logger.LogWarning("Failed to build manifest for {Name}", originalManifest.Name);
            return [];
        }

        logger.LogInformation(
            "Created manifest {ManifestId} with {FileCount} files",
            manifest.Id,
            manifest.Files.Count);

        return [manifest];
    }

    /// <inheritdoc />
    public string GetManifestDirectory(ContentManifest manifest, string extractedDirectory)
    {
        // Control Bar (and similar) variants place BIG files at the extract root while a ZH/
        // folder may also exist from merged language deps. Prefer the root whenever the
        // manifest's RelativePaths are root-level so SourcePath validation does not fail.
        if (manifest.Files is { Count: > 0 } &&
            manifest.Files.All(IsRootRelativeManifestPath))
        {
            return extractedDirectory;
        }

        // Get the content code to determine the correct subdirectory
        var contentCode = GetContentCodeFromManifest(manifest);

        // Check if there's a subdirectory matching the content code
        var contentSubdir = Path.Combine(extractedDirectory, contentCode);
        if (Directory.Exists(contentSubdir))
        {
            return contentSubdir;
        }

        // Check for common subdirectory patterns (CCG for Generals, ZH for Zero Hour)
        var ccgSubdir = Path.Combine(extractedDirectory, "CCG");
        var zhSubdir = Path.Combine(extractedDirectory, "ZH");

        if (manifest.TargetGame == GameType.Generals && Directory.Exists(ccgSubdir))
        {
            return ccgSubdir;
        }

        if (manifest.TargetGame == GameType.ZeroHour && Directory.Exists(zhSubdir))
        {
            return zhSubdir;
        }

        // Default to extracted directory
        return extractedDirectory;
    }

    private static bool IsRootRelativeManifestPath(ManifestFile file)
    {
        var relative = (file.RelativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrEmpty(relative))
        {
            return true;
        }

        return !relative.StartsWith("ZH/", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith("CCG/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the content code from manifest metadata tags or manifest ID.
    /// </summary>
    private static string GetContentCodeFromManifest(ContentManifest manifest)
    {
        // Look for contentCode tag in metadata
        var contentCodeTag = manifest.Metadata?.Tags?
            .FirstOrDefault(t => t.StartsWith("contentCode:", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(contentCodeTag))
        {
            var tagValue = contentCodeTag["contentCode:".Length..];
            var directMeta = GenPatcherContentRegistry.GetMetadata(tagValue);
            if (directMeta.ContentType != ContentType.UnknownContentType)
            {
                return directMeta.ContentCode;
            }

            var dashIdx = tagValue.IndexOf('-');
            if (dashIdx > 0)
            {
                var prefix = tagValue[..dashIdx];
                var prefixMeta = GenPatcherContentRegistry.GetMetadata(prefix);
                if (prefixMeta.ContentType != ContentType.UnknownContentType)
                {
                    return prefixMeta.ContentCode;
                }
            }

            return tagValue;
        }

        // Try to extract from manifest ID
        // Format: 1.version.communityoutpost.contentType.contentName
        var idParts = manifest.Id.Value?.Split('.') ?? [];
        if (idParts.Length >= 5)
        {
            var contentName = idParts[4];
            var metadata = GenPatcherContentRegistry.GetMetadata(contentName);
            if (metadata.ContentType != ContentType.UnknownContentType)
            {
                return metadata.ContentCode;
            }

            var dashIndex = contentName.IndexOf('-');
            var codePrefix = dashIndex > 0 ? contentName[..dashIndex] : contentName;
            var prefixMetadata = GenPatcherContentRegistry.GetMetadata(codePrefix);
            if (prefixMetadata.ContentType != ContentType.UnknownContentType)
            {
                return prefixMetadata.ContentCode;
            }

            foreach (var code in GenPatcherContentRegistry.GetKnownContentCodes())
            {
                if (contentName.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                {
                    return code;
                }
            }

            return codePrefix;
        }

        return "unknown";
    }

    /// <summary>
    /// Extracts requested variant ID from manifest metadata, tags, ID, or name (e.g. cbpr-1080p -> 1080p).
    /// </summary>
    private static string? GetRequestedVariantIdFromManifest(ContentManifest manifest)
    {
        // 1. Check Metadata.SelectedVariantId
        if (!string.IsNullOrEmpty(manifest.Metadata?.SelectedVariantId))
        {
            return manifest.Metadata.SelectedVariantId;
        }

        // 2. Check tags for requestedVariant:, selectedVariant:, variant:
        if (manifest.Metadata?.Tags != null)
        {
            var variantTag = manifest.Metadata.Tags.FirstOrDefault(t =>
                t.StartsWith("requestedVariant:", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("selectedVariant:", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("variant:", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(variantTag))
            {
                var colonIndex = variantTag.IndexOf(':');
                if (colonIndex >= 0 && colonIndex < variantTag.Length - 1)
                {
                    return variantTag[(colonIndex + 1)..].Trim();
                }
            }
        }

        var contentCode = GetContentCodeFromManifest(manifest);
        var metadata = GenPatcherContentRegistry.GetMetadata(contentCode);

        // 3. Try to extract from manifest ID
        var idParts = manifest.Id.Value?.Split('.') ?? [];
        if (idParts.Length >= 5)
        {
            var contentName = idParts[4];
            var dashIndex = contentName.IndexOf('-');
            if (dashIndex > 0 && dashIndex < contentName.Length - 1)
            {
                return contentName[(dashIndex + 1)..];
            }

            if (metadata.Variants != null && metadata.Variants.Count > 0)
            {
                if (contentName.StartsWith(contentCode, StringComparison.OrdinalIgnoreCase))
                {
                    var suffix = contentName[contentCode.Length..];
                    var matchingVariant = metadata.Variants.FirstOrDefault(v =>
                        string.Equals(v.Id, suffix, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v.Id.Replace("-", string.Empty), suffix, StringComparison.OrdinalIgnoreCase));
                    if (matchingVariant != null)
                    {
                        return matchingVariant.Id;
                    }
                }

                foreach (var variant in metadata.Variants)
                {
                    var cleanVariantId = variant.Id.Replace("-", string.Empty);
                    if (contentName.EndsWith(variant.Id, StringComparison.OrdinalIgnoreCase) ||
                        contentName.EndsWith(cleanVariantId, StringComparison.OrdinalIgnoreCase))
                    {
                        return variant.Id;
                    }
                }
            }
        }

        // 4. Try to extract from manifest Name
        if (!string.IsNullOrEmpty(manifest.Name) && metadata.Variants != null && metadata.Variants.Count > 0)
        {
            foreach (var variant in metadata.Variants)
            {
                if (manifest.Name.EndsWith(variant.Name, StringComparison.OrdinalIgnoreCase) ||
                    manifest.Name.Contains(variant.Name, StringComparison.OrdinalIgnoreCase) ||
                    manifest.Name.EndsWith(variant.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return variant.Id;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Determines the install target for a specific file based on its path and content type.
    /// </summary>
    private static ContentInstallTarget DetermineFileInstallTarget(
        string relativePath,
        ContentInstallTarget defaultTarget)
    {
        // Normalize path separators
        var normalizedPath = relativePath.Replace('\\', '/').ToLowerInvariant();

        // Map files (.map extension or in Maps folder) always go to UserMapsDirectory
        if (normalizedPath.EndsWith(".map") ||
            normalizedPath.Contains("/maps/") ||
            normalizedPath.StartsWith("maps/"))
        {
            return ContentInstallTarget.UserMapsDirectory;
        }

        // Replay files go to UserReplaysDirectory
        if (normalizedPath.EndsWith(".rep") ||
            normalizedPath.Contains("/replays/") ||
            normalizedPath.StartsWith("replays/"))
        {
            return ContentInstallTarget.UserReplaysDirectory;
        }

        // Screenshot files go to UserScreenshotsDirectory
        if ((normalizedPath.EndsWith(".bmp") || normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg")) &&
            (normalizedPath.Contains("/screenshots/") || normalizedPath.StartsWith("screenshots/")))
        {
            return ContentInstallTarget.UserScreenshotsDirectory;
        }

        // Game data files (BIG, INI, etc.) go to workspace
        if (normalizedPath.EndsWith(".big") ||
            normalizedPath.EndsWith(".ini") ||
            normalizedPath.EndsWith(".exe") ||
            normalizedPath.EndsWith(".dll") ||
            normalizedPath.Contains("/data/"))
        {
            return ContentInstallTarget.Workspace;
        }

        // Use the content type's default target
        return defaultTarget;
    }

    /// <summary>
    /// Attempts to copy a file with retry logic for transient file lock issues.
    /// </summary>
    private static async Task TryCopyFileWithRetryAsync(string source, string destination, ILogger logger, int maxRetries = 3, int delayMs = 100)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException ex) when (attempt < maxRetries)
            {
                logger.LogWarning(
                    "File copy attempt {Attempt}/{MaxRetries} failed for {Source}: {Message}. Retrying...",
                    attempt,
                    maxRetries,
                    Path.GetFileName(source),
                    ex.Message);
                await Task.Delay(delayMs * attempt);
            }
        }

        // Final attempt without catch - let it throw if it fails
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Recursion guard
        var sourceInfo = new DirectoryInfo(sourceDir);
        var destInfo = new DirectoryInfo(destinationDir);
        if (destInfo.FullName.StartsWith(sourceInfo.FullName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot copy directory into itself: Source={sourceDir}, Dest={destinationDir}");
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            try
            {
                var targetFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, targetFile, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Log and continue, or rely on caller to handle?
                // Since this is a helper, we let exceptions bubble up or just do a best-effort?
                // The comment said "doesn't handle IOException for individual files".
                // We'll throw to be safe, but at least we have the recursion guard.
                throw;
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
             var targetDir = Path.Combine(destinationDir, Path.GetFileName(dir));
             CopyDirectory(dir, targetDir);
        }
    }

    private static bool IsDependencyPayloadBundled(
        ContentDependency dependency,
        IEnumerable<ManifestFile> files)
    {
        var dependencyId = dependency.Id.Value;
        var separatorIndex = dependencyId.LastIndexOf('.');
        if (separatorIndex < 0 || separatorIndex == dependencyId.Length - 1)
        {
            return false;
        }

        var metadata = GenPatcherContentRegistry.GetMetadata(dependencyId[(separatorIndex + 1)..]);
        if (string.IsNullOrWhiteSpace(metadata.OutputFilename))
        {
            return false;
        }

        return files.Any(file => string.Equals(
            Path.GetFileName(file.RelativePath),
            metadata.OutputFilename,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds a manifest with all files from the extracted directory.
    /// If variant is provided, filters files based on variant's IncludePatterns and ExcludePatterns.
    /// </summary>
    private async Task<ContentManifest?> BuildManifestWithFilesAsync(
        ContentManifest originalManifest,
        string extractedDirectory,
        GenPatcherContentMetadata contentMetadata,
        ContentVariant? variant,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all files from extracted directory
            var allFiles = Directory.GetFiles(extractedDirectory, "*.*", SearchOption.AllDirectories);

            if (allFiles.Length == 0)
            {
                logger.LogWarning("No files found in extracted directory: {Directory}", extractedDirectory);
                return null;
            }

            logger.LogDebug("Found {FileCount} files in extracted directory", allFiles.Length);

            var fileEntries = new List<ManifestFile>();

            var dependencyBigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in contentMetadata.GetDependencies()
                         .Where(d => d.InstallBehavior == DependencyInstallBehavior.AutoInstall))
            {
                var depId = dependency.Id.Value;
                var lastDot = depId.LastIndexOf('.');
                if (lastDot > -1 && lastDot < depId.Length - 1)
                {
                    var depCode = depId[(lastDot + 1)..];
                    var depMetadata = GenPatcherContentRegistry.GetMetadata(depCode);
                    if (!string.IsNullOrEmpty(depMetadata.OutputFilename))
                    {
                        dependencyBigFiles.Add(depMetadata.OutputFilename);
                    }
                }
            }

            var alwaysIncludeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (contentMetadata.Category == GenPatcherContentCategory.ControlBar)
            {
                // Small metadata BIG included alongside variant-specific files in GenPatcher builds
                alwaysIncludeFiles.Add("340_ControlBarProZH.big");
            }

            var controlBarRepackedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var isControlBarVariant = contentMetadata.Category == GenPatcherContentCategory.ControlBar &&
                                      contentMetadata.SupportsVariants &&
                                      variant != null;

            if (isControlBarVariant)
            {
                var processor = controlBarProcessor;

                if (processor != null)
                {
                    var repacked = await processor.ProcessAndRepackControlBarAsync(
                        extractedDirectory,
                        originalManifest,
                        variant!.Id,
                        cancellationToken);
                    foreach (var f in repacked)
                    {
                        controlBarRepackedOutputs.Add(f);
                    }
                }
            }

            if (controlBarRepackedOutputs.Count > 0)
            {
                allFiles = Directory.GetFiles(extractedDirectory, "*.*", SearchOption.AllDirectories);
            }

            var hasVariantBigFiles = false;
            if (variant != null)
            {
                foreach (var path in allFiles)
                {
                    var name = Path.GetFileName(path);
                    if (!name.EndsWith(".big", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (controlBarRepackedOutputs.Contains(name) ||
                        alwaysIncludeFiles.Contains(name) ||
                        dependencyBigFiles.Contains(name))
                    {
                        hasVariantBigFiles = true;
                        break;
                    }

                    var normalized = name.ToLowerInvariant();
                    if (variant.IncludePatterns != null && variant.IncludePatterns.Any(p => GetCachedRegex(p.ToLowerInvariant()).IsMatch(normalized)))
                    {
                        hasVariantBigFiles = true;
                        break;
                    }
                }
            }

            foreach (var fullPath in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(extractedDirectory, fullPath);

                var fileName = Path.GetFileName(relativePath);
                var normalizedPath = relativePath.Replace('\\', '/').ToLowerInvariant();
                var isDependencyBig = dependencyBigFiles.Contains(fileName);
                var isAlwaysInclude = alwaysIncludeFiles.Contains(fileName);
                var isControlBarVariantFile = isControlBarVariant;
                var isRepackedOutput = controlBarRepackedOutputs.Contains(fileName);

                if (isControlBarVariantFile && controlBarRepackedOutputs.Count > 0)
                {
                    if (!isRepackedOutput && !isDependencyBig && !isAlwaysInclude)
                    {
                        logger.LogDebug(
                            "Skipping file {File} because control bar variant is repacked into Art/Data BIG files",
                            relativePath);
                        continue;
                    }
                }

                if (isControlBarVariantFile && hasVariantBigFiles && !fileName.EndsWith(".big", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug(
                        "Skipping non-BIG file {File} for control bar variant {Variant}",
                        relativePath,
                        variant!.Name);
                    continue;
                }

                // Filter files based on variant patterns if variant is specified
                if (variant != null)
                {
                    // Check if file matches include patterns
                    bool matchesInclude = false;
                    if (variant.IncludePatterns != null && variant.IncludePatterns.Count > 0)
                    {
                        foreach (var pattern in variant.IncludePatterns)
                        {
                            var regex = GetCachedRegex(pattern);

                            if (regex.IsMatch(fileName) || regex.IsMatch(normalizedPath))
                            {
                                matchesInclude = true;
                                break;
                            }
                        }

                        // If include patterns exist but file doesn't match any, skip it
                        // UNLESS it's a dependency/base file or an always-include file
                        // File matching logic:
                        // 1. Matches inclusion pattern
                        // 2. OR: Starts with '!' (Special GenPatcher prefix for mandatory files like hotkeys)
                        // 3. AND: Is not a dependency BIG or always-include BIG (handled separately)
                        if (!matchesInclude && !isDependencyBig && !isAlwaysInclude)
                        {
                            logger.LogDebug("Skipping file {File} - does not match variant {Variant} include patterns", relativePath, variant.Name);
                            continue;
                        }
                    }

                    // Check if file matches exclude patterns
                    if (variant.ExcludePatterns != null && variant.ExcludePatterns.Count > 0)
                    {
                        bool matchesExclude = false;
                        foreach (var pattern in variant.ExcludePatterns)
                        {
                            var regex = GetCachedRegex(pattern);

                            if (regex.IsMatch(fileName) || regex.IsMatch(normalizedPath))
                            {
                                matchesExclude = true;
                                break;
                            }
                        }

                        if (matchesExclude && !isDependencyBig && !isAlwaysInclude)
                        {
                            logger.LogDebug("Skipping file {File} - matches variant {Variant} exclude pattern", relativePath, variant.Name);
                            continue;
                        }
                    }
                }

                var hash = await hashProvider.ComputeFileHashAsync(fullPath, cancellationToken);
                var fileSize = new FileInfo(fullPath).Length;
                var isExecutable = relativePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                // Determine install target for this file
                var fileInstallTarget = DetermineFileInstallTarget(
                    relativePath,
                    contentMetadata.InstallTarget);

                fileEntries.Add(new ManifestFile
                {
                    RelativePath = relativePath,
                    Hash = hash,
                    Size = fileSize,
                    IsExecutable = isExecutable,
                    SourceType = ContentSourceType.ExtractedPackage,
                    SourcePath = fullPath,
                    InstallTarget = fileInstallTarget,
                });

                logger.LogDebug(
                    "Added file: {Path} (Size: {Size} bytes, InstallTarget: {Target})",
                    relativePath,
                    fileSize,
                    fileInstallTarget);
            }

            // Create variant-specific manifest ID and name if variant is provided
            var manifestId = originalManifest.Id;
            var manifestName = originalManifest.Name;

            string? variantGroupId = null;
            string? variantFamilyName = null;
            if (variant != null)
            {
                // Get the base content code from contentMetadata.ContentCode or the original manifest ID
                var idParts = originalManifest.Id.Value.Split('.');
                if (idParts.Length >= 5)
                {
                    var baseCode = !string.IsNullOrEmpty(contentMetadata.ContentCode) && !string.Equals(contentMetadata.ContentCode, "unknown", StringComparison.OrdinalIgnoreCase)
                        ? contentMetadata.ContentCode
                        : idParts[4];
                    var existingDash = baseCode.IndexOf('-');
                    if (existingDash > 0)
                    {
                        baseCode = baseCode[..existingDash];
                    }

                    // Stable group key shared by every sibling variant, scoped to publisher +
                    // content type + content code so distinct releases never collide.
                    variantGroupId = $"{idParts[2].ToLowerInvariant()}.{idParts[3].ToLowerInvariant()}.{baseCode.ToLowerInvariant()}";

                    // Create new content name with variant suffix (e.g., "cbpr-1080p")
                    // This maintains the 5-segment format: schemaVersion.userVersion.publisher.contentType.contentName-variant
                    var variantContentName = $"{baseCode}-{variant.Id}";

                    // Rebuild manifest ID with variant-suffixed content name (still 5 segments)
                    manifestId = ManifestId.Create($"{idParts[0]}.{idParts[1]}.{idParts[2]}.{idParts[3]}.{variantContentName}");
                }

                // Use registry display name as the stable family so we never double-append
                // a resolution that was already present on the resolved catalog name
                // (e.g. "Control Bar Pro (ExiLe) - 720p" + "1080p" → "... - 720p - 1080p").
                variantFamilyName = contentMetadata.DisplayName;
                manifestName = $"{contentMetadata.DisplayName} - {variant.Name}";

                logger.LogInformation(
                    "Creating variant manifest: {ManifestId} ({ManifestName}) with {FileCount} files",
                    manifestId,
                    manifestName,
                    fileEntries.Count);
            }

            // Create the manifest preserving original data but with updated files
            var manifest = new ContentManifest
            {
                Id = manifestId,
                Name = manifestName,
                Version = !string.IsNullOrWhiteSpace(originalManifest.Version)
                    ? originalManifest.Version
                    : (!string.IsNullOrWhiteSpace(contentMetadata.Version)
                        ? contentMetadata.Version
                        : CommunityOutpostCatalogConstants.DefaultMetadataVersion),
                SchemaVersion = originalManifest.SchemaVersion,
                ContentType = originalManifest.ContentType,
                TargetGame = (variant != null && variant.TargetGame.HasValue) ? variant.TargetGame.Value : originalManifest.TargetGame,
                Files = fileEntries,

                // Only remove an auto-install dependency when its generated payload was
                // actually merged into this manifest. Some dependencies (for example
                // GenTool's DLL) are not BIG files and must remain resolvable at profile
                // activation time.
                Dependencies = [.. contentMetadata.GetDependencies().Where(dependency =>
                    dependency.InstallBehavior != DependencyInstallBehavior.AutoInstall ||
                    !IsDependencyPayloadBundled(dependency, fileEntries))],
                InstallationInstructions = originalManifest.InstallationInstructions ?? new InstallationInstructions(),
                Publisher = originalManifest.Publisher,

                // Carry source provenance onto every output manifest so the in-session
                // correlation path (ContentStateService origin match + session-downloads map)
                // keeps working even when a publisher factory renames or splits content.
                OriginalProviderName = originalManifest.OriginalProviderName,
                OriginalContentId = originalManifest.OriginalContentId,

                Metadata = new ContentMetadata
                {
                    Description = originalManifest.Metadata?.Description ?? string.Empty,
                    ReleaseDate = originalManifest.Metadata?.ReleaseDate ?? DateTime.UtcNow,
                    IconUrl = CommunityOutpostConstants.LogoSource,
                    CoverUrl = CommunityOutpostConstants.CoverSource,
                    ThemeColor = CommunityOutpostConstants.ThemeColor,
                    ScreenshotUrls = originalManifest.Metadata?.ScreenshotUrls ?? [],
                    Tags = originalManifest.Metadata?.Tags?.Any(t => t.StartsWith("contentCode:", StringComparison.OrdinalIgnoreCase)) == true
                        ? originalManifest.Metadata.Tags
                        : [.. originalManifest.Metadata?.Tags ?? [], $"contentCode:{contentMetadata.ContentCode}"],
                    ChangelogUrl = originalManifest.Metadata?.ChangelogUrl,

                    // Every sibling variant carries the full variant list so the UI can render
                    // the whole family from any one manifest, and declares that selection is
                    // required (a user must pick a resolution). SelectedVariantId marks which
                    // sibling this manifest physically represents.
                    Variants = contentMetadata.Variants ?? [],
                    RequiresVariantSelection = variant != null,
                    SelectedVariantId = variant?.Id,
                    VariantGroupId = variantGroupId,
                    VariantFamilyName = variantFamilyName,
                },
            };

            logger.LogInformation(
                "Built manifest {ManifestId} for {ContentType} '{Name}' with {FileCount} files and {DependencyCount} dependencies",
                manifest.Id,
                manifest.ContentType,
                manifest.Name,
                fileEntries.Count,
                manifest.Dependencies?.Count ?? 0);

            // Log each dependency for debugging
            if (manifest.Dependencies != null && manifest.Dependencies.Count > 0)
            {
                foreach (var dep in manifest.Dependencies)
                {
                    logger.LogDebug(
                        "  Dependency: {DepName} ({DepId}) - Type: {DepType}",
                        dep.Name,
                        dep.Id,
                        dep.DependencyType);
                }
            }
            else
            {
                logger.LogWarning("Manifest {ManifestId} has NO dependencies! Category: {Category}", manifest.Id, contentMetadata.Category);
            }

            return manifest;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build manifest for {Name}", originalManifest.Name);
            return null;
        }
    }
}
