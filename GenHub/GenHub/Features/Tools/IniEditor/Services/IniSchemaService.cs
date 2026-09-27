using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Tools.IniEditor;
using GenHub.Core.Models.Tools.IniEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GenHub.Features.Tools.IniEditor.Services;

/// <summary>
/// Schema metadata for known Generals and Zero Hour INI block types,
/// grounded in the public generalsgamecode INI parser and block tables.
/// Unknown blocks and fields remain editable generically.
/// </summary>
public sealed class IniSchemaService : IIniSchemaService
{
    private readonly Dictionary<string, IniBlockSchema> _schemas;

    /// <summary>
    /// Initializes a new instance of the <see cref="IniSchemaService"/> class.
    /// </summary>
    public IniSchemaService()
    {
        var schemas = new List<IniBlockSchema>
        {
            new(
                IniConstants.BlockTypes.Object,
                "Playable or static game object: health, cost, armor, weapons, upgrades, and art.",
                [
                    new("DisplayName", "String table label shown in the UI.", false),
                    new("Side", "Faction side for the object.", false),
                    new("EditorSorting", "Object editor category and sort key.", false),
                    new("BuildCost", "Command center build cost.", true),
                    new("BuildTime", "Build time in seconds.", true),
                    new("Health", "Hit points.", true),
                    new("MaxHealth", "Maximum hit points.", true),
                    new("ArmorSet", "Armor set condition reference.", false),
                    new("WeaponSet", "Weapon set condition reference.", false),
                    new("CommandSet", "Command set shown on the command bar.", false),
                    new("Upgrades", "Required or granted upgrades.", false),
                    new("Prerequisites", "Required objects or science.", false),
                    new("Science", "Required science.", false),
                    new("TransportSlotCount", "Passenger slots.", true),
                    new("VisionRange", "Sight range.", true),
                    new("ShroudClearingRange", "Shroud clearing range.", true),
                    new("MaxSimultaneousOfType", "Build limit.", true),
                    new("Scale", "Model scale.", true),
                    new("Speed", "Locomotor speed override.", true),
                    new("TurnRate", "Turn rate.", true),
                    new("CrusherLevel", "Crush level versus infantry.", true),
                    new("CrushableLevel", "Crushability of this object.", true),
                    new("KindOf", "Object kind flags.", false),
                    new("Body", "Body module reference.", false),
                    new("Behavior", "Behavior module declaration.", false),
                    new("Draw", "Draw module declaration.", false),
                    new("ClientUpdate", "Client update module declaration.", false),
                    new("Geometry", "Geometry and shadow settings.", false),
                    new("GeometryMajorRadius", "Selection geometry radius.", true),
                    new("VoiceSelect", "Selection voice.", false),
                    new("VoiceMove", "Move order voice.", false),
                    new("VoiceAttack", "Attack order voice.", false),
                    new("UnitSpecificSounds", "Unit sound set.", false),
                    new("Armor", "Inline armor table reference.", false),
                    new("Icon", "Command bar icon reference.", false),
                    new("ButtonImage", "Command button image.", false),
                    new("UpgradeCameo1", "Upgrade cameo icons.", false),
                    new("UpgradeCameo2", "Upgrade cameo icons.", false),
                    new("UpgradeCameo3", "Upgrade cameo icons.", false),
                    new("UpgradeCameo4", "Upgrade cameo icons.", false),
                    new("UpgradeCameo5", "Upgrade cameo icons.", false),
                ]),
            new(
                IniConstants.BlockTypes.Weapon,
                "Weapon definition: damage, range, rate of fire, and effects.",
                [
                    new("PrimaryDamage", "Base damage per hit.", true),
                    new("PrimaryDamageRadius", "Splash radius.", true),
                    new("SecondaryDamage", "Secondary damage.", true),
                    new("SecondaryDamageRadius", "Secondary splash radius.", true),
                    new("AttackRange", "Maximum range.", true),
                    new("MinimumAttackRange", "Minimum range.", true),
                    new("DelayBetweenShots", "Delay between shots in milliseconds.", true),
                    new("ClipSize", "Shots per clip.", true),
                    new("ClipReloadTime", "Clip reload time in milliseconds.", true),
                    new("AutoReloadsClip", "Automatic clip reload behavior.", false),
                    new("DamageType", "Damage type applied on hit.", false),
                    new("DeathType", "Death type applied on kill.", false),
                    new("WeaponSpeed", "Projectile speed.", true),
                    new("RadiusDamageAffects", "Which objects splash affects.", false),
                    new("DamageScalar", "Damage scalar versus armor.", true),
                    new("AntiAirborneVehicle", "Anti-air tuning.", false),
                    new("AntiAirborneInfantry", "Anti-air versus infantry.", false),
                    new("AntiGround", "Anti-ground tuning.", false),
                    new("ProjectileObject", "Projectile object fired.", false),
                    new("FireFX", "Muzzle effect.", false),
                    new("HitGroundFX", "Ground hit effect.", false),
                    new("HitObjectFX", "Object hit effect.", false),
                    new("LaserName", "Laser effect.", false),
                    new("WeaponBonus", "Weapon bonus versus target type.", false),
                    new("HistoricBonusTime", "Veterancy bonus window.", true),
                    new("HistoricBonusCount", "Veterancy bonus kills.", true),
                    new("HistoricBonusMultiplier", "Veterancy bonus multiplier.", true),
                    new("ScatterRadius", "Scatter radius.", true),
                    new("AcceptableAimDelta", "Aim tolerance.", true),
                ]),
            new(
                IniConstants.BlockTypes.Armor,
                "Armor table: damage multiplier per damage type.",
                IniConstants.DamageTypes.All.Select(type => new IniFieldSchema(type, $"Damage multiplier versus {type}.", true)).ToArray()),
            new(
                IniConstants.BlockTypes.ArmorSet,
                "Armor set: selects an Armor table when conditions hold.",
                [
                    new("Conditions", "Veterancy and upgrade conditions.", false),
                    new("Armor", "Armor table used when conditions hold.", false),
                    new("DamageFX", "Damage effect set.", false),
                ]),
            new(
                IniConstants.BlockTypes.WeaponSet,
                "Weapon set: selects weapon slots when conditions hold.",
                [
                    new("Conditions", "Veterancy and upgrade conditions.", false),
                    new("PRIMARY", "Primary weapon slot.", false),
                    new("SECONDARY", "Secondary weapon slot.", false),
                    new("TERTIARY", "Tertiary weapon slot.", false),
                    new("WeaponLock", "Locked weapon behavior.", false),
                    new("AutoChooseSources", "Automatic weapon source selection.", false),
                ]),
            new(
                IniConstants.BlockTypes.CommandButton,
                "Command bar button: icon, label, and triggered action.",
                [
                    new("Command", "Button action.", false),
                    new("Object", "Object created or targeted.", false),
                    new("Upgrade", "Upgrade researched or required.", false),
                    new("Science", "Science researched or required.", false),
                    new("SpecialPower", "Special power triggered.", false),
                    new("TextLabel", "String table label.", false),
                    new("ButtonImage", "Button icon texture.", false),
                    new("ButtonBorderType", "Button border art.", false),
                    new("DescriptLabel", "Description string table label.", false),
                    new("Radial", "Radial button behavior.", false),
                    new("InPalantir", "Palantir visibility.", false),
                    new("NeedUpgrade", "Required upgrade.", false),
                    new("NeedScience", "Required science.", false),
                    new("Options", "Button options.", false),
                    new("Cursor", "Cursor shown while targeting.", false),
                    new("InvalidCursor", "Cursor for invalid targets.", false),
                    new("LacksPrerequisiteLabel", "Missing prerequisite label.", false),
                ]),
            new(
                IniConstants.BlockTypes.CommandSet,
                "Command set: ordered command bar buttons.",
                [
                    new("1", "Command bar slot 1.", false),
                    new("2", "Command bar slot 2.", false),
                    new("3", "Command bar slot 3.", false),
                    new("4", "Command bar slot 4.", false),
                    new("5", "Command bar slot 5.", false),
                    new("6", "Command bar slot 6.", false),
                    new("7", "Command bar slot 7.", false),
                    new("8", "Command bar slot 8.", false),
                    new("9", "Command bar slot 9.", false),
                    new("10", "Command bar slot 10.", false),
                    new("11", "Command bar slot 11.", false),
                    new("12", "Command bar slot 12.", false),
                    new("13", "Command bar slot 13.", false),
                    new("14", "Command bar slot 14.", false),
                ]),
            new(
                IniConstants.BlockTypes.Upgrade,
                "Upgrade: researchable technology gating objects and weapons.",
                [
                    new("Type", "Upgrade scope.", false),
                    new("BuildCost", "Research cost.", true),
                    new("BuildTime", "Research time in seconds.", true),
                    new("DisplayName", "String table label.", false),
                    new("ButtonImage", "Research button icon.", false),
                    new("UpgradeCameo1", "Cameo icons.", false),
                    new("UpgradeCameo2", "Cameo icons.", false),
                    new("UpgradeCameo3", "Cameo icons.", false),
                    new("ResearchSound", "Research sound.", false),
                    new("UnitSpecificSounds", "Unit sound set.", false),
                    new("SkirmishAIHeuristic", "AI research weight.", false),
                    new("PersistsInCampaign", "Campaign persistence.", false),
                ]),
            new(
                IniConstants.BlockTypes.Science,
                "Science: faction technology prerequisite.",
                [
                    new("DisplayName", "String table label.", false),
                    new("Description", "String table description.", false),
                    new("PrerequisiteSciences", "Required sciences.", false),
                    new("SciencePurchasePointCost", "Science point cost.", true),
                    new("IsGrantable", "Grantable to other players.", false),
                    new("ButtonImage", "Science button icon.", false),
                ]),
            new(
                IniConstants.BlockTypes.SpecialPower,
                "Special power: targeted superweapon or support ability.",
                [
                    new("ReloadTime", "Recharge time in milliseconds.", true),
                    new("RadiusCursor", "Targeting cursor radius.", true),
                    new("DisplayName", "String table label.", false),
                    new("ButtonImage", "Button icon.", false),
                    new("Cursor", "Targeting cursor.", false),
                    new("InvalidCursor", "Invalid target cursor.", false),
                    new("ViewObject", "View object.", false),
                    new("OCL", "Object creation list.", false),
                    new("ChangeWeapon", "Weapon swap while active.", false),
                    new("PublicTimer", "Shared timer behavior.", false),
                    new("SharedSyncedTimer", "Synced timer behavior.", false),
                    new("ShortcutPower", "Shortcut power.", false),
                ]),
            new(
                IniConstants.BlockTypes.Locomotor,
                "Locomotor: movement speeds and terrain handling.",
                [
                    new("Speed", "Top speed.", true),
                    new("MinSpeed", "Minimum speed.", true),
                    new("Acceleration", "Acceleration.", true),
                    new("Braking", "Braking rate.", true),
                    new("TurnRate", "Turn rate.", true),
                    new("MaxTurnRate", "Maximum turn rate.", true),
                    new("Lift", "Hover lift.", true),
                    new("HoverHeight", "Hover height.", true),
                    new("CrusherLevel", "Crush level.", true),
                    new("CrushableLevel", "Crushability.", true),
                    new("CanMoveOverRubber", "Rubble traversal.", false),
                    new("Appearance", "Movement appearance.", false),
                    new("AccelerationPitch", "Pitch under acceleration.", true),
                    new("AllowAirborne", "Airborne movement.", false),
                    new("Surfaces", "Allowed surfaces.", false),
                    new("GroupMovement", "Group movement behavior.", false),
                    new("StickToGround", "Ground sticking.", false),
                ]),
            new(
                IniConstants.BlockTypes.ObjectCreationList,
                "Object creation list: nuggets spawning objects and effects.",
                [
                    new("CreateObject", "Object spawned by the list.", false),
                    new("Disposition", "Spawn disposition.", false),
                    new("Count", "Spawn count.", true),
                    new("SpreadFormation", "Formation spread.", false),
                    new("MinDistanceA", "Minimum distance.", true),
                    new("MaxDistanceA", "Maximum distance.", true),
                    new("OffsetA", "Spawn offset.", false),
                    new("MinDistanceB", "Secondary minimum distance.", true),
                    new("MaxDistanceB", "Secondary maximum distance.", true),
                    new("OffsetB", "Secondary offset.", false),
                    new("IgnorePrimaryObstacles", "Obstacle handling.", false),
                    new("IgnoreSecondaryObstacles", "Secondary obstacle handling.", false),
                    new("StartingWeapon", "Starting weapon override.", false),
                    new("InheritVeterancy", "Veterancy inheritance.", false),
                    new("DeliverPayload", "Payload delivery.", false),
                ]),
            new(
                IniConstants.BlockTypes.DamageFX,
                "Damage effects: throb, particles, and detail for damage states.",
                [
                    new("Throb", "Throb behavior.", false),
                    new("ThrobRate", "Throb rate.", true),
                    new("ThrobIntensity", "Throb intensity.", true),
                    new("Particle", "Damage particle.", false),
                    new("DamageParticle", "Damage particle system.", false),
                    new("Fire", "Fire effect.", false),
                    new("Smoke", "Smoke effect.", false),
                    new("Sparks", "Spark effect.", false),
                    new("DamageTypes", "Damage types covered.", false),
                    new("Veterancy", "Veterancy level.", false),
                ]),
            new(
                IniConstants.BlockTypes.PlayerTemplate,
                "Player template: starting units, money, and faction defaults.",
                [
                    new("Side", "Faction side.", false),
                    new("PlayableSide", "Playable side flag.", false),
                    new("StartMoney", "Starting money.", true),
                    new("PreferredColor", "Preferred color.", false),
                    new("StartingBuilding", "Starting building.", false),
                    new("StartingUnit0", "Starting units.", false),
                    new("StartingUnit1", "Starting units.", false),
                    new("StartingUnit2", "Starting units.", false),
                    new("StartingUnitTanks", "Starting tanks.", false),
                    new("StartingUnitAir", "Starting aircraft.", false),
                    new("SciencePurchasePoints", "Science points.", true),
                    new("DisplayName", "String table label.", false),
                    new("StartingTaunt", "Starting taunt.", false),
                ]),
            new(
                IniConstants.BlockTypes.FactionTemplate,
                "Faction template: arms dealer and superweapon defaults.",
                [
                    new("DisplayName", "String table label.", false),
                    new("ArmsDealer", "Arms dealer template.", false),
                    new("StartingBuilding", "Starting building.", false),
                    new("Superweapon", "Superweapon.", false),
                ]),
            new(
                IniConstants.BlockTypes.ExperienceLevels,
                "Experience levels: veterancy thresholds and multipliers.",
                [
                    new("1", "Level 1 experience.", false),
                    new("2", "Level 2 experience.", false),
                    new("3", "Level 3 experience.", false),
                    new("ExperienceScalar", "Experience scalar.", true),
                    new("InformSecondLevel", "Second level notification.", false),
                ]),
            new(
                IniConstants.BlockTypes.Veterancy,
                "Veterancy multipliers for a damage or armor context.",
                [
                    new("VETERAN", "Veteran multipliers.", false),
                    new("ELITE", "Elite multipliers.", false),
                    new("HEROIC", "Heroic multipliers.", false),
                ]),
        };

        _schemas = schemas.ToDictionary(schema => schema.BlockType, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<IniBlockSchema> BlockSchemas => _schemas.Values.ToList();

    /// <inheritdoc />
    public IniBlockSchema? GetBlockSchema(string blockType)
    {
        ArgumentNullException.ThrowIfNull(blockType);
        return _schemas.TryGetValue(blockType, out var schema) ? schema : null;
    }

    /// <inheritdoc />
    public bool TryGetField(string blockType, string key, out IniFieldSchema? schema)
    {
        ArgumentNullException.ThrowIfNull(blockType);
        ArgumentNullException.ThrowIfNull(key);
        schema = null;
        if (!_schemas.TryGetValue(blockType, out var block))
        {
            return false;
        }

        foreach (var field in block.Fields)
        {
            if (string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                schema = field;
                return true;
            }
        }

        return false;
    }
}
