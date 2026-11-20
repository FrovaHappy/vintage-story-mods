using SeraphQoL.Config;
using SeraphQoL.StackSizes;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class StackSizeTests
{
    private static StackCollectibleDescriptor MakeItem(
        string code,
        int vanillaMaxStack = 64,
        bool isBlock = false,
        int durability = 0,
        bool hasTool = false,
        bool hasNutrition = false,
        bool isLiquid = false,
        bool isBackpack = false,
        bool isWearable = false,
        string className = "Item",
        params string[] tabs)
    {
        return new StackCollectibleDescriptor(
            Code: code,
            ClassName: className,
            VanillaMaxStackSize: vanillaMaxStack,
            IsBlock: isBlock,
            Durability: durability,
            HasTool: hasTool,
            HasNutritionProps: hasNutrition,
            IsLiquid: isLiquid,
            IsBackpackStorage: isBackpack,
            IsWearable: isWearable,
            CreativeTabs: tabs);
    }

    private static EffectiveServerPolicy PolicyFor(StackSizePreset preset)
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.ApplyStackPreset(preset);
        return EffectiveServerPolicy.FromServerConfig(cfg);
    }

    [Theory]
    [InlineData("game:pickaxe-copper", 1, false, 250, true, false, false, false, "ItemPickaxe")]
    [InlineData("game:armor-body-chain-iron", 1, false, 900, false, false, false, true, "ItemWearable")]
    [InlineData("game:waterportion", 100, false, 0, false, true, false, false, "ItemLiquidPortion")]
    [InlineData("game:miningbag", 1, false, 0, false, false, true, false, "ItemBag")]
    [InlineData("game:skep-populated-east", 1, true, 0, false, false, false, false, "BlockSkep")]
    [InlineData("game:bowl-meal", 1, true, 0, false, false, false, false, "BlockMeal")]
    public void Classifier_ProtectsToolsArmorLiquidsBackpacksPopulatedSkepsAndMeals(
        string code,
        int vanillaMaxStack,
        bool isBlock,
        int durability,
        bool hasTool,
        bool isLiquid,
        bool isBackpack,
        bool isWearable,
        string className)
    {
        var classifier = new StackSizeClassifier();
        StackCollectibleDescriptor item = MakeItem(
            code,
            vanillaMaxStack: vanillaMaxStack,
            isBlock: isBlock,
            durability: durability,
            hasTool: hasTool,
            isLiquid: isLiquid,
            isBackpack: isBackpack,
            isWearable: isWearable,
            className: className);

        Assert.Equal(StackCategory.ProtectedSingle, classifier.Classify(item));
    }

    [Theory]
    [InlineData("game:cobblestone-granite", 64, true, false, false, false, "Block", StackCategory.BuildingBlocks)]
    [InlineData("game:plank-oak", 64, false, false, false, false, "Item", StackCategory.BuildingBlocks)]
    [InlineData("game:ingot-copper", 16, false, false, false, false, "ItemIngot", StackCategory.ResourcesAndMetals)]
    [InlineData("game:nugget-nativecopper", 128, false, false, false, false, "ItemNugget", StackCategory.ResourcesAndMetals)]
    [InlineData("game:redmeat-raw", 64, false, true, false, false, "Item", StackCategory.FoodAndAgriculture)]
    [InlineData("game:seeds-spelt", 64, false, false, false, false, "ItemPlantableSeed", StackCategory.FoodAndAgriculture)]
    [InlineData("game:barrel", 1, true, false, false, false, "BlockBarrel", StackCategory.Containers)]
    [InlineData("game:woodbucket", 1, true, false, false, false, "BlockBucket", StackCategory.Containers)]
    [InlineData("game:bed-wood-head-north", 1, true, false, false, false, "BlockBed", StackCategory.FurnitureAndUtility)]
    [InlineData("game:quern-granite", 1, true, false, false, false, "BlockQuern", StackCategory.FurnitureAndUtility)]
    [InlineData("game:clothes-upperbody-raw-linen-shirt", 1, false, false, false, true, "ItemWearable", StackCategory.Clothing)]
    public void Classifier_AssignsExpectedCategoriesForCoreItems(
        string code,
        int vanillaMaxStack,
        bool isBlock,
        bool hasNutrition,
        bool isBackpack,
        bool isWearable,
        string className,
        StackCategory expected)
    {
        var classifier = new StackSizeClassifier();
        StackCollectibleDescriptor item = MakeItem(
            code,
            vanillaMaxStack: vanillaMaxStack,
            isBlock: isBlock,
            hasNutrition: hasNutrition,
            isBackpack: isBackpack,
            isWearable: isWearable,
            className: className);

        Assert.Equal(expected, classifier.Classify(item));
    }

    [Fact]
    public void Calculator_WhenVanillaOrDisabled_PreservesOriginalMaxStackSizes()
    {
        var calculator = new StackSizeCalculator();
        EffectiveServerPolicy policy = PolicyFor(StackSizePreset.Vanilla);

        Assert.False(policy.EnableStackSizeModifier);
        Assert.Equal(64, calculator.CalculateTargetMaxStackSize(MakeItem("game:cobblestone-granite", 64, isBlock: true), policy));
        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:barrel", 1, isBlock: true, className: "BlockBarrel"), policy));
        Assert.Equal(16, calculator.CalculateTargetMaxStackSize(MakeItem("game:ingot-copper", 16), policy));
    }

    [Fact]
    public void Calculator_P1nksBalanced_ScalesStackablesAndCapsContainersAndClothingSafely()
    {
        var calculator = new StackSizeCalculator();
        EffectiveServerPolicy policy = PolicyFor(StackSizePreset.P1nksBalanced);

        Assert.Equal(128, calculator.CalculateTargetMaxStackSize(MakeItem("game:cobblestone-granite", 64, isBlock: true), policy));
        Assert.Equal(32, calculator.CalculateTargetMaxStackSize(MakeItem("game:ingot-copper", 16), policy));
        Assert.Equal(128, calculator.CalculateTargetMaxStackSize(MakeItem("game:redmeat-raw", 64, hasNutrition: true), policy));
        Assert.Equal(256, calculator.CalculateTargetMaxStackSize(MakeItem("game:nugget-nativecopper", 128), policy));
        Assert.Equal(policy.EmptyContainersMaxStack, calculator.CalculateTargetMaxStackSize(MakeItem("game:barrel", 1, isBlock: true, className: "BlockBarrel"), policy));
        Assert.Equal(policy.FurnitureUtilityMaxStack, calculator.CalculateTargetMaxStackSize(MakeItem("game:bed-wood-head-north", 1, isBlock: true, className: "BlockBed"), policy));
        Assert.Equal(policy.ClothingMaxStack, calculator.CalculateTargetMaxStackSize(MakeItem("game:clothes-upperbody-linen", 1, isWearable: true, className: "ItemWearable"), policy));

        // Protected items must stay at vanilla
        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:pickaxe-copper", 1, durability: 250, hasTool: true, className: "ItemPickaxe"), policy));
        Assert.Equal(100, calculator.CalculateTargetMaxStackSize(MakeItem("game:waterportion", 100, isLiquid: true, className: "ItemLiquidPortion"), policy));
        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:miningbag", 1, isBackpack: true, className: "ItemBag"), policy));
        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:skep-populated-east", 1, isBlock: true, className: "BlockSkep"), policy));
    }

    [Fact]
    public void Calculator_ConservativeFlag_KeepsUnstackableContainersFurnitureAndClothingAtOne()
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.ApplyStackPreset(StackSizePreset.P1nksBalanced);
        cfg.AllowUnstackableContainersAndFurniture = false;
        EffectiveServerPolicy policy = EffectiveServerPolicy.FromServerConfig(cfg);
        var calculator = new StackSizeCalculator();

        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:barrel", 1, isBlock: true, className: "BlockBarrel"), policy));
        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:bed-wood-head-north", 1, isBlock: true, className: "BlockBed"), policy));
        Assert.Equal(1, calculator.CalculateTargetMaxStackSize(MakeItem("game:clothes-upperbody-linen", 1, isWearable: true, className: "ItemWearable"), policy));
        Assert.Equal(128, calculator.CalculateTargetMaxStackSize(MakeItem("game:cobblestone-granite", 64, isBlock: true), policy));
    }

    [Fact]
    public void Calculator_RespectsBlacklistOverridesAndCap()
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.ApplyStackPreset(StackSizePreset.Builder4x);
        cfg.MaxStackSizeCap = 300;
        cfg.StackSizeBlacklist.Add("game:cobblestone-*");
        cfg.StackSizeOverrides["game:ingot-*"] = 48;
        cfg.StackSizeOverrides["game:ingot-gold"] = 12;
        cfg.StackSizeOverrides["game:nugget-*"] = 999;

        EffectiveServerPolicy policy = EffectiveServerPolicy.FromServerConfig(cfg);
        var calculator = new StackSizeCalculator();

        // Blacklist keeps vanilla
        Assert.Equal(64, calculator.CalculateTargetMaxStackSize(MakeItem("game:cobblestone-granite", 64, isBlock: true), policy));
        // Exact override wins over wildcard override
        Assert.Equal(12, calculator.CalculateTargetMaxStackSize(MakeItem("game:ingot-gold", 16), policy));
        Assert.Equal(48, calculator.CalculateTargetMaxStackSize(MakeItem("game:ingot-copper", 16), policy));
        // Cap limits scaled and overridden items
        Assert.Equal(300, calculator.CalculateTargetMaxStackSize(MakeItem("game:nugget-nativecopper", 128), policy));
        Assert.Equal(256, calculator.CalculateTargetMaxStackSize(MakeItem("game:plank-oak", 64), policy));
    }
}
