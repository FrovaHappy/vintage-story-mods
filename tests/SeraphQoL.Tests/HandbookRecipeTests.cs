using SeraphQoL.Handbook;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class HandbookRecipeTests
{
    private static CraftingItemInfo Item(
        string code,
        int stackSize = 1,
        int maxStackSize = 64,
        int remainingDurability = 0,
        string? mergeKey = null)
    {
        return new CraftingItemInfo(
            ItemCode: code,
            StackSize: stackSize,
            MaxStackSize: maxStackSize,
            RemainingDurability: remainingDurability,
            MergeKey: mergeKey ?? code);
    }

    private static CraftingIngredientSpec Exact(string code, int qty = 1) =>
        new(
            Key: code,
            Quantity: qty,
            IsTool: false,
            ToolDurabilityCost: 0,
            Matches: item => string.Equals(item.ItemCode, code, StringComparison.OrdinalIgnoreCase));

    private static CraftingIngredientSpec Wildcard(string key, Func<string, bool> matcher, int qty = 1) =>
        new(
            Key: key,
            Quantity: qty,
            IsTool: false,
            ToolDurabilityCost: 0,
            Matches: item => matcher(item.ItemCode));

    private static CraftingIngredientSpec Tool(string key, Func<string, bool> matcher, int durabilityCost) =>
        new(
            Key: key,
            Quantity: 1,
            IsTool: true,
            ToolDurabilityCost: durabilityCost,
            Matches: item => matcher(item.ItemCode) && item.RemainingDurability >= durabilityCost);

    [Theory]
    [InlineData(0, 5, 1, 1)]
    [InlineData(4, 5, 1, 0)]
    [InlineData(0, 5, -1, 4)]
    [InlineData(2, 5, -7, 0)]
    [InlineData(0, 0, 1, 0)]
    public void VariantIndexCalculator_ComputeNextIndex_WrapsCircularly(int current, int total, int delta, int expected)
    {
        int actual = HandbookVariantIndexCalculator.ComputeNextIndex(current, total, delta);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void VariantIndexCalculator_ComputeAdjustedSecondCounter_WrapsNegativeUsingLcm()
    {
        int wrapped = HandbookVariantIndexCalculator.ComputeAdjustedSecondCounter(
            currentSecondCounter: 0,
            indexDelta: -1,
            unnamedVariantLengths: [4, 6]);

        // LCM(4, 6) = 12 -> 0 - 1 wraps to 11 (non-negative and congruent mod 4 and mod 6)
        Assert.Equal(11, wrapped);
        Assert.Equal(3, wrapped % 4);
        Assert.Equal(5, wrapped % 6);
    }

    [Fact]
    public void RecipeGridBounds_AlignsOnRightAndBottomEdgesOf3x3Grid()
    {
        var bounds = new RecipeGridBounds(width: 2, height: 2, outerSize: 3);

        // Inner slot 0 placed at outer slot 4 (row 1, col 1) -> occupies cols 1..2, rows 1..2
        Assert.True(bounds.TryAlign(outerIndex: 4, innerIndex: 0));
        Assert.Equal(1, bounds.XPos);
        Assert.Equal(1, bounds.YPos);
        Assert.False(bounds.Contains(0));
        Assert.True(bounds.Contains(4));
        Assert.True(bounds.Contains(8));
        Assert.Equal(3, bounds.ToInnerIndex(8));

        // Inner slot 0 cannot be placed at outer slot 8 (would overflow 3x3)
        Assert.False(bounds.TryAlign(outerIndex: 8, innerIndex: 0));
    }

    [Fact]
    public void CraftingFillPlanner_Shaped2x2_PreservesAlignedItemAndFillsRest()
    {
        // Asymmetric 2x2 recipe; grid has plank in slot 8 -> unique alignment is bottom-right (slots 4, 5, 7, 8)
        var grid = new CraftingItemInfo?[9];
        grid[8] = Item("game:plank-oak", 1);

        var sources = new CraftingItemInfo?[]
        {
            Item("game:stick", 10),
            Item("game:plank-oak", 10)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 2,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Exact("game:stick"), Exact("game:stick"),
                Exact("game:stick"), Exact("game:plank-oak")
            ],
            MatchesCurrentGrid: false);

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(grid, sources, [recipe], fillMax: false);

        Assert.True(plan.Success);
        Assert.Empty(plan.SlotsToClear);
        Assert.Equal(3, plan.Transfers.Count);
        Assert.Contains(plan.Transfers, t => t.TargetGridIndex == 4 && t.Quantity == 1);
        Assert.Contains(plan.Transfers, t => t.TargetGridIndex == 5 && t.Quantity == 1);
        Assert.Contains(plan.Transfers, t => t.TargetGridIndex == 7 && t.Quantity == 1);
    }

    [Fact]
    public void CraftingFillPlanner_ClearsIncompatibleGridItems_AndFillsMax()
    {
        var grid = new CraftingItemInfo?[9];
        grid[0] = Item("game:stone-granite", 2);

        var sources = new CraftingItemInfo?[]
        {
            Item("game:stick", 7)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Exact("game:stick"),
                Exact("game:stick")
            ],
            MatchesCurrentGrid: false);

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(grid, sources, [recipe], fillMax: true);

        Assert.True(plan.Success);
        Assert.Equal([0], plan.SlotsToClear);
        // 7 sticks -> 3 crafts of 2 sticks each (3 into slot 0, 3 into slot 3)
        int slot0Total = plan.Transfers.Where(t => t.TargetGridIndex == 0).Sum(t => t.Quantity);
        int slot3Total = plan.Transfers.Where(t => t.TargetGridIndex == 3).Sum(t => t.Quantity);
        Assert.Equal(3, slot0Total);
        Assert.Equal(3, slot3Total);
    }

    [Fact]
    public void CraftingFillPlanner_ShapelessRecipe_ReusesCompatibleAndPullsMissing()
    {
        var grid = new CraftingItemInfo?[9];
        grid[6] = Item("game:flint", 1);
        grid[2] = Item("game:dirt", 1);

        var sources = new CraftingItemInfo?[]
        {
            Item("game:stick", 4)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 2,
            Height: 1,
            Shapeless: true,
            Ingredients:
            [
                Exact("game:flint"),
                Exact("game:stick")
            ],
            MatchesCurrentGrid: false);

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(grid, sources, [recipe], fillMax: false);

        Assert.True(plan.Success);
        Assert.Equal([2], plan.SlotsToClear);
        CraftingTransferStep step = Assert.Single(plan.Transfers);
        Assert.Equal(0, step.SourceSlotIndex);
        Assert.Equal(1, step.Quantity);
        Assert.NotEqual(6, step.TargetGridIndex);
    }

    [Fact]
    public void CraftingFillPlanner_ToolRecipe_ChecksDurabilityAndDoesNotConsumeExtraPerMaxCraft()
    {
        var grid = new CraftingItemInfo?[9];
        var sources = new CraftingItemInfo?[]
        {
            Item("game:saw-copper", stackSize: 1, maxStackSize: 1, remainingDurability: 5),
            Item("game:log-oak", stackSize: 5, maxStackSize: 16)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Tool("saw-*", c => c.StartsWith("game:saw-", StringComparison.Ordinal), durabilityCost: 3),
                Wildcard("log-*", c => c.StartsWith("game:log-", StringComparison.Ordinal), qty: 1)
            ],
            MatchesCurrentGrid: false);

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(grid, sources, [recipe], fillMax: true);

        Assert.True(plan.Success);
        Assert.Equal(1, plan.Transfers.Where(t => t.TargetGridIndex == 0).Sum(t => t.Quantity));
        Assert.Equal(5, plan.Transfers.Where(t => t.TargetGridIndex == 3).Sum(t => t.Quantity));
    }

    [Fact]
    public void CraftingFillPlanner_FailsWhenToolLacksRequiredDurability()
    {
        var grid = new CraftingItemInfo?[9];
        var sources = new CraftingItemInfo?[]
        {
            Item("game:saw-copper", stackSize: 1, maxStackSize: 1, remainingDurability: 1),
            Item("game:log-oak", stackSize: 4, maxStackSize: 16)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Tool("saw-*", c => c.StartsWith("game:saw-", StringComparison.Ordinal), durabilityCost: 3),
                Exact("game:log-oak")
            ],
            MatchesCurrentGrid: false);

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(grid, sources, [recipe], fillMax: false);

        Assert.False(plan.Success);
        Assert.Empty(plan.Transfers);
    }

    [Fact]
    public void CraftingFillPlanner_PrefersRecipeMatchingCurrentGrid()
    {
        var grid = new CraftingItemInfo?[9];
        grid[0] = Item("game:plank-oak", 1);

        var sources = new CraftingItemInfo?[]
        {
            Item("game:plank-birch", 4),
            Item("game:plank-oak", 4)
        };

        var birchRecipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 2,
            Shapeless: false,
            Ingredients: [Exact("game:plank-birch"), Exact("game:plank-birch")],
            MatchesCurrentGrid: false);

        var oakRecipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 2,
            Shapeless: false,
            Ingredients: [Exact("game:plank-oak"), Exact("game:plank-oak")],
            MatchesCurrentGrid: true);

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(grid, sources, [birchRecipe, oakRecipe], fillMax: false);

        Assert.True(plan.Success);
        Assert.Empty(plan.SlotsToClear);
        CraftingTransferStep transfer = Assert.Single(plan.Transfers);
        Assert.Equal(1, transfer.SourceSlotIndex); // oak source
        Assert.Equal(3, transfer.TargetGridIndex);
        Assert.Equal(1, transfer.Quantity);
    }

    [Fact]
    public void FindMissingIngredientIndices_ReturnsEmptyWhenAllIngredientsAvailable()
    {
        var grid = new CraftingItemInfo?[9];
        var sources = new CraftingItemInfo?[]
        {
            Item("game:plank-oak", 4)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 2,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Exact("game:plank-oak"), Exact("game:plank-oak"),
                Exact("game:plank-oak"), Exact("game:plank-oak")
            ],
            MatchesCurrentGrid: false);

        IReadOnlyList<int> missing = CraftingFillPlanner.FindMissingIngredientIndices(grid, sources, recipe);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingIngredientIndices_MarksOnlyDeficientSlotsAcrossCumulativeInventory()
    {
        var grid = new CraftingItemInfo?[9];
        var sources = new CraftingItemInfo?[]
        {
            Item("game:plank-oak", 2)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 2,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Exact("game:plank-oak"), Exact("game:plank-oak"),
                Exact("game:plank-oak"), Exact("game:plank-oak")
            ],
            MatchesCurrentGrid: false);

        IReadOnlyList<int> missing = CraftingFillPlanner.FindMissingIngredientIndices(grid, sources, recipe);

        Assert.Equal([2, 3], missing);
    }

    [Fact]
    public void FindMissingIngredientIndices_AccountsForAlignedGridItemsAndToolDurability()
    {
        // 1x2 recipe aligned at bottom-right (slots 5 and 8): slot 5 already has valid log
        var grid = new CraftingItemInfo?[9];
        grid[5] = Item("game:log-oak", 1);

        var sources = new CraftingItemInfo?[]
        {
            Item("game:saw-copper", stackSize: 1, maxStackSize: 1, remainingDurability: 1) // too broken for cost 3
        };

        var recipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 2,
            Shapeless: false,
            Ingredients:
            [
                Exact("game:log-oak"),
                Tool("saw-*", c => c.StartsWith("game:saw-", StringComparison.Ordinal), durabilityCost: 3)
            ],
            MatchesCurrentGrid: false);

        IReadOnlyList<int> missing = CraftingFillPlanner.FindMissingIngredientIndices(grid, sources, recipe);

        Assert.Equal([1], missing);
    }

    [Fact]
    public void FindMissingIngredientIndices_HandlesSingleSlotQuantityDeficit()
    {
        var grid = new CraftingItemInfo?[9];
        grid[0] = Item("game:drygrass", 2);
        var sources = new CraftingItemInfo?[]
        {
            Item("game:drygrass", 1)
        };

        var recipe = new CraftingRecipeSpec(
            Width: 1,
            Height: 1,
            Shapeless: false,
            Ingredients: [Exact("game:drygrass", qty: 4)],
            MatchesCurrentGrid: false);

        IReadOnlyList<int> missing = CraftingFillPlanner.FindMissingIngredientIndices(grid, sources, recipe);

        Assert.Equal([0], missing);
    }

    [Fact]
    public void HandbookFillButton_ComputeNextOffsetX_AdvancesPastButtonColumnForLastButtonOnly()
    {
        double gridEndOffsetX = 160.0;
        double buttonX = gridEndOffsetX - 3.0;
        double buttonWidth = 24.0;
        double rightGap = 4.0;

        double afterFirstButton = HandbookFillButtonComponent.ComputeNextOffsetX(
            gridEndOffsetX, buttonX, buttonWidth, rightGap, advanceCursor: false);
        double afterSecondButton = HandbookFillButtonComponent.ComputeNextOffsetX(
            afterFirstButton, buttonX, buttonWidth, rightGap, advanceCursor: true);

        Assert.Equal(gridEndOffsetX, afterFirstButton);
        Assert.True(afterSecondButton >= buttonX + buttonWidth + rightGap);
        Assert.Equal(185.0, afterSecondButton);
    }
}
