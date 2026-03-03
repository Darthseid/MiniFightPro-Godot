using System;
using Xunit;

public class DiceHelpersTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("10")]
    [InlineData("  7  ")]
    [InlineData("D6")]
    [InlineData("d6")]
    [InlineData("2D6")]
    [InlineData(" 2d6 + 1 ")]
    [InlineData("3D10-2")]
    public void IsDamageExpressionValid_AcceptsValidInputs(string input)
    {
        Assert.True(DiceHelpers.IsDamageExpressionValid(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2dd6")]
    [InlineData("D")]
    [InlineData("2D")]
    [InlineData("2D0")]
    [InlineData("0D6")]
    [InlineData("2D6++1")]
    [InlineData("abc")]
    public void IsDamageExpressionValid_RejectsInvalidInputs(string input)
    {
        Assert.False(DiceHelpers.IsDamageExpressionValid(input));
    }

    [Fact]
    public void DamageParser_ParsesConstant()
    {
        Assert.Equal(5, DiceHelpers.DamageParser("5"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2dd6")]
    public void DamageParser_InvalidInputs_ThrowArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => DiceHelpers.DamageParser(input));
    }

    [Fact]
    public void DamageParser_WithSeededRng_Parses2D6Plus3Deterministically()
    {
        const ulong seed = 1234;
        RngTestHelper.SeedDiceHelpers(seed);

        var actual = DiceHelpers.DamageParser("2D6+3");
        var expected = RngTestHelper.PredictDiceSum(seed, 2, 6, 3);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DamageParser_WithSeededRng_ParsesD3Deterministically()
    {
        const ulong seed = 42;
        RngTestHelper.SeedDiceHelpers(seed);

        var actual = DiceHelpers.DamageParser("D3");
        var expected = RngTestHelper.PredictDiceSum(seed, 1, 3);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("2D0")]
    [InlineData("0D6")]
    public void DamageParser_InvalidDiceValues_ThrowArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => DiceHelpers.DamageParser(input));
    }
}
