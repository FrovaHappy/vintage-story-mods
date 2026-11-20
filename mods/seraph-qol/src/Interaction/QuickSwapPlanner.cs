namespace SeraphQoL.Interaction;

public sealed record QuickSwapSlotSnapshot(
    string InventoryClass,
    int SlotIndex,
    string ItemCode,
    string DisplayName,
    bool IsTool,
    int LightLevel,
    bool IsActiveHotbar);

public sealed record QuickSwapCandidate(
    int Index,
    string InventoryClass,
    int SlotIndex,
    string ItemCode,
    string DisplayName,
    int LightLevel,
    bool IsSelected);

/// <summary>
/// Planifica las entradas del menu radial Quick-Swap agrupando herramientas y fuentes de luz sin duplicados.
/// </summary>
public static class QuickSwapPlanner
{
    public static IReadOnlyList<QuickSwapCandidate> BuildCandidates(IEnumerable<QuickSwapSlotSnapshot> slots)
    {
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<QuickSwapCandidate>();

        foreach (QuickSwapSlotSnapshot slot in slots)
        {
            if (string.IsNullOrWhiteSpace(slot.ItemCode)) continue;
            if (!slot.IsTool && slot.LightLevel <= 0) continue;
            if (!seenCodes.Add(slot.ItemCode)) continue;

            result.Add(new QuickSwapCandidate(
                Index: result.Count,
                InventoryClass: slot.InventoryClass,
                SlotIndex: slot.SlotIndex,
                ItemCode: slot.ItemCode,
                DisplayName: string.IsNullOrWhiteSpace(slot.DisplayName) ? slot.ItemCode : slot.DisplayName,
                LightLevel: slot.LightLevel,
                IsSelected: slot.IsActiveHotbar));
        }

        return result;
    }

    public static IReadOnlyList<ToolModeDescriptor> ToRadialDescriptors(IReadOnlyList<QuickSwapCandidate> candidates)
    {
        var list = new List<ToolModeDescriptor>(candidates.Count);
        foreach (QuickSwapCandidate item in candidates)
        {
            list.Add(new ToolModeDescriptor(
                NativeModeIndex: item.Index,
                Code: item.ItemCode,
                Name: item.DisplayName,
                Description: string.Empty,
                IsSelected: item.IsSelected,
                IsEnabled: true));
        }

        return list;
    }
}
