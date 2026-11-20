using SeraphQoL.Alloy;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class AlloyCalculatorTests
{
    private static AlloyRecipeSpec TinBronzeRecipe => new(
        OutputCode: "game:ingot-tinbronze",
        OutputName: "Tin bronze",
        Ingredients:
        [
            new AlloyIngredientSpec("game:ingot-copper", "copper", 0.88, 0.92),
            new AlloyIngredientSpec("game:ingot-tin", "tin", 0.08, 0.12)
        ]);

    private static AlloyRecipeSpec PureCopperRecipe => new(
        OutputCode: "game:ingot-copper",
        OutputName: "Copper",
        Ingredients:
        [
            new AlloyIngredientSpec("game:ingot-copper", "copper", 1.0, 1.0)
        ]);

    [Fact]
    public void BuildDefaultPercentages_UsesMidpointsSummingToOne()
    {
        double[] pcts = AlloyCalculatorLogic.BuildDefaultPercentages(TinBronzeRecipe);

        Assert.Equal(2, pcts.Length);
        Assert.Equal(1.0, pcts.Sum(), 5);
        Assert.InRange(pcts[0], 0.88, 0.92);
        Assert.InRange(pcts[1], 0.08, 0.12);
    }

    [Fact]
    public void AdjustIngredientPercentage_ClampsAndRedistributesRemainderWithinBounds()
    {
        double[] initial = [0.90, 0.10];

        double[] updated = AlloyCalculatorLogic.AdjustIngredientPercentage(TinBronzeRecipe, initial, editedIndex: 0, targetRatio: 0.95);

        Assert.Equal(0.92, updated[0], 4);
        Assert.Equal(0.08, updated[1], 4);
        Assert.Equal(1.0, updated.Sum(), 4);
    }

    [Fact]
    public void CalculateIngredientPlan_ComputesExactNuggetsAccountingForCrucibleContents()
    {
        // Target 200 units Tin Bronze at 90% copper (180u = 36 nuggets), 10% tin (20u = 4 nuggets)
        // Crucible already has 30 copper units -> needs 150u copper = 30 nuggets, 20u tin = 4 nuggets
        var crucible = new List<CrucibleMetalSnapshot>
        {
            new("game:ingot-copper", 30)
        };
        var available = new List<SmeltableMetalItem>
        {
            new("game:nugget-nativecopper", "Native copper nugget", "game:ingot-copper", UnitsPerItem: 5, MeltTempC: 1084, TotalCount: 64),
            new("game:nugget-cassiterite", "Cassiterite nugget", "game:ingot-tin", UnitsPerItem: 5, MeltTempC: 232, TotalCount: 16)
        };

        AlloyBatchPlan plan = AlloyCalculatorLogic.CalculateBatchPlan(
            TinBronzeRecipe,
            percentages: [0.90, 0.10],
            targetUnits: 200,
            crucibleContents: crucible,
            availableItems: available,
            unitsPerNugget: 5);

        Assert.True(plan.IsRatioValid);
        Assert.True(plan.CanFulfillFromInventory);
        Assert.Equal(200, plan.TargetOutputUnits);
        Assert.Equal(1084, plan.RequiredMeltTempC);

        AlloyIngredientNeed copper = plan.Needs[0];
        Assert.Equal(180, copper.TargetUnits);
        Assert.Equal(30, copper.AlreadyInCrucibleUnits);
        Assert.Equal(150, copper.DeficitUnits);
        Assert.Equal(30, copper.ItemsToDeposit);

        AlloyIngredientNeed tin = plan.Needs[1];
        Assert.Equal(20, tin.TargetUnits);
        Assert.Equal(0, tin.AlreadyInCrucibleUnits);
        Assert.Equal(20, tin.DeficitUnits);
        Assert.Equal(4, tin.ItemsToDeposit);
    }

    [Fact]
    public void CalculateBatchPlan_FlagsInsufficientInventory()
    {
        var available = new List<SmeltableMetalItem>
        {
            new("game:nugget-nativecopper", "Copper nugget", "game:ingot-copper", 5, 1084, 10)
        };

        AlloyBatchPlan plan = AlloyCalculatorLogic.CalculateBatchPlan(
            PureCopperRecipe,
            percentages: [1.0],
            targetUnits: 100,
            crucibleContents: [],
            availableItems: available,
            unitsPerNugget: 5);

        Assert.False(plan.CanFulfillFromInventory);
        Assert.Equal(20, plan.Needs[0].ItemsNeededTotal);
        Assert.Equal(10, plan.Needs[0].ItemsToDeposit);
        Assert.Equal(50, plan.Needs[0].MissingUnitsAfterDeposit);
    }

    [Fact]
    public void FindNextWasteFreeUnits_FindsExactIntegerIngotSteps()
    {
        // At 90/10, 100u = 90u (18 nuggets) + 10u (2 nuggets), 200u = 180 + 20, etc.
        int up = AlloyCalculatorLogic.FindNextWasteFreeUnits(TinBronzeRecipe, [0.90, 0.10], currentUnits: 100, direction: 1, unitsPerNugget: 5);
        int down = AlloyCalculatorLogic.FindNextWasteFreeUnits(TinBronzeRecipe, [0.90, 0.10], currentUnits: 200, direction: -1, unitsPerNugget: 5);

        Assert.Equal(200, up);
        Assert.Equal(100, down);
    }

    [Fact]
    public void CalculateFuelPlan_RejectsColdFuelAndCountsPrewarmAndMeltTime()
    {
        var firewood = new FuelOptionSpec("game:firewood", "Firewood", BurnTemperatureC: 700, BurnDurationSeconds: 24, AvailableCount: 32);
        var charcoal = new FuelOptionSpec("game:charcoal", "Charcoal", BurnTemperatureC: 1300, BurnDurationSeconds: 40, AvailableCount: 16);

        FuelRequirementResult coldResult = AlloyCalculatorLogic.CalculateFuelRequirement(
            firewood,
            currentFurnaceTempC: 20,
            requiredMeltTempC: 1084,
            crucibleStackFactor: 1,
            baseMeltDurationSeconds: 45);

        FuelRequirementResult hotResult = AlloyCalculatorLogic.CalculateFuelRequirement(
            charcoal,
            currentFurnaceTempC: 20,
            requiredMeltTempC: 1084,
            crucibleStackFactor: 1,
            baseMeltDurationSeconds: 45);

        Assert.False(coldResult.ReachesRequiredTemperature);
        Assert.True(coldResult.PiecesNeeded >= 1);
        Assert.True(hotResult.ReachesRequiredTemperature);
        Assert.True(hotResult.PiecesNeeded >= 2);
        Assert.True(hotResult.TotalSecondsNeeded > 45);
    }

    [Fact]
    public void FindNextWasteFreeUnits_AppliesStepMultipliers5_10_100()
    {
        int x5Alloy = AlloyCalculatorLogic.FindNextWasteFreeUnits(TinBronzeRecipe, [0.90, 0.10], currentUnits: 100, direction: 1, unitsPerNugget: 5, stepCount: 5);
        int x10Alloy = AlloyCalculatorLogic.FindNextWasteFreeUnits(TinBronzeRecipe, [0.90, 0.10], currentUnits: 100, direction: 1, unitsPerNugget: 5, stepCount: 10);
        int x100Alloy = AlloyCalculatorLogic.FindNextWasteFreeUnits(TinBronzeRecipe, [0.90, 0.10], currentUnits: 100, direction: 1, unitsPerNugget: 5, stepCount: 100);

        Assert.Equal(600, x5Alloy);
        Assert.Equal(1100, x10Alloy);
        Assert.Equal(AlloyCalculatorLogic.MaxTargetUnits, x100Alloy);

        int x5Pure = AlloyCalculatorLogic.FindNextWasteFreeUnits(PureCopperRecipe, [1.0], currentUnits: 100, direction: 1, unitsPerNugget: 5, stepCount: 5);
        int x10Pure = AlloyCalculatorLogic.FindNextWasteFreeUnits(PureCopperRecipe, [1.0], currentUnits: 100, direction: 1, unitsPerNugget: 5, stepCount: 10);
        int x100Pure = AlloyCalculatorLogic.FindNextWasteFreeUnits(PureCopperRecipe, [1.0], currentUnits: 100, direction: 1, unitsPerNugget: 5, stepCount: 100);

        Assert.Equal(125, x5Pure);
        Assert.Equal(150, x10Pure);
        Assert.Equal(600, x100Pure);
    }

    [Fact]
    public void CrucibleCleanupPlanner_ExtractsWrongAndExcessNuggetsAndKeepsValidTargetAmount()
    {
        var crucibleSlots = new List<CrucibleSlotItemSnapshot>
        {
            new(0, "game:ingot-copper", 12),
            new(1, "game:ingot-bismuth", 5),
            new(2, "game:ingot-copper", 10),
            new(3, "game:ingot-tin", 2)
        };
        var maxByMetal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["game:ingot-copper"] = 18,
            ["game:ingot-tin"] = 2
        };

        CrucibleCleanupPlan cleanup = CrucibleCleanupPlanner.PlanExtractions(crucibleSlots, maxByMetal);

        Assert.Equal(2, cleanup.Extractions.Count);
        Assert.Equal((1, 5), cleanup.Extractions[0]); // all 5 bismuth removed
        Assert.Equal((2, 4), cleanup.Extractions[1]); // 12 + 10 copper -> keep 18, extract 4
        Assert.Equal(18, cleanup.KeptItemsByMetal["game:ingot-copper"]);
        Assert.Equal(2, cleanup.KeptItemsByMetal["game:ingot-tin"]);
    }
}
