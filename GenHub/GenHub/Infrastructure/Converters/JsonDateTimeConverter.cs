using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Converts DateTime to and from JSON with proper format handling for GitHub API
    /// </summary>
    public class JsonDateTimeConverter : JsonConverter<DateTime>
    {
        private const string ISO8601Format = "yyyy-MM-ddTHH:mm:ssZ";

        /// <summary>
        /// Reads DateTime from JSON with improved handling of GitHub's format
        /// </summary>
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return DateTime.MinValue;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for DateTime, got {reader.TokenType}");

            string dateText = reader.GetString() ?? string.Empty;

            // Try standard parsing first
            if (DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime result))
                return result;

            // Try ISO 8601 format used by GitHub
            if (DateTime.TryParseExact(dateText, ISO8601Format, CultureInfo.InvariantCulture, 
                DateTimeStyles.AdjustToUniversal, out result))
                return result;

            // Last resort - try with RoundtripKind
            if (DateTime.TryParse(dateText, CultureInfo.InvariantCulture, 
                DateTimeStyles.RoundtripKind, out result))
                return result;

            return DateTime.MinValue;
        }

        /// <summary>
        /// Writes DateTime to JSON in ISO 8601 format
        /// </summary>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(ISO8601Format, CultureInfo.InvariantCulture));
        }
    }
}
