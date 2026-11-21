using SeraphQoL.Common;
using SeraphQoL.Config;

namespace SeraphQoL.StackSizes;

/// <summary>
/// Calculador puro de tamaños de stack efectivos según políticas, presets, overrides y blacklist.
/// </summary>
public sealed class StackSizeCalculator
{
    private readonly StackSizeClassifier _classifier = new();

    public int CalculateTargetMaxStackSize(StackCollectibleDescriptor item, EffectiveServerPolicy policy)
    {
        int vanilla = Math.Max(1, item.VanillaMaxStackSize);
        if (!policy.EnableStackSizeModifier || string.IsNullOrWhiteSpace(item.Code))
        {
            return vanilla;
        }

        if (CodePatternMatcher.MatchesAny(item.Code, policy.StackSizeBlacklist))
        {
            return vanilla;
        }

        if (CodePatternMatcher.TryResolveOverride(item.Code, policy.StackSizeOverrides, out int overrideValue))
        {
            return Math.Clamp(overrideValue, 1, policy.MaxStackSizeCap);
        }

        StackCategory category = _classifier.Classify(item);
        if (category == StackCategory.ProtectedSingle)
        {
            return vanilla;
        }

        return category switch
        {
            StackCategory.Containers => CalculateCappedSingleCategory(vanilla, policy.EmptyContainersMaxStack, policy),
            StackCategory.FurnitureAndUtility => CalculateCappedSingleCategory(vanilla, policy.FurnitureUtilityMaxStack, policy),
            StackCategory.Clothing => CalculateCappedSingleCategory(vanilla, policy.ClothingMaxStack, policy),
            StackCategory.BuildingBlocks => ScaleStackable(vanilla, policy.BuildingBlocksStackMultiplier, policy),
            StackCategory.ResourcesAndMetals => ScaleStackable(vanilla, policy.ResourcesStackMultiplier, policy),
            StackCategory.FoodAndAgriculture => ScaleStackable(vanilla, policy.FoodStackMultiplier, policy),
            _ => ScaleStackable(vanilla, policy.MiscStackMultiplier, policy)
        };
    }

    private static int CalculateCappedSingleCategory(int vanilla, int categoryTarget, EffectiveServerPolicy policy)
    {
        if (vanilla <= 1)
        {
            if (!policy.AllowUnstackableContainersAndFurniture)
            {
                return vanilla;
            }

            return Math.Clamp(categoryTarget, 1, policy.MaxStackSizeCap);
        }

        int scaled = (int)Math.Round(vanilla * policy.GlobalStackMultiplier, MidpointRounding.AwayFromZero);
        int target = policy.AllowUnstackableContainersAndFurniture
            ? Math.Max(scaled, categoryTarget)
            : scaled;

        return Math.Clamp(target, 1, policy.MaxStackSizeCap);
    }

    private static int ScaleStackable(int vanilla, double categoryMultiplier, EffectiveServerPolicy policy)
    {
        if (vanilla <= 1)
        {
            return vanilla;
        }

        double combined = policy.GlobalStackMultiplier * categoryMultiplier;
        int scaled = (int)Math.Round(vanilla * combined, MidpointRounding.AwayFromZero);
        return Math.Clamp(scaled, 1, policy.MaxStackSizeCap);
    }
}
