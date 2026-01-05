namespace GenHub.Core.Constants;

/// <summary>
/// Constants for Windows Registry keys and values.
/// </summary>
public static class RegistryConstants
{
    // ===== EA App / Origin Keys =====

    /// <summary>Registry key path for Generals command and conquer.</summary>
    public const string EAAppGeneralsKeyPath = @"SOFTWARE\Electronic Arts\EA Games\Generals";

    /// <summary>Registry key path for Zero Hour.</summary>
    public const string EAAppZeroHourKeyPath = @"SOFTWARE\Electronic Arts\EA Games\Command and Conquer Generals Zero Hour";

    /// <summary>Registry key path for Generals Ergc (Serial).</summary>
    public const string EAAppGeneralsErgcKeyPath = @"SOFTWARE\Electronic Arts\EA Games\Generals\ergc";

    /// <summary>Registry key path for Zero Hour Ergc (Serial).</summary>
    public const string EAAppZeroHourErgcKeyPath = @"SOFTWARE\Electronic Arts\EA Games\Command and Conquer Generals Zero Hour\ergc";

    // ===== VCRedist Keys =====

    /// <summary>Registry key for VCRedist 2010 x86 (32-bit).</summary>
    public const string VCRedist2010x86Key = @"SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist\x86";

    /// <summary>Registry key for VCRedist 2010 x86 (64-bit environment / WOW6432Node).</summary>
    public const string VCRedist2010x86KeyWow64 = @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\10.0\VC\VCRedist\x86";

    // ===== Value Names =====

    /// <summary>Registry value name for 'Install Path'.</summary>
    public const string InstallPathValueName = "Install Path";

    /// <summary>Registry value name for 'Version'.</summary>
    public const string VersionValueName = "Version";

    /// <summary>Registry value name for 'Installed'.</summary>
    public const string InstalledValueName = "Installed";

    // ===== Registry Versions (DWORD) =====

    /// <summary>Registry version for Generals 1.08 (0x10008).</summary>
    public const int GeneralsVersionDWord = 0x10008;

    /// <summary>Registry version for Zero Hour 1.04 (0x10004).</summary>
    public const int ZeroHourVersionDWord = 0x10004;
}
