using SeraphQoL.Config;

namespace SeraphQoL.Inventory;

/// <summary>
/// Planificador puro de compactado y ordenamiento de inventario sin dependencias a Vintage Story UI.
/// </summary>
public static class InventorySortPlanner
{
    private sealed class MutableBucket
    {
        public required string ItemCode { get; init; }
        public required string DisplayName { get; init; }
        public required string Category { get; init; }
        public required int MaxStackSize { get; init; }
        public required bool IsPerishable { get; init; }
        public int StackSize { get; set; }
        public double FreshnessRatio { get; set; }
        public List<(int SourceSlotIndex, int Quantity)> Sources { get; } = new();

        public bool CanMergeWith(SortSlotSnapshot candidate, bool stackPerishables)
        {
            if (!string.Equals(ItemCode, candidate.ItemCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (StackSize >= MaxStackSize)
            {
                return false;
            }

            if (IsPerishable && !stackPerishables)
            {
                return Math.Abs(FreshnessRatio - candidate.FreshnessRatio) <= 0.01;
            }

            return true;
        }
    }

    public static SortPlan BuildPlan(
        IReadOnlyList<SortSlotSnapshot> slots,
        SortOrder sortOrder,
        bool stackPerishables,
        bool skipFavorites)
    {
        var fixedIndices = new HashSet<int>();
        var fixedAssignments = new Dictionary<int, SortSlotAssignment>();
        var sortableIndices = new List<int>();
        var buckets = new List<MutableBucket>();

        foreach (SortSlotSnapshot slot in slots)
        {
            bool isProtected = !slot.IsSortable || slot.IsLocked || (skipFavorites && slot.IsFavorite);
            if (isProtected)
            {
                fixedIndices.Add(slot.SlotIndex);
                fixedAssignments[slot.SlotIndex] = new SortSlotAssignment(
                    slot.SlotIndex,
                    slot.IsEmpty ? null : slot.ItemCode,
                    slot.IsEmpty ? 0 : slot.StackSize,
                    slot.FreshnessRatio,
                    slot.IsEmpty ? [] : [(slot.SlotIndex, slot.StackSize)]);
                continue;
            }

            sortableIndices.Add(slot.SlotIndex);
            if (slot.IsEmpty) continue;

            AddToBuckets(buckets, slot, stackPerishables);
        }

        Dictionary<string, int> accumulatedCounts = ComputeAccumulatedCounts(buckets);
        List<MutableBucket> ordered = OrderBuckets(buckets, sortOrder, accumulatedCounts);

        var result = new List<SortSlotAssignment>(slots.Count);
        int bucketCursor = 0;

        foreach (SortSlotSnapshot slot in slots)
        {
            if (fixedIndices.Contains(slot.SlotIndex))
            {
                result.Add(fixedAssignments[slot.SlotIndex]);
                continue;
            }

            if (bucketCursor < ordered.Count)
            {
                MutableBucket bucket = ordered[bucketCursor++];
                result.Add(new SortSlotAssignment(
                    slot.SlotIndex,
                    bucket.ItemCode,
                    bucket.StackSize,
                    bucket.FreshnessRatio,
                    bucket.Sources));
            }
            else
            {
                result.Add(new SortSlotAssignment(slot.SlotIndex, null, 0, 1.0, []));
            }
        }

        return new SortPlan(result);
    }

    private static void AddToBuckets(List<MutableBucket> buckets, SortSlotSnapshot slot, bool stackPerishables)
    {
        int remaining = slot.StackSize;
        int maxStack = Math.Max(1, slot.MaxStackSize);

        foreach (MutableBucket bucket in buckets)
        {
            if (remaining <= 0) break;
            if (!bucket.CanMergeWith(slot, stackPerishables)) continue;

            int space = maxStack - bucket.StackSize;
            int add = Math.Min(space, remaining);
            if (add <= 0) continue;

            if (bucket.IsPerishable)
            {
                double totalWeight = bucket.StackSize + add;
                bucket.FreshnessRatio = ((bucket.FreshnessRatio * bucket.StackSize) + (slot.FreshnessRatio * add)) / totalWeight;
            }

            bucket.StackSize += add;
            bucket.Sources.Add((slot.SlotIndex, add));
            remaining -= add;
        }

        while (remaining > 0)
        {
            int take = Math.Min(maxStack, remaining);
            var created = new MutableBucket
            {
                ItemCode = slot.ItemCode!,
                DisplayName = slot.DisplayName,
                Category = slot.Category,
                MaxStackSize = maxStack,
                IsPerishable = slot.IsPerishable,
                StackSize = take,
                FreshnessRatio = slot.FreshnessRatio
            };
            created.Sources.Add((slot.SlotIndex, take));
            buckets.Add(created);
            remaining -= take;
        }
    }

    private static Dictionary<string, int> ComputeAccumulatedCounts(List<MutableBucket> buckets)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (MutableBucket b in buckets)
        {
            counts.TryGetValue(b.ItemCode, out int prev);
            counts[b.ItemCode] = prev + b.StackSize;
        }
        return counts;
    }

    private static List<MutableBucket> OrderBuckets(
        List<MutableBucket> buckets,
        SortOrder sortOrder,
        Dictionary<string, int> accumulatedCounts)
    {
        return sortOrder switch
        {
            SortOrder.Name => buckets
                .OrderBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(b => b.StackSize)
                .ThenByDescending(b => b.FreshnessRatio)
                .ToList(),

            SortOrder.Code => buckets
                .OrderBy(b => b.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(b => b.StackSize)
                .ThenByDescending(b => b.FreshnessRatio)
                .ToList(),

            SortOrder.StackCount => buckets
                .OrderByDescending(b => accumulatedCounts[b.ItemCode])
                .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(b => b.StackSize)
                .ToList(),

            _ => buckets // SortOrder.Category
                .OrderBy(b => b.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(b => b.StackSize)
                .ThenByDescending(b => b.FreshnessRatio)
                .ToList()
        };
    }
}
