using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using GenHub.Windows.Features.Shortcuts;
using Microsoft.Win32;
using Xunit;

namespace GenHub.Tests.Windows.Features.Shortcuts;

/// <summary>
/// Unit tests for <see cref="UriSchemeRegistrar"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UriSchemeRegistrarTests : IDisposable
{
    private const string TargetKeyPath = @"Software\Classes\genhub";
    private readonly RegistryKeySnapshot? _snapshot;
    private readonly bool _existedPrior;

    /// <summary>
    /// Initializes a new instance of the <see cref="UriSchemeRegistrarTests"/> class.
    /// Captures a snapshot of any pre-existing registry state to restore during teardown.
    /// </summary>
    public UriSchemeRegistrarTests()
    {
        using var rootKey = Registry.CurrentUser.OpenSubKey(TargetKeyPath, writable: false);
        _existedPrior = rootKey != null;
        if (rootKey != null)
        {
            _snapshot = CaptureSnapshot(rootKey);
        }
    }

    /// <summary>
    /// Verifies that Register creates or updates the genhub registry keys in HKCU.
    /// </summary>
    [Fact]
    public void Register_CreatesOrUpdatesGenhubRegistryKey()
    {
        // Act
        UriSchemeRegistrar.Register();

        // Assert
        using var key = Registry.CurrentUser.OpenSubKey(TargetKeyPath);
        Assert.NotNull(key);

        var protocolValue = key.GetValue(string.Empty) as string;
        Assert.Equal("URL:genhub protocol", protocolValue);

        var urlProtocolFlag = key.GetValue("URL Protocol");
        Assert.NotNull(urlProtocolFlag);

        using var commandKey = Registry.CurrentUser.OpenSubKey($@"{TargetKeyPath}\shell\open\command");
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

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(TargetKeyPath, throwOnMissingSubKey: false);

            if (_existedPrior && _snapshot != null)
            {
                using var rootKey = Registry.CurrentUser.CreateSubKey(TargetKeyPath, writable: true);
                if (rootKey != null)
                {
                    RestoreSnapshot(rootKey, _snapshot);
                }
            }
        }
        catch
        {
            // Suppress cleanup exceptions in tests to avoid masking assertion results
        }
    }

    private static RegistryKeySnapshot CaptureSnapshot(RegistryKey key)
    {
        var snapshot = new RegistryKeySnapshot
        {
            Name = Path.GetFileName(key.Name),
        };

        foreach (var valueName in key.GetValueNames())
        {
            var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            var kind = key.GetValueKind(valueName);
            snapshot.Values[valueName] = (value, kind);
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName, writable: false);
            if (subKey != null)
            {
                snapshot.SubKeys.Add(CaptureSnapshot(subKey));
            }
        }

        return snapshot;
    }

    private static void RestoreSnapshot(RegistryKey targetKey, RegistryKeySnapshot snapshot)
    {
        foreach (var (valueName, (value, kind)) in snapshot.Values)
        {
            if (value != null)
            {
                targetKey.SetValue(valueName, value, kind);
            }
        }

        foreach (var subKeySnapshot in snapshot.SubKeys)
        {
            using var subKey = targetKey.CreateSubKey(subKeySnapshot.Name, writable: true);
            if (subKey != null)
            {
                RestoreSnapshot(subKey, subKeySnapshot);
            }
        }
    }

    private sealed class RegistryKeySnapshot
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<string, (object? Value, RegistryValueKind Kind)> Values { get; } = [];

        public List<RegistryKeySnapshot> SubKeys { get; } = [];
    }
}
