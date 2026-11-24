using Vintagestory.API.Common;
using Xunit;

namespace Comerciantes.Tests;

public sealed class ItemValueOverridesTests
{
    private static ItemValuationRegistry CreateBaseRegistry()
    {
        var registry = new ItemValuationRegistry();
        registry.ApplyExplicitConfig(TestRepoPaths.LoadItemValuesFile());
        return registry;
    }

    // ── Exact override ──────────────────────────────────────────────────────

    [Fact]
    public void ApplyConfigOverrides_ExactKey_OverridesExplicitAndInferredValues()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        // copper ingot is 1.0 in item_values.json; override to 2.5
        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:ingot-copper"] = 2.5
        });

        double val = registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-copper"), 1));
        Assert.Equal(2.5, val, 4);
    }

    [Fact]
    public void ApplyConfigOverrides_ExactKeyZero_BlacklistsItem()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:ingot-copper"] = 0.0
        });

        double val = registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-copper"), 1));
        Assert.Equal(0.0, val, 4);
    }

    // ── Wildcard (suffix *) ─────────────────────────────────────────────────

    [Fact]
    public void ApplyConfigOverrides_WildcardKey_MatchesItemsByPrefix()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:axe-felling-*"] = 3.0
        });

        // tinbronze is explicitly 4.0 in item_values.json; wildcard overrides that
        double tinVal = registry.GetBaseValue(new ItemStack(new TestItem("game:axe-felling-tinbronze", maxStackSize: 1), 1));
        Assert.Equal(3.0, tinVal, 4);

        // bismuthbronze is 5.0 in item_values.json; also overridden
        double bisVal = registry.GetBaseValue(new ItemStack(new TestItem("game:axe-felling-bismuthbronze", maxStackSize: 1), 1));
        Assert.Equal(3.0, bisVal, 4);
    }

    [Fact]
    public void ApplyConfigOverrides_WildcardKey_DoesNotMatchUnrelatedItems()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:axe-felling-*"] = 3.0
        });

        // copper ingot must stay at 1.0 (from item_values.json)
        double copperVal = registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-copper"), 1));
        Assert.Equal(1.0, copperVal, 4);
    }

    [Fact]
    public void ApplyConfigOverrides_ExactKeyWinsOverWildcard()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        // Wildcard sets all axes to 3.0, but tinbronze is pinned exactly to 6.0
        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:axe-felling-*"] = 3.0,
            ["game:axe-felling-tinbronze"] = 6.0
        });

        double tinVal = registry.GetBaseValue(new ItemStack(new TestItem("game:axe-felling-tinbronze", maxStackSize: 1), 1));
        Assert.Equal(6.0, tinVal, 4);

        double bisVal = registry.GetBaseValue(new ItemStack(new TestItem("game:axe-felling-bismuthbronze", maxStackSize: 1), 1));
        Assert.Equal(3.0, bisVal, 4);
    }

    [Fact]
    public void ApplyConfigOverrides_LongerWildcardWinsOverShorterWildcard()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        // "game:axe-*" is broader; "game:axe-felling-*" is more specific -> longer prefix wins
        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:axe-*"] = 1.0,
            ["game:axe-felling-*"] = 5.0
        });

        double fellingVal = registry.GetBaseValue(new ItemStack(new TestItem("game:axe-felling-tinbronze", maxStackSize: 1), 1));
        Assert.Equal(5.0, fellingVal, 4);

        // "game:axe-splitting-*" matches only the shorter wildcard
        double splittingVal = registry.GetBaseValue(new ItemStack(new TestItem("game:axe-splitting-copper", maxStackSize: 1), 1));
        Assert.Equal(1.0, splittingVal, 4);
    }

    // ── Interaction with existing priority chain ────────────────────────────

    [Fact]
    public void ApplyConfigOverrides_WildcardOverridesHeuristicFamilyValue()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        // iron ingot is resolved via EstimateByPath as 1.5 * 1.5 = 2.25; override with wildcard
        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:ingot-*"] = 1.0
        });

        double ironVal = registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-iron"), 1));
        Assert.Equal(1.0, ironVal, 4);
    }

    // ── Re-application clears previous overrides ────────────────────────────

    [Fact]
    public void ApplyConfigOverrides_CalledTwice_ReplacesAllPreviousOverrides()
    {
        ItemValuationRegistry registry = CreateBaseRegistry();

        registry.ApplyConfigOverrides(new Dictionary<string, double>
        {
            ["game:ingot-copper"] = 99.0
        });

        // Second call with empty dict removes the override
        registry.ApplyConfigOverrides(new Dictionary<string, double>());

        double val = registry.GetBaseValue(new ItemStack(new TestItem("game:ingot-copper"), 1));
        Assert.Equal(1.0, val, 4); // back to item_values.json value
    }

    // ── ComerciantesConfig holds the dict ──────────────────────────────────

    [Fact]
    public void ComerciantesConfig_ItemValueOverrides_DefaultsToEmpty()
    {
        var config = new ComerciantesConfig();
        Assert.NotNull(config.ItemValueOverrides);
        Assert.Empty(config.ItemValueOverrides);
    }

    [Fact]
    public void ComerciantesConfig_ItemValueOverrides_SerializesAndDeserializesRoundtrip()
    {
        var config = new ComerciantesConfig
        {
            ItemValueOverrides = new Dictionary<string, double>
            {
                ["game:ingot-copper"] = 2.5,
                ["game:axe-felling-*"] = 3.0
            }
        };

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        ComerciantesConfig? loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<ComerciantesConfig>(json);

        Assert.NotNull(loaded);
        Assert.Equal(2.5, loaded.ItemValueOverrides["game:ingot-copper"], 4);
        Assert.Equal(3.0, loaded.ItemValueOverrides["game:axe-felling-*"], 4);
    }
}
