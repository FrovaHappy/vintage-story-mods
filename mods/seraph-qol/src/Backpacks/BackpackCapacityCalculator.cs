using SeraphQoL.Common;
using SeraphQoL.Config;

namespace SeraphQoL.Backpacks;

/// <summary>
/// Calculador puro de capacidad efectiva de mochilas y resguardo contra pérdida de ítems al reducir tamaño.
/// </summary>
public sealed class BackpackCapacityCalculator
{
    private const string SlotKeyPrefix = "slot-";
    private readonly BackpackClassifier _classifier = new();

    public int CalculateTargetSlots(BackpackCollectibleDescriptor item, EffectiveServerPolicy policy)
    {
        int baseSlots = Math.Max(1, item.BaseQuantitySlots);
        if (!policy.EnableBackpackExpansion || string.IsNullOrWhiteSpace(item.Code))
        {
            return baseSlots;
        }

        if (CodePatternMatcher.MatchesAny(item.Code, policy.BackpackBlacklist))
        {
            return baseSlots;
        }

        if (CodePatternMatcher.TryResolveOverride(item.Code, policy.BackpackOverrides, out int overrideSlots))
        {
            return Math.Clamp(overrideSlots, 1, policy.MaxBackpackSlotsCap);
        }

        BackpackCategory category = _classifier.Classify(item);
        if (category == BackpackCategory.Excluded)
        {
            return baseSlots;
        }

        double categoryMultiplier = category switch
        {
            BackpackCategory.StandardBackpacks => policy.StandardBackpacksMultiplier,
            BackpackCategory.SpecializedBags => policy.SpecializedBagsMultiplier,
            BackpackCategory.Saddlebags => policy.SaddlebagsMultiplier,
            BackpackCategory.ModdedBackpacks => policy.ModdedBackpacksMultiplier,
            _ => 1.0
        };

        double combined = policy.GlobalBackpackMultiplier * categoryMultiplier;
        int scaled = (int)Math.Round(baseSlots * combined, MidpointRounding.AwayFromZero);
        return Math.Clamp(scaled, 1, policy.MaxBackpackSlotsCap);
    }

    public static bool TryParseSlotKeyIndex(string? key, out int index)
    {
        if (string.IsNullOrEmpty(key)
            || !key.StartsWith(SlotKeyPrefix, StringComparison.Ordinal)
            || key.Length <= SlotKeyPrefix.Length)
        {
            index = -1;
            return false;
        }

        return int.TryParse(key.AsSpan(SlotKeyPrefix.Length), out index) && index >= 0;
    }
}
