using Vintagestory.API.Common;
using Xunit;

namespace Comerciantes.Tests;

public sealed class ItemValuationRegistryTests
{
    private static ItemValuationRegistry CreateLoadedRegistry()
    {
        var registry = new ItemValuationRegistry();
        registry.ApplyExplicitConfig(TestRepoPaths.LoadItemValuesFile());
        return registry;
    }

    [Fact]
    public void IsBlacklisted_RejectsRustyGearNullAndConfiguredPrefixes()
    {
        ItemValuationRegistry registry = CreateLoadedRegistry();

        Assert.True(registry.IsBlacklisted(null));
        Assert.True(registry.IsBlacklisted(new TestItem("game:gear-rusty")));
        Assert.True(registry.IsBlacklisted(new TestItem("game:soil-medium-none")));
        Assert.True(registry.IsBlacklisted(new TestItem("game:sand-quartz")));
        Assert.False(registry.IsBlacklisted(new TestItem("game:ingot-copper")));
    }

    [Fact]
    public void GetBaseValue_UsesExplicitOverridesPathEstimationAndFallbacks()
    {
        ItemValuationRegistry registry = CreateLoadedRegistry();

        // Explicit from item_values.json (copper ingot is 1.0 instead of 1.5 * 0.7 = 1.05)
        Assert.Equal(1.0, registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-copper"), 1)), 4);

        // Path estimation: steel ingot not in item_values? Actually ingot-steel IS 3.0 in item_values; test iron ingot (1.5 * 1.5 = 2.25)
        Assert.Equal(2.25, registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-iron"), 1)), 4);

        // Stone tools have 0.0 value
        var flintAxe = new TestItem("game:axe-felling-flint", maxStackSize: 1, variants: new() { ["metal"] = "flint" });
        Assert.Equal(0.0, registry.GetBaseValue(new ItemStack(flintAxe, 1)), 4);

        // Unknown item/block fallback
        Assert.Equal(0.25, registry.GetBaseValue(new ItemStack(new TestItem("custom:mystery-item"), 1)), 4);
        Assert.Equal(0.20, registry.GetBaseValue(new ItemStack(new TestBlock("custom:mystery-block"), 1)), 4);

        // Blacklisted -> 0
        Assert.Equal(0.0, registry.GetBaseValue(new ItemStack(new TestItem("game:gear-rusty"), 1)), 4);
    }

    [Fact]
    public void GetConditionMultiplier_RespectsMinDurabilityThreshold()
    {
        ItemValuationRegistry registry = CreateLoadedRegistry();
        var config = new ComerciantesConfig(); // default MinDurabilityRatio = 0.80

        var nonDamageable = new TestItem("game:ingot-copper", maxDurability: 0);
        Assert.Equal(1.0, registry.GetConditionMultiplier(new ItemStack(nonDamageable, 1), config), 4);

        var fullTool = new TestItem("game:pickaxe-iron", maxStackSize: 1, maxDurability: 100, remainingDurability: 100);
        Assert.Equal(1.0, registry.GetConditionMultiplier(new ItemStack(fullTool, 1), config), 4);

        var wornAcceptedTool = new TestItem("game:pickaxe-iron", maxStackSize: 1, maxDurability: 100, remainingDurability: 85);
        Assert.Equal(0.85, registry.GetConditionMultiplier(new ItemStack(wornAcceptedTool, 1), config), 4);

        var halfTool = new TestItem("game:pickaxe-iron", maxStackSize: 1, maxDurability: 100, remainingDurability: 50);
        Assert.Equal(0.0, registry.GetConditionMultiplier(new ItemStack(halfTool, 1), config), 4);
    }

    [Fact]
    public void GetBaseValue_EstimatesSpecialFamiliesAndMetalTiers()
    {
        ItemValuationRegistry registry = CreateLoadedRegistry();

        // Lantern with steel material attribute: 5.0 * 2.2 = 11.0
        var steelLantern = new ItemStack(new TestBlock("game:lantern-up"), 1);
        steelLantern.Attributes.SetString("material", "steel");
        Assert.Equal(11.0, registry.GetBaseValue(steelLantern), 4);

        // Anvil copper: 8.0 * 0.7 = 5.6
        Assert.Equal(5.6, registry.GetBaseValue(new ItemStack(new TestBlock("game:anvil-copper"), 1)), 4);

        // Clothes & gem fallback paths
        Assert.Equal(2.5, registry.GetBaseValue(new ItemStack(new TestItem("game:clothes-upperbody-linen"), 1)), 4);
        Assert.Equal(10.0, registry.GetBaseValue(new ItemStack(new TestItem("game:gem-peridot-rough"), 1)), 4);
    }

    [Fact]
    public void GetInventoryValuationHint_ComputesLotOrSingleValue()
    {
        ItemValuationRegistry registry = CreateLoadedRegistry();

        InventoryValuationHint? nullHint = registry.GetInventoryValuationHint(null);
        Assert.False(nullHint.IsValuable);

        InventoryValuationHint? blacklisted = registry.GetInventoryValuationHint(new ItemStack(new TestItem("game:gear-rusty"), 1));
        Assert.False(blacklisted.IsValuable);

        // feather = 0.0625 -> 16x -> 1 gear
        Assert.Equal(0.0625, registry.GetBaseValue(new ItemStack(new TestItem("game:feather", maxStackSize: 64), 1)), 4);

        // fat = 0.125 -> lot 8 -> 1 gear
        InventoryValuationHint lotHint = registry.GetInventoryValuationHint(new ItemStack(new TestItem("game:fat"), 1));
        Assert.True(lotHint.IsValuable);
        Assert.Equal(8, lotHint.LotSize);
        Assert.Equal(1, lotHint.LotGearPayout);

        // leather = 0.5 -> standardized minimum lot 4 -> 2 gears
        InventoryValuationHint leatherHint = registry.GetInventoryValuationHint(new ItemStack(new TestItem("game:leather-normal-plain"), 1));
        Assert.True(leatherHint.IsValuable);
        Assert.Equal(4, leatherHint.LotSize);
        Assert.Equal(2, leatherHint.LotGearPayout);

        // copper ingot = 1.0 -> single value 1.0
        InventoryValuationHint singleHint = registry.GetInventoryValuationHint(new ItemStack(new TestItem("game:ingot-copper"), 1));
        Assert.True(singleHint.IsValuable);
        Assert.Equal(1, singleHint.LotSize);
        Assert.Equal(1, singleHint.LotGearPayout);
        Assert.Equal(1.0, singleHint.BaseValue, 4);

        // limited stack 16 item worth 0.04 (needs 28 > 16) -> not tradeable
        InventoryValuationHint blockedHint = registry.GetInventoryValuationHint(new ItemStack(new TestItem("game:torch-basic-extinct", maxStackSize: 16), 1));
        Assert.False(blockedHint.IsValuable);
    }
}
