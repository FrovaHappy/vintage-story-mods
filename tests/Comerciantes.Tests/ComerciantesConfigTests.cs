using Vintagestory.API.Datastructures;
using Xunit;

namespace Comerciantes.Tests;

public sealed class ComerciantesConfigTests
{
    [Theory]
    [InlineData(EconomyMode.Stingy, 1.25, 0.75)]
    [InlineData(EconomyMode.Balanced, 1.00, 1.00)]
    [InlineData(EconomyMode.Businessman, 1.00, 1.20)]
    public void EconomyModeMultipliers_MatchExpectedBalance(EconomyMode mode, double expectedBuy, double expectedSell)
    {
        var config = new ComerciantesConfig { EconomyMode = mode };

        Assert.Equal(expectedBuy, config.GetBuyPriceMultiplier(), 4);
        Assert.Equal(expectedSell, config.GetSellPayoutMultiplier(), 4);
    }

    [Fact]
    public void ApplySetting_ParsesEnumsCaseInsensitively()
    {
        var config = new ComerciantesConfig();

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.EconomyMode), new StringAttribute("businessman")));
        Assert.Equal(EconomyMode.Businessman, config.EconomyMode);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.DirectorySyncMode), new StringAttribute("liveknowntraders")));
        Assert.Equal(DirectorySyncMode.LiveKnownTraders, config.DirectorySyncMode);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.SaturationScope), new StringAttribute("global")));
        Assert.Equal(SaturationScope.Global, config.SaturationScope);
    }

    [Fact]
    public void ApplySetting_HandlesBooleansAndNumbers()
    {
        var config = new ComerciantesConfig();

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.ShowValuationInInventoryTooltip), new BoolAttribute(true)));
        Assert.True(config.ShowValuationInInventoryTooltip);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.EnableDebugLogging), new IntAttribute(1)));
        Assert.True(config.EnableDebugLogging);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.SaturationSpilloverRadius), new DoubleAttribute(2400.5)));
        Assert.Equal(2400.5, config.SaturationSpilloverRadius, 4);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.SaturationCurvePower), new StringAttribute("1.75")));
        Assert.Equal(1.75, config.SaturationCurvePower, 4);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.DailyDemandMinPct), new FloatAttribute(15f)));
        Assert.Equal(15.0, config.DailyDemandMinPct, 4);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.MaxPhysicalTradeDistance), new LongAttribute(9L)));
        Assert.Equal(9.0, config.MaxPhysicalTradeDistance, 4);

        var tree = new TreeAttribute();
        tree.SetDouble("game:ingot-copper", 3.5);
        tree.SetDouble("game:axe-felling-*", 4.0);
        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.ItemValueOverrides), tree));
        Assert.Equal(3.5, config.ItemValueOverrides["game:ingot-copper"], 4);
        Assert.Equal(4.0, config.ItemValueOverrides["game:axe-felling-*"], 4);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.ItemValueOverrides), new StringAttribute("{\"game:pickaxe-*\": 2.0}")));
        Assert.Equal(2.0, config.ItemValueOverrides["game:pickaxe-*"], 4);
    }

    [Fact]
    public void ApplySetting_RejectsInvalidInputs()
    {
        var config = new ComerciantesConfig();

        Assert.False(config.ApplySetting(null, new StringAttribute("x")));
        Assert.False(config.ApplySetting("", new StringAttribute("x")));
        Assert.False(config.ApplySetting(nameof(ComerciantesConfig.EconomyMode), null));
        Assert.False(config.ApplySetting(nameof(ComerciantesConfig.EconomyMode), new StringAttribute("UnknownMode")));
        Assert.False(config.ApplySetting(nameof(ComerciantesConfig.SaturationS50), new StringAttribute("not-a-number")));
        Assert.False(config.ApplySetting("UnknownSetting", new IntAttribute(42)));
    }

    [Fact]
    public void Defaults_MinDurabilityRatio_IsEightyPercent()
    {
        var config = new ComerciantesConfig();
        Assert.Equal(0.80, config.MinDurabilityRatio, 4);
    }

    [Fact]
    public void EnableInstantTrade_DefaultsFalseAndAppliesBoolSetting()
    {
        var config = new ComerciantesConfig();
        Assert.False(config.EnableInstantTrade);

        Assert.True(config.ApplySetting(nameof(ComerciantesConfig.EnableInstantTrade), new BoolAttribute(true)));
        Assert.True(config.EnableInstantTrade);
    }

    [Fact]
    public void IsWithinTradeDistance_AllowsRemoteTradeOnlyWhenInstantTradeEnabled()
    {
        var disabled = new ComerciantesConfig { EnableInstantTrade = false, MaxPhysicalTradeDistance = 6.0 };
        Assert.True(disabled.IsWithinTradeDistance(4.0));
        Assert.False(disabled.IsWithinTradeDistance(50.0));

        var enabled = new ComerciantesConfig { EnableInstantTrade = true, MaxPhysicalTradeDistance = 6.0 };
        Assert.True(enabled.IsWithinTradeDistance(5000.0));
    }
}
