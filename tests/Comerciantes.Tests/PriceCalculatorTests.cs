using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace Comerciantes.Tests;

public sealed class PriceCalculatorTests
{
    private static (PriceCalculator Calc, WorldSaturationRegistry Sat) CreateCalculator()
    {
        var val = new ItemValuationRegistry();
        val.ApplyExplicitConfig(TestRepoPaths.LoadItemValuesFile());

        var aff = new TraderAffinityMatrix();
        aff.Load(TestRepoPaths.LoadTraderAffinitiesFile());

        var sat = new WorldSaturationRegistry();
        return (new PriceCalculator(val, aff, sat), sat);
    }

    [Fact]
    public void Evaluate_LowValueStackable_ComputesLotSize()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig { EconomyMode = EconomyMode.Balanced };
        var clay = new TestItem("game:clay-blue", maxStackSize: 64); // base=0.015625 (artisan_raw)

        // survivalgoods has artisan_raw=1.0 -> 0.015625 -> 64u = 1 gear
        ItemValuationResult neutralRes = calc.Evaluate(
            new ItemStack(clay, 1),
            traderEntityId: 1,
            traderArchetype: "survivalgoods",
            traderPos: new BlockPos(0, 100, 0),
            currentTotalDays: 1.0,
            dailyWantedCodes: new HashSet<string>(),
            traderSellingPrices: new Dictionary<string, int>(),
            config);

        Assert.True(neutralRes.IsTradeable);
        Assert.Equal(64, neutralRes.MinLotSize);
        Assert.Equal(1, neutralRes.LotGearPayout);

        // commodities has artisan_raw=1.15 -> 0.015625 * 1.15 = 0.01796875 -> Ceil(55.65) = 56u = 1 gear
        ItemValuationResult affRes = calc.Evaluate(
            new ItemStack(clay, 1),
            traderEntityId: 1,
            traderArchetype: "commodities",
            traderPos: new BlockPos(0, 100, 0),
            currentTotalDays: 1.0,
            dailyWantedCodes: new HashSet<string>(),
            traderSellingPrices: new Dictionary<string, int>(),
            config);

        Assert.True(affRes.IsTradeable);
        Assert.Equal(56, affRes.MinLotSize);
        Assert.Equal(1, affRes.LotGearPayout);
    }

    [Fact]
    public void Evaluate_LotExceedingMaxStackSize_IsUntradeable()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig { EconomyMode = EconomyMode.Stingy }; // 0.75x
        var clay = new TestItem("game:clay-blue", maxStackSize: 64); // 0.015625 * 0.75 => lot > 64

        ItemValuationResult res = calc.Evaluate(
            new ItemStack(clay, 1),
            traderEntityId: 1,
            traderArchetype: "commodities",
            traderPos: new BlockPos(0, 100, 0),
            currentTotalDays: 1.0,
            dailyWantedCodes: new HashSet<string>(),
            traderSellingPrices: new Dictionary<string, int>(),
            config);

        Assert.False(res.IsTradeable);
    }

    [Fact]
    public void Evaluate_AppliesDemandBonusAndAntiArbitrageCap()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig
        {
            EconomyMode = EconomyMode.Balanced,
            AntiArbitrageCapFactor = 0.80
        };
        var goldIngot = new TestItem("game:ingot-gold", maxStackSize: 64); // explicit 4.0, luxuries affinity 1.35 -> 5.4

        ItemValuationResult withDemand = calc.Evaluate(
            new ItemStack(goldIngot, 1),
            1,
            "luxuries",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string> { "game:ingot-gold" },
            new Dictionary<string, int>(),
            config,
            traderDemandMultiplier: 1.5);

        // 4.0 * 1.35 * 1.5 = 8.1 -> rounded 8
        Assert.True(withDemand.IsTradeable);
        Assert.Equal(8, withDemand.LotGearPayout);

        ItemValuationResult capped = calc.Evaluate(
            new ItemStack(goldIngot, 1),
            1,
            "luxuries",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string> { "game:ingot-gold" },
            new Dictionary<string, int> { ["game:ingot-gold"] = 5 }, // cap = Floor(5 * 0.80) = 4
            config,
            traderDemandMultiplier: 1.5);

        Assert.True(capped.IsTradeable);
        Assert.Equal(4, capped.LotGearPayout);
    }

    [Fact]
    public void Evaluate_DamagedToolBelowThreshold_IsUntradeable()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig { MinDurabilityRatio = 0.25 };
        var pick = new TestItem("game:pickaxe-tinbronze", maxStackSize: 1, maxDurability: 100, remainingDurability: 10);

        ItemValuationResult res = calc.Evaluate(
            new ItemStack(pick, 1),
            1,
            "treasurehunter",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config);

        Assert.False(res.IsTradeable);
    }

    [Fact]
    public void EvaluateSellSlot_ComputesPartialAndProgressiveSaturationAcrossSlots()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig
        {
            EconomyMode = EconomyMode.Balanced,
            SaturationS50 = 10.0,
            SaturationCurvePower = 1.0,
            SaturationMinFloor = 0.20
        };

        var leather = new TestItem("game:leather-normal-plain", maxStackSize: 64); // base=0.5, commodities=1.0 -> 2u = 1 gear
        var simulatedGev = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // 1..3 units is below standardized 4u lot -> 0 payout, 0 consumed
        SellSlotValuationEntry partial = calc.EvaluateSellSlot(
            0,
            new ItemStack(leather, 3),
            1,
            "commodities",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config,
            1.0,
            simulatedGev);

        Assert.True(partial.IsTradeable);
        Assert.Equal(0, partial.TotalPayoutGears);
        Assert.Equal(0, partial.ConsumedUnits);
        Assert.Equal(4, partial.CurrentLotSize);
        Assert.Equal(2, partial.LotGearPayout);

        // Large stack in slot 1 saturates progressively so payout < unsaturated 32 gears
        SellSlotValuationEntry slot1 = calc.EvaluateSellSlot(
            1,
            new ItemStack(leather, 64),
            1,
            "commodities",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config,
            1.0,
            simulatedGev);

        Assert.True(slot1.IsTradeable);
        Assert.InRange(slot1.TotalPayoutGears, 1, 31);
        Assert.True(slot1.ConsumedUnits > 0);
        Assert.True(simulatedGev["game:leather-normal-plain"] > 0);

        // Second identical stack in slot 2 pays strictly less than slot 1 due to simulatedGev
        SellSlotValuationEntry slot2 = calc.EvaluateSellSlot(
            2,
            new ItemStack(leather, 64),
            1,
            "commodities",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config,
            1.0,
            simulatedGev);

        Assert.True(slot2.TotalPayoutGears < slot1.TotalPayoutGears);
    }

    [Theory]
    [InlineData(10, EconomyMode.Balanced, 10)]
    [InlineData(10, EconomyMode.Stingy, 13)]      // 10 * 1.25 = 12.5 -> AwayFromZero = 13
    [InlineData(10, EconomyMode.Businessman, 10)] // standard buy price; better deals only on selling
    [InlineData(1, EconomyMode.Businessman, 1)]
    public void GetEffectiveBuyPrice_AppliesModeMultiplierAndMinimum(int rawPrice, EconomyMode mode, int expected)
    {
        var config = new ComerciantesConfig { EconomyMode = mode };
        Assert.Equal(expected, PriceCalculator.GetEffectiveBuyPrice(rawPrice, config));
    }

    [Fact]
    public void ComputeTraderDemandBonusPct_IsDeterministicAndBounded()
    {
        var config = new ComerciantesConfig { DailyDemandMinPct = 20.0, DailyDemandMaxPct = 50.0 };

        double first = PriceCalculator.ComputeTraderDemandBonusPct(42L, 14.5, config);
        double second = PriceCalculator.ComputeTraderDemandBonusPct(42L, 14.5, config);
        Assert.Equal(first, second);
        Assert.InRange(first, 20.0, 50.0);

        var fixedConfig = new ComerciantesConfig { DailyDemandMinPct = 35.0, DailyDemandMaxPct = 35.0 };
        Assert.Equal(35.0, PriceCalculator.ComputeTraderDemandBonusPct(99L, 7.0, fixedConfig));
    }

    [Fact]
    public void EvaluateSellSlot_NonStackable_HandlesTradeableAndUntradeableItems()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig { EconomyMode = EconomyMode.Balanced, MinDurabilityRatio = 0.15 };

        var pick = new TestItem("game:pickaxe-iron", maxStackSize: 1, maxDurability: 100, remainingDurability: 100);
        SellSlotValuationEntry valid = calc.EvaluateSellSlot(
            0,
            new ItemStack(pick, 1),
            1,
            "treasurehunter", // 1.35x -> 7.0 * 1.35 = 9.45 -> 9 gears
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config);

        Assert.True(valid.IsTradeable);
        Assert.Equal(1, valid.ConsumedUnits);
        Assert.Equal(9, valid.TotalPayoutGears);

        var brokenPick = new TestItem("game:pickaxe-iron", maxStackSize: 1, maxDurability: 100, remainingDurability: 5);
        SellSlotValuationEntry invalid = calc.EvaluateSellSlot(
            0,
            new ItemStack(brokenPick, 1),
            1,
            "treasurehunter",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config);

        Assert.False(invalid.IsTradeable);
        Assert.Equal(0, invalid.TotalPayoutGears);
    }

    [Fact]
    public void Evaluate_StandardizedLotSizes_MinimumFourAndMultiplesOfFour()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig { EconomyMode = EconomyMode.Balanced };

        // 0.50/u -> standardized minimum 4x for 2 gears
        var leather = new TestItem("game:leather-normal-plain", maxStackSize: 64);
        ItemValuationResult leatherRes = calc.Evaluate(
            new ItemStack(leather, 1),
            1,
            "commodities",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config);

        Assert.True(leatherRes.IsTradeable);
        Assert.Equal(4, leatherRes.MinLotSize);
        Assert.Equal(2, leatherRes.LotGearPayout);

        // 0.50 * 0.70 (buildmaterials affinity for leather) = 0.35 -> 4x for 1 gear
        ItemValuationResult lowAffLeather = calc.Evaluate(
            new ItemStack(leather, 1),
            1,
            "buildmaterials",
            new BlockPos(0, 100, 0),
            1.0,
            new HashSet<string>(),
            new Dictionary<string, int>(),
            config);

        Assert.True(lowAffLeather.IsTradeable);
        Assert.Equal(4, lowAffLeather.MinLotSize);
        Assert.Equal(1, lowAffLeather.LotGearPayout);
    }

    [Fact]
    public void Evaluate_FeathersProgressionAndBlocksPastMaxStackLimit()
    {
        (PriceCalculator calc, WorldSaturationRegistry sat) = CreateCalculator();
        var config = new ComerciantesConfig
        {
            EconomyMode = EconomyMode.Balanced,
            SaturationScope = SaturationScope.LocalOnly,
            SaturationS50 = 10.0,
            SaturationCurvePower = 1.0,
            SaturationMinFloor = 0.20
        };
        var pos = new BlockPos(0, 100, 0);
        var feather = new TestItem("game:feather", maxStackSize: 64); // 0.0625 -> 16x = 1 gear

        ItemValuationResult baseRes = calc.Evaluate(
            new ItemStack(feather, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.True(baseRes.IsTradeable);
        Assert.Equal(16, baseRes.MinLotSize);
        Assert.Equal(1, baseRes.LotGearPayout);

        // S = 5.0 -> satMod = 1/(1+0.5) = 0.6667 -> unitPrice = 0.04167 -> 24x = 1 gear
        sat.RecordSale(1, pos, 1.0, "game:feather", 5.0, config);
        ItemValuationResult midSatRes = calc.Evaluate(
            new ItemStack(feather, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.True(midSatRes.IsTradeable);
        Assert.Equal(24, midSatRes.MinLotSize);
        Assert.Equal(1, midSatRes.LotGearPayout);

        // S = 30.0 -> satMod = 1/(1+3) = 0.25 -> unitPrice = 0.015625 -> 64x = 1 gear
        sat.RecordSale(1, pos, 1.0, "game:feather", 25.0, config);
        ItemValuationResult capSatRes = calc.Evaluate(
            new ItemStack(feather, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.True(capSatRes.IsTradeable);
        Assert.Equal(64, capSatRes.MinLotSize);
        Assert.Equal(1, capSatRes.LotGearPayout);

        // S = 40.0 -> satMod = 0.20 -> unitPrice = 0.0125 -> needs 80 > 64 -> blocked
        sat.RecordSale(1, pos, 1.0, "game:feather", 10.0, config);
        ItemValuationResult blockedRes = calc.Evaluate(
            new ItemStack(feather, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.False(blockedRes.IsTradeable);
    }

    [Fact]
    public void Evaluate_RespectsStackCap16ForLimitedItemsAnd64ForOversizedStacks()
    {
        (PriceCalculator calc, WorldSaturationRegistry sat) = CreateCalculator();
        var config = new ComerciantesConfig
        {
            EconomyMode = EconomyMode.Balanced,
            SaturationScope = SaturationScope.LocalOnly,
            SaturationS50 = 10.0,
            SaturationCurvePower = 1.0,
            SaturationMinFloor = 0.20
        };
        var pos = new BlockPos(0, 100, 0);

        // Quicklime = 0.0625 (16x) but limited to MaxStackSize = 16
        var limited16 = new TestItem("game:quicklime", maxStackSize: 16);
        ItemValuationResult ok16 = calc.Evaluate(
            new ItemStack(limited16, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.True(ok16.IsTradeable);
        Assert.Equal(16, ok16.MinLotSize);

        // Slight saturation pushes required lot to 20 > 16 -> blocked
        sat.RecordSale(1, pos, 1.0, "game:quicklime", 2.0, config);
        ItemValuationResult blocked16 = calc.Evaluate(
            new ItemStack(limited16, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.False(blocked16.IsTradeable);

        // Oversized stack item (MaxStackSize = 128) requiring > 64 is still capped at 64 -> blocked
        var oversized = new TestItem("game:feather", maxStackSize: 128);
        sat.RecordSale(1, pos, 1.0, "game:feather", 40.0, config);
        ItemValuationResult blocked64 = calc.Evaluate(
            new ItemStack(oversized, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.False(blocked64.IsTradeable);
    }

    [Fact]
    public void EvaluateSellSlot_FeathersConsumeExactMultipleOfFourLotsUntilBlocked()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var config = new ComerciantesConfig
        {
            EconomyMode = EconomyMode.Balanced,
            SaturationScope = SaturationScope.LocalOnly,
            SaturationS50 = 1.5, // tight S50 so progressive lot steps are visible inside one 64-stack
            SaturationCurvePower = 1.0,
            SaturationMinFloor = 0.20
        };
        var pos = new BlockPos(0, 100, 0);
        var feather = new TestItem("game:feather", maxStackSize: 64);

        SellSlotValuationEntry entry = calc.EvaluateSellSlot(
            0, new ItemStack(feather, 64), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);

        Assert.True(entry.IsTradeable);
        Assert.Equal(16, entry.CurrentLotSize);
        Assert.InRange(entry.TotalPayoutGears, 1, 3);
        Assert.Equal(0, entry.ConsumedUnits % 4);
        Assert.InRange(entry.ConsumedUnits, 16, 64);
    }

    [Fact]
    public void Evaluate_EconomyModes_CompareStingyBalancedAndBusinessman()
    {
        (PriceCalculator calc, _) = CreateCalculator();
        var goldIngot = new TestItem("game:ingot-gold", maxStackSize: 64); // base=4.0, commodities=1.0
        var pos = new BlockPos(0, 100, 0);

        var stingy = new ComerciantesConfig { EconomyMode = EconomyMode.Stingy };
        var balanced = new ComerciantesConfig { EconomyMode = EconomyMode.Balanced };
        var businessman = new ComerciantesConfig { EconomyMode = EconomyMode.Businessman };

        // Buy price: Stingy expensive, Balanced and Businessman standard
        Assert.Equal(13, PriceCalculator.GetEffectiveBuyPrice(10, stingy));
        Assert.Equal(10, PriceCalculator.GetEffectiveBuyPrice(10, balanced));
        Assert.Equal(10, PriceCalculator.GetEffectiveBuyPrice(10, businessman));

        // Sell payout: Stingy < Balanced < Businessman
        ItemValuationResult stingySell = calc.Evaluate(new ItemStack(goldIngot, 1), 1, "commodities", pos, 1.0, new HashSet<string>(), new Dictionary<string, int>(), stingy);
        ItemValuationResult balancedSell = calc.Evaluate(new ItemStack(goldIngot, 1), 1, "commodities", pos, 1.0, new HashSet<string>(), new Dictionary<string, int>(), balanced);
        ItemValuationResult bizSell = calc.Evaluate(new ItemStack(goldIngot, 1), 1, "commodities", pos, 1.0, new HashSet<string>(), new Dictionary<string, int>(), businessman);

        Assert.Equal(3, stingySell.LotGearPayout);
        Assert.Equal(4, balancedSell.LotGearPayout);
        Assert.Equal(5, bizSell.LotGearPayout);

        // Businessman still respects anti-arbitrage cap (retail=5 * 0.85 = Floor(4.25) = 4)
        ItemValuationResult bizCapped = calc.Evaluate(
            new ItemStack(goldIngot, 1), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int> { ["game:ingot-gold"] = 5 }, businessman);
        Assert.Equal(4, bizCapped.LotGearPayout);
    }

    [Fact]
    public void EvaluateSellSlot_DistinguishesOverstockedFromIntrinsicNoValueAndTracksConsumedLots()
    {
        (PriceCalculator calc, WorldSaturationRegistry sat) = CreateCalculator();
        var config = new ComerciantesConfig
        {
            EconomyMode = EconomyMode.Balanced,
            SaturationScope = SaturationScope.LocalOnly,
            SaturationS50 = 10.0,
            SaturationCurvePower = 1.0,
            SaturationMinFloor = 0.20
        };
        var pos = new BlockPos(0, 100, 0);

        // 12 leather (4x3) with S50=10.0: Lot 1 pays 2 gears, Lots 2 & 3 pay 1 gear each -> 4 gears
        var leather = new TestItem("game:leather-normal-plain", maxStackSize: 64);
        SellSlotValuationEntry leatherEntry = calc.EvaluateSellSlot(
            0, new ItemStack(leather, 12), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.True(leatherEntry.IsTradeable);
        Assert.Equal(4, leatherEntry.CurrentLotSize);
        Assert.Equal(3, leatherEntry.ConsumedLots);
        Assert.Equal(4, leatherEntry.TotalPayoutGears);

        // Blacklisted dirt -> IsTradeable=false, IsOverstocked=false ("Sin Valor")
        var dirt = new TestBlock("game:soil-medium-none");
        SellSlotValuationEntry dirtEntry = calc.EvaluateSellSlot(
            1, new ItemStack(dirt, 16), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.False(dirtEntry.IsTradeable);
        Assert.False(dirtEntry.IsOverstocked);

        // Over-saturated feather -> IsTradeable=false, IsOverstocked=true ("Sobre Stock")
        var feather = new TestItem("game:feather", maxStackSize: 64);
        sat.RecordSale(1, pos, 1.0, "game:feather", 40.0, config);
        SellSlotValuationEntry overFeather = calc.EvaluateSellSlot(
            2, new ItemStack(feather, 64), 1, "commodities", pos, 1.0,
            new HashSet<string>(), new Dictionary<string, int>(), config);
        Assert.False(overFeather.IsTradeable);
        Assert.True(overFeather.IsOverstocked);
    }
}
