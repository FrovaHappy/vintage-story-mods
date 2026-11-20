using HarmonyLib;
using Vintagestory.API.Common;

namespace SeraphQoL.Inventory;

/// <summary>
/// Protege slots especiales de mods terceros y determina que ranuras pueden ordenarse o recibir items.
/// </summary>
public static class SlotProtectionPolicy
{
    private static readonly HashSet<string> AllowedSlotTypeNames = new(StringComparer.Ordinal)
    {
        "ItemSlotSurvival",
        "ItemSlotBagContent",
        "ItemSlotBagContentWithWildcardMatch",
        "BackpackSlot",
        "VanillaBagContentSlot",
        "ItemSlotDynamic",
        "StandardSlot",
        "ItemSlotToolBagContent"
    };

    private static readonly HashSet<string> TargetWhitelistTypeNames = new(StringComparer.Ordinal)
    {
        "ItemSlotSurvival",
        "ItemSlotBagContent",
        "ItemSlotBagContentWithWildcardMatch",
        "BackpackSlot",
        "VanillaBagContentSlot",
        "ItemSlotDynamic",
        "StandardSlot"
    };

    public static bool IsPlayerBackpack(IInventory? inv) =>
        IsPlayerBackpackName(inv?.ClassName);

    public static bool IsPlayerHotbar(IInventory? inv) =>
        IsPlayerHotbarName(inv?.ClassName);

    public static bool IsPlayerBackpackName(string? inventoryClass) =>
        string.Equals(inventoryClass, "backpack", StringComparison.OrdinalIgnoreCase);

    public static bool IsPlayerHotbarName(string? inventoryClass) =>
        string.Equals(inventoryClass, "hotbar", StringComparison.OrdinalIgnoreCase);

    public static bool IsRestrictedWildcardSlot(ItemSlot? slot)
    {
        if (slot == null) return false;
        string typeName = slot.GetType().Name;
        if (!string.Equals(typeName, "ItemSlotBagContentWithWildcardMatch", StringComparison.Ordinal)
            && !string.Equals(typeName, "ItemSlotToolBagContent", StringComparison.Ordinal))
        {
            return false;
        }

        object? config = AccessTools.Property(slot.GetType(), "Config")?.GetValue(slot)
            ?? AccessTools.Field(slot.GetType(), "Config")?.GetValue(slot);
        if (config == null) return true;

        object? canHoldWildcards = AccessTools.Property(config.GetType(), "CanHoldWildcards")?.GetValue(config)
            ?? AccessTools.Field(config.GetType(), "CanHoldWildcards")?.GetValue(config);
        return canHoldWildcards is bool allowed && !allowed;
    }

    public static bool CanSort(ItemSlot? slot)
    {
        if (slot == null) return false;
        string typeName = slot.GetType().Name;
        if (!AllowedSlotTypeNames.Contains(typeName)) return false;
        if (IsRestrictedWildcardSlot(slot)) return false;
        return slot.Empty || slot.CanTake();
    }

    public static bool CanSortInto(ItemSlot? slot)
    {
        if (!CanSort(slot)) return false;
        return TargetWhitelistTypeNames.Contains(slot!.GetType().Name);
    }
}
