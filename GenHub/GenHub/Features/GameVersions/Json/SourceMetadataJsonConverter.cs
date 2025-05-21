using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Features.GameVersions.Json
{
    public class SourceMetadataJsonConverter : JsonConverter<BaseSourceMetadata>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(BaseSourceMetadata).IsAssignableFrom(typeToConvert);
        }

        /// <summary>
        /// Creates a copy of JsonSerializerOptions without this converter to avoid stack overflow
        /// </summary>
        private JsonSerializerOptions GetInnerOptions(JsonSerializerOptions outerOptions)
        {
            // Create new options object
            var innerOptions = new JsonSerializerOptions(outerOptions);
            
            // Remove any instances of this converter type to prevent recursion
            for (int i = innerOptions.Converters.Count - 1; i >= 0; i--)
            {
                if (innerOptions.Converters[i] is SourceMetadataJsonConverter)
                {
                    innerOptions.Converters.RemoveAt(i);
                }
            }
            
            return innerOptions;
        }

        public override BaseSourceMetadata? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected StartObject but got {reader.TokenType}");
            
            // Parse current JSON data to a JsonNode
            JsonNode? node = JsonNode.Parse(ref reader);
            if (node == null)
                return null;
                
            // Get recursion-safe options
            var innerOptions = GetInnerOptions(options);
            
            // First check for a type discriminator
            JsonNode? typeNode = node["MetadataType"];
            Type? concreteType = null;
            
            if (typeNode != null)
            {
                string? metadataType = typeNode.GetValue<string>();
                
                // Determine the concrete type based on the discriminator
                concreteType = metadataType switch
                {
                    "GitHub" => typeof(GitHubSourceMetadata),
                    "Local" => typeof(FileSystemSourceMetadata),
                    "Manual" => typeof(CustomSourceMetadata),
                    _ => typeof(GenericSourceMetadata)
                };
            }
            // If no type discriminator found, try to infer from properties
            else if (node["AssociatedArtifact"] != null)
            {
                concreteType = typeof(GitHubSourceMetadata);
            }
            else if (node["OriginalPath"] != null)
            {
                concreteType = typeof(FileSystemSourceMetadata);
            }
            else
            {
                // Default to generic if we can't determine type
                concreteType = typeof(GenericSourceMetadata);
            }
            
            // Deserialize to the concrete type using our recursion-safe options
            return (BaseSourceMetadata?)node.Deserialize(concreteType, innerOptions);
        }

        public override void Write(Utf8JsonWriter writer, BaseSourceMetadata value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
            
            // Get recursion-safe options
            var innerOptions = GetInnerOptions(options);
            
            // Determine the type discriminator
            string metadataType = value switch
            {
                GitHubSourceMetadata _ => "GitHub",
                FileSystemSourceMetadata _ => "Local",
                CustomSourceMetadata _ => "Manual",
                _ => "Unknown"
            };
            
            // First, serialize the object to a JsonNode using the inner options
            JsonNode? jsonNode = JsonSerializer.SerializeToNode(value, value.GetType(), innerOptions);
            if (jsonNode is JsonObject jsonObject)
            {
                // Add or replace the type discriminator
                jsonObject["MetadataType"] = metadataType;
                
                // Write the modified JSON object to the writer
                jsonObject.WriteTo(writer, options);
            }
            else
            {
                // Fallback in case serialization didn't produce a JsonObject
                writer.WriteStartObject();
                writer.WriteString("MetadataType", metadataType);
                writer.WriteEndObject();
            }
        }
    }
}
