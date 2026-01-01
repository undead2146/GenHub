using GenHub.Core.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Tools.ReplayManager;
using GenHub.Core.Models.Tools.ReplayManager;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Tools.ReplayManager.Services;

/// <summary>
/// Implementation of <see cref="IReplayExportService"/> for exporting and sharing replays using UploadThing v7.
/// </summary>
public sealed class ReplayExportService(
    HttpClient httpClient,
    IZipValidationService zipValidationService,
    ILogger<ReplayExportService> logger) : IReplayExportService
{
    private const string UploadThingApiVersion = ApiConstants.UploadThingApiVersion;

    /// <inheritdoc />
    public async Task<string?> UploadToUploadThingAsync(
        IEnumerable<ReplayFile> replays,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var token = Environment.GetEnvironmentVariable("UPLOADTHING_TOKEN") ??
                    Environment.GetEnvironmentVariable("GENHUB_UPLOADTHING_TOKEN");

        if (string.IsNullOrEmpty(token))
        {
            logger.LogError(ErrorMessages.UploadThingTokenMissing);
            return null;
        }

        string? zipToUpload = null;
        bool isTemporaryZip = false;

        try
        {
            var replayList = replays.ToList();
            if (replayList.Count == 0) return null;

            if (replayList.Count == 1 && replayList[0].FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var (isValid, errorMessage) = zipValidationService.ValidateZip(replayList[0].FullPath);
                if (!isValid)
                {
                    logger.LogError(ErrorMessages.ZipValidationFailed, errorMessage);
                    throw new ArgumentException(errorMessage ?? "Invalid ZIP archive for upload.");
                }

                zipToUpload = replayList[0].FullPath;
            }
            else
            {
                var tempZip = Path.Combine(Path.GetTempPath(), $"{ReplayManagerConstants.TempShareFilePrefix}{Guid.NewGuid()}.zip");
                var createdZip = await this.ExportToZipAsync(replayList, tempZip, progress, ct);
                if (createdZip == null) return null;

                zipToUpload = createdZip;
                isTemporaryZip = true;
            }

            if (new FileInfo(zipToUpload).Length > ReplayManagerConstants.MaxReplaySizeBytes)
            {
                logger.LogError(ErrorMessages.FileExceedsSizeLimit, zipToUpload);
                return null;
            }

            return await this.PerformV7UploadAsync(zipToUpload, token, progress, ct);
        }
        catch (ArgumentException)
        {
            throw; // Bubble up validation errors
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload to UploadThing V7");
            return null;
        }
        finally
        {
            if (isTemporaryZip && !string.IsNullOrEmpty(zipToUpload) && File.Exists(zipToUpload))
            {
                File.Delete(zipToUpload);
            }
        }
    }

    /// <inheritdoc />
    public async Task<string?> ExportToZipAsync(IEnumerable<ReplayFile> replays, string destinationPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(
                () =>
                {
                    var replayList = replays.ToList();
                    if (replayList.Count == 0) return null;

                    using var zipFile = File.Create(destinationPath);
                    using var archive = new ZipArchive(zipFile, ZipArchiveMode.Create);

                    int total = replayList.Count;
                    int count = 0;

                    foreach (var replay in replayList)
                {
                    count++;
                    progress?.Report((double)count / total * 0.4);

                    if (!File.Exists(replay.FullPath)) continue;
                    archive.CreateEntryFromFile(replay.FullPath, replay.FileName);
                }

                    return destinationPath;
            },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogMessages.FailedToCreateZip, destinationPath);
            return null;
        }
    }

    private async Task<string?> PerformV7UploadAsync(
        string filePath,
        string token,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        logger.LogInformation(LogMessages.UploadingToUploadThing, filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileName(filePath);

            // Step 1: Prepare the upload
            const string PrepareUrl = ApiConstants.UploadThingPrepareUrl;

            var requestPayload = new V7FileRequestDetail
            {
                FileName = fileName,
                FileSize = fileInfo.Length,
                ContentTypes = [
                    "application/zip",
                ],
            };

            var prepareRequest = new HttpRequestMessage(HttpMethod.Post, PrepareUrl);
            prepareRequest.Headers.Add("x-uploadthing-api-key", token);
            prepareRequest.Headers.Add("x-uploadthing-version", UploadThingApiVersion);
            prepareRequest.Content = JsonContent.Create(requestPayload);

            var prepareResponse = await httpClient.SendAsync(prepareRequest, ct);
            if (!prepareResponse.IsSuccessStatusCode)
            {
                var error = await prepareResponse.Content.ReadAsStringAsync(ct);
                logger.LogError(ErrorMessages.V7PrepareUploadFailed, prepareResponse.StatusCode, error);
                return null;
            }

            var instruction = await prepareResponse.Content.ReadFromJsonAsync<V7FileInstruction>(cancellationToken: ct);

            if (instruction?.PresignedUrl == null || instruction?.Key == null)
            {
                var rawResponse = await prepareResponse.Content.ReadAsStringAsync(ct);
                logger.LogError(ErrorMessages.UploadThingMissingFields, rawResponse);
                return null;
            }

            // Step 2: Upload binary via PUT
            // CRITICAL FIX: The UploadThing docs specify using FormData (multipart/form-data)
            // for the PUT request, not raw binary.
            using var fileStream = File.OpenRead(filePath); // FIXED: Removed invalid buffer size argument

            // Create the multipart content
            var multipartContent = new MultipartFormDataContent();

            // Add the file to the form data with the name "file"
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            multipartContent.Add(fileContent, "file", fileName);

            var uploadRequest = new HttpRequestMessage(HttpMethod.Put, instruction.PresignedUrl)
            {
                Content = multipartContent,
            };

            // MultipartFormDataContent will automatically set the Content-Type with a boundary
            uploadRequest.Headers.UserAgent.ParseAdd("GenHub/1.0");

            progress?.Report(0.6);

            var uploadResponse = await httpClient.SendAsync(uploadRequest, ct);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                var uploadError = await uploadResponse.Content.ReadAsStringAsync(ct);
                logger.LogError(ErrorMessages.V7BinaryUploadFailed, uploadResponse.StatusCode, uploadError);
                return null;
            }

            var publicFileUrl = string.Format(ApiConstants.UploadThingPublicUrlFormat, instruction.Key);

            logger.LogInformation(LogMessages.UploadThingSuccessful, publicFileUrl);
            progress?.Report(1.0);

            return publicFileUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ErrorMessages.ExceptionInUploadThingFlow);
            return null;
        }
    }

    // --- V7 Request DTOs ---
    private sealed class V7FileRequestDetail
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("contentTypes")]
        public List<string> ContentTypes { get; set; } = [];
    }

    // --- V7 Response DTO ---
    private sealed class V7FileInstruction
    {
        [JsonPropertyName("url")]
        public string? PresignedUrl { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }
    }
}
