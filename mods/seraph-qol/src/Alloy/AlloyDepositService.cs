using SeraphQoL.Config;
using SeraphQoL.Inventory;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SeraphQoL.Alloy;

public sealed record FirepitContextSnapshot(
    bool HasCrucible,
    double FurnaceTempC,
    IReadOnlyList<CrucibleMetalSnapshot> CrucibleContents,
    IReadOnlyList<SmeltableMetalItem> AvailableItems,
    IReadOnlyList<FuelOptionSpec> AvailableFuels);

/// <summary>
/// Adapta recetas de aleacion, crisol de fogata, inventario del jugador y cofres cercanos para depositar mediante servidor.
/// </summary>
public sealed class AlloyDepositService
{
    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;

    private BlockPos? _cachedScanPos;
    private int _cachedScanRadius;
    private Dictionary<string, int>? _cachedNearbyCounts;

    public AlloyDepositService(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
    }

    public void ClearScanCache()
    {
        _cachedScanPos = null;
        _cachedScanRadius = 0;
        _cachedNearbyCounts = null;
    }

    public void ApplyServerScanResult(AlloyScanResultPacket packet)
    {
        _cachedScanPos = packet.FirepitPos.Copy();
        _cachedScanRadius = packet.SearchRadius;
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int count = Math.Min(packet.ItemCodes.Count, packet.ItemCounts.Count);
        for (int i = 0; i < count; i++)
        {
            string code = packet.ItemCodes[i];
            int qty = packet.ItemCounts[i];
            if (string.IsNullOrWhiteSpace(code) || qty <= 0) continue;
            map[code] = map.GetValueOrDefault(code) + qty;
        }
        _cachedNearbyCounts = map;
    }

    public IReadOnlyList<AlloyRecipeSpec> LoadRecipes()
    {
        var alloySpecs = new List<AlloyRecipeSpec>();
        var knownPureMetals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        List<AlloyRecipe>? rawAlloys = _capi.GetMetalAlloys();
        if (rawAlloys != null)
        {
            foreach (AlloyRecipe alloy in rawAlloys)
            {
                if (!alloy.Enabled || alloy.Output?.ResolvedItemstack?.Collectible == null || alloy.Ingredients == null || alloy.Ingredients.Length == 0)
                {
                    continue;
                }

                var ingredients = new List<AlloyIngredientSpec>(alloy.Ingredients.Length);
                bool valid = true;
                foreach (MetalAlloyIngredient ing in alloy.Ingredients)
                {
                    ItemStack? ingStack = ing.ResolvedItemstack;
                    string? code = ingStack?.Collectible?.Code?.ToString();
                    if (string.IsNullOrWhiteSpace(code) || ingStack == null)
                    {
                        valid = false;
                        break;
                    }

                    string label = FormatMetalName(ingStack.GetName(), code);
                    ingredients.Add(new AlloyIngredientSpec(code, label, ing.MinRatio, ing.MaxRatio));
                    knownPureMetals.TryAdd(code, label);
                }

                if (!valid) continue;

                string outCode = alloy.Output.ResolvedItemstack.Collectible.Code.ToString();
                string outName = FormatMetalName(alloy.Output.ResolvedItemstack.GetName(), outCode);
                int outMeltTemp = (int)Math.Round((double)(alloy.Output.ResolvedItemstack.Collectible.CombustibleProps?.MeltingPoint ?? 0f));
                alloySpecs.Add(new AlloyRecipeSpec(outCode, outName, ingredients, outMeltTemp));
            }
        }

        // Incluir metales puros desde aleaciones y desde pepitas fundibles en crisol
        var pureMeltTemps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (CollectibleObject col in _capi.World.Collectibles)
        {
            CombustibleProperties? comb = col?.CombustibleProps;
            ItemStack? smelted = comb?.SmeltedStack?.ResolvedItemstack;
            if (comb == null || smelted?.Collectible == null || comb.SmeltedRatio <= 0) continue;
            if (comb.MeltingPoint > 1350) continue;

            string code = smelted.Collectible.Code.ToString();
            if (!code.Contains("ingot-", StringComparison.OrdinalIgnoreCase)) continue;
            knownPureMetals.TryAdd(code, FormatMetalName(smelted.GetName(), code));
            if (comb.MeltingPoint > 0)
            {
                pureMeltTemps[code] = Math.Max(pureMeltTemps.GetValueOrDefault(code), (int)Math.Round((double)comb.MeltingPoint));
            }
        }

        alloySpecs.Sort((a, b) => string.Compare(a.OutputName, b.OutputName, StringComparison.OrdinalIgnoreCase));

        var ordered = new List<AlloyRecipeSpec>(alloySpecs.Count + knownPureMetals.Count);
        ordered.AddRange(alloySpecs);
        foreach ((string metalCode, string metalName) in knownPureMetals.OrderBy(k => k.Value, StringComparer.OrdinalIgnoreCase))
        {
            ordered.Add(new AlloyRecipeSpec(
                metalCode,
                metalName,
                [new AlloyIngredientSpec(metalCode, metalName, 1.0, 1.0)],
                pureMeltTemps.GetValueOrDefault(metalCode)));
        }

        return DisambiguateDuplicateNames(ordered);
    }

    public FirepitContextSnapshot InspectFirepit(GuiDialogBlockEntityFirepit dialog)
    {
        if (dialog.Inventory is not InventorySmelting smelting || _capi.World?.Player == null)
        {
            return new FirepitContextSnapshot(false, 20, Array.Empty<CrucibleMetalSnapshot>(), Array.Empty<SmeltableMetalItem>(), Array.Empty<FuelOptionSpec>());
        }

        ItemSlot inputSlot = smelting[1];
        bool hasCrucible = smelting.HaveCookingContainer && inputSlot?.Itemstack?.Block is BlockSmeltingContainer;

        double furnaceTemp = 20;
        if (_capi.World.BlockAccessor.GetBlockEntity(dialog.BlockEntityPosition) is BlockEntityFirepit be)
        {
            furnaceTemp = Math.Max(20, be.furnaceTemperature);
        }

        var crucibleList = new List<CrucibleMetalSnapshot>();
        if (hasCrucible)
        {
            foreach (ItemSlot slot in smelting.CookingSlots)
            {
                if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null) continue;
                CombustibleProperties? comb = slot.Itemstack.Collectible.GetCombustibleProperties(_capi.World, slot.Itemstack, null);
                string? smeltedCode = comb?.SmeltedStack?.ResolvedItemstack?.Collectible?.Code?.ToString();
                if (comb == null || string.IsNullOrWhiteSpace(smeltedCode)) continue;

                int unitsPer = ResolveUnitsPerItem(comb);
                crucibleList.Add(new CrucibleMetalSnapshot(smeltedCode, slot.StackSize * unitsPer));
            }
        }

        var items = new List<SmeltableMetalItem>();
        var fuels = new Dictionary<string, FuelOptionSpec>(StringComparer.OrdinalIgnoreCase);
        CollectPlayerMaterials(items, fuels);

        if (_modSystem.ClientConfig.AlloySearchNearbyContainers && dialog.BlockEntityPosition != null)
        {
            CollectNearbyMaterials(dialog.BlockEntityPosition, items, fuels);
        }

        return new FirepitContextSnapshot(
            hasCrucible,
            furnaceTemp,
            crucibleList,
            items,
            fuels.Values.OrderByDescending(f => f.BurnTemperatureC).ThenByDescending(f => f.BurnDurationSeconds).ToList());
    }

    public void RequestDeposit(
        GuiDialogBlockEntityFirepit dialog,
        AlloyBatchPlan plan,
        FuelOptionSpec? fuel,
        int fuelPiecesNeeded,
        bool depositItems,
        bool depositFuel)
    {
        if (dialog.BlockEntityPosition == null || _capi.World?.Player == null) return;

        var metalCodes = new List<string>();
        var metalItemsNeeded = new List<int>();
        var metalMaxItemsAllowed = new List<int>();
        bool hasCrucibleItems = false;

        if (depositItems)
        {
            foreach (AlloyIngredientNeed need in plan.Needs)
            {
                int maxItems = (int)Math.Ceiling((double)need.TargetUnits / Math.Max(1, need.UnitsPerItem));
                metalCodes.Add(need.MetalCode);
                metalItemsNeeded.Add(Math.Max(0, need.ItemsToDeposit));
                metalMaxItemsAllowed.Add(Math.Max(0, maxItems));
            }

            if (dialog.Inventory is InventorySmelting smelting)
            {
                foreach (ItemSlot slot in smelting.CookingSlots)
                {
                    if (slot != null && !slot.Empty)
                    {
                        hasCrucibleItems = true;
                        break;
                    }
                }
            }
        }

        int fuelNeeded = depositFuel && fuel != null ? Math.Max(1, fuelPiecesNeeded) : 0;
        bool hasMetalAction = depositItems && (metalItemsNeeded.Any(x => x > 0) || hasCrucibleItems);
        if (!hasMetalAction && fuelNeeded <= 0) return;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        _modSystem.SendAlloyDepositPacket(new AlloyDepositPacket
        {
            FirepitPos = dialog.BlockEntityPosition.Copy(),
            DepositItems = hasMetalAction,
            DepositFuel = fuelNeeded > 0,
            IncludeNearby = cfg.AlloySearchNearbyContainers,
            SearchRadius = Math.Clamp(cfg.AlloySearchRadius, SeraphQoLClientConfig.MinAlloySearchRadius, SeraphQoLClientConfig.MaxAlloySearchRadius),
            MetalCodes = metalCodes,
            MetalItemsNeeded = metalItemsNeeded,
            MetalMaxItemsAllowed = metalMaxItemsAllowed,
            FuelItemCode = fuel?.ItemCode ?? string.Empty,
            FuelPiecesNeeded = fuelNeeded
        });
    }

    private void CollectPlayerMaterials(List<SmeltableMetalItem> items, Dictionary<string, FuelOptionSpec> fuels)
    {
        foreach ((string invClass, IInventory inv) in EnumerateSourceInventories())
        {
            for (int i = 0; i < inv.Count; i++)
            {
                if (_modSystem.ClientConfig.IsSlotLocked(invClass, i)) continue;
                ItemSlot? slot = inv[i];
                if (slot == null || slot.Empty || slot is ItemSlotBackpack || slot.Itemstack?.Collectible == null) continue;

                AppendFromStack(slot.Itemstack, slot.StackSize, invClass, i, items, fuels);
            }
        }
    }

    private void CollectNearbyMaterials(
        BlockPos firepitPos,
        List<SmeltableMetalItem> items,
        Dictionary<string, FuelOptionSpec> fuels)
    {
        int radius = Math.Clamp(
            _modSystem.ClientConfig.AlloySearchRadius,
            SeraphQoLClientConfig.MinAlloySearchRadius,
            SeraphQoLClientConfig.MaxAlloySearchRadius);

        if (_cachedNearbyCounts != null
            && _cachedScanRadius == radius
            && _cachedScanPos != null
            && _cachedScanPos.Equals(firepitPos))
        {
            foreach ((string code, int count) in _cachedNearbyCounts)
            {
                ItemStack? stack = ResolveStackFromCode(code);
                if (stack != null && count > 0)
                {
                    AppendFromStack(stack, count, "nearby", -1, items, fuels);
                }
            }
            return;
        }

        // Vista previa inmediata usando contenedores cargados en cliente antes de recibir respuesta del servidor
        IBlockAccessor? accessor = _capi.World?.BlockAccessor;
        if (accessor == null) return;

        BlockPos min = firepitPos.AddCopy(-radius, -radius, -radius);
        BlockPos max = firepitPos.AddCopy(radius, radius, radius);
        EffectiveServerPolicy policy = _modSystem.EffectivePolicy;

        accessor.WalkBlocks(min, max, (block, x, y, z) =>
        {
            if (!ContainerWhitelistPolicy.IsAllowed(block?.Code?.ToString(), policy)) return;
            BlockPos pos = new(x, y, z, firepitPos.dimension);
            if (pos.Equals(firepitPos)) return;

            if (accessor.GetBlockEntity(pos) is not BlockEntityContainer container || container.Inventory == null) return;
            foreach (ItemSlot slot in container.Inventory)
            {
                if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null) continue;
                AppendFromStack(slot.Itemstack, slot.StackSize, "nearby", -1, items, fuels);
            }
        });
    }

    private void AppendFromStack(
        ItemStack stack,
        int stackSize,
        string invClass,
        int slotIndex,
        List<SmeltableMetalItem> items,
        Dictionary<string, FuelOptionSpec> fuels)
    {
        CombustibleProperties? comb = stack.Collectible.GetCombustibleProperties(_capi.World, stack, null);
        if (comb == null) return;

        if (comb.BurnDuration > 0.1f && comb.BurnTemperature > 0)
        {
            string fCode = stack.Collectible.Code.ToString();
            if (fuels.TryGetValue(fCode, out FuelOptionSpec? existing))
            {
                fuels[fCode] = existing with { AvailableCount = existing.AvailableCount + stackSize };
            }
            else
            {
                fuels[fCode] = new FuelOptionSpec(
                    fCode,
                    stack.GetName(),
                    comb.BurnTemperature,
                    comb.BurnDuration,
                    stackSize,
                    invClass,
                    slotIndex);
            }
        }

        ItemStack? smelted = comb.SmeltedStack?.ResolvedItemstack;
        if (smelted?.Collectible != null && comb.SmeltedRatio > 0)
        {
            items.Add(new SmeltableMetalItem(
                stack.Collectible.Code.ToString(),
                stack.GetName(),
                smelted.Collectible.Code.ToString(),
                ResolveUnitsPerItem(comb),
                comb.MeltingPoint,
                stackSize,
                invClass,
                slotIndex));
        }
    }

    private ItemStack? ResolveStackFromCode(string code)
    {
        var loc = new AssetLocation(code);
        Item? item = _capi.World.GetItem(loc);
        if (item != null) return new ItemStack(item);
        Block? block = _capi.World.GetBlock(loc);
        return block != null ? new ItemStack(block) : null;
    }

    private IEnumerable<(string InvClass, IInventory Inventory)> EnumerateSourceInventories()
    {
        IPlayerInventoryManager? mgr = _capi.World?.Player?.InventoryManager;
        if (mgr == null) yield break;

        IInventory? backpack = mgr.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (backpack != null) yield return (GlobalConstants.backpackInvClassName, backpack);

        IInventory? hotbar = mgr.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        if (hotbar != null) yield return (GlobalConstants.hotBarInvClassName, hotbar);
    }

    private int ResolveUnitsPerItem(CombustibleProperties comb)
    {
        if (comb.SmeltedRatio > 0)
        {
            return Math.Max(1, (int)Math.Round(100.0 / comb.SmeltedRatio));
        }
        return _modSystem.ClientConfig.AlloyUnitsPerNugget;
    }

    private static IReadOnlyList<AlloyRecipeSpec> DisambiguateDuplicateNames(IReadOnlyList<AlloyRecipeSpec> recipes)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (AlloyRecipeSpec r in recipes)
        {
            counts[r.OutputName] = counts.GetValueOrDefault(r.OutputName) + 1;
        }

        var result = new List<AlloyRecipeSpec>(recipes.Count);
        foreach (AlloyRecipeSpec r in recipes)
        {
            if (counts.GetValueOrDefault(r.OutputName) <= 1 || r.Ingredients.Count <= 1)
            {
                result.Add(r);
                continue;
            }

            string hint = string.Join("+", r.Ingredients.Select(i => i.Label));
            result.Add(r with { OutputName = $"{r.OutputName} ({hint})" });
        }
        return result;
    }

    private static string FormatMetalName(string rawName, string code)
    {
        if (!string.IsNullOrWhiteSpace(rawName))
        {
            return rawName.Replace(" ingot", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }
        return code;
    }
}
