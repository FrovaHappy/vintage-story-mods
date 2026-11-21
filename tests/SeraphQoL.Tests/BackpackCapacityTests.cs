using SeraphQoL.Backpacks;
using SeraphQoL.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class BackpackCapacityTests
{
    private static BackpackCollectibleDescriptor MakeBag(
        string code,
        int baseSlots,
        bool hasRestrictedStorageFlags = false,
        bool hasStorageTags = false,
        string className = "Item")
    {
        return new BackpackCollectibleDescriptor(
            Code: code,
            ClassName: className,
            BaseQuantitySlots: baseSlots,
            HasRestrictedStorageFlags: hasRestrictedStorageFlags,
            HasStorageTags: hasStorageTags);
    }

    private static EffectiveServerPolicy PolicyFor(BackpackPreset preset)
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.ApplyBackpackPreset(preset);
        return EffectiveServerPolicy.FromServerConfig(cfg);
    }

    [Theory]
    [InlineData("game:glider", 2, false, false, "ItemGlider", BackpackCategory.Excluded)]
    [InlineData("game:backpack-normal", 6, false, false, "Item", BackpackCategory.StandardBackpacks)]
    [InlineData("game:backpack-sturdy", 8, false, false, "Item", BackpackCategory.StandardBackpacks)]
    [InlineData("game:basket-normal-reed", 3, false, false, "Item", BackpackCategory.StandardBackpacks)]
    [InlineData("game:hunterbackpack", 4, false, false, "Item", BackpackCategory.StandardBackpacks)]
    [InlineData("game:linensack", 5, false, false, "Item", BackpackCategory.StandardBackpacks)]
    [InlineData("game:miningbag", 12, true, false, "Item", BackpackCategory.SpecializedBags)]
    [InlineData("game:quiver", 10, true, false, "Item", BackpackCategory.SpecializedBags)]
    [InlineData("butchering:butcherybag-leather", 5, false, false, "Item", BackpackCategory.SpecializedBags)]
    [InlineData("lumbersling:woodpack_8", 8, false, false, "Item", BackpackCategory.SpecializedBags)]
    [InlineData("wildcrafttree:foragebasket", 10, false, false, "Item", BackpackCategory.SpecializedBags)]
    [InlineData("game:hoovedwearables-lowerbackside-saddlebags1", 16, false, false, "Item", BackpackCategory.Saddlebags)]
    [InlineData("game:hoovedwearables-lowerbackside-saddlebags2", 20, false, false, "Item", BackpackCategory.Saddlebags)]
    [InlineData("ithaniabackpacks:ithanialeather", 8, false, false, "Item", BackpackCategory.ModdedBackpacks)]
    [InlineData("theremnants:wickerpack", 5, false, false, "Item", BackpackCategory.ModdedBackpacks)]
    [InlineData("immersivebackpacks:hunterbackpack-right", 4, false, false, "Item", BackpackCategory.ModdedBackpacks)]
    public void Classifier_CategorizesVanillaSpecializedSaddlebagsAndModdedBackpacks(
        string code,
        int baseSlots,
        bool hasRestrictedStorageFlags,
        bool hasStorageTags,
        string className,
        BackpackCategory expected)
    {
        var classifier = new BackpackClassifier();
        BackpackCollectibleDescriptor bag = MakeBag(code, baseSlots, hasRestrictedStorageFlags, hasStorageTags, className);

        Assert.Equal(expected, classifier.Classify(bag));
    }

    [Fact]
    public void Calculator_WhenVanillaOrDisabled_PreservesBaseSlots()
    {
        var calculator = new BackpackCapacityCalculator();
        EffectiveServerPolicy policy = PolicyFor(BackpackPreset.Vanilla);

        Assert.False(policy.EnableBackpackExpansion);
        Assert.Equal(6, calculator.CalculateTargetSlots(MakeBag("game:backpack-normal", 6), policy));
        Assert.Equal(16, calculator.CalculateTargetSlots(MakeBag("game:hoovedwearables-lowerbackside-saddlebags1", 16), policy));
        Assert.Equal(20, calculator.CalculateTargetSlots(MakeBag("game:hoovedwearables-lowerbackside-saddlebags2", 20), policy));
        Assert.Equal(5, calculator.CalculateTargetSlots(MakeBag("butchering:butcherybag-leather", 5), policy));
    }

    [Fact]
    public void Calculator_BiggerBackpacks2x_DoublesVanillaSaddlebagsAndModdedBagsAndExcludesGlider()
    {
        var calculator = new BackpackCapacityCalculator();
        EffectiveServerPolicy policy = PolicyFor(BackpackPreset.BiggerBackpacks2x);

        Assert.Equal(6, calculator.CalculateTargetSlots(MakeBag("game:basket-normal-reed", 3), policy));
        Assert.Equal(8, calculator.CalculateTargetSlots(MakeBag("game:hunterbackpack", 4), policy));
        Assert.Equal(10, calculator.CalculateTargetSlots(MakeBag("game:linensack", 5), policy));
        Assert.Equal(12, calculator.CalculateTargetSlots(MakeBag("game:backpack-normal", 6), policy));
        Assert.Equal(16, calculator.CalculateTargetSlots(MakeBag("game:backpack-sturdy", 8), policy));
        Assert.Equal(24, calculator.CalculateTargetSlots(MakeBag("game:miningbag", 12, hasRestrictedStorageFlags: true), policy));
        Assert.Equal(28, calculator.CalculateTargetSlots(MakeBag("game:miningbagsturdy", 14, hasRestrictedStorageFlags: true), policy));
        Assert.Equal(20, calculator.CalculateTargetSlots(MakeBag("game:quiver", 10, hasRestrictedStorageFlags: true), policy));
        Assert.Equal(32, calculator.CalculateTargetSlots(MakeBag("game:hoovedwearables-lowerbackside-saddlebags1", 16), policy));
        Assert.Equal(40, calculator.CalculateTargetSlots(MakeBag("game:hoovedwearables-lowerbackside-saddlebags2", 20), policy));
        Assert.Equal(10, calculator.CalculateTargetSlots(MakeBag("butchering:butcherybag-leather", 5), policy));
        Assert.Equal(16, calculator.CalculateTargetSlots(MakeBag("ithaniabackpacks:ithanialeather", 8), policy));
        Assert.Equal(2, calculator.CalculateTargetSlots(MakeBag("game:glider", 2, className: "ItemGlider"), policy));
    }

    [Fact]
    public void Calculator_AppliesCategoryMultipliersBlacklistOverridesAndCap()
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.ApplyBackpackPreset(BackpackPreset.Balanced15x);
        cfg.StandardBackpacksMultiplier = 2.0;
        cfg.SpecializedBagsMultiplier = 1.5;
        cfg.SaddlebagsMultiplier = 2.5;
        cfg.ModdedBackpacksMultiplier = 1.5;
        cfg.MaxBackpackSlotsCap = 36;
        cfg.BackpackBlacklist.Add("game:basket-*");
        cfg.BackpackOverrides["ithaniabackpacks:*"] = 22;
        cfg.BackpackOverrides["ithaniabackpacks:ithanialeather"] = 18;
        cfg.BackpackOverrides["game:glider"] = 4;

        EffectiveServerPolicy policy = EffectiveServerPolicy.FromServerConfig(cfg);
        var calculator = new BackpackCapacityCalculator();

        Assert.Equal(3, calculator.CalculateTargetSlots(MakeBag("game:basket-normal-reed", 3), policy));
        Assert.Equal(12, calculator.CalculateTargetSlots(MakeBag("game:backpack-normal", 6), policy));
        Assert.Equal(18, calculator.CalculateTargetSlots(MakeBag("game:miningbag", 12, hasRestrictedStorageFlags: true), policy));
        Assert.Equal(36, calculator.CalculateTargetSlots(MakeBag("game:hoovedwearables-lowerbackside-saddlebags1", 16), policy));
        Assert.Equal(18, calculator.CalculateTargetSlots(MakeBag("ithaniabackpacks:ithanialeather", 8), policy));
        Assert.Equal(22, calculator.CalculateTargetSlots(MakeBag("ithaniabackpacks:ithaniasturdy", 10), policy));
        Assert.Equal(4, calculator.CalculateTargetSlots(MakeBag("game:glider", 2, className: "ItemGlider"), policy));
    }

    [Theory]
    [InlineData("slot-0", true, 0)]
    [InlineData("slot-15", true, 15)]
    [InlineData("slot-", false, -1)]
    [InlineData("other-3", false, -1)]
    [InlineData(null, false, -1)]
    public void TryParseSlotKeyIndex_ParsesBagSlotKeysSafely(string? key, bool expectedOk, int expectedIndex)
    {
        bool ok = BackpackCapacityCalculator.TryParseSlotKeyIndex(key, out int idx);
        Assert.Equal(expectedOk, ok);
        if (expectedOk)
        {
            Assert.Equal(expectedIndex, idx);
        }
    }

    [Fact]
    public void ExtractOverflowAndSyncSlots_ExpandsExistingBagWithoutTouchingExistingItems()
    {
        var slotsTree = new TreeAttribute();
        var item = new Item { Code = new AssetLocation("game:stick") };
        for (int i = 0; i < 6; i++)
        {
            slotsTree[$"slot-{i}"] = new ItemstackAttribute(i == 2 ? new ItemStack(item, 5) : null);
        }

        List<ItemStack> overflow = BackpackAssetsService.ExtractOverflowAndSyncSlots(slotsTree, 12);

        Assert.Empty(overflow);
        for (int i = 0; i < 12; i++)
        {
            Assert.True(slotsTree.HasAttribute($"slot-{i}"));
        }

        var preserved = slotsTree["slot-2"]?.GetValue() as ItemStack;
        Assert.NotNull(preserved);
        Assert.Equal(5, preserved!.StackSize);
        Assert.Null(slotsTree["slot-11"]?.GetValue());
    }

    [Fact]
    public void ExtractOverflowAndSyncSlots_ExtractsOverflowItemsAndRemovesAllTrailingKeysOnShrink()
    {
        var slotsTree = new TreeAttribute();
        var item = new Item { Code = new AssetLocation("game:stick") };
        for (int i = 0; i < 12; i++)
        {
            ItemStack? stack = i switch
            {
                1 => new ItemStack(item, 4),
                7 => new ItemStack(item, 3),
                10 => new ItemStack(item, 9),
                _ => null
            };
            slotsTree[$"slot-{i}"] = new ItemstackAttribute(stack);
        }

        List<ItemStack> overflow = BackpackAssetsService.ExtractOverflowAndSyncSlots(slotsTree, 6);

        Assert.Equal(2, overflow.Count);
        Assert.Contains(overflow, s => s.StackSize == 3);
        Assert.Contains(overflow, s => s.StackSize == 9);

        var kept = slotsTree["slot-1"]?.GetValue() as ItemStack;
        Assert.NotNull(kept);
        Assert.Equal(4, kept!.StackSize);

        for (int i = 0; i < 6; i++)
        {
            Assert.True(slotsTree.HasAttribute($"slot-{i}"));
        }
        for (int i = 6; i < 12; i++)
        {
            Assert.False(slotsTree.HasAttribute($"slot-{i}"));
        }
    }
}
