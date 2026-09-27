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
        /// All Zero Hour damage types in engine order, matching
        /// <c>DamageTypeFlags::s_bitNameList</c> in the public game code.
        /// Original Generals additionally defines <c>FLESHY_SNIPER</c>.
        /// </summary>
        public static readonly string[] All =
        [
            "EXPLOSION", "CRUSH", "ARMOR_PIERCING", "SMALL_ARMS", "GATTLING",
            "RADIATION", "FLAME", "LASER", "SNIPER", "POISON", "HEALING",
            "UNRESISTABLE", "WATER", "DEPLOY", "SURRENDER", "HACK", "KILL_PILOT",
            "PENALTY", "FALLING", "MELEE", "DISARM", "HAZARD_CLEANUP",
            "PARTICLE_BEAM", "TOPPLING", "INFANTRY_MISSILE", "AURORA_BOMB",
            "LAND_MINE", "JET_MISSILES", "STEALTHJET_MISSILES", "MOLOTOV_COCKTAIL",
            "COMANCHE_VULCAN", "SUBDUAL_MISSILE", "SUBDUAL_VEHICLE",
            "SUBDUAL_BUILDING", "SUBDUAL_UNRESISTABLE", "MICROWAVE",
            "KILL_GARRISONED", "STATUS",
        ];
    }

    /// <summary>
    /// Well known INI field keys shared by the schema and the editor.
    /// </summary>
    public static class FieldKeys
    {
        /// <summary>Display name key.</summary>
        public const string DisplayName = "DisplayName";

        /// <summary>Button image key.</summary>
        public const string ButtonImage = "ButtonImage";

        /// <summary>Build cost key.</summary>
        public const string BuildCost = "BuildCost";

        /// <summary>Build time key.</summary>
        public const string BuildTime = "BuildTime";

        /// <summary>Damage type key.</summary>
        public const string DamageType = "DamageType";

        /// <summary>Primary damage key.</summary>
        public const string PrimaryDamage = "PrimaryDamage";

        /// <summary>Primary damage radius key.</summary>
        public const string PrimaryDamageRadius = "PrimaryDamageRadius";

        /// <summary>Death type key.</summary>
        public const string DeathType = "DeathType";

        /// <summary>Upgrade key.</summary>
        public const string Upgrade = "Upgrade";

        /// <summary>Triggered by key.</summary>
        public const string TriggeredBy = "TriggeredBy";
    }

    /// <summary>
    /// Editor limits.
    /// </summary>
    public static class Editor
    {
        /// <summary>Maximum undo history entries.</summary>
        public const int MaxUndoHistory = 200;

        /// <summary>Maximum explorer depth when listing INI files.</summary>
        public const int MaxExplorerDepth = 20;

        /// <summary>Debounce delay before refreshing previews after an edit, in milliseconds.</summary>
        public const int PreviewRefreshDebounceMs = 250;

        /// <summary>Debounce delay before applying the block filter, in milliseconds.</summary>
        public const int FilterDebounceMs = 200;
    }
}
