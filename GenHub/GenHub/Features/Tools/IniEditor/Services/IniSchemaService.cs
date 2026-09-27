using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Tools.IniEditor;
using GenHub.Core.Models.Tools.IniEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace GenHub.Features.Tools.IniEditor.Services;

/// <summary>
/// Schema metadata for known Generals and Zero Hour INI block types,
/// grounded in the public generalsgamecode INI parser and block tables.
/// Unknown blocks and fields remain editable generically.
/// </summary>
public sealed class IniSchemaService : IIniSchemaService
{
    private sealed record SchemaFieldEntry(string Key, string DescriptionKey, bool IsNumeric);

    private sealed record SchemaBlockEntry(
        string BlockType,
        string DescriptionKey,
        SchemaFieldEntry[] Fields,
        string? GeneratedDescriptionKey = null);

    private static readonly SchemaBlockEntry[] SchemaTables =
    [
            new(
                IniConstants.BlockTypes.Object,
                "Tools.IniEditor.Schema.Blocks.Object",
                [
                    new(IniConstants.FieldKeys.DisplayName, "Tools.IniEditor.Schema.Fields.Object.DisplayName", false),
                    new("Side", "Tools.IniEditor.Schema.Fields.Object.Side", false),
                    new("EditorSorting", "Tools.IniEditor.Schema.Fields.Object.EditorSorting", false),
                    new(IniConstants.FieldKeys.BuildCost, "Tools.IniEditor.Schema.Fields.Object.BuildCost", true),
                    new(IniConstants.FieldKeys.BuildTime, "Tools.IniEditor.Schema.Fields.Object.BuildTime", true),
                    new("Health", "Tools.IniEditor.Schema.Fields.Object.Health", true),
                    new("MaxHealth", "Tools.IniEditor.Schema.Fields.Object.MaxHealth", true),
                    new("ArmorSet", "Tools.IniEditor.Schema.Fields.Object.ArmorSet", false),
                    new("WeaponSet", "Tools.IniEditor.Schema.Fields.Object.WeaponSet", false),
                    new("CommandSet", "Tools.IniEditor.Schema.Fields.Object.CommandSet", false),
                    new("Upgrades", "Tools.IniEditor.Schema.Fields.Object.Upgrades", false),
                    new("Prerequisites", "Tools.IniEditor.Schema.Fields.Object.Prerequisites", false),
                    new("Science", "Tools.IniEditor.Schema.Fields.Object.Science", false),
                    new("TransportSlotCount", "Tools.IniEditor.Schema.Fields.Object.TransportSlotCount", true),
                    new("VisionRange", "Tools.IniEditor.Schema.Fields.Object.VisionRange", true),
                    new("ShroudClearingRange", "Tools.IniEditor.Schema.Fields.Object.ShroudClearingRange", true),
                    new("MaxSimultaneousOfType", "Tools.IniEditor.Schema.Fields.Object.MaxSimultaneousOfType", true),
                    new("Scale", "Tools.IniEditor.Schema.Fields.Object.Scale", true),
                    new("Speed", "Tools.IniEditor.Schema.Fields.Object.Speed", true),
                    new("TurnRate", "Tools.IniEditor.Schema.Fields.Object.TurnRate", true),
                    new("CrusherLevel", "Tools.IniEditor.Schema.Fields.Object.CrusherLevel", true),
                    new("CrushableLevel", "Tools.IniEditor.Schema.Fields.Object.CrushableLevel", true),
                    new("KindOf", "Tools.IniEditor.Schema.Fields.Object.KindOf", false),
                    new("Body", "Tools.IniEditor.Schema.Fields.Object.Body", false),
                    new("Behavior", "Tools.IniEditor.Schema.Fields.Object.Behavior", false),
                    new("Draw", "Tools.IniEditor.Schema.Fields.Object.Draw", false),
                    new("ClientUpdate", "Tools.IniEditor.Schema.Fields.Object.ClientUpdate", false),
                    new("Geometry", "Tools.IniEditor.Schema.Fields.Object.Geometry", false),
                    new("GeometryMajorRadius", "Tools.IniEditor.Schema.Fields.Object.GeometryMajorRadius", true),
                    new("VoiceSelect", "Tools.IniEditor.Schema.Fields.Object.VoiceSelect", false),
                    new("VoiceMove", "Tools.IniEditor.Schema.Fields.Object.VoiceMove", false),
                    new("VoiceAttack", "Tools.IniEditor.Schema.Fields.Object.VoiceAttack", false),
                    new("UnitSpecificSounds", "Tools.IniEditor.Schema.Fields.Object.UnitSpecificSounds", false),
                    new("Armor", "Tools.IniEditor.Schema.Fields.Object.Armor", false),
                    new("Icon", "Tools.IniEditor.Schema.Fields.Object.Icon", false),
                    new(IniConstants.FieldKeys.ButtonImage, "Tools.IniEditor.Schema.Fields.Object.ButtonImage", false),
                    new("UpgradeCameo1", "Tools.IniEditor.Schema.Fields.Object.UpgradeCameo1", false),
                    new("UpgradeCameo2", "Tools.IniEditor.Schema.Fields.Object.UpgradeCameo2", false),
                    new("UpgradeCameo3", "Tools.IniEditor.Schema.Fields.Object.UpgradeCameo3", false),
                    new("UpgradeCameo4", "Tools.IniEditor.Schema.Fields.Object.UpgradeCameo4", false),
                    new("UpgradeCameo5", "Tools.IniEditor.Schema.Fields.Object.UpgradeCameo5", false),
                ]),
            new(
                IniConstants.BlockTypes.Weapon,
                "Tools.IniEditor.Schema.Blocks.Weapon",
                [
                    new(IniConstants.FieldKeys.PrimaryDamage, "Tools.IniEditor.Schema.Fields.Weapon.PrimaryDamage", true),
                    new(IniConstants.FieldKeys.PrimaryDamageRadius, "Tools.IniEditor.Schema.Fields.Weapon.PrimaryDamageRadius", true),
                    new("SecondaryDamage", "Tools.IniEditor.Schema.Fields.Weapon.SecondaryDamage", true),
                    new("SecondaryDamageRadius", "Tools.IniEditor.Schema.Fields.Weapon.SecondaryDamageRadius", true),
                    new("AttackRange", "Tools.IniEditor.Schema.Fields.Weapon.AttackRange", true),
                    new("MinimumAttackRange", "Tools.IniEditor.Schema.Fields.Weapon.MinimumAttackRange", true),
                    new("DelayBetweenShots", "Tools.IniEditor.Schema.Fields.Weapon.DelayBetweenShots", true),
                    new("ClipSize", "Tools.IniEditor.Schema.Fields.Weapon.ClipSize", true),
                    new("ClipReloadTime", "Tools.IniEditor.Schema.Fields.Weapon.ClipReloadTime", true),
                    new("AutoReloadsClip", "Tools.IniEditor.Schema.Fields.Weapon.AutoReloadsClip", false),
                    new(IniConstants.FieldKeys.DamageType, "Tools.IniEditor.Schema.Fields.Weapon.DamageType", false),
                    new(IniConstants.FieldKeys.DeathType, "Tools.IniEditor.Schema.Fields.Weapon.DeathType", false),
                    new("WeaponSpeed", "Tools.IniEditor.Schema.Fields.Weapon.WeaponSpeed", true),
                    new("RadiusDamageAffects", "Tools.IniEditor.Schema.Fields.Weapon.RadiusDamageAffects", false),
                    new("DamageScalar", "Tools.IniEditor.Schema.Fields.Weapon.DamageScalar", true),
                    new("AntiAirborneVehicle", "Tools.IniEditor.Schema.Fields.Weapon.AntiAirborneVehicle", false),
                    new("AntiAirborneInfantry", "Tools.IniEditor.Schema.Fields.Weapon.AntiAirborneInfantry", false),
                    new("AntiGround", "Tools.IniEditor.Schema.Fields.Weapon.AntiGround", false),
                    new("ProjectileObject", "Tools.IniEditor.Schema.Fields.Weapon.ProjectileObject", false),
                    new("FireFX", "Tools.IniEditor.Schema.Fields.Weapon.FireFX", false),
                    new("HitGroundFX", "Tools.IniEditor.Schema.Fields.Weapon.HitGroundFX", false),
                    new("HitObjectFX", "Tools.IniEditor.Schema.Fields.Weapon.HitObjectFX", false),
                    new("LaserName", "Tools.IniEditor.Schema.Fields.Weapon.LaserName", false),
                    new("WeaponBonus", "Tools.IniEditor.Schema.Fields.Weapon.WeaponBonus", false),
                    new("HistoricBonusTime", "Tools.IniEditor.Schema.Fields.Weapon.HistoricBonusTime", true),
                    new("HistoricBonusCount", "Tools.IniEditor.Schema.Fields.Weapon.HistoricBonusCount", true),
                    new("HistoricBonusMultiplier", "Tools.IniEditor.Schema.Fields.Weapon.HistoricBonusMultiplier", true),
                    new("ScatterRadius", "Tools.IniEditor.Schema.Fields.Weapon.ScatterRadius", true),
                    new("AcceptableAimDelta", "Tools.IniEditor.Schema.Fields.Weapon.AcceptableAimDelta", true),
                ]),
            new(
                IniConstants.BlockTypes.Armor,
                "Tools.IniEditor.Schema.Blocks.Armor",
                [],
                "Tools.IniEditor.Schema.Fields.Armor.DamageMultiplier"),
            new(
                IniConstants.BlockTypes.ArmorSet,
                "Tools.IniEditor.Schema.Blocks.ArmorSet",
                [
                    new("Conditions", "Tools.IniEditor.Schema.Fields.ArmorSet.Conditions", false),
                    new("Armor", "Tools.IniEditor.Schema.Fields.ArmorSet.Armor", false),
                    new("DamageFX", "Tools.IniEditor.Schema.Fields.ArmorSet.DamageFX", false),
                ]),
            new(
                IniConstants.BlockTypes.WeaponSet,
                "Tools.IniEditor.Schema.Blocks.WeaponSet",
                [
                    new("Conditions", "Tools.IniEditor.Schema.Fields.WeaponSet.Conditions", false),
                    new("Weapon", "Tools.IniEditor.Schema.Fields.WeaponSet.Weapon", false),
                    new("AutoChooseSources", "Tools.IniEditor.Schema.Fields.WeaponSet.AutoChooseSources", false),
                    new("PreferredAgainst", "Tools.IniEditor.Schema.Fields.WeaponSet.PreferredAgainst", false),
                    new("ShareWeaponReloadTime", "Tools.IniEditor.Schema.Fields.WeaponSet.ShareWeaponReloadTime", false),
                    new("WeaponLockSharedAcrossSets", "Tools.IniEditor.Schema.Fields.WeaponSet.WeaponLockSharedAcrossSets", false),
                ]),
            new(
                IniConstants.BlockTypes.CommandButton,
                "Tools.IniEditor.Schema.Blocks.CommandButton",
                [
                    new("Command", "Tools.IniEditor.Schema.Fields.CommandButton.Command", false),
                    new("Object", "Tools.IniEditor.Schema.Fields.CommandButton.Object", false),
                    new(IniConstants.FieldKeys.Upgrade, "Tools.IniEditor.Schema.Fields.CommandButton.Upgrade", false),
                    new("Science", "Tools.IniEditor.Schema.Fields.CommandButton.Science", false),
                    new("SpecialPower", "Tools.IniEditor.Schema.Fields.CommandButton.SpecialPower", false),
                    new("TextLabel", "Tools.IniEditor.Schema.Fields.CommandButton.TextLabel", false),
                    new(IniConstants.FieldKeys.ButtonImage, "Tools.IniEditor.Schema.Fields.CommandButton.ButtonImage", false),
                    new("ButtonBorderType", "Tools.IniEditor.Schema.Fields.CommandButton.ButtonBorderType", false),
                    new("DescriptLabel", "Tools.IniEditor.Schema.Fields.CommandButton.DescriptLabel", false),
                    new("Radial", "Tools.IniEditor.Schema.Fields.CommandButton.Radial", false),
                    new("InPalantir", "Tools.IniEditor.Schema.Fields.CommandButton.InPalantir", false),
                    new("NeedUpgrade", "Tools.IniEditor.Schema.Fields.CommandButton.NeedUpgrade", false),
                    new("NeedScience", "Tools.IniEditor.Schema.Fields.CommandButton.NeedScience", false),
                    new("Options", "Tools.IniEditor.Schema.Fields.CommandButton.Options", false),
                    new("Cursor", "Tools.IniEditor.Schema.Fields.CommandButton.Cursor", false),
                    new("InvalidCursor", "Tools.IniEditor.Schema.Fields.CommandButton.InvalidCursor", false),
                    new("LacksPrerequisiteLabel", "Tools.IniEditor.Schema.Fields.CommandButton.LacksPrerequisiteLabel", false),
                ]),
            new(
                IniConstants.BlockTypes.CommandSet,
                "Tools.IniEditor.Schema.Blocks.CommandSet",
                [
                    new("1", "Tools.IniEditor.Schema.Fields.CommandSet.1", false),
                    new("2", "Tools.IniEditor.Schema.Fields.CommandSet.2", false),
                    new("3", "Tools.IniEditor.Schema.Fields.CommandSet.3", false),
                    new("4", "Tools.IniEditor.Schema.Fields.CommandSet.4", false),
                    new("5", "Tools.IniEditor.Schema.Fields.CommandSet.5", false),
                    new("6", "Tools.IniEditor.Schema.Fields.CommandSet.6", false),
                    new("7", "Tools.IniEditor.Schema.Fields.CommandSet.7", false),
                    new("8", "Tools.IniEditor.Schema.Fields.CommandSet.8", false),
                    new("9", "Tools.IniEditor.Schema.Fields.CommandSet.9", false),
                    new("10", "Tools.IniEditor.Schema.Fields.CommandSet.10", false),
                    new("11", "Tools.IniEditor.Schema.Fields.CommandSet.11", false),
                    new("12", "Tools.IniEditor.Schema.Fields.CommandSet.12", false),
                    new("13", "Tools.IniEditor.Schema.Fields.CommandSet.13", false),
                    new("14", "Tools.IniEditor.Schema.Fields.CommandSet.14", false),
                ]),
            new(
                IniConstants.BlockTypes.Upgrade,
                "Tools.IniEditor.Schema.Blocks.Upgrade",
                [
                    new("Type", "Tools.IniEditor.Schema.Fields.Upgrade.Type", false),
                    new(IniConstants.FieldKeys.BuildCost, "Tools.IniEditor.Schema.Fields.Upgrade.BuildCost", true),
                    new(IniConstants.FieldKeys.BuildTime, "Tools.IniEditor.Schema.Fields.Upgrade.BuildTime", true),
                    new(IniConstants.FieldKeys.DisplayName, "Tools.IniEditor.Schema.Fields.Upgrade.DisplayName", false),
                    new(IniConstants.FieldKeys.ButtonImage, "Tools.IniEditor.Schema.Fields.Upgrade.ButtonImage", false),
                    new("UpgradeCameo1", "Tools.IniEditor.Schema.Fields.Upgrade.UpgradeCameo1", false),
                    new("UpgradeCameo2", "Tools.IniEditor.Schema.Fields.Upgrade.UpgradeCameo2", false),
                    new("UpgradeCameo3", "Tools.IniEditor.Schema.Fields.Upgrade.UpgradeCameo3", false),
                    new("ResearchSound", "Tools.IniEditor.Schema.Fields.Upgrade.ResearchSound", false),
                    new("UnitSpecificSounds", "Tools.IniEditor.Schema.Fields.Upgrade.UnitSpecificSounds", false),
                    new("SkirmishAIHeuristic", "Tools.IniEditor.Schema.Fields.Upgrade.SkirmishAIHeuristic", false),
                    new("PersistsInCampaign", "Tools.IniEditor.Schema.Fields.Upgrade.PersistsInCampaign", false),
                ]),
            new(
                IniConstants.BlockTypes.Science,
                "Tools.IniEditor.Schema.Blocks.Science",
                [
                    new(IniConstants.FieldKeys.DisplayName, "Tools.IniEditor.Schema.Fields.Science.DisplayName", false),
                    new("Description", "Tools.IniEditor.Schema.Fields.Science.Description", false),
                    new("PrerequisiteSciences", "Tools.IniEditor.Schema.Fields.Science.PrerequisiteSciences", false),
                    new("SciencePurchasePointCost", "Tools.IniEditor.Schema.Fields.Science.SciencePurchasePointCost", true),
                    new("IsGrantable", "Tools.IniEditor.Schema.Fields.Science.IsGrantable", false),
                    new(IniConstants.FieldKeys.ButtonImage, "Tools.IniEditor.Schema.Fields.Science.ButtonImage", false),
                ]),
            new(
                IniConstants.BlockTypes.SpecialPower,
                "Tools.IniEditor.Schema.Blocks.SpecialPower",
                [
                    new("ReloadTime", "Tools.IniEditor.Schema.Fields.SpecialPower.ReloadTime", true),
                    new("RadiusCursor", "Tools.IniEditor.Schema.Fields.SpecialPower.RadiusCursor", true),
                    new(IniConstants.FieldKeys.DisplayName, "Tools.IniEditor.Schema.Fields.SpecialPower.DisplayName", false),
                    new(IniConstants.FieldKeys.ButtonImage, "Tools.IniEditor.Schema.Fields.SpecialPower.ButtonImage", false),
                    new("Cursor", "Tools.IniEditor.Schema.Fields.SpecialPower.Cursor", false),
                    new("InvalidCursor", "Tools.IniEditor.Schema.Fields.SpecialPower.InvalidCursor", false),
                    new("ViewObject", "Tools.IniEditor.Schema.Fields.SpecialPower.ViewObject", false),
                    new("OCL", "Tools.IniEditor.Schema.Fields.SpecialPower.OCL", false),
                    new("ChangeWeapon", "Tools.IniEditor.Schema.Fields.SpecialPower.ChangeWeapon", false),
                    new("PublicTimer", "Tools.IniEditor.Schema.Fields.SpecialPower.PublicTimer", false),
                    new("SharedSyncedTimer", "Tools.IniEditor.Schema.Fields.SpecialPower.SharedSyncedTimer", false),
                    new("ShortcutPower", "Tools.IniEditor.Schema.Fields.SpecialPower.ShortcutPower", false),
                ]),
            new(
                IniConstants.BlockTypes.Locomotor,
                "Tools.IniEditor.Schema.Blocks.Locomotor",
                [
                    new("Speed", "Tools.IniEditor.Schema.Fields.Locomotor.Speed", true),
                    new("MinSpeed", "Tools.IniEditor.Schema.Fields.Locomotor.MinSpeed", true),
                    new("Acceleration", "Tools.IniEditor.Schema.Fields.Locomotor.Acceleration", true),
                    new("Braking", "Tools.IniEditor.Schema.Fields.Locomotor.Braking", true),
                    new("TurnRate", "Tools.IniEditor.Schema.Fields.Locomotor.TurnRate", true),
                    new("MaxTurnRate", "Tools.IniEditor.Schema.Fields.Locomotor.MaxTurnRate", true),
                    new("Lift", "Tools.IniEditor.Schema.Fields.Locomotor.Lift", true),
                    new("HoverHeight", "Tools.IniEditor.Schema.Fields.Locomotor.HoverHeight", true),
                    new("CrusherLevel", "Tools.IniEditor.Schema.Fields.Locomotor.CrusherLevel", true),
                    new("CrushableLevel", "Tools.IniEditor.Schema.Fields.Locomotor.CrushableLevel", true),
                    new("CanMoveOverRubber", "Tools.IniEditor.Schema.Fields.Locomotor.CanMoveOverRubber", false),
                    new("Appearance", "Tools.IniEditor.Schema.Fields.Locomotor.Appearance", false),
                    new("AccelerationPitch", "Tools.IniEditor.Schema.Fields.Locomotor.AccelerationPitch", true),
                    new("AllowAirborne", "Tools.IniEditor.Schema.Fields.Locomotor.AllowAirborne", false),
                    new("Surfaces", "Tools.IniEditor.Schema.Fields.Locomotor.Surfaces", false),
                    new("GroupMovement", "Tools.IniEditor.Schema.Fields.Locomotor.GroupMovement", false),
                    new("StickToGround", "Tools.IniEditor.Schema.Fields.Locomotor.StickToGround", false),
                ]),
            new(
                IniConstants.BlockTypes.ObjectCreationList,
                "Tools.IniEditor.Schema.Blocks.ObjectCreationList",
                [
                    new("CreateObject", "Tools.IniEditor.Schema.Fields.ObjectCreationList.CreateObject", false),
                    new("Disposition", "Tools.IniEditor.Schema.Fields.ObjectCreationList.Disposition", false),
                    new("Count", "Tools.IniEditor.Schema.Fields.ObjectCreationList.Count", true),
                    new("SpreadFormation", "Tools.IniEditor.Schema.Fields.ObjectCreationList.SpreadFormation", false),
                    new("MinDistanceA", "Tools.IniEditor.Schema.Fields.ObjectCreationList.MinDistanceA", true),
                    new("MaxDistanceA", "Tools.IniEditor.Schema.Fields.ObjectCreationList.MaxDistanceA", true),
                    new("OffsetA", "Tools.IniEditor.Schema.Fields.ObjectCreationList.OffsetA", false),
                    new("MinDistanceB", "Tools.IniEditor.Schema.Fields.ObjectCreationList.MinDistanceB", true),
                    new("MaxDistanceB", "Tools.IniEditor.Schema.Fields.ObjectCreationList.MaxDistanceB", true),
                    new("OffsetB", "Tools.IniEditor.Schema.Fields.ObjectCreationList.OffsetB", false),
                    new("IgnorePrimaryObstacles", "Tools.IniEditor.Schema.Fields.ObjectCreationList.IgnorePrimaryObstacles", false),
                    new("IgnoreSecondaryObstacles", "Tools.IniEditor.Schema.Fields.ObjectCreationList.IgnoreSecondaryObstacles", false),
                    new("StartingWeapon", "Tools.IniEditor.Schema.Fields.ObjectCreationList.StartingWeapon", false),
                    new("InheritVeterancy", "Tools.IniEditor.Schema.Fields.ObjectCreationList.InheritVeterancy", false),
                    new("DeliverPayload", "Tools.IniEditor.Schema.Fields.ObjectCreationList.DeliverPayload", false),
                ]),
            new(
                IniConstants.BlockTypes.DamageFX,
                "Tools.IniEditor.Schema.Blocks.DamageFX",
                [
                    new("Throb", "Tools.IniEditor.Schema.Fields.DamageFX.Throb", false),
                    new("ThrobRate", "Tools.IniEditor.Schema.Fields.DamageFX.ThrobRate", true),
                    new("ThrobIntensity", "Tools.IniEditor.Schema.Fields.DamageFX.ThrobIntensity", true),
                    new("Particle", "Tools.IniEditor.Schema.Fields.DamageFX.Particle", false),
                    new("DamageParticle", "Tools.IniEditor.Schema.Fields.DamageFX.DamageParticle", false),
                    new("Fire", "Tools.IniEditor.Schema.Fields.DamageFX.Fire", false),
                    new("Smoke", "Tools.IniEditor.Schema.Fields.DamageFX.Smoke", false),
                    new("Sparks", "Tools.IniEditor.Schema.Fields.DamageFX.Sparks", false),
                    new("DamageTypes", "Tools.IniEditor.Schema.Fields.DamageFX.DamageTypes", false),
                    new("Veterancy", "Tools.IniEditor.Schema.Fields.DamageFX.Veterancy", false),
                ]),
            new(
                IniConstants.BlockTypes.PlayerTemplate,
                "Tools.IniEditor.Schema.Blocks.PlayerTemplate",
                [
                    new("Side", "Tools.IniEditor.Schema.Fields.PlayerTemplate.Side", false),
                    new("PlayableSide", "Tools.IniEditor.Schema.Fields.PlayerTemplate.PlayableSide", false),
                    new("StartMoney", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartMoney", true),
                    new("PreferredColor", "Tools.IniEditor.Schema.Fields.PlayerTemplate.PreferredColor", false),
                    new("StartingBuilding", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingBuilding", false),
                    new("StartingUnit0", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingUnit0", false),
                    new("StartingUnit1", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingUnit1", false),
                    new("StartingUnit2", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingUnit2", false),
                    new("StartingUnitTanks", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingUnitTanks", false),
                    new("StartingUnitAir", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingUnitAir", false),
                    new("SciencePurchasePoints", "Tools.IniEditor.Schema.Fields.PlayerTemplate.SciencePurchasePoints", true),
                    new(IniConstants.FieldKeys.DisplayName, "Tools.IniEditor.Schema.Fields.PlayerTemplate.DisplayName", false),
                    new("StartingTaunt", "Tools.IniEditor.Schema.Fields.PlayerTemplate.StartingTaunt", false),
                ]),
            new(
                IniConstants.BlockTypes.FactionTemplate,
                "Tools.IniEditor.Schema.Blocks.FactionTemplate",
                [
                    new(IniConstants.FieldKeys.DisplayName, "Tools.IniEditor.Schema.Fields.FactionTemplate.DisplayName", false),
                    new("ArmsDealer", "Tools.IniEditor.Schema.Fields.FactionTemplate.ArmsDealer", false),
                    new("StartingBuilding", "Tools.IniEditor.Schema.Fields.FactionTemplate.StartingBuilding", false),
                    new("Superweapon", "Tools.IniEditor.Schema.Fields.FactionTemplate.Superweapon", false),
                ]),
            new(
                IniConstants.BlockTypes.ExperienceLevels,
                "Tools.IniEditor.Schema.Blocks.ExperienceLevels",
                [
                    new("Level1", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level1", true),
                    new("Level2", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level2", true),
                    new("Level3", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level3", true),
                    new("Level4", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level4", true),
                    new("Level5", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level5", true),
                    new("Level6", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level6", true),
                    new("Level7", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level7", true),
                    new("Level8", "Tools.IniEditor.Schema.Fields.ExperienceLevels.Level8", true),
                    new("ExperienceScalar", "Tools.IniEditor.Schema.Fields.ExperienceLevels.ExperienceScalar", true),
                    new("InformSecondLevel", "Tools.IniEditor.Schema.Fields.ExperienceLevels.InformSecondLevel", false),
                ]),
            new(
                IniConstants.BlockTypes.Veterancy,
                "Tools.IniEditor.Schema.Blocks.Veterancy",
                [
                    new("VETERAN", "Tools.IniEditor.Schema.Fields.Veterancy.VETERAN", false),
                    new("ELITE", "Tools.IniEditor.Schema.Fields.Veterancy.ELITE", false),
                    new("HEROIC", "Tools.IniEditor.Schema.Fields.Veterancy.HEROIC", false),
                ]),
    ];

    private readonly ILocalizationService _localizationService;
    private Dictionary<string, IniBlockSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<IniBlockSchema> _blockSchemas = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="IniSchemaService"/> class.
    /// </summary>
    /// <param name="localizationService">The localization service resolving schema descriptions.</param>
    public IniSchemaService(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        RebuildSchemas();
        localizationService.PropertyChanged += OnLocalizationPropertyChanged;
    }

    /// <inheritdoc />
    public IReadOnlyList<IniBlockSchema> BlockSchemas => _blockSchemas;

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

        schema = block.Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        return schema != null;
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ILocalizationService.CurrentCulture))
        {
            RebuildSchemas();
        }
    }

    private void RebuildSchemas()
    {
        var schemas = new List<IniBlockSchema>(SchemaTables.Length);
        foreach (var table in SchemaTables)
        {
            schemas.Add(BuildBlockSchema(table));
        }

        _blockSchemas = schemas;
        _schemas = schemas.ToDictionary(schema => schema.BlockType, StringComparer.OrdinalIgnoreCase);
    }

    private IniBlockSchema BuildBlockSchema(SchemaBlockEntry table)
    {
        var description = _localizationService.GetString(table.DescriptionKey);
        if (string.Equals(table.BlockType, IniConstants.BlockTypes.Armor, StringComparison.Ordinal) &&
            table.GeneratedDescriptionKey != null)
        {
            var armorFields = IniConstants.DamageTypes.All
                .Select(type => new IniFieldSchema(type, _localizationService.GetString(table.GeneratedDescriptionKey, type), true))
                .ToArray();
            return new IniBlockSchema(table.BlockType, description, armorFields);
        }

        var fields = table.Fields
            .Select(entry => new IniFieldSchema(entry.Key, _localizationService.GetString(entry.DescriptionKey), entry.IsNumeric))
            .ToArray();
        return new IniBlockSchema(table.BlockType, description, fields);
    }
}
