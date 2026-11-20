namespace SeraphQoL.Inventory;

/// <summary>
/// Determina que indices de origen son elegibles para descarga rapida hacia contenedores con contenido coincidente.
/// </summary>
public static class QuickStorePlanner
{
    public static IReadOnlyList<int> FindEligibleSourceIndices(
        IReadOnlyList<SortSlotSnapshot> sourceSlots,
        IReadOnlySet<string> destinationItemCodes,
        bool includeHotbar)
    {
        if (destinationItemCodes.Count == 0) return Array.Empty<int>();

        var eligible = new List<int>();
        foreach (SortSlotSnapshot slot in sourceSlots)
        {
            if (slot.IsEmpty || !slot.IsSortable || slot.IsLocked || slot.IsFavorite)
            {
                continue;
            }

            bool isHotbar = string.Equals(slot.InventoryClass, "hotbar", StringComparison.OrdinalIgnoreCase);
            if (isHotbar && !includeHotbar)
            {
                continue;
            }

            if (slot.ItemCode != null && destinationItemCodes.Contains(slot.ItemCode))
            {
                eligible.Add(slot.SlotIndex);
            }
        }

        return eligible;
    }
}
