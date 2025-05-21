# Security Infrastructure

This directory contains components related to security aspects of the application, such as secure storage of sensitive information like API tokens.

## Components

*   **`TokenStorageService.cs`**:
    *   **Purpose**: Implements the `ITokenStorageService` interface (defined in `GenHub.Core.Interfaces.Github`). It is responsible for securely storing and retrieving sensitive tokens, particularly GitHub Personal Access Tokens (PATs).
    *   **Key Features**:
        *   `SaveToken(string token)`: Encrypts the provided token and stores it in a secure location (e.g., Windows Credential Manager, macOS Keychain, platform-specific secure storage, or an encrypted file).
        *   `RetrieveToken()`: Retrieves the stored token and decrypts it.
        *   `ClearToken()`: Securely removes the stored token.
    *   **Implementation Details**:
        *   Uses platform-specific APIs for secure storage if available (e.g., `DataProtectionProvider` on Windows for user-scope encryption).
        *   May fall back to file-based encryption if cross-platform secure storage is complex or not directly available, ensuring the encryption key is handled appropriately.
        *   Handles cases where no token is stored or retrieval fails.
    *   **Usage**: Used by `IGitHubApiClient` (or services that configure it) to obtain the GitHub token required for authenticated API requests. This allows the application to access private repositories or benefit from higher API rate limits.

## Responsibilities and Interactions

*   The primary responsibility of this layer is to protect sensitive user data, like API tokens, from unauthorized access.
*   `TokenStorageService` abstracts the complexities of secure storage, providing a simple interface for other parts of the application to use.
*   It plays a crucial role in enabling secure and effective interaction with external services that require authentication, such as GitHub.
*   Care must be taken in the implementation to use strong encryption methods and appropriate key management practices, especially if not relying on OS-provided secure credential stores.
