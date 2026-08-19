using System;
using System.Runtime.Versioning;
using GenHub.Windows.Features.Shortcuts;
using Microsoft.Win32;
using Xunit;

namespace GenHub.Tests.Windows.Features.Shortcuts;

/// <summary>
/// Unit tests for <see cref="UriSchemeRegistrar"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UriSchemeRegistrarTests
{
    /// <summary>
    /// Verifies that Register creates or updates the genhub registry keys in HKCU.
    /// </summary>
    [Fact]
    public void Register_CreatesOrUpdatesGenhubRegistryKey()
    {
        // Act
        UriSchemeRegistrar.Register();

        // Assert
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\genhub");
        Assert.NotNull(key);

        var protocolValue = key.GetValue(string.Empty) as string;
        Assert.Equal("URL:genhub protocol", protocolValue);

        var urlProtocolFlag = key.GetValue("URL Protocol");
        Assert.NotNull(urlProtocolFlag);

        using var commandKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\genhub\shell\open\command");
        Assert.NotNull(commandKey);

        var command = commandKey.GetValue(string.Empty) as string;
        Assert.NotNull(command);
        Assert.Contains("%1", command);
        Assert.Contains(Environment.ProcessPath ?? string.Empty, command, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that Register can be invoked repeatedly without failure or unexpected mutations.
    /// </summary>
    [Fact]
    public void Register_IsIdempotent()
    {
        // Act - Call twice in succession to ensure no exceptions or unintended side effects occur
        UriSchemeRegistrar.Register();
        var ex = Record.Exception(() => UriSchemeRegistrar.Register());

        // Assert
        Assert.Null(ex);
    }
}
