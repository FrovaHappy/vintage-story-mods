using System.Runtime.CompilerServices;
using System.Text;
using Cairo;
using SeraphQoL.Config;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace SeraphQoL.Handbook;

/// <summary>
/// Servicio cliente que coordina scroll de variantes en el Handbook, auto-relleno de grilla,
/// resaltado rojo en tiempo real de ingredientes faltantes, búsqueda en cofres cercanos,
/// ícono de herramienta no consumible y tooltip de tamaño máximo de stack.
/// </summary>
public sealed class HandbookRecipeService : IDisposable
{
    private const double NearbyScanTtlMs = 800.0;
    private const double MissingEvalTtlMs = 150.0;
    private static readonly AssetLocation WrenchIconAsset = new(SeraphQoLLang.Domain, "textures/icons/wrench.svg");

    private sealed record NearbyVirtualEntry(
        string ItemCode,
        ItemStack ProbeStack,
        int TotalCount,
        int MaxRemainingDurability,
        CraftingItemInfo Info);

    private sealed record CachedMissingEval(
        double TimestampMs,
        bool ShiftHeld,
        HashSet<CraftingRecipeIngredient> MissingSet);

    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private readonly List<NearbyVirtualEntry> _cachedNearbyEntries = new();
    private readonly ConditionalWeakTable<GridRecipe, CachedMissingEval> _missingEvalCache = new();

    private LoadedTexture? _wrenchTexture;
    private bool _wrenchLoadAttempted;
    private LoadedTexture? _redOverlayTexture;
    private bool _interceptScroll;
    private bool _mouseMovedLast;
    private int _indexChange;
    private CollectibleObject? _currentDurabilityObject;
    private int _currentDurabilityCost;

    private BlockPos? _cachedNearbyCenter;
    private int _cachedNearbyRadius;
    private double _lastNearbyScanRequestMs = -10_000.0;

    public HandbookRecipeService(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
    }

    public bool IsAutoFillEnabled =>
        _modSystem.ClientConfig.EnableHandbookRecipeHelpers
        && _modSystem.ClientConfig.EnableHandbookGridAutoFill;

    private bool CanUseNearbyContainers =>
        _modSystem.ClientConfig.HandbookSearchNearbyContainers
        && _modSystem.EffectivePolicy.AllowHandbookNearbyCrafting
        && _modSystem.EffectivePolicy.EnableInventoryManagement;

    public static bool HasSurvivalCraftingGridOpened(ICoreClientAPI capi)
    {
        IClientPlayer? player = capi.World?.Player;
        if (player?.WorldData?.CurrentGameMode != EnumGameMode.Survival) return false;

        foreach (GuiDialog dialog in capi.Gui.OpenedGuis)
        {
            if (dialog is GuiDialogInventory && dialog.IsOpened())
            {
                return true;
            }
        }
        return false;
    }

    public void ClearScanCache()
    {
        _cachedNearbyEntries.Clear();
        _cachedNearbyCenter = null;
        _cachedNearbyRadius = 0;
        _lastNearbyScanRequestMs = -10_000.0;
        InvalidateMissingEvalCache();
    }

    public void InvalidateMissingEvalCache()
    {
        _missingEvalCache.Clear();
    }

    public void RequestNearbyScanIfNeeded(bool force = false)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableHandbookRecipeHelpers || !CanUseNearbyContainers)
        {
            return;
        }

        BlockPos? pos = _capi.World?.Player?.Entity?.Pos?.AsBlockPos;
        if (pos == null) return;

        int radius = Math.Clamp(cfg.HandbookSearchRadius, SeraphQoLClientConfig.MinHandbookSearchRadius, SeraphQoLClientConfig.MaxHandbookSearchRadius);
        double nowMs = _capi.World?.ElapsedMilliseconds ?? 0.0;

        bool samePos = _cachedNearbyCenter != null && _cachedNearbyCenter.Equals(pos);
        if (!force && samePos && _cachedNearbyRadius == radius && (nowMs - _lastNearbyScanRequestMs) < NearbyScanTtlMs)
        {
            return;
        }

        _cachedNearbyCenter = pos.Copy();
        _cachedNearbyRadius = radius;
        _lastNearbyScanRequestMs = nowMs;
        _modSystem.RequestHandbookScanFromClient(radius);
    }

    public void ApplyServerScanResult(HandbookScanResultPacket packet)
    {
        _cachedNearbyEntries.Clear();
        if (_capi.World == null) return;

        int count = Math.Min(packet.ItemCodes.Count, packet.ItemCounts.Count);
        for (int i = 0; i < count; i++)
        {
            string code = packet.ItemCodes[i];
            int qty = packet.ItemCounts[i];
            if (string.IsNullOrWhiteSpace(code) || qty <= 0) continue;

            var assetLoc = new AssetLocation(code);
            CollectibleObject? collectible = (CollectibleObject?)_capi.World.GetItem(assetLoc)
                ?? _capi.World.GetBlock(assetLoc);
            if (collectible == null) continue;

            int maxDur = i < packet.MaxRemainingDurabilities.Count ? packet.MaxRemainingDurabilities[i] : 0;
            var probeStack = new ItemStack(collectible);
            var info = new CraftingItemInfo(
                ItemCode: code,
                StackSize: qty,
                MaxStackSize: Math.Max(1, collectible.MaxStackSize),
                RemainingDurability: maxDur,
                MergeKey: $"nearby|{code}");

            _cachedNearbyEntries.Add(new NearbyVirtualEntry(code, probeStack, qty, maxDur, info));
        }

        InvalidateMissingEvalCache();
    }

    public void OnServerFillResult(HandbookFillResultPacket packet)
    {
        InvalidateMissingEvalCache();
        if (packet.Success)
        {
            _capi.Gui.PlaySound("menubutton_press", false, 1f);
            RequestNearbyScanIfNeeded(force: true);
        }
        else
        {
            _capi.Gui.PlaySound("menubutton_wood", false, 1f);
        }
    }

    public void OnHandbookRenderBegin()
    {
        _interceptScroll = false;
        _currentDurabilityObject = null;
        _currentDurabilityCost = 0;
        RequestNearbyScanIfNeeded();
    }

    public void OnHandbookMouseMove()
    {
        _mouseMovedLast = true;
    }

    public void OnHandbookMouseWheel(MouseWheelEventArgs args)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableHandbookRecipeHelpers || !cfg.EnableHandbookVariantScroll)
        {
            return;
        }

        if (_interceptScroll)
        {
            _indexChange -= Math.Sign(args.delta);
            args.SetHandled(true);
            return;
        }

        _mouseMovedLast = false;
    }

    public void UpdateSlideshowIndex(
        bool mouseOver,
        ref int index,
        ref int secondCounter,
        RichTextComponentBase component)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!mouseOver || !cfg.EnableHandbookRecipeHelpers || !cfg.EnableHandbookVariantScroll)
        {
            return;
        }

        int totalCount;
        GridRecipeAndUnnamedIngredients[]? gridEntries = null;

        if (component is SlideshowGridRecipeTextComponent gridComp)
        {
            gridEntries = gridComp.GridRecipesAndUnnamedIngredients;
            totalCount = gridEntries?.Length ?? 0;
        }
        else if (component is SlideshowItemstackTextComponent stackComp)
        {
            totalCount = stackComp.Itemstacks?.Length ?? 0;
        }
        else
        {
            return;
        }

        bool hasMultipleUnnamed = gridEntries != null && gridEntries.Any(entry =>
            entry?.UnnamedIngredients != null && entry.UnnamedIngredients.Values.Any(arr => arr != null && arr.Length > 1));

        if (totalCount <= 1 && !hasMultipleUnnamed)
        {
            return;
        }

        _interceptScroll = _mouseMovedLast;
        if (_indexChange == 0) return;

        index = HandbookVariantIndexCalculator.ComputeNextIndex(index, totalCount, _indexChange);

        if (gridEntries != null && index >= 0 && index < gridEntries.Length)
        {
            Dictionary<int, ItemStack[]>? unnamed = gridEntries[index]?.UnnamedIngredients;
            int[]? lengths = unnamed?.Values.Select(arr => arr?.Length ?? 0).ToArray();
            secondCounter = HandbookVariantIndexCalculator.ComputeAdjustedSecondCounter(
                secondCounter,
                _indexChange,
                lengths);
        }

        _indexChange = 0;
    }

    public bool AreAllRecipesMissing(GridRecipe[] recipes)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableHandbookRecipeHelpers || !cfg.HighlightMissingHandbookIngredients || recipes.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < recipes.Length; i++)
        {
            HashSet<CraftingRecipeIngredient> missing = GetOrEvaluateMissingSet(recipes[i]);
            if (missing.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    public void OnHandbookRecipeRender(
        CollectibleObject collectible,
        IRecipeBase? recipeBase,
        CraftingRecipeIngredient? currentIngredient,
        double x,
        double y,
        double size)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableHandbookRecipeHelpers) return;

        if (cfg.HighlightMissingHandbookIngredients
            && recipeBase is GridRecipe gridRecipe
            && currentIngredient != null
            && GetOrEvaluateMissingSet(gridRecipe).Contains(currentIngredient))
        {
            LoadedTexture? redTex = EnsureRedOverlayTexture();
            if (redTex is { TextureId: > 0 })
            {
                float drawX = (float)(x - (size * 0.5));
                float drawY = (float)(y - (size * 0.5));
                _capi.Render.Render2DTexturePremultipliedAlpha(redTex.TextureId, drawX, drawY, (float)size, (float)size, 50f);
            }
        }

        if (!cfg.ShowHandbookToolWrenchAndDurability || currentIngredient == null || !currentIngredient.IsTool)
        {
            return;
        }

        LoadedTexture? wrench = EnsureWrenchTexture();
        if (wrench is { TextureId: > 0 })
        {
            float iconSize = (float)(size * 0.4);
            float height = wrench.Width > 0 ? iconSize / wrench.Width * wrench.Height : iconSize;
            double pad = size * 0.05;
            float drawX = (float)(x - (size * 0.5) + pad);
            float drawY = (float)(y - (size * 0.5) + pad);

            _capi.Render.Render2DTexturePremultipliedAlpha(wrench.TextureId, drawX, drawY, iconSize, height, 110f);
        }

        _currentDurabilityObject = collectible;
        _currentDurabilityCost = currentIngredient.ToolDurabilityCost;
    }

    public void OnGetHeldItemInfo(CollectibleObject collectible, StringBuilder dsc)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableHandbookRecipeHelpers) return;

        if (cfg.ShowItemMaxStackTooltip && !collectible.IsLiquid())
        {
            dsc.Append(SeraphQoLLang.Get("handbook-tooltip-max-stack", collectible.MaxStackSize));
        }

        if (cfg.ShowHandbookToolWrenchAndDurability
            && _currentDurabilityCost > 0
            && ReferenceEquals(collectible, _currentDurabilityObject))
        {
            dsc.Append(SeraphQoLLang.Get("handbook-tooltip-tool-durability", _currentDurabilityCost));
        }
    }

    public void OnCreatedByInfo(List<RichTextComponentBase>? components)
    {
        if (components == null || !IsAutoFillEnabled) return;

        RequestNearbyScanIfNeeded();

        for (int i = 0; i < components.Count; i++)
        {
            if (components[i] is not SlideshowGridRecipeTextComponent gridComp) continue;

            GridRecipe[] recipes = gridComp.GridRecipesAndUnnamedIngredients
                .Where(x => x?.Recipe != null)
                .Select(x => x!.Recipe!)
                .ToArray();

            if (recipes.Length == 0) continue;

            var addOne = new HandbookFillButtonComponent(_capi, this, buttonIndex: 0, fillMax: false, recipes);
            var addMax = new HandbookFillButtonComponent(_capi, this, buttonIndex: 1, fillMax: true, recipes);
            components.InsertRange(i + 1, [addOne, addMax]);
            i += 2;
        }
    }

    public bool TryFillGrid(GridRecipe[] recipes, bool fillMax, out bool handledAsyncByServer)
    {
        handledAsyncByServer = false;
        IClientPlayer? player = _capi.World?.Player;
        IPlayerInventoryManager? invManager = player?.InventoryManager;
        if (player == null || invManager == null || recipes.Length == 0) return false;

        IInventory? craftingInv = invManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
        IInventory? backpackInv = invManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        IInventory? hotbarInv = invManager.GetHotbarInventory();
        if (craftingInv == null) return false;

        ItemSlot[] gridSlots = craftingInv.Take(9).ToArray();
        if (gridSlots.Length < 9) return false;

        List<ItemSlot> sourceSlots = CollectLocalSourceSlots(invManager, backpackInv, hotbarInv);
        IWorldAccessor world = _capi.World!;
        CraftingItemInfo?[] gridInfos = gridSlots.Select(s => ToItemInfo(s)).ToArray();
        CraftingItemInfo?[] localSourceInfos = sourceSlots.Select(s => ToItemInfo(s)).ToArray();

        CraftingRecipeSpec[] recipeSpecs = BuildRecipeSpecs(recipes, player, world, gridSlots, sourceSlots, includeNearbyVirtual: false);
        CraftingFillPlan localPlan = CraftingFillPlanner.BuildPlan(gridInfos, localSourceInfos, recipeSpecs, fillMax);

        bool useNearby = CanUseNearbyContainers && !IsShiftHeld();
        if (useNearby && _cachedNearbyEntries.Count > 0 && (!localPlan.Success || fillMax))
        {
            var packet = new HandbookFillGridPacket
            {
                FillMax = fillMax,
                IncludeNearby = true,
                SearchRadius = Math.Clamp(
                    _modSystem.ClientConfig.HandbookSearchRadius,
                    SeraphQoLClientConfig.MinHandbookSearchRadius,
                    SeraphQoLClientConfig.MaxHandbookSearchRadius)
            };

            foreach (GridRecipe r in recipes)
            {
                var cand = new HandbookRecipeCandidatePacket
                {
                    RecipeId = r.RecipeId,
                    OutputCode = r.Output?.ResolvedItemStack?.Collectible?.Code?.ToString() ?? string.Empty,
                    Width = r.Width,
                    Height = r.Height,
                    Shapeless = r.Shapeless
                };
                CraftingRecipeIngredient?[] resolved = r.ResolvedIngredients ?? Array.Empty<CraftingRecipeIngredient?>();
                foreach (CraftingRecipeIngredient? ing in resolved)
                {
                    cand.SlotIngredientCodes.Add(ing?.ResolvedItemStack?.Collectible?.Code?.ToString() ?? string.Empty);
                }
                packet.Candidates.Add(cand);
            }

            _modSystem.SendHandbookFillGridFromClient(packet);
            handledAsyncByServer = true;
            return true;
        }

        if (!localPlan.Success) return false;

        foreach (int clearIdx in localPlan.SlotsToClear)
        {
            if (clearIdx < 0 || clearIdx >= gridSlots.Length) continue;
            EmptyGridSlot(player, invManager, gridSlots[clearIdx]);
        }

        bool movedAny = false;
        foreach (CraftingTransferStep step in localPlan.Transfers)
        {
            if (step.SourceSlotIndex < 0 || step.SourceSlotIndex >= sourceSlots.Count) continue;
            if (step.TargetGridIndex < 0 || step.TargetGridIndex >= gridSlots.Length) continue;

            ItemSlot src = sourceSlots[step.SourceSlotIndex];
            ItemSlot dst = gridSlots[step.TargetGridIndex];
            if (src.Empty || !dst.CanTakeFrom(src, EnumMergePriority.AutoMerge)) continue;

            var op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, step.Quantity)
            {
                ActingPlayer = player
            };

            int beforeCount = dst.StackSize;
            object? packetResult = invManager.TryTransferTo(src, dst, ref op);
            if (dst.StackSize > beforeCount)
            {
                SendClientPackets(packetResult);
                movedAny = true;
            }
        }

        if (movedAny)
        {
            InvalidateMissingEvalCache();
        }

        return movedAny;
    }

    private HashSet<CraftingRecipeIngredient> GetOrEvaluateMissingSet(GridRecipe recipe)
    {
        double nowMs = _capi.World?.ElapsedMilliseconds ?? 0.0;
        bool shiftHeld = IsShiftHeld();

        if (_missingEvalCache.TryGetValue(recipe, out CachedMissingEval? cached)
            && cached.ShiftHeld == shiftHeld
            && (nowMs - cached.TimestampMs) < MissingEvalTtlMs)
        {
            return cached.MissingSet;
        }

        var missingSet = new HashSet<CraftingRecipeIngredient>(ReferenceEqualityComparer.Instance);
        IClientPlayer? player = _capi.World?.Player;
        IPlayerInventoryManager? invManager = player?.InventoryManager;
        IWorldAccessor? world = _capi.World;
        if (player == null || invManager == null || world == null)
        {
            return missingSet;
        }

        IInventory? craftingInv = invManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
        IInventory? backpackInv = invManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        IInventory? hotbarInv = invManager.GetHotbarInventory();

        ItemSlot[] gridSlots = craftingInv != null ? craftingInv.Take(9).ToArray() : Array.Empty<ItemSlot>();
        CraftingItemInfo?[] gridInfos = gridSlots.Length >= 9
            ? gridSlots.Select(s => ToItemInfo(s)).ToArray()
            : new CraftingItemInfo?[9];

        List<ItemSlot> localSources = CollectLocalSourceSlots(invManager, backpackInv, hotbarInv);
        bool includeNearby = CanUseNearbyContainers && !shiftHeld && _cachedNearbyEntries.Count > 0;

        var allSourceInfos = new List<CraftingItemInfo?>(localSources.Count + (includeNearby ? _cachedNearbyEntries.Count : 0));
        for (int i = 0; i < localSources.Count; i++)
        {
            allSourceInfos.Add(ToItemInfo(localSources[i]));
        }

        if (includeNearby)
        {
            for (int i = 0; i < _cachedNearbyEntries.Count; i++)
            {
                allSourceInfos.Add(_cachedNearbyEntries[i].Info);
            }
        }

        CraftingRecipeSpec spec = BuildRecipeSpecs([recipe], player, world, gridSlots, localSources, includeNearby)[0];
        IReadOnlyList<int> missingIndices = CraftingFillPlanner.FindMissingIngredientIndices(gridInfos, allSourceInfos, spec);

        CraftingRecipeIngredient?[] resolved = recipe.ResolvedIngredients ?? Array.Empty<CraftingRecipeIngredient?>();
        foreach (int idx in missingIndices)
        {
            if (idx >= 0 && idx < resolved.Length && resolved[idx] != null)
            {
                missingSet.Add(resolved[idx]!);
            }
        }

        _missingEvalCache.AddOrUpdate(recipe, new CachedMissingEval(nowMs, shiftHeld, missingSet));
        return missingSet;
    }

    private CraftingRecipeSpec[] BuildRecipeSpecs(
        GridRecipe[] recipes,
        IClientPlayer player,
        IWorldAccessor world,
        ItemSlot[] gridSlots,
        List<ItemSlot> localSourceSlots,
        bool includeNearbyVirtual)
    {
        var specs = new CraftingRecipeSpec[recipes.Length];
        for (int i = 0; i < recipes.Length; i++)
        {
            GridRecipe r = recipes[i];
            CraftingRecipeIngredient?[] resolved = r.ResolvedIngredients ?? Array.Empty<CraftingRecipeIngredient?>();
            var ingredients = new CraftingIngredientSpec?[resolved.Length];
            for (int j = 0; j < resolved.Length; j++)
            {
                CraftingRecipeIngredient? ing = resolved[j];
                if (ing == null) continue;
                ingredients[j] = new CraftingIngredientSpec(
                    Key: ing.Code?.ToString() ?? string.Empty,
                    Quantity: Math.Max(1, ing.Quantity),
                    IsTool: ing.IsTool,
                    ToolDurabilityCost: ing.ToolDurabilityCost,
                    Matches: item => MatchesIngredient(ing, item, localSourceSlots, gridSlots, includeNearbyVirtual));
            }

            bool matchesGrid = gridSlots.Length >= 9 && r.Matches(player, world, gridSlots, 3);
            specs[i] = new CraftingRecipeSpec(r.Width, r.Height, r.Shapeless, ingredients, matchesGrid);
        }
        return specs;
    }

    private List<ItemSlot> CollectLocalSourceSlots(
        IPlayerInventoryManager invManager,
        IInventory? backpackInv,
        IInventory? hotbarInv)
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        bool skipLocked = cfg.HandbookAutoFillSkipLockedSlots;
        bool includeOpenChests = cfg.HandbookAutoFillIncludeOpenChests && !IsShiftHeld();

        var result = new List<ItemSlot>();
        AddInventorySourceSlots(backpackInv, skipBackpackContainers: true, skipLocked, cfg, result);
        AddInventorySourceSlots(hotbarInv, skipBackpackContainers: false, skipLocked, cfg, result);

        if (includeOpenChests)
        {
            foreach (InventoryGeneric inv in invManager.OpenedInventories.OfType<InventoryGeneric>())
            {
                if (ReferenceEquals(inv, backpackInv) || ReferenceEquals(inv, hotbarInv)) continue;
                AddInventorySourceSlots(inv, skipBackpackContainers: false, skipLocked: false, cfg, result);
            }
        }

        return result;
    }

    private static void AddInventorySourceSlots(
        IInventory? inventory,
        bool skipBackpackContainers,
        bool skipLocked,
        SeraphQoLClientConfig cfg,
        List<ItemSlot> target)
    {
        if (inventory == null) return;
        string invClass = inventory.ClassName ?? string.Empty;

        for (int i = 0; i < inventory.Count; i++)
        {
            ItemSlot? slot = inventory[i];
            if (slot == null || slot.Empty) continue;
            if (skipBackpackContainers && slot is ItemSlotBackpack) continue;
            if (skipLocked && cfg.IsSlotLocked(invClass, i)) continue;
            target.Add(slot);
        }
    }

    private static CraftingItemInfo? ToItemInfo(ItemSlot? slot)
    {
        ItemStack? stack = slot?.Itemstack;
        if (stack?.Collectible == null || stack.StackSize <= 0) return null;

        int remainingDurability = stack.Collectible.GetRemainingDurability(stack);
        string code = stack.Collectible.Code?.ToString() ?? string.Empty;
        string attrToken = stack.Attributes?.ToJsonToken() ?? string.Empty;

        return new CraftingItemInfo(
            ItemCode: code,
            StackSize: stack.StackSize,
            MaxStackSize: Math.Max(1, stack.Collectible.MaxStackSize),
            RemainingDurability: remainingDurability,
            MergeKey: $"{code}|{attrToken}");
    }

    private bool MatchesIngredient(
        CraftingRecipeIngredient ingredient,
        CraftingItemInfo itemInfo,
        IReadOnlyList<ItemSlot> sourceSlots,
        IReadOnlyList<ItemSlot> gridSlots,
        bool includeNearbyVirtual)
    {
        if (includeNearbyVirtual && itemInfo.MergeKey.StartsWith("nearby|", StringComparison.Ordinal))
        {
            for (int i = 0; i < _cachedNearbyEntries.Count; i++)
            {
                NearbyVirtualEntry entry = _cachedNearbyEntries[i];
                if (!string.Equals(entry.ItemCode, itemInfo.ItemCode, StringComparison.Ordinal)) continue;
                if (!ingredient.SatisfiesAsIngredient(entry.ProbeStack, checkStackSize: false)) return false;
                return !ingredient.IsTool || entry.MaxRemainingDurability >= ingredient.ToolDurabilityCost;
            }
            return false;
        }

        ItemStack? stack = FindMatchingStack(itemInfo, sourceSlots) ?? FindMatchingStack(itemInfo, gridSlots);
        if (stack == null || !ingredient.SatisfiesAsIngredient(stack, checkStackSize: false))
        {
            return false;
        }

        if (ingredient.IsTool)
        {
            return stack.Collectible.GetRemainingDurability(stack) >= ingredient.ToolDurabilityCost;
        }

        return true;
    }

    private static ItemStack? FindMatchingStack(CraftingItemInfo info, IReadOnlyList<ItemSlot> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack? stack = slots[i]?.Itemstack;
            if (stack?.Collectible == null) continue;
            string code = stack.Collectible.Code?.ToString() ?? string.Empty;
            if (!string.Equals(code, info.ItemCode, StringComparison.Ordinal)) continue;
            string attrToken = stack.Attributes?.ToJsonToken() ?? string.Empty;
            if (string.Equals($"{code}|{attrToken}", info.MergeKey, StringComparison.Ordinal))
            {
                return stack;
            }
        }
        return null;
    }

    private void EmptyGridSlot(IPlayer player, IPlayerInventoryManager invManager, ItemSlot slot)
    {
        if (slot.Empty) return;

        var op = new ItemStackMoveOperation(_capi.World, EnumMouseButton.Left, EnumModifierKey.SHIFT, EnumMergePriority.AutoMerge, slot.StackSize)
        {
            ActingPlayer = player
        };

        object? packetResult = invManager.TryTransferAway(slot, ref op, onlyPlayerInventory: false);
        SendClientPackets(packetResult);

        if (!slot.Empty)
        {
            invManager.DropItem(slot, fullStack: true);
        }
    }

    private void SendClientPackets(object? packetResult)
    {
        if (packetResult == null) return;
        if (packetResult is Packet_Client singlePacket)
        {
            _capi.Network.SendPacketClient(singlePacket);
            return;
        }

        if (packetResult is System.Collections.IEnumerable sequence)
        {
            foreach (object? item in sequence)
            {
                if (item is Packet_Client clientPacket)
                {
                    _capi.Network.SendPacketClient(clientPacket);
                }
            }
        }
    }

    private bool IsShiftHeld()
    {
        bool[] keys = _capi.Input.KeyboardKeyStateRaw;
        return keys[(int)GlKeys.ShiftLeft] || keys[(int)GlKeys.ShiftRight];
    }

    private LoadedTexture? EnsureWrenchTexture()
    {
        if (_wrenchTexture != null) return _wrenchTexture;
        if (_wrenchLoadAttempted) return null;
        _wrenchLoadAttempted = true;

        try
        {
            if (_capi.Assets.TryGet(WrenchIconAsset) != null)
            {
                _wrenchTexture = _capi.Gui.LoadSvg(WrenchIconAsset, 32, 32, 32, 32, -1);
            }
        }
        catch (Exception ex)
        {
            _capi.Logger.Warning("[SeraphQoL] Failed to load wrench icon texture: {0}", ex.Message);
        }

        return _wrenchTexture;
    }

    private LoadedTexture? EnsureRedOverlayTexture()
    {
        if (_redOverlayTexture is { TextureId: > 0 }) return _redOverlayTexture;

        _redOverlayTexture?.Dispose();
        _redOverlayTexture = new LoadedTexture(_capi);
        using var surface = new ImageSurface(Format.Argb32, 1, 1);
        using var ctx = new Context(surface);
        ctx.SetSourceRGBA(0.92, 0.16, 0.16, 0.42);
        ctx.Paint();
        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref _redOverlayTexture);
        return _redOverlayTexture;
    }

    public void Dispose()
    {
        _wrenchTexture?.Dispose();
        _wrenchTexture = null;
        _redOverlayTexture?.Dispose();
        _redOverlayTexture = null;
    }
}
