using SeraphQoL.Common;

namespace SeraphQoL.Inventory;

public enum RefillItemKind
{
    None,
    Block,
    Consumable,
    Tool,
    Other
}

/// <summary>
/// Instantanea pura de un slot para planificar reposicion automatica de hotbar.
/// </summary>
public sealed record RefillSlotSnapshot(
    string InventoryClass,
    int SlotIndex,
    string? ItemCode,
    RefillItemKind Kind,
    int StackSize,
    int RemainingDurability,
    int MaxDurability,
    bool IsLocked,
    bool IsFavorite);

/// <summary>
/// Reglas configurables que gobiernan que categorias se reponen y con que prioridad.
/// </summary>
public sealed record RefillRuleOptions(
    bool RefillConsumables,
    bool RefillBlocks,
    bool RefillToolsBeforeBreak,
    int ToolDurabilityThreshold,
    bool PreferSmallestStack,
    IReadOnlyCollection<string> BlacklistPatterns);

/// <summary>
/// Plan determinista de recarga o reemplazo preventivo de un slot de hotbar desde mochila.
/// </summary>
public sealed record RefillPlan(
    int SourceBackpackSlotIndex,
    int TargetHotbarSlotIndex,
    bool IsPreventiveToolSwap,
    string ItemCode);

/// <summary>
/// Planificador puro para Hotbar Refill (agotamiento de consumibles/bloques y reemplazo preventivo de herramientas).
/// </summary>
public static class HotbarRefillPlanner
{
    public static RefillPlan? FindRefillPlan(
        RefillSlotSnapshot previousHotbar,
        RefillSlotSnapshot currentHotbar,
        IReadOnlyList<RefillSlotSnapshot> backpackSlots,
        RefillRuleOptions options)
    {
        if (previousHotbar.IsLocked || currentHotbar.IsLocked) return null;
        if (string.IsNullOrWhiteSpace(previousHotbar.ItemCode)) return null;

        string prevCode = previousHotbar.ItemCode;
        if (CodePatternMatcher.MatchesAny(prevCode, options.BlacklistPatterns)) return null;

        bool depletedToEmpty = previousHotbar.StackSize > 0
            && (currentHotbar.StackSize <= 0 || string.IsNullOrWhiteSpace(currentHotbar.ItemCode));

        if (depletedToEmpty && IsDepletionEligible(previousHotbar.Kind, options))
        {
            RefillSlotSnapshot? source = SelectStackCandidate(backpackSlots, prevCode, options.PreferSmallestStack);
            if (source != null)
            {
                return new RefillPlan(source.SlotIndex, currentHotbar.SlotIndex, false, prevCode);
            }
        }

        if (options.RefillToolsBeforeBreak
            && previousHotbar.Kind == RefillItemKind.Tool
            && currentHotbar.Kind == RefillItemKind.Tool
            && string.Equals(prevCode, currentHotbar.ItemCode, StringComparison.OrdinalIgnoreCase)
            && currentHotbar.MaxDurability > options.ToolDurabilityThreshold
            && previousHotbar.RemainingDurability > options.ToolDurabilityThreshold
            && currentHotbar.RemainingDurability > 0
            && currentHotbar.RemainingDurability <= options.ToolDurabilityThreshold)
        {
            RefillSlotSnapshot? replacement = SelectHealthyToolCandidate(
                backpackSlots,
                prevCode,
                options.ToolDurabilityThreshold);
            if (replacement != null)
            {
                return new RefillPlan(replacement.SlotIndex, currentHotbar.SlotIndex, true, prevCode);
            }
        }

        return null;
    }

    private static bool IsDepletionEligible(RefillItemKind kind, RefillRuleOptions options) => kind switch
    {
        RefillItemKind.Block => options.RefillBlocks,
        RefillItemKind.Consumable => options.RefillConsumables,
        _ => false
    };

    private static RefillSlotSnapshot? SelectStackCandidate(
        IReadOnlyList<RefillSlotSnapshot> backpackSlots,
        string itemCode,
        bool preferSmallestStack)
    {
        RefillSlotSnapshot? best = null;
        foreach (RefillSlotSnapshot candidate in backpackSlots)
        {
            if (candidate.IsLocked || candidate.StackSize <= 0) continue;
            if (!string.Equals(candidate.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase)) continue;

            if (best == null)
            {
                best = candidate;
                continue;
            }

            bool better = preferSmallestStack
                ? candidate.StackSize < best.StackSize
                : candidate.StackSize > best.StackSize;
            if (better)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static RefillSlotSnapshot? SelectHealthyToolCandidate(
        IReadOnlyList<RefillSlotSnapshot> backpackSlots,
        string itemCode,
        int threshold)
    {
        RefillSlotSnapshot? best = null;
        foreach (RefillSlotSnapshot candidate in backpackSlots)
        {
            if (candidate.IsLocked || candidate.StackSize <= 0) continue;
            if (!string.Equals(candidate.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (candidate.RemainingDurability <= threshold) continue;

            if (best == null || candidate.RemainingDurability > best.RemainingDurability)
            {
                best = candidate;
            }
        }

        return best;
    }
}
