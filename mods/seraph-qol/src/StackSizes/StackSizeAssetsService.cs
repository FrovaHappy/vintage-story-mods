using SeraphQoL.Config;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace SeraphQoL.StackSizes;

/// <summary>
/// Adaptador de infraestructura que captura los MaxStackSize base y aplica políticas en vivo sobre CollectibleObject.
/// </summary>
public sealed class StackSizeAssetsService
{
    private readonly StackSizeCalculator _calculator = new();
    private readonly Dictionary<string, int> _baseMaxStackSizes = new(StringComparer.OrdinalIgnoreCase);

    public void ApplyToCollectibles(ICoreAPI api, EffectiveServerPolicy policy)
    {
        if (api.World?.Collectibles == null) return;

        int updatedCount = 0;
        foreach (CollectibleObject collectible in api.World.Collectibles)
        {
            string? code = collectible?.Code?.ToString();
            if (collectible == null || collectible.Id == 0 || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (!_baseMaxStackSizes.TryGetValue(code, out int vanillaMax))
            {
                vanillaMax = Math.Max(1, collectible.MaxStackSize);
                _baseMaxStackSizes[code] = vanillaMax;
            }

            StackCollectibleDescriptor descriptor = BuildDescriptor(collectible, code, vanillaMax);
            int targetMax = _calculator.CalculateTargetMaxStackSize(descriptor, policy);

            if (collectible.MaxStackSize != targetMax)
            {
                collectible.MaxStackSize = targetMax;
                updatedCount++;
            }
        }

        if (policy.EnableStackSizeModifier || updatedCount > 0)
        {
            api.Logger.Notification(
                "[SeraphQoL] Stack size policy applied ({0}): {1} collectible(s) updated.",
                policy.EnableStackSizeModifier ? "enabled" : "vanilla",
                updatedCount);
        }
    }

    private static StackCollectibleDescriptor BuildDescriptor(CollectibleObject collectible, string code, int vanillaMax)
    {
        bool isBlock = collectible is Block;
        string className = collectible.Class ?? collectible.GetType().Name;
        bool isLiquid = collectible.MatterState == EnumMatterState.Liquid;
        bool isBackpack = (collectible.StorageFlags & EnumItemStorageFlags.Backpack) != 0;
        bool isWearable = collectible.HasBehavior<CollectibleBehaviorWearable>();
        string[] tabs = collectible.CreativeInventoryTabs ?? Array.Empty<string>();

        return new StackCollectibleDescriptor(
            Code: code,
            ClassName: className,
            VanillaMaxStackSize: vanillaMax,
            IsBlock: isBlock,
            Durability: collectible.Durability,
            HasTool: collectible.Tool.HasValue,
            HasNutritionProps: collectible.NutritionProps != null,
            IsLiquid: isLiquid,
            IsBackpackStorage: isBackpack,
            IsWearable: isWearable,
            CreativeTabs: tabs);
    }
}
