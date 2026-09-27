namespace GenHub.Core.Constants;

/// <summary>
/// Constants for Generals and Zero Hour INI data files
/// (game objects, weapons, upgrades, damage, armor, command sets).
/// Syntax follows the engine INI parser: semicolon comments, no tab characters,
/// blocks opened by a block type line and closed by <c>End</c>.
/// </summary>
public static class IniConstants
{
    /// <summary>
    /// Block tag literals.
    /// </summary>
    public static class BlockTags
    {
        /// <summary>Closes an INI block.</summary>
        public const string End = "End";
    }

    /// <summary>
    /// Statement syntax constants.
    /// </summary>
    public static class Syntax
    {
        /// <summary>Starts a comment running to the end of the line.</summary>
        public const char Comment = ';';

        /// <summary>Separates a key from its value.</summary>
        public const char KeyValueSeparator = '=';

        /// <summary>Canonical newline used when writing files.</summary>
        public const string NewLine = "\r\n";
    }

    /// <summary>
    /// File level constants. The extension itself is reused from
    /// <see cref="ModBuilderConstants.FileExtensions.Ini"/>; no duplicate literal lives here.
    /// </summary>
    public static class File
    {
        /// <summary>Search pattern for INI data files.</summary>
        public const string SearchPattern = "*.ini";
    }

    /// <summary>
    /// Well known INI block types parsed with schema assistance.
    /// Unknown block types are preserved and edited generically.
    /// </summary>
    public static class BlockTypes
    {
        /// <summary>Defines a game object.</summary>
        public const string Object = "Object";

        /// <summary>Defines a weapon.</summary>
        public const string Weapon = "Weapon";

        /// <summary>Defines armor damage multipliers.</summary>
        public const string Armor = "Armor";

        /// <summary>Defines an armor set condition.</summary>
        public const string ArmorSet = "ArmorSet";

        /// <summary>Defines an object weapon set condition.</summary>
        public const string WeaponSet = "WeaponSet";

        /// <summary>Defines a command button.</summary>
        public const string CommandButton = "CommandButton";

        /// <summary>Defines a command set.</summary>
        public const string CommandSet = "CommandSet";

        /// <summary>Defines an upgrade.</summary>
        public const string Upgrade = "Upgrade";

        /// <summary>Defines a science.</summary>
        public const string Science = "Science";

        /// <summary>Defines a special power.</summary>
        public const string SpecialPower = "SpecialPower";

        /// <summary>Defines a locomotor.</summary>
        public const string Locomotor = "Locomotor";

        /// <summary>Defines an object creation list.</summary>
        public const string ObjectCreationList = "ObjectCreationList";

        /// <summary>Defines damage effects.</summary>
        public const string DamageFX = "DamageFX";

        /// <summary>Defines a player template.</summary>
        public const string PlayerTemplate = "PlayerTemplate";

        /// <summary>Defines a faction template.</summary>
        public const string FactionTemplate = "FactionTemplate";

        /// <summary>Defines experience levels.</summary>
        public const string ExperienceLevels = "ExperienceLevels";

        /// <summary>Defines veterancy multipliers.</summary>
        public const string Veterancy = "Veterancy";

        /// <summary>
        /// All block types with schema assistance.
        /// </summary>
        public static readonly string[] All =
        [
            Object, Weapon, Armor, ArmorSet, WeaponSet, CommandButton, CommandSet,
            Upgrade, Science, SpecialPower, Locomotor, ObjectCreationList,
            DamageFX, PlayerTemplate, FactionTemplate, ExperienceLevels, Veterancy,
        ];
    }

    /// <summary>
    /// Damage type names from the engine damage system.
    /// </summary>
    public static class DamageTypes
    {
        /// <summary>
        /// All damage types in engine canonical order.
        /// </summary>
        public static readonly string[] All =
        [
            "EXPLOSION", "CRUSH", "ARMOR_PIERCING", "SMALL_ARMS", "GATTLING",
            "RADIATION", "FLAME", "LASER", "SNIPER", "POISON", "HEALING",
            "UNRESISTABLE", "WATER", "DEPLOY", "SURRENDER", "HACK", "KILL_PILOT",
            "PLOW", "MELEE", "FALLING", "VIS", "PARTICLE_BEAM", "IGNITION",
            "NUKE", "MINE", "BOOBY_TRAP", "HAZARD", "STEALTH", "LAND_MINE",
            "AIRCRAFT_MISSILE", "JET_MISSILES", "CANNON", "ROCKET", "GUN",
            "RIFLE", "PISTOL", "HAND_GUN",
        ];
    }

    /// <summary>
    /// Weapon slot names used inside WeaponSet conditions.
    /// </summary>
    public static class WeaponSlots
    {
        /// <summary>Primary weapon slot.</summary>
        public const string Primary = "PRIMARY";

        /// <summary>Secondary weapon slot.</summary>
        public const string Secondary = "SECONDARY";

        /// <summary>Tertiary weapon slot.</summary>
        public const string Tertiary = "TERTIARY";

        /// <summary>
        /// All weapon slots.
        /// </summary>
        public static readonly string[] All = [Primary, Secondary, Tertiary];
    }

    /// <summary>
    /// Editor limits.
    /// </summary>
    public static class Editor
    {
        /// <summary>Maximum undo history entries.</summary>
        public const int MaxUndoHistory = 200;
    }
}
