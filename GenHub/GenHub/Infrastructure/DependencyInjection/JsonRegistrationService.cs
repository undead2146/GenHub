using System;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GenHub.Features.GameVersions.Json;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration for JSON serialization options
    /// </summary>
    public static class JsonRegistrationService
    {
        public static IServiceCollection AddJsonOptions(this IServiceCollection services, ILogger logger)
        {
            logger.LogInformation("Configuring JSON options with converters");

            try 
            {
                // Create shared JsonSerializerOptions that will be used application-wide
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // For better readability of stored JSON
                    AllowTrailingCommas = true,  // More forgiving when reading JSON
                    ReadCommentHandling = JsonCommentHandling.Skip // Allow comments in JSON files
                };

                // Register the source metadata converter
                jsonOptions.Converters.Add(new SourceMetadataJsonConverter());
                logger.LogDebug("Added SourceMetadataJsonConverter to JSON options");
                
                // Add an enum converter that serializes to strings for better readability
                jsonOptions.Converters.Add(new JsonStringEnumConverter());
                logger.LogDebug("Added JsonStringEnumConverter to JSON options");

                // Register as a singleton so all services can use the same instance
                services.AddSingleton(jsonOptions);
                logger.LogDebug("JsonSerializerOptions registered as singleton");
                
                // Also register a serialize/deserialize helper service if needed
                // services.AddSingleton<IJsonService, JsonService>();
                
                logger.LogInformation("JSON options configured successfully");
                
                return services;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to configure JSON options");
                
                // Register a basic fallback option to prevent null errors
                services.AddSingleton(new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true 
                });
                
                throw;
            }
        }
    }
}
