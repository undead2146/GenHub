namespace GenHub.Core.Constants;

/// <summary>
/// Centralized constants for ActionSet fixes, registry keys, and file operations.
/// </summary>
public static class ActionSetConstants
{
    // RegistryKeys moved to GenHub.Core.Constants.RegistryConstants.cs

    /// <summary>
    /// File names and content.
    /// </summary>
    public static class FileNames
    {
        public const string DesktopIni = "desktop.ini";
        public const string GeneralsExe = "generals.exe";
        public const string GameDat = "Game.dat";
        public const string GameExe = "game.exe"; // Often used for ZH
    }

    /// <summary>
    /// Initialization file sections and keys.
    /// </summary>
    public static class IniFiles
    {
        // Sections
        public const string ShellClassInfoSection = "[.ShellClassInfo]";
        public const string TheSuperHackersSection = "TheSuperHackers";

        // Keys
        public const string ThisPCPolicyKey = "ThisPCPolicy";
        public const string ThisPCPolicyValue = "DisableCloudSync";
        public const string ConfirmFileOpKey = "ConfirmFileOp";

        // TheSuperHackers keys
        public const string ScrollEdgeZoneKey = "ScrollEdgeZone";
        public const string ScrollEdgeSpeedKey = "ScrollEdgeSpeed";
        public const string ScrollEdgeAccelerationKey = "ScrollEdgeAcceleration";
    }

    /// <summary>
    /// Firewall rule names and protocols.
    /// </summary>
    public static class FirewallRules
    {
        public const string Prefix = "GP"; // Compatibility with GenPatcher

        public const string PortRuleUdp16000 = "GP Open UDP Port 16000";
        public const string PortRuleUdp16001 = "GP Open UDP Port 16001";
        public const string PortRuleTcp16001 = "GP Open TCP Port 16001";

        public const string GeneralsRule = "GP Command & Conquer Generals";
        public const string GeneralsGameDatRule = "GP Command & Conquer Generals Game.dat";
        public const string ZeroHourRule = "GP Command & Conquer Generals Zero Hour";
        public const string ZeroHourGameDatRule = "GP Command & Conquer Generals Zero Hour Game.dat";

        public const string ProtocolUdp = "UDP";
        public const string ProtocolTcp = "TCP";
    }

    /// <summary>
    /// Constants for Malwarebytes detection and paths.
    /// </summary>
    public static class Malwarebytes
    {
        public const string RegistryUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        public const string DisplayNameValue = "DisplayName";
        public const string NameContains = "Malwarebytes";

        public static readonly string[] ExecutablePaths =
        [
            Path.Combine("Malwarebytes", "Anti-Malware", "mbam.exe"),
            Path.Combine("Malwarebytes", "Anti-Malware", "mbamtray.exe")
        ];
    }
}
