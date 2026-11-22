namespace SeraphQoL.Handbook;

public sealed record CraftingItemInfo(
    string ItemCode,
    int StackSize,
    int MaxStackSize,
    int RemainingDurability,
    string MergeKey);

public sealed record CraftingIngredientSpec(
    string Key,
    int Quantity,
    bool IsTool,
    int ToolDurabilityCost,
    Func<CraftingItemInfo, bool> Matches);

public sealed record CraftingRecipeSpec(
    int Width,
    int Height,
    bool Shapeless,
    IReadOnlyList<CraftingIngredientSpec?> Ingredients,
    bool MatchesCurrentGrid);

public sealed record CraftingTransferStep(
    int SourceSlotIndex,
    int TargetGridIndex,
    int Quantity);

public sealed record CraftingFillPlan(
    bool Success,
    int SelectedRecipeIndex,
    IReadOnlyList<int> SlotsToClear,
    IReadOnlyList<CraftingTransferStep> Transfers);

/// <summary>
/// Alinea sub-recetas (p. ej. 2x2 o 1x2) dentro de la grilla de crafteo 3x3 preservando ítems ya ubicados.
/// </summary>
public struct RecipeGridBounds
{
    public int XPos { get; private set; }
    public int YPos { get; private set; }
    public int Width { get; }
    public int Height { get; }
    public int OuterSize { get; }

    public RecipeGridBounds(int width, int height, int outerSize = 3)
    {
        XPos = 0;
        YPos = 0;
        Width = width;
        Height = height;
        OuterSize = outerSize;
    }

    public void SetPosition(int xPos, int yPos)
    {
        XPos = xPos;
        YPos = yPos;
    }

    public bool TryAlign(int outerIndex, int innerIndex)
    {
        if (Width <= 0 || Height <= 0 || OuterSize <= 0) return false;
        if (outerIndex < 0 || outerIndex >= OuterSize * OuterSize) return false;
        if (innerIndex < 0 || innerIndex >= Width * Height) return false;

        int x = (outerIndex % OuterSize) - (innerIndex % Width);
        int y = (outerIndex / OuterSize) - (innerIndex / Width);

        if (x < 0 || y < 0 || x + Width > OuterSize || y + Height > OuterSize)
        {
            return false;
        }

        XPos = x;
        YPos = y;
        return true;
    }

    public readonly bool Contains(int outerIndex)
    {
        if (outerIndex < 0 || outerIndex >= OuterSize * OuterSize) return false;
        int x = outerIndex % OuterSize;
        int y = outerIndex / OuterSize;
        return x >= XPos && x < XPos + Width && y >= YPos && y < YPos + Height;
    }

    public readonly int ToInnerIndex(int outerIndex)
    {
        int x = outerIndex % OuterSize;
        int y = outerIndex / OuterSize;
        return (x - XPos) + (y - YPos) * Width;
    }
}

/// <summary>
/// Planificador puro para seleccionar recetas fabricables y calcular movimientos de ingredientes
/// hacia la grilla 3x3 sin efectos secundarios sobre inventarios reales.
/// </summary>
public static class CraftingFillPlanner
{
    private const int GridSlotCount = 9;
    private const int GridDimension = 3;
    private const int MaxCraftIterations = 1024;

    public static CraftingFillPlan BuildPlan(
        IReadOnlyList<CraftingItemInfo?> gridSlots,
        IReadOnlyList<CraftingItemInfo?> sourceSlots,
        IReadOnlyList<CraftingRecipeSpec> candidateRecipes,
        bool fillMax)
    {
        if (gridSlots == null || gridSlots.Count < GridSlotCount || sourceSlots == null || candidateRecipes == null || candidateRecipes.Count == 0)
        {
            return new CraftingFillPlan(false, -1, Array.Empty<int>(), Array.Empty<CraftingTransferStep>());
        }

        int selectedIndex = SelectRecipeIndex(gridSlots, sourceSlots, candidateRecipes);
        if (selectedIndex < 0)
        {
            return new CraftingFillPlan(false, -1, Array.Empty<int>(), Array.Empty<CraftingTransferStep>());
        }

        CraftingRecipeSpec recipe = candidateRecipes[selectedIndex];
        var workingGrid = new CraftingItemInfo?[GridSlotCount];
        for (int i = 0; i < GridSlotCount; i++)
        {
            workingGrid[i] = gridSlots[i];
        }

        var workingSources = new CraftingItemInfo?[sourceSlots.Count];
        for (int i = 0; i < sourceSlots.Count; i++)
        {
            workingSources[i] = sourceSlots[i];
        }

        var slotsToClear = new List<int>();
        var transfers = new List<CraftingTransferStep>();
        bool anyAdded = false;

        int iterations = fillMax ? MaxCraftIterations : 1;
        for (int iter = 0; iter < iterations; iter++)
        {
            if (!TryPlanSingleStep(recipe, workingGrid, workingSources, slotsToClear, transfers))
            {
                break;
            }
            anyAdded = true;
        }

        if (!anyAdded)
        {
            return new CraftingFillPlan(false, selectedIndex, Array.Empty<int>(), Array.Empty<CraftingTransferStep>());
        }

        return new CraftingFillPlan(true, selectedIndex, slotsToClear, transfers);
    }

    /// <summary>
    /// Determina qué índices de ingredientes de una receta faltan (por cantidad o durabilidad)
    /// considerando lo ya alineado en la grilla y las fuentes disponibles acumuladas.
    /// </summary>
    public static IReadOnlyList<int> FindMissingIngredientIndices(
        IReadOnlyList<CraftingItemInfo?> gridSlots,
        IReadOnlyList<CraftingItemInfo?> sourceSlots,
        CraftingRecipeSpec recipe)
    {
        if (recipe == null || recipe.Ingredients.Count == 0)
        {
            return Array.Empty<int>();
        }

        var workingGrid = new CraftingItemInfo?[GridSlotCount];
        if (gridSlots != null)
        {
            for (int i = 0; i < Math.Min(GridSlotCount, gridSlots.Count); i++)
            {
                workingGrid[i] = gridSlots[i];
            }
        }

        var workingSources = new CraftingItemInfo?[sourceSlots?.Count ?? 0];
        if (sourceSlots != null)
        {
            for (int i = 0; i < sourceSlots.Count; i++)
            {
                workingSources[i] = sourceSlots[i];
            }
        }

        var gridContributions = new int[recipe.Ingredients.Count];
        if (recipe.Shapeless)
        {
            var claimedGrid = new bool[GridSlotCount];
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftingIngredientSpec? ing = recipe.Ingredients[i];
                if (ing == null) continue;

                for (int g = 0; g < GridSlotCount; g++)
                {
                    if (claimedGrid[g] || workingGrid[g] == null) continue;
                    if (!ing.Matches(workingGrid[g]!)) continue;

                    claimedGrid[g] = true;
                    gridContributions[i] = ing.IsTool ? 1 : workingGrid[g]!.StackSize;
                    break;
                }
            }
        }
        else if (recipe.Width * recipe.Height < GridSlotCount)
        {
            RecipeGridBounds bounds = FindBestBoundsAlignment(recipe, workingGrid);
            for (int g = 0; g < GridSlotCount; g++)
            {
                if (!bounds.Contains(g) || workingGrid[g] == null) continue;
                int inner = bounds.ToInnerIndex(g);
                if (inner < 0 || inner >= recipe.Ingredients.Count) continue;

                CraftingIngredientSpec? ing = recipe.Ingredients[inner];
                if (ing != null && ing.Matches(workingGrid[g]!))
                {
                    gridContributions[inner] = ing.IsTool ? 1 : workingGrid[g]!.StackSize;
                }
            }
        }
        else
        {
            for (int g = 0; g < Math.Min(GridSlotCount, recipe.Ingredients.Count); g++)
            {
                if (workingGrid[g] == null) continue;
                CraftingIngredientSpec? ing = recipe.Ingredients[g];
                if (ing != null && ing.Matches(workingGrid[g]!))
                {
                    gridContributions[g] = ing.IsTool ? 1 : workingGrid[g]!.StackSize;
                }
            }
        }

        var missing = new List<int>();
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            CraftingIngredientSpec? ing = recipe.Ingredients[i];
            if (ing == null) continue;

            int required = ing.IsTool ? 1 : Math.Max(1, ing.Quantity);
            int remainingNeed = Math.Max(0, required - gridContributions[i]);
            if (remainingNeed == 0) continue;

            for (int s = 0; s < workingSources.Length && remainingNeed > 0; s++)
            {
                CraftingItemInfo? src = workingSources[s];
                if (src == null || src.StackSize <= 0) continue;
                if (!ing.Matches(src)) continue;

                if (ing.IsTool)
                {
                    remainingNeed = 0;
                    break;
                }

                int take = Math.Min(remainingNeed, src.StackSize);
                remainingNeed -= take;
                int left = src.StackSize - take;
                workingSources[s] = left > 0 ? src with { StackSize = left } : null;
            }

            if (remainingNeed > 0)
            {
                missing.Add(i);
            }
        }

        return missing;
    }

    private static int SelectRecipeIndex(
        IReadOnlyList<CraftingItemInfo?> gridSlots,
        IReadOnlyList<CraftingItemInfo?> sourceSlots,
        IReadOnlyList<CraftingRecipeSpec> candidateRecipes)
    {
        for (int i = 0; i < candidateRecipes.Count; i++)
        {
            if (candidateRecipes[i].MatchesCurrentGrid)
            {
                return i;
            }
        }

        for (int i = 0; i < candidateRecipes.Count; i++)
        {
            if (CanCraftOneMore(candidateRecipes[i], gridSlots, sourceSlots))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool CanCraftOneMore(
        CraftingRecipeSpec recipe,
        IReadOnlyList<CraftingItemInfo?> gridSlots,
        IReadOnlyList<CraftingItemInfo?> sourceSlots)
    {
        var workingGrid = new CraftingItemInfo?[GridSlotCount];
        for (int i = 0; i < GridSlotCount; i++)
        {
            workingGrid[i] = gridSlots[i];
        }

        var workingSources = new CraftingItemInfo?[sourceSlots.Count];
        for (int i = 0; i < sourceSlots.Count; i++)
        {
            workingSources[i] = sourceSlots[i];
        }

        var dummyClear = new List<int>();
        var dummyTransfers = new List<CraftingTransferStep>();
        return TryPlanSingleStep(recipe, workingGrid, workingSources, dummyClear, dummyTransfers);
    }

    private static bool TryPlanSingleStep(
        CraftingRecipeSpec recipe,
        CraftingItemInfo?[] workingGrid,
        CraftingItemInfo?[] workingSources,
        List<int> slotsToClear,
        List<CraftingTransferStep> transfers)
    {
        CraftingItemInfo?[] gridBackup = (CraftingItemInfo?[])workingGrid.Clone();
        CraftingItemInfo?[] sourcesBackup = (CraftingItemInfo?[])workingSources.Clone();
        int clearCountBackup = slotsToClear.Count;
        int transferCountBackup = transfers.Count;

        var mappedIngredients = new CraftingIngredientSpec?[GridSlotCount];
        if (!MapRecipeToGrid(recipe, workingGrid, mappedIngredients, slotsToClear))
        {
            RestoreBackup(workingGrid, gridBackup, workingSources, sourcesBackup, slotsToClear, clearCountBackup, transfers, transferCountBackup);
            return false;
        }

        int currentSets = ComputeCurrentSets(workingGrid, mappedIngredients);
        bool stepMovedAny = false;

        for (int g = 0; g < GridSlotCount; g++)
        {
            CraftingIngredientSpec? ing = mappedIngredients[g];
            if (ing == null) continue;

            CraftingItemInfo? current = workingGrid[g];
            int needed;
            if (current != null)
            {
                if (ing.IsTool) continue;

                int qty = Math.Max(1, ing.Quantity);
                needed = current.StackSize > currentSets * qty
                    ? ((currentSets + 1) * qty) - current.StackSize
                    : qty;
                if (needed <= 0) continue;

                if (current.StackSize + needed > current.MaxStackSize)
                {
                    RestoreBackup(workingGrid, gridBackup, workingSources, sourcesBackup, slotsToClear, clearCountBackup, transfers, transferCountBackup);
                    return false;
                }
            }
            else
            {
                needed = ing.IsTool ? 1 : Math.Max(1, ing.Quantity);
            }

            for (int s = 0; s < workingSources.Length && needed > 0; s++)
            {
                CraftingItemInfo? src = workingSources[s];
                if (src == null || src.StackSize <= 0) continue;
                if (!ing.Matches(src)) continue;

                CraftingItemInfo? target = workingGrid[g];
                if (target != null && !string.Equals(target.MergeKey, src.MergeKey, StringComparison.Ordinal))
                {
                    continue;
                }

                int targetCapacity = (target?.MaxStackSize ?? src.MaxStackSize) - (target?.StackSize ?? 0);
                int take = Math.Min(needed, Math.Min(src.StackSize, targetCapacity));
                if (take <= 0) continue;

                transfers.Add(new CraftingTransferStep(s, g, take));
                workingGrid[g] = target == null
                    ? src with { StackSize = take }
                    : target with { StackSize = target.StackSize + take };

                int remainingSrc = src.StackSize - take;
                workingSources[s] = remainingSrc > 0 ? src with { StackSize = remainingSrc } : null;

                needed -= take;
                stepMovedAny = true;
            }

            if (needed > 0)
            {
                RestoreBackup(workingGrid, gridBackup, workingSources, sourcesBackup, slotsToClear, clearCountBackup, transfers, transferCountBackup);
                return false;
            }
        }

        if (!stepMovedAny)
        {
            RestoreBackup(workingGrid, gridBackup, workingSources, sourcesBackup, slotsToClear, clearCountBackup, transfers, transferCountBackup);
            return false;
        }

        return true;
    }

    private static bool MapRecipeToGrid(
        CraftingRecipeSpec recipe,
        CraftingItemInfo?[] workingGrid,
        CraftingIngredientSpec?[] mappedIngredients,
        List<int> slotsToClear)
    {
        if (recipe.Shapeless)
        {
            return MapShapeless(recipe, workingGrid, mappedIngredients, slotsToClear);
        }

        if (recipe.Width * recipe.Height < GridSlotCount)
        {
            RecipeGridBounds bounds = FindBestBoundsAlignment(recipe, workingGrid);

            for (int g = 0; g < GridSlotCount; g++)
            {
                if (!bounds.Contains(g))
                {
                    MarkClearIfOccupied(g, workingGrid, slotsToClear);
                    mappedIngredients[g] = null;
                    continue;
                }

                int inner = bounds.ToInnerIndex(g);
                CraftingIngredientSpec? ing = inner >= 0 && inner < recipe.Ingredients.Count
                    ? recipe.Ingredients[inner]
                    : null;

                if (workingGrid[g] != null && (ing == null || !ing.Matches(workingGrid[g]!)))
                {
                    MarkClearIfOccupied(g, workingGrid, slotsToClear);
                }

                mappedIngredients[g] = ing;
            }

            return true;
        }

        for (int g = 0; g < GridSlotCount; g++)
        {
            CraftingIngredientSpec? ing = g < recipe.Ingredients.Count ? recipe.Ingredients[g] : null;
            if (workingGrid[g] != null && (ing == null || !ing.Matches(workingGrid[g]!)))
            {
                MarkClearIfOccupied(g, workingGrid, slotsToClear);
            }
            mappedIngredients[g] = ing;
        }

        return true;
    }

    private static RecipeGridBounds FindBestBoundsAlignment(
        CraftingRecipeSpec recipe,
        CraftingItemInfo?[] workingGrid)
    {
        var best = new RecipeGridBounds(recipe.Width, recipe.Height, GridDimension);
        int maxX = Math.Max(0, GridDimension - recipe.Width);
        int maxY = Math.Max(0, GridDimension - recipe.Height);
        int bestMatches = -1;
        int bestConflicts = int.MaxValue;

        for (int y = 0; y <= maxY; y++)
        {
            for (int x = 0; x <= maxX; x++)
            {
                var candidate = new RecipeGridBounds(recipe.Width, recipe.Height, GridDimension);
                candidate.SetPosition(x, y);

                int matches = 0;
                int conflicts = 0;
                for (int g = 0; g < GridSlotCount; g++)
                {
                    CraftingItemInfo? existing = workingGrid[g];
                    if (existing == null) continue;

                    if (!candidate.Contains(g))
                    {
                        conflicts++;
                        continue;
                    }

                    int inner = candidate.ToInnerIndex(g);
                    CraftingIngredientSpec? ing = inner >= 0 && inner < recipe.Ingredients.Count
                        ? recipe.Ingredients[inner]
                        : null;
                    if (ing != null && ing.Matches(existing))
                    {
                        matches++;
                    }
                    else
                    {
                        conflicts++;
                    }
                }

                if (matches > bestMatches || (matches == bestMatches && conflicts < bestConflicts))
                {
                    bestMatches = matches;
                    bestConflicts = conflicts;
                    best = candidate;
                }
            }
        }

        return best;
    }

    private static bool MapShapeless(
        CraftingRecipeSpec recipe,
        CraftingItemInfo?[] workingGrid,
        CraftingIngredientSpec?[] mappedIngredients,
        List<int> slotsToClear)
    {
        var remaining = new List<CraftingIngredientSpec>();
        var claimedGrid = new bool[GridSlotCount];

        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            CraftingIngredientSpec? ing = recipe.Ingredients[i];
            if (ing == null) continue;

            int matchedSlot = -1;
            for (int g = 0; g < GridSlotCount; g++)
            {
                if (claimedGrid[g] || workingGrid[g] == null) continue;
                if (ing.Matches(workingGrid[g]!))
                {
                    matchedSlot = g;
                    break;
                }
            }

            if (matchedSlot >= 0)
            {
                claimedGrid[matchedSlot] = true;
                mappedIngredients[matchedSlot] = ing;
            }
            else
            {
                remaining.Add(ing);
            }
        }

        for (int g = 0; g < GridSlotCount; g++)
        {
            if (!claimedGrid[g])
            {
                MarkClearIfOccupied(g, workingGrid, slotsToClear);
            }
        }

        int remIdx = 0;
        for (int g = 0; g < GridSlotCount && remIdx < remaining.Count; g++)
        {
            if (mappedIngredients[g] == null)
            {
                mappedIngredients[g] = remaining[remIdx++];
            }
        }

        return remIdx == remaining.Count;
    }

    private static void MarkClearIfOccupied(int gridIndex, CraftingItemInfo?[] workingGrid, List<int> slotsToClear)
    {
        if (workingGrid[gridIndex] != null)
        {
            if (!slotsToClear.Contains(gridIndex))
            {
                slotsToClear.Add(gridIndex);
            }
            workingGrid[gridIndex] = null;
        }
    }

    private static int ComputeCurrentSets(
        CraftingItemInfo?[] workingGrid,
        CraftingIngredientSpec?[] mappedIngredients)
    {
        int minSets = int.MaxValue;
        for (int g = 0; g < GridSlotCount; g++)
        {
            CraftingIngredientSpec? ing = mappedIngredients[g];
            if (ing == null) continue;

            CraftingItemInfo? item = workingGrid[g];
            if (ing.IsTool)
            {
                if (item == null || item.StackSize <= 0)
                {
                    minSets = Math.Min(minSets, 0);
                }
                continue;
            }

            int qty = Math.Max(1, ing.Quantity);
            int sets = item == null ? 0 : item.StackSize / qty;
            if (sets < minSets)
            {
                minSets = sets;
            }
        }

        return minSets == int.MaxValue ? 0 : minSets;
    }

    private static void RestoreBackup(
        CraftingItemInfo?[] workingGrid,
        CraftingItemInfo?[] gridBackup,
        CraftingItemInfo?[] workingSources,
        CraftingItemInfo?[] sourcesBackup,
        List<int> slotsToClear,
        int clearCountBackup,
        List<CraftingTransferStep> transfers,
        int transferCountBackup)
    {
        Array.Copy(gridBackup, workingGrid, GridSlotCount);
        Array.Copy(sourcesBackup, workingSources, workingSources.Length);
        if (slotsToClear.Count > clearCountBackup)
        {
            slotsToClear.RemoveRange(clearCountBackup, slotsToClear.Count - clearCountBackup);
        }
        if (transfers.Count > transferCountBackup)
        {
            transfers.RemoveRange(transferCountBackup, transfers.Count - transferCountBackup);
        }
    }
}
