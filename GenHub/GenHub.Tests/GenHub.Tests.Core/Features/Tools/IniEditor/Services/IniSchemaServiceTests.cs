using FluentAssertions;
using GenHub.Core.Constants;
using GenHub.Features.Tools.IniEditor.Services;

namespace GenHub.Tests.Core.Features.Tools.IniEditor.Services;

/// <summary>
/// Unit tests for <see cref="IniSchemaService"/>.
/// </summary>
public sealed class IniSchemaServiceTests
{
    private readonly IniSchemaService _service = new();

    /// <summary>
    /// Verifies that core block types have schema assistance.
    /// </summary>
    /// <param name="blockType">The block type under test.</param>
    [Theory]
    [InlineData(IniConstants.BlockTypes.Object)]
    [InlineData(IniConstants.BlockTypes.Weapon)]
    [InlineData(IniConstants.BlockTypes.Armor)]
    [InlineData(IniConstants.BlockTypes.ArmorSet)]
    [InlineData(IniConstants.BlockTypes.WeaponSet)]
    [InlineData(IniConstants.BlockTypes.CommandButton)]
    [InlineData(IniConstants.BlockTypes.CommandSet)]
    [InlineData(IniConstants.BlockTypes.Upgrade)]
    [InlineData(IniConstants.BlockTypes.Science)]
    [InlineData(IniConstants.BlockTypes.SpecialPower)]
    [InlineData(IniConstants.BlockTypes.Locomotor)]
    [InlineData(IniConstants.BlockTypes.ObjectCreationList)]
    [InlineData(IniConstants.BlockTypes.PlayerTemplate)]
    public void GetBlockSchema_KnownBlock_ReturnsSchema(string blockType)
    {
        var schema = _service.GetBlockSchema(blockType);

        schema.Should().NotBeNull();
        schema!.Fields.Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies that unknown blocks return no schema but stay editable.
    /// </summary>
    [Fact]
    public void GetBlockSchema_UnknownBlock_ReturnsNull()
    {
        _service.GetBlockSchema("SomeCommunityBlock").Should().BeNull();
    }

    /// <summary>
    /// Verifies that object health and cost fields are known.
    /// </summary>
    [Fact]
    public void TryGetField_ObjectHealthAndCost_ReturnsSchemas()
    {
        _service.TryGetField(IniConstants.BlockTypes.Object, "Health", out var health).Should().BeTrue();
        health.Should().NotBeNull();
        _service.TryGetField(IniConstants.BlockTypes.Object, "BuildCost", out var cost).Should().BeTrue();
        cost!.IsNumeric.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that weapon damage fields are known.
    /// </summary>
    [Fact]
    public void TryGetField_WeaponDamage_ReturnsSchema()
    {
        _service.TryGetField(IniConstants.BlockTypes.Weapon, "PrimaryDamage", out var damage).Should().BeTrue();
        damage!.IsNumeric.Should().BeTrue();
        _service.TryGetField(IniConstants.BlockTypes.Weapon, "DamageType", out var damageType).Should().BeTrue();
        damageType!.IsNumeric.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that armor tables cover every damage type.
    /// </summary>
    [Fact]
    public void GetBlockSchema_Armor_CoversAllDamageTypes()
    {
        var schema = _service.GetBlockSchema(IniConstants.BlockTypes.Armor);

        schema.Should().NotBeNull();
        foreach (var damageType in IniConstants.DamageTypes.All)
        {
            schema!.Fields.Should().Contain(field => field.Key == damageType);
        }
    }

    /// <summary>
    /// Verifies that command sets expose numbered slots.
    /// </summary>
    [Fact]
    public void GetBlockSchema_CommandSet_ExposesNumberedSlots()
    {
        var schema = _service.GetBlockSchema(IniConstants.BlockTypes.CommandSet);

        schema.Should().NotBeNull();
        schema!.Fields.Should().Contain(field => field.Key == "1");
    }

    /// <summary>
    /// Verifies that field lookup is case insensitive.
    /// </summary>
    [Fact]
    public void TryGetField_DifferentCasing_ReturnsSchema()
    {
        _service.TryGetField(IniConstants.BlockTypes.Object, "health", out var schema).Should().BeTrue();
        schema.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that unknown fields return false.
    /// </summary>
    [Fact]
    public void TryGetField_UnknownField_ReturnsFalse()
    {
        _service.TryGetField(IniConstants.BlockTypes.Object, "NotARealField", out var schema).Should().BeFalse();
        schema.Should().BeNull();
    }
}
