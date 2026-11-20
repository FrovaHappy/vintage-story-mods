using SeraphQoL.Inventory;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class HotbarRefillPlannerTests
{
    private static RefillRuleOptions DefaultRules(
        bool consumables = true,
        bool blocks = true,
        bool tools = true,
        int threshold = 5,
        bool preferSmallest = true,
        params string[] blacklist) =>
        new(consumables, blocks, tools, threshold, preferSmallest, blacklist);

    [Fact]
    public void FindRefillPlan_WhenBlockStackDepletes_SelectsSmallestUnlockedMatchingBackpackStack()
    {
        var previousHotbar = new RefillSlotSnapshot("hotbar", 0, "game:cobblestone-granite", RefillItemKind.Block, 1, 0, 0, false, false);
        var currentHotbar = new RefillSlotSnapshot("hotbar", 0, null, RefillItemKind.None, 0, 0, 0, false, false);

        var backpack = new List<RefillSlotSnapshot>
        {
            new("backpack", 0, "game:cobblestone-granite", RefillItemKind.Block, 64, 0, 0, false, false),
            new("backpack", 1, "game:cobblestone-granite", RefillItemKind.Block, 12, 0, 0, true, false),
            new("backpack", 2, "game:cobblestone-granite", RefillItemKind.Block, 24, 0, 0, false, false)
        };

        RefillPlan? plan = HotbarRefillPlanner.FindRefillPlan(previousHotbar, currentHotbar, backpack, DefaultRules());

        Assert.NotNull(plan);
        Assert.Equal(2, plan!.SourceBackpackSlotIndex);
        Assert.Equal(0, plan.TargetHotbarSlotIndex);
        Assert.False(plan.IsPreventiveToolSwap);
        Assert.Equal("game:cobblestone-granite", plan.ItemCode);
    }

    [Fact]
    public void FindRefillPlan_WhenPreferSmallestFalse_SelectsLargestMatchingStack()
    {
        var previousHotbar = new RefillSlotSnapshot("hotbar", 1, "game:bread-rye", RefillItemKind.Consumable, 1, 0, 0, false, false);
        var currentHotbar = new RefillSlotSnapshot("hotbar", 1, null, RefillItemKind.None, 0, 0, 0, false, false);

        var backpack = new List<RefillSlotSnapshot>
        {
            new("backpack", 0, "game:bread-rye", RefillItemKind.Consumable, 6, 0, 0, false, false),
            new("backpack", 3, "game:bread-rye", RefillItemKind.Consumable, 32, 0, 0, false, false)
        };

        RefillPlan? plan = HotbarRefillPlanner.FindRefillPlan(
            previousHotbar, currentHotbar, backpack, DefaultRules(preferSmallest: false));

        Assert.NotNull(plan);
        Assert.Equal(3, plan!.SourceBackpackSlotIndex);
    }

    [Fact]
    public void FindRefillPlan_WhenToolReachesThreshold_TriggersPreventiveSwapWithHealthyReplacement()
    {
        var previousHotbar = new RefillSlotSnapshot("hotbar", 2, "game:pickaxe-copper", RefillItemKind.Tool, 1, 6, 250, false, false);
        var currentHotbar = new RefillSlotSnapshot("hotbar", 2, "game:pickaxe-copper", RefillItemKind.Tool, 1, 5, 250, false, false);

        var backpack = new List<RefillSlotSnapshot>
        {
            new("backpack", 0, "game:pickaxe-copper", RefillItemKind.Tool, 1, 3, 250, false, false),
            new("backpack", 1, "game:pickaxe-copper", RefillItemKind.Tool, 1, 220, 250, false, false)
        };

        RefillPlan? plan = HotbarRefillPlanner.FindRefillPlan(previousHotbar, currentHotbar, backpack, DefaultRules(threshold: 5));

        Assert.NotNull(plan);
        Assert.Equal(1, plan!.SourceBackpackSlotIndex);
        Assert.Equal(2, plan.TargetHotbarSlotIndex);
        Assert.True(plan.IsPreventiveToolSwap);
    }

    [Fact]
    public void FindRefillPlan_RespectsLockedTargetBlacklistAndDisabledCategories()
    {
        var previousHotbar = new RefillSlotSnapshot("hotbar", 0, "game:torch-basic-lit", RefillItemKind.Block, 1, 0, 0, true, false);
        var currentHotbar = new RefillSlotSnapshot("hotbar", 0, null, RefillItemKind.None, 0, 0, 0, true, false);
        var backpack = new List<RefillSlotSnapshot>
        {
            new("backpack", 0, "game:torch-basic-lit", RefillItemKind.Block, 16, 0, 0, false, false)
        };

        Assert.Null(HotbarRefillPlanner.FindRefillPlan(previousHotbar, currentHotbar, backpack, DefaultRules()));

        var unlockedPrev = previousHotbar with { IsLocked = false };
        var unlockedCurr = currentHotbar with { IsLocked = false };
        Assert.Null(HotbarRefillPlanner.FindRefillPlan(unlockedPrev, unlockedCurr, backpack, DefaultRules(blacklist: "game:torch-*")));
        Assert.Null(HotbarRefillPlanner.FindRefillPlan(unlockedPrev, unlockedCurr, backpack, DefaultRules(blocks: false)));
    }
}
