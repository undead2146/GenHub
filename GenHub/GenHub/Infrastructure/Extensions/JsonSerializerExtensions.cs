using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenHub.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for JsonSerializer configuration
    /// </summary>
    public static class JsonSerializerExtensions
    {
        /// <summary>
        /// Creates a standard set of JsonSerializerOptions for GitHub API communication
        /// </summary>
        public static JsonSerializerOptions CreateGitHubApiJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new JsonDateTimeConverter()
                }
            };
        }
    }
}
