using SeraphQoL.Interaction;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class QuickSwapPlannerTests
{
    [Fact]
    public void BuildQuickSwapEntries_FiltersEmptyAndIncludesToolsLightsAndWeapons()
    {
        var slots = new List<QuickSwapSlotSnapshot>
        {
            new("hotbar", 0, "game:pickaxe-bronze", "Bronze pickaxe", IsTool: true, LightLevel: 0, IsActiveHotbar: true),
            new("hotbar", 1, "game:lantern-copper", "Copper lantern", IsTool: false, LightLevel: 18, IsActiveHotbar: false),
            new("hotbar", 2, "", "", IsTool: false, LightLevel: 0, IsActiveHotbar: false),
            new("hotbar", 3, "game:stone-granite", "Granite stone", IsTool: false, LightLevel: 0, IsActiveHotbar: false),
            new("backpack", 4, "game:torch-basic-lit-up", "Lit torch", IsTool: false, LightLevel: 14, IsActiveHotbar: false),
            new("hotbar", 5, "game:pickaxe-bronze", "Bronze pickaxe", IsTool: true, LightLevel: 0, IsActiveHotbar: false)
        };

        IReadOnlyList<QuickSwapCandidate> entries = QuickSwapPlanner.BuildCandidates(slots);

        Assert.Equal(3, entries.Count);
        Assert.Equal("game:pickaxe-bronze", entries[0].ItemCode);
        Assert.True(entries[0].IsSelected);
        Assert.Equal("game:lantern-copper", entries[1].ItemCode);
        Assert.Equal("game:torch-basic-lit-up", entries[2].ItemCode);

        IReadOnlyList<ToolModeDescriptor> radial = QuickSwapPlanner.ToRadialDescriptors(entries);
        Assert.Equal(3, radial.Count);
        Assert.True(radial[0].IsSelected);
    }
}
