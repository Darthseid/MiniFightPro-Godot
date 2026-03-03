using Xunit;

public class WeaponAbilityTests
{
    [Fact]
    public void ResolveModifier_UsesModifier_WhenExpressionIsEmpty()
    {
        var ability = new WeaponAbility("Fusion", "Fusion 2", 2, false);

        Assert.Equal(2, ability.ResolveModifier());
    }

    [Fact]
    public void ResolveModifier_UsesDiceParser_WhenExpressionProvided()
    {
        const ulong seed = 2024;
        var ability = new WeaponAbility("Fusion", "Fusion D3", 0, false, true, "D3");
        var expected = RngTestHelper.PredictDiceSum(seed, 1, 3);

        RngTestHelper.SeedDiceHelpers(seed);
        var actual = ability.ResolveModifier();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreateVariableAbility_SetsVariableFields_AndParsesModifier()
    {
        var created = WeaponAbilities.CreateVariableAbility(WeaponAbilities.AntiInfantry2, "3");

        Assert.True(created.IsVariableGenerated);
        Assert.Equal("3", created.ModifierExpression);
        Assert.Equal(3, created.Modifier);
    }

    [Fact]
    public void CreateVariableAbility_StripsTrailingNumericTokenFromBaseName()
    {
        var created = WeaponAbilities.CreateVariableAbility(WeaponAbilities.AntiInfantry2, "D3");

        Assert.Equal("Anti-Infantry D3", created.Name);
    }

    [Fact]
    public void CreateVariableAbility_AppendsConstantModifierToName()
    {
        var created = WeaponAbilities.CreateVariableAbility(WeaponAbilities.Fusion1, "3");

        Assert.EndsWith(" 3", created.Name);
        Assert.Equal(3, created.Modifier);
    }
}
