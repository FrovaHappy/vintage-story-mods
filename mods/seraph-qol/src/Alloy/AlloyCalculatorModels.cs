namespace SeraphQoL.Alloy;

public sealed record AlloyIngredientSpec(
    string MetalCode,
    string Label,
    double MinRatio,
    double MaxRatio);

public sealed record AlloyRecipeSpec(
    string OutputCode,
    string OutputName,
    IReadOnlyList<AlloyIngredientSpec> Ingredients,
    int MeltTempC = 0);

public sealed record CrucibleMetalSnapshot(
    string MetalCode,
    int Units);

public sealed record SmeltableMetalItem(
    string ItemCode,
    string DisplayName,
    string MetalCode,
    int UnitsPerItem,
    int MeltTempC,
    int TotalCount,
    string InventoryClass = "",
    int SlotIndex = -1);

public sealed record FuelOptionSpec(
    string ItemCode,
    string DisplayName,
    int BurnTemperatureC,
    double BurnDurationSeconds,
    int AvailableCount,
    string InventoryClass = "",
    int SlotIndex = -1);

public sealed record AlloyIngredientNeed(
    string MetalCode,
    string Label,
    double Ratio,
    int TargetUnits,
    int AlreadyInCrucibleUnits,
    int DeficitUnits,
    int UnitsPerItem,
    int ItemsNeededTotal,
    int AvailableItemCount,
    int ItemsToDeposit,
    int MissingUnitsAfterDeposit,
    string PreferredItemCode);

public sealed record AlloyBatchPlan(
    string OutputCode,
    string OutputName,
    int TargetOutputUnits,
    bool IsRatioValid,
    bool CanFulfillFromInventory,
    int RequiredMeltTempC,
    IReadOnlyList<AlloyIngredientNeed> Needs);

public sealed record FuelRequirementResult(
    bool ReachesRequiredTemperature,
    double PrewarmSeconds,
    double MeltSeconds,
    double TotalSecondsNeeded,
    int PiecesNeeded,
    int AvailableCount);

/// <summary>
/// Logica pura de planificacion de aleaciones, ajuste de proporciones sin desperdicio y consumo de combustible.
/// </summary>
public static class AlloyCalculatorLogic
{
    public const int DefaultUnitsPerIngot = 100;
    public const int MaxTargetUnits = 5000;

    public static double[] BuildDefaultPercentages(AlloyRecipeSpec recipe)
    {
        int count = recipe.Ingredients.Count;
        if (count == 0) return Array.Empty<double>();
        if (count == 1) return [1.0];

        double[] ratios = new double[count];
        for (int i = 0; i < count; i++)
        {
            AlloyIngredientSpec ing = recipe.Ingredients[i];
            ratios[i] = (ing.MinRatio + ing.MaxRatio) * 0.5;
        }

        NormalizeWithinBounds(recipe, ratios, lockedIndex: -1);
        return ratios;
    }

    public static double[] AdjustIngredientPercentage(
        AlloyRecipeSpec recipe,
        IReadOnlyList<double> current,
        int editedIndex,
        double targetRatio)
    {
        int count = recipe.Ingredients.Count;
        double[] next = new double[count];
        for (int i = 0; i < count; i++)
        {
            next[i] = i < current.Count ? current[i] : recipe.Ingredients[i].MinRatio;
        }

        if (editedIndex < 0 || editedIndex >= count) return next;
        if (count == 1)
        {
            next[0] = 1.0;
            return next;
        }

        double otherMinSum = 0;
        double otherMaxSum = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == editedIndex) continue;
            otherMinSum += recipe.Ingredients[i].MinRatio;
            otherMaxSum += recipe.Ingredients[i].MaxRatio;
        }

        AlloyIngredientSpec edited = recipe.Ingredients[editedIndex];
        double minAllowed = Math.Max(edited.MinRatio, 1.0 - otherMaxSum);
        double maxAllowed = Math.Min(edited.MaxRatio, 1.0 - otherMinSum);
        next[editedIndex] = Math.Clamp(targetRatio, minAllowed, maxAllowed);

        NormalizeWithinBounds(recipe, next, editedIndex);
        return next;
    }

    public static AlloyBatchPlan CalculateBatchPlan(
        AlloyRecipeSpec recipe,
        IReadOnlyList<double> percentages,
        int targetUnits,
        IReadOnlyList<CrucibleMetalSnapshot> crucibleContents,
        IReadOnlyList<SmeltableMetalItem> availableItems,
        int unitsPerNugget = 5)
    {
        int step = Math.Max(1, unitsPerNugget);
        int clampedTarget = Math.Clamp(targetUnits, step, MaxTargetUnits);
        int count = recipe.Ingredients.Count;
        if (count == 0)
        {
            return new AlloyBatchPlan(recipe.OutputCode, recipe.OutputName, 0, false, false, 0, Array.Empty<AlloyIngredientNeed>());
        }

        int[] unitAllocations = AllocateIngredientUnits(recipe, percentages, clampedTarget, step);
        int totalAllocated = unitAllocations.Sum();

        bool ratioValid = totalAllocated > 0;
        if (ratioValid)
        {
            for (int i = 0; i < count; i++)
            {
                double actualRatio = (double)unitAllocations[i] / totalAllocated;
                if (actualRatio < recipe.Ingredients[i].MinRatio - 0.001
                    || actualRatio > recipe.Ingredients[i].MaxRatio + 0.001)
                {
                    ratioValid = false;
                    break;
                }
            }
        }

        var inCrucibleByMetal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (CrucibleMetalSnapshot snap in crucibleContents)
        {
            if (snap.Units <= 0 || string.IsNullOrWhiteSpace(snap.MetalCode)) continue;
            inCrucibleByMetal[snap.MetalCode] = inCrucibleByMetal.GetValueOrDefault(snap.MetalCode) + snap.Units;
        }

        var availableByMetal = availableItems
            .GroupBy(i => i.MetalCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.UnitsPerItem).ToList(), StringComparer.OrdinalIgnoreCase);

        var needs = new List<AlloyIngredientNeed>(count);
        bool allFulfilled = ratioValid;
        int maxMeltTemp = 0;

        for (int i = 0; i < count; i++)
        {
            AlloyIngredientSpec spec = recipe.Ingredients[i];
            int targetForIng = unitAllocations[i];
            int inCrucible = inCrucibleByMetal.GetValueOrDefault(spec.MetalCode);
            int deficit = Math.Max(0, targetForIng - inCrucible);

            int unitsPerItem = step;
            int totalAvailableItems = 0;
            string preferredCode = spec.MetalCode;

            if (availableByMetal.TryGetValue(spec.MetalCode, out List<SmeltableMetalItem>? candidates) && candidates.Count > 0)
            {
                SmeltableMetalItem primary = candidates[0];
                unitsPerItem = Math.Max(1, primary.UnitsPerItem);
                preferredCode = primary.ItemCode;
                totalAvailableItems = candidates.Sum(c => Math.Max(0, c.TotalCount));
                foreach (SmeltableMetalItem candidate in candidates)
                {
                    if (candidate.MeltTempC > maxMeltTemp) maxMeltTemp = candidate.MeltTempC;
                }
            }

            int itemsNeeded = deficit == 0 ? 0 : (int)Math.Ceiling((double)deficit / unitsPerItem);
            int itemsToDeposit = Math.Min(totalAvailableItems, itemsNeeded);
            int unitsDeposited = itemsToDeposit * unitsPerItem;
            int missingUnits = Math.Max(0, deficit - unitsDeposited);
            if (missingUnits > 0) allFulfilled = false;

            needs.Add(new AlloyIngredientNeed(
                MetalCode: spec.MetalCode,
                Label: spec.Label,
                Ratio: totalAllocated > 0 ? (double)targetForIng / totalAllocated : 0,
                TargetUnits: targetForIng,
                AlreadyInCrucibleUnits: inCrucible,
                DeficitUnits: deficit,
                UnitsPerItem: unitsPerItem,
                ItemsNeededTotal: itemsNeeded,
                AvailableItemCount: totalAvailableItems,
                ItemsToDeposit: itemsToDeposit,
                MissingUnitsAfterDeposit: missingUnits,
                PreferredItemCode: preferredCode));
        }

        int resolvedMeltTemp = recipe.MeltTempC > 0
            ? recipe.MeltTempC
            : (maxMeltTemp > 0 ? maxMeltTemp : 1084);

        return new AlloyBatchPlan(
            OutputCode: recipe.OutputCode,
            OutputName: recipe.OutputName,
            TargetOutputUnits: totalAllocated,
            IsRatioValid: ratioValid,
            CanFulfillFromInventory: allFulfilled,
            RequiredMeltTempC: resolvedMeltTemp,
            Needs: needs);
    }

    public static int FindNextWasteFreeUnits(
        AlloyRecipeSpec recipe,
        IReadOnlyList<double> percentages,
        int currentUnits,
        int direction,
        int unitsPerNugget = 5,
        int stepCount = 1)
    {
        int step = Math.Max(1, unitsPerNugget);
        int dir = direction >= 0 ? 1 : -1;
        int stepsRemaining = Math.Max(1, stepCount);
        int lastValid = Math.Clamp(currentUnits, step, MaxTargetUnits);

        for (int candidate = lastValid + (dir * step); candidate >= step && candidate <= MaxTargetUnits; candidate += dir * step)
        {
            if (IsExactWasteFreeTarget(recipe, percentages, candidate, step))
            {
                lastValid = candidate;
                stepsRemaining--;
                if (stepsRemaining <= 0)
                {
                    return lastValid;
                }
            }
        }

        return lastValid;
    }

    public static FuelRequirementResult CalculateFuelRequirement(
        FuelOptionSpec fuel,
        double currentFurnaceTempC,
        double requiredMeltTempC,
        double crucibleStackFactor,
        double baseMeltDurationSeconds = 45.0)
    {
        if (fuel.BurnDurationSeconds <= 0.1)
        {
            return new FuelRequirementResult(
                ReachesRequiredTemperature: false,
                PrewarmSeconds: 0,
                MeltSeconds: 0,
                TotalSecondsNeeded: 0,
                PiecesNeeded: 0,
                AvailableCount: fuel.AvailableCount);
        }

        bool hotEnough = fuel.BurnTemperatureC >= requiredMeltTempC;
        double deltaTemp = Math.Max(0.0, requiredMeltTempC - currentFurnaceTempC);
        double heatRatePerSec = Math.Max(15.0, Math.Abs(fuel.BurnTemperatureC - currentFurnaceTempC) * 0.045);
        double prewarmSeconds = deltaTemp > 1.0 ? deltaTemp / heatRatePerSec : 0.0;
        double meltSeconds = Math.Max(5.0, baseMeltDurationSeconds * Math.Max(0.25, crucibleStackFactor));
        double totalSeconds = prewarmSeconds + meltSeconds;
        int piecesNeeded = Math.Max(1, (int)Math.Ceiling(totalSeconds / fuel.BurnDurationSeconds));

        return new FuelRequirementResult(
            ReachesRequiredTemperature: hotEnough,
            PrewarmSeconds: prewarmSeconds,
            MeltSeconds: meltSeconds,
            TotalSecondsNeeded: totalSeconds,
            PiecesNeeded: piecesNeeded,
            AvailableCount: fuel.AvailableCount);
    }

    private static bool IsExactWasteFreeTarget(
        AlloyRecipeSpec recipe,
        IReadOnlyList<double> percentages,
        int candidateUnits,
        int unitsPerNugget)
    {
        if (candidateUnits % DefaultUnitsPerIngot != 0 && recipe.Ingredients.Count > 1)
        {
            // Keep clean whole-ingot steps for alloys when possible.
            return false;
        }

        int[] alloc = AllocateIngredientUnits(recipe, percentages, candidateUnits, unitsPerNugget);
        int sum = alloc.Sum();
        if (sum != candidateUnits || sum <= 0) return false;

        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            double ratio = (double)alloc[i] / sum;
            if (ratio < recipe.Ingredients[i].MinRatio - 0.001
                || ratio > recipe.Ingredients[i].MaxRatio + 0.001)
            {
                return false;
            }
        }

        return true;
    }

    private static int[] AllocateIngredientUnits(
        AlloyRecipeSpec recipe,
        IReadOnlyList<double> percentages,
        int targetUnits,
        int step)
    {
        int count = recipe.Ingredients.Count;
        int[] alloc = new int[count];
        int runningSum = 0;

        for (int i = 0; i < count; i++)
        {
            double pct = i < percentages.Count ? percentages[i] : recipe.Ingredients[i].MinRatio;
            int rawUnits = (int)Math.Round((targetUnits * pct) / step) * step;
            alloc[i] = Math.Max(step, rawUnits);
            runningSum += alloc[i];
        }

        int diff = targetUnits - runningSum;
        if (diff != 0 && count > 0)
        {
            int primaryIdx = 0;
            for (int i = 1; i < count; i++)
            {
                if (alloc[i] > alloc[primaryIdx]) primaryIdx = i;
            }
            alloc[primaryIdx] = Math.Max(step, alloc[primaryIdx] + diff);
        }

        return alloc;
    }

    private static void NormalizeWithinBounds(AlloyRecipeSpec recipe, double[] ratios, int lockedIndex)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            double lockedSum = lockedIndex >= 0 ? ratios[lockedIndex] : 0;
            double freeSum = 0;

            for (int i = 0; i < ratios.Length; i++)
            {
                if (i == lockedIndex) continue;
                AlloyIngredientSpec spec = recipe.Ingredients[i];
                ratios[i] = Math.Clamp(ratios[i], spec.MinRatio, spec.MaxRatio);
                freeSum += ratios[i];
            }

            double targetFreeSum = Math.Max(0.0, 1.0 - lockedSum);
            if (freeSum <= 1e-6) break;

            double scale = targetFreeSum / freeSum;
            for (int i = 0; i < ratios.Length; i++)
            {
                if (i == lockedIndex) continue;
                AlloyIngredientSpec spec = recipe.Ingredients[i];
                ratios[i] = Math.Clamp(ratios[i] * scale, spec.MinRatio, spec.MaxRatio);
            }
        }
    }
}

public sealed record CrucibleSlotItemSnapshot(
    int SlotIndex,
    string? MetalCode,
    int ItemCount);

public sealed record CrucibleCleanupPlan(
    IReadOnlyList<(int SlotIndex, int ExtractCount)> Extractions,
    IReadOnlyDictionary<string, int> KeptItemsByMetal);

/// <summary>
/// Calcula que pepitas del crisol sobran o no pertenecen a la receta actual para devolverlas o soltarlas.
/// </summary>
public static class CrucibleCleanupPlanner
{
    public static CrucibleCleanupPlan PlanExtractions(
        IReadOnlyList<CrucibleSlotItemSnapshot> crucibleSlots,
        IReadOnlyDictionary<string, int> maxItemsByMetal)
    {
        var extractions = new List<(int SlotIndex, int ExtractCount)>();
        var keptByMetal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (CrucibleSlotItemSnapshot slot in crucibleSlots)
        {
            if (slot.ItemCount <= 0) continue;
            if (string.IsNullOrWhiteSpace(slot.MetalCode)
                || !maxItemsByMetal.TryGetValue(slot.MetalCode, out int maxAllowed)
                || maxAllowed <= 0)
            {
                extractions.Add((slot.SlotIndex, slot.ItemCount));
                continue;
            }

            int alreadyKept = keptByMetal.GetValueOrDefault(slot.MetalCode);
            int keepHere = Math.Min(slot.ItemCount, Math.Max(0, maxAllowed - alreadyKept));
            keptByMetal[slot.MetalCode] = alreadyKept + keepHere;

            int extract = slot.ItemCount - keepHere;
            if (extract > 0)
            {
                extractions.Add((slot.SlotIndex, extract));
            }
        }

        return new CrucibleCleanupPlan(extractions, keptByMetal);
    }
}
