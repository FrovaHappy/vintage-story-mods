namespace SeraphQoL.Inventory;

/// <summary>
/// Candidato de fuente de luz presente en el inventario del jugador.
/// </summary>
public sealed record LightSourceCandidate(
    string InventoryClass,
    int SlotIndex,
    string ItemCode,
    int LightLevel,
    int Hue,
    int Saturation,
    bool IsLocked);

/// <summary>
/// Candidato de escudo presente en el inventario del jugador.
/// </summary>
public sealed record ShieldCandidate(
    string InventoryClass,
    int SlotIndex,
    string ItemCode,
    float DamageAbsorption,
    float ProjectileDamageAbsorption,
    int RemainingDurability,
    int MaxDurability,
    bool IsLocked);

/// <summary>
/// Estado recordado del intercambio anterior para permitir toggle reversible de Equip Light / Equip Shield.
/// </summary>
public sealed record EquipLightSwapState(
    string SourceInventoryClass,
    int SourceSlotIndex,
    string TargetInventoryClass,
    int TargetSlotIndex,
    string EquippedItemCode);

/// <summary>
/// Decision de intercambio para equipar o restaurar la mejor fuente de luz o escudo.
/// </summary>
public sealed record EquipLightDecision(
    string SourceInventoryClass,
    int SourceSlotIndex,
    string TargetInventoryClass,
    int TargetSlotIndex,
    bool IsRestore);

/// <summary>
/// Planificador generico por prioridad y toggle reversible compartido por Equip Light y Equip Shield.
/// </summary>
internal static class PriorityEquipPlanner
{
    public sealed record RankedCandidate(
        string InventoryClass,
        int SlotIndex,
        string ItemCode,
        double PrimaryScore,
        double SecondaryScore,
        bool IsLocked);

    public static EquipLightDecision? PlanEquip(
        IReadOnlyList<RankedCandidate> candidates,
        string targetInventoryClass,
        int targetSlotIndex,
        string? currentTargetItemCode,
        EquipLightSwapState? previousSwap)
    {
        if (string.IsNullOrWhiteSpace(targetInventoryClass) || targetSlotIndex < 0) return null;

        if (previousSwap != null
            && string.Equals(previousSwap.TargetInventoryClass, targetInventoryClass, StringComparison.OrdinalIgnoreCase)
            && previousSwap.TargetSlotIndex == targetSlotIndex
            && string.Equals(previousSwap.EquippedItemCode, currentTargetItemCode, StringComparison.OrdinalIgnoreCase))
        {
            return new EquipLightDecision(
                previousSwap.SourceInventoryClass,
                previousSwap.SourceSlotIndex,
                targetInventoryClass,
                targetSlotIndex,
                IsRestore: true);
        }

        RankedCandidate? currentInTarget = candidates.FirstOrDefault(c =>
            string.Equals(c.InventoryClass, targetInventoryClass, StringComparison.OrdinalIgnoreCase)
            && c.SlotIndex == targetSlotIndex);

        double currentPrimary = currentInTarget?.PrimaryScore ?? 0.0;

        RankedCandidate? best = null;
        foreach (RankedCandidate candidate in candidates)
        {
            if (candidate.IsLocked || candidate.PrimaryScore <= 0.0) continue;
            if (string.Equals(candidate.InventoryClass, targetInventoryClass, StringComparison.OrdinalIgnoreCase)
                && candidate.SlotIndex == targetSlotIndex)
            {
                continue;
            }

            if (best == null || ComparePriority(candidate, best) > 0)
            {
                best = candidate;
            }
        }

        if (best == null) return null;
        if (currentInTarget != null && currentPrimary > 0.0 && CompareScore(currentInTarget, best) >= 0)
        {
            return null;
        }

        return new EquipLightDecision(
            best.InventoryClass,
            best.SlotIndex,
            targetInventoryClass,
            targetSlotIndex,
            IsRestore: false);
    }

    private static int CompareScore(RankedCandidate a, RankedCandidate b)
    {
        int primaryCmp = a.PrimaryScore.CompareTo(b.PrimaryScore);
        if (primaryCmp != 0) return primaryCmp;
        return a.SecondaryScore.CompareTo(b.SecondaryScore);
    }

    private static int ComparePriority(RankedCandidate a, RankedCandidate b)
    {
        int scoreCmp = CompareScore(a, b);
        if (scoreCmp != 0) return scoreCmp;

        bool aBackpack = SlotProtectionPolicy.IsPlayerBackpackName(a.InventoryClass);
        bool bBackpack = SlotProtectionPolicy.IsPlayerBackpackName(b.InventoryClass);
        if (aBackpack != bBackpack) return aBackpack ? 1 : -1;

        // En igualdad, menor indice primero
        return b.SlotIndex.CompareTo(a.SlotIndex);
    }
}

/// <summary>
/// Planificador puro para el atajo Equip Light (selecciona la fuente de luz mas brillante y permite restaurar).
/// </summary>
public static class EquipLightPlanner
{
    public static EquipLightDecision? PlanEquip(
        IReadOnlyList<LightSourceCandidate> candidates,
        string targetInventoryClass,
        int targetSlotIndex,
        string? currentTargetItemCode,
        EquipLightSwapState? previousSwap)
    {
        var ranked = candidates
            .Select(c => new PriorityEquipPlanner.RankedCandidate(
                c.InventoryClass,
                c.SlotIndex,
                c.ItemCode,
                c.LightLevel,
                0.0,
                c.IsLocked))
            .ToArray();

        return PriorityEquipPlanner.PlanEquip(ranked, targetInventoryClass, targetSlotIndex, currentTargetItemCode, previousSwap);
    }
}

/// <summary>
/// Planificador puro para el atajo Equip Shield (selecciona el mejor escudo y permite restaurar).
/// </summary>
public static class EquipShieldPlanner
{
    public static EquipLightDecision? PlanEquip(
        IReadOnlyList<ShieldCandidate> candidates,
        string targetInventoryClass,
        int targetSlotIndex,
        string? currentTargetItemCode,
        EquipLightSwapState? previousSwap)
    {
        var ranked = candidates
            .Select(c =>
            {
                bool usable = c.MaxDurability <= 0 || c.RemainingDurability > 0;
                double protectionScore = usable
                    ? Math.Max(0.1, (c.DamageAbsorption * 100.0) + (c.ProjectileDamageAbsorption * 10.0))
                    : 0.0;

                return new PriorityEquipPlanner.RankedCandidate(
                    c.InventoryClass,
                    c.SlotIndex,
                    c.ItemCode,
                    protectionScore,
                    c.RemainingDurability,
                    c.IsLocked);
            })
            .ToArray();

        return PriorityEquipPlanner.PlanEquip(ranked, targetInventoryClass, targetSlotIndex, currentTargetItemCode, previousSwap);
    }
}
