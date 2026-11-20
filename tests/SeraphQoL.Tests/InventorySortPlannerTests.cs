using SeraphQoL.Config;
using SeraphQoL.Inventory;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class InventorySortPlannerTests
{
    private static SortSlotSnapshot Slot(
        int index,
        string? code = null,
        int stackSize = 0,
        int maxStackSize = 64,
        string name = "",
        string category = "general",
        bool isSortable = true,
        bool isLocked = false,
        bool isFavorite = false,
        bool isPerishable = false,
        double freshnessRatio = 1.0)
    {
        return new SortSlotSnapshot(
            SlotIndex: index,
            InventoryClass: "backpack",
            IsSortable: isSortable,
            IsLocked: isLocked,
            IsFavorite: isFavorite,
            ItemCode: code,
            DisplayName: string.IsNullOrEmpty(name) ? (code ?? string.Empty) : name,
            Category: category,
            StackSize: stackSize,
            MaxStackSize: maxStackSize,
            IsPerishable: isPerishable,
            FreshnessRatio: freshnessRatio,
            OriginalOrder: index);
    }

    [Fact]
    public void Plan_CompactsPartialStacksAndSortsByName_ConservingTotalCounts()
    {
        var slots = new[]
        {
            Slot(0, "game:stone-granite", 10, name: "Granite stone", category: "blocks"),
            Slot(1, null),
            Slot(2, "game:axe-copper", 1, maxStackSize: 1, name: "Copper axe", category: "tools"),
            Slot(3, "game:stone-granite", 60, name: "Granite stone", category: "blocks")
        };

        SortPlan plan = InventorySortPlanner.BuildPlan(slots, SortOrder.Name, stackPerishables: false, skipFavorites: true);

        Assert.Equal("game:axe-copper", plan.Assignments[0].ItemCode);
        Assert.Equal(1, plan.Assignments[0].StackSize);
        Assert.Equal("game:stone-granite", plan.Assignments[1].ItemCode);
        Assert.Equal(64, plan.Assignments[1].StackSize);
        Assert.Equal("game:stone-granite", plan.Assignments[2].ItemCode);
        Assert.Equal(6, plan.Assignments[2].StackSize);
        Assert.Null(plan.Assignments[3].ItemCode);
    }

    [Fact]
    public void Plan_PreservesLockedAndFavoriteSlotsInPlace()
    {
        var slots = new[]
        {
            Slot(0, "game:torch", 8, name: "Torch", isLocked: true),
            Slot(1, "game:stone-granite", 12, name: "Granite stone"),
            Slot(2, "game:bandage-clean", 4, name: "Clean bandage", isFavorite: true),
            Slot(3, "game:charcoal", 16, name: "Charcoal")
        };

        SortPlan plan = InventorySortPlanner.BuildPlan(slots, SortOrder.Name, stackPerishables: false, skipFavorites: true);

        // Locked slot 0 and favorite slot 2 remain untouched
        Assert.Equal("game:torch", plan.Assignments[0].ItemCode);
        Assert.Equal(8, plan.Assignments[0].StackSize);
        Assert.Equal("game:bandage-clean", plan.Assignments[2].ItemCode);
        Assert.Equal(4, plan.Assignments[2].StackSize);

        // Non-protected slots 1 and 3 are sorted alphabetically ("Charcoal" before "Granite stone")
        Assert.Equal("game:charcoal", plan.Assignments[1].ItemCode);
        Assert.Equal("game:stone-granite", plan.Assignments[3].ItemCode);
    }

    [Fact]
    public void Plan_Perishables_RespectsStackPerishablesFlagAndBlendsFreshness()
    {
        var slots = new[]
        {
            Slot(0, "game:bread-rye", 10, maxStackSize: 32, name: "Rye bread", isPerishable: true, freshnessRatio: 0.90),
            Slot(1, "game:bread-rye", 10, maxStackSize: 32, name: "Rye bread", isPerishable: true, freshnessRatio: 0.50)
        };

        SortPlan separate = InventorySortPlanner.BuildPlan(slots, SortOrder.Name, stackPerishables: false, skipFavorites: true);
        Assert.Equal(10, separate.Assignments[0].StackSize);
        Assert.Equal(10, separate.Assignments[1].StackSize);
        Assert.Equal(0.90, separate.Assignments[0].FreshnessRatio, 3);
        Assert.Equal(0.50, separate.Assignments[1].FreshnessRatio, 3);

        SortPlan merged = InventorySortPlanner.BuildPlan(slots, SortOrder.Name, stackPerishables: true, skipFavorites: true);
        Assert.Equal(20, merged.Assignments[0].StackSize);
        Assert.Equal(0.70, merged.Assignments[0].FreshnessRatio, 3);
        Assert.Null(merged.Assignments[1].ItemCode);
    }

    [Fact]
    public void Plan_SortByStackCountDescending_PlacesLargestAccumulatedCountFirst()
    {
        var slots = new[]
        {
            Slot(0, "game:stick", 5, name: "Stick"),
            Slot(1, "game:stone-basalt", 20, name: "Basalt"),
            Slot(2, "game:stick", 30, name: "Stick")
        };

        SortPlan plan = InventorySortPlanner.BuildPlan(slots, SortOrder.StackCount, stackPerishables: false, skipFavorites: true);

        Assert.Equal("game:stick", plan.Assignments[0].ItemCode);
        Assert.Equal(35, plan.Assignments[0].StackSize);
        Assert.Equal("game:stone-basalt", plan.Assignments[1].ItemCode);
        Assert.Equal(20, plan.Assignments[1].StackSize);
    }
}
