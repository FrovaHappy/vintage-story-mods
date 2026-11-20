namespace SeraphQoL.Inventory;

public sealed record SortSlotSnapshot(
    int SlotIndex,
    string InventoryClass,
    bool IsSortable,
    bool IsLocked,
    bool IsFavorite,
    string? ItemCode,
    string DisplayName,
    string Category,
    int StackSize,
    int MaxStackSize,
    bool IsPerishable,
    double FreshnessRatio,
    int OriginalOrder)
{
    public bool IsEmpty => string.IsNullOrEmpty(ItemCode) || StackSize <= 0;
}

public sealed record SortSlotAssignment(
    int SlotIndex,
    string? ItemCode,
    int StackSize,
    double FreshnessRatio,
    IReadOnlyList<(int SourceSlotIndex, int Quantity)> Sources);

public sealed record SortPlan(IReadOnlyList<SortSlotAssignment> Assignments);
