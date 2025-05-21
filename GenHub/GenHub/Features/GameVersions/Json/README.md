# Game Versions JSON Converters

This directory contains custom `System.Text.Json` converters tailored for models used within the Game Versions feature. These converters handle specific serialization and deserialization requirements that are not covered by default JSON processing.

## Converters

*   **[`SourceMetadataJsonConverter.cs`](GenHub/GenHub/Features/GameVersions/Json/SourceMetadataJsonConverter.cs)**:
    *   **Purpose**: Provides custom JSON serialization and deserialization for the `BaseSourceMetadata` abstract class and its concrete implementations (e.g., `GitHubSourceMetadata`, `FileSystemSourceMetadata`, `CustomSourceMetadata`, `GenericSourceMetadata`).
    *   **Necessity**: Required to handle polymorphism. Since `GameVersion` objects can store different types of source metadata under a `BaseSourceMetadata` property, this converter ensures that the correct concrete type is instantiated during deserialization and that type information is preserved during serialization.
    *   **Logic**:
        *   **`Write` Method**: When serializing, it inspects the actual type of the `BaseSourceMetadata` object. It then adds a type discriminator field (e.g., `"MetadataType": "GitHub"`) to the JSON output before serializing the rest of the object's properties. This allows the `Read` method to identify the correct concrete type later.
        *   **`Read` Method**: When deserializing, it first parses the JSON into a `JsonNode`. It then looks for the type discriminator field (e.g., `"MetadataType"`). Based on the value of this discriminator, it determines the concrete class (e.g., `GitHubSourceMetadata`) to deserialize the JSON object into.
        *   **Recursion Handling**: It creates a new `JsonSerializerOptions` instance internally, removing itself from the converters list for the recursive call to `JsonSerializer.Deserialize` or `SerializeToNode`. This prevents stack overflow exceptions that would occur if the converter called itself indefinitely for the same type.
    *   **Integration**: This converter is typically registered globally in `JsonSerializerOptions` when the application sets up its JSON serialization services, or it can be applied directly to properties using `[JsonConverter(typeof(SourceMetadataJsonConverter))]`.

## Usage

These converters are used by `System.Text.Json` whenever `GameVersion` objects or other models containing these specific types are serialized to or deserialized from JSON, for example, when saving to or loading from repository files.
