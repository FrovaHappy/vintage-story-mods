using HarmonyLib;
using SeraphQoL.Alloy;
using SeraphQoL.Config;
using SeraphQoL.Handbook;
using SeraphQoL.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphQoL.Inventory;

/// <summary>
/// Ejecuta ordenamientos, Quick Store y Store Nearby de forma autoritativa en servidor con snapshot y rollback.
/// </summary>
public sealed class ServerInventoryExecutor
{
    private readonly LockedSlotsService _lockedSlots;

    public ServerInventoryExecutor(LockedSlotsService lockedSlots)
    {
        _lockedSlots = lockedSlots;
    }

    public void HandleSortInventory(
        IServerPlayer player,
        SortInventoryPacket packet,
        EffectiveServerPolicy policy)
    {
        if (!policy.EnableInventoryManagement) return;

        IInventory? targetInv = player.InventoryManager.GetInventory(packet.InventoryId);
        if (targetInv == null) return;

        List<ItemSlot> targetSlots = CollectTargetSlots(player, targetInv, packet.SortHotbarWithBackpack);
        if (targetSlots.Count == 0) return;

        ItemStack?[] snapshot = TakeSnapshot(targetSlots);
        var isProtected = new bool[targetSlots.Count];
        try
        {
            var snapshots = new List<SortSlotSnapshot>(targetSlots.Count);
            for (int i = 0; i < targetSlots.Count; i++)
            {
                ItemSlot slot = targetSlots[i];
                string invClass = slot.Inventory?.ClassName ?? string.Empty;
                int invSlotId = slot.Inventory?.GetSlotId(slot) ?? i;
                bool isLocked = _lockedSlots.IsSlotLockedOnServer(player.PlayerUID, invClass, invSlotId);
                bool isSortable = SlotProtectionPolicy.CanSort(slot) && SlotProtectionPolicy.CanSortInto(slot);
                ItemStack? stack = slot.Itemstack;
                bool isFavorite = stack != null && FavoritesManager.IsFavorite(player, stack);
                isProtected[i] = !isSortable || isLocked || (packet.SkipFavoritesWhenSorting && isFavorite);

                double freshness = ExtractFreshnessRatio(player.Entity.World, stack, out bool isPerishable);
                string code = stack?.Collectible?.Code?.ToString() ?? string.Empty;
                string displayName = stack != null ? stack.GetName() : string.Empty;
                string category = stack?.Collectible?.GetType().Name ?? "general";

                snapshots.Add(new SortSlotSnapshot(
                    SlotIndex: i,
                    InventoryClass: invClass,
                    IsSortable: isSortable,
                    IsLocked: isLocked,
                    IsFavorite: isFavorite,
                    ItemCode: stack == null ? null : code,
                    DisplayName: displayName,
                    Category: category,
                    StackSize: stack?.StackSize ?? 0,
                    MaxStackSize: stack?.Collectible?.MaxStackSize ?? 64,
                    IsPerishable: isPerishable,
                    FreshnessRatio: freshness,
                    OriginalOrder: i));
            }

            SortOrder order = Enum.IsDefined(typeof(SortOrder), packet.SortOrder)
                ? (SortOrder)packet.SortOrder
                : SortOrder.Category;

            SortPlan plan = InventorySortPlanner.BuildPlan(
                snapshots,
                order,
                packet.StackPerishables,
                packet.SkipFavoritesWhenSorting);

            ApplySortPlan(player.Entity.World, targetSlots, snapshot, plan, isProtected);
        }
        catch (Exception ex)
        {
            player.Entity.World.Logger.Fatal("[SeraphQoL] Sort failed for {0}, rolling back: {1}", player.PlayerName, ex);
            RollbackSnapshot(targetSlots, snapshot, isProtected);
            player.SendIngameError("seraphqol:sort-rollback", SeraphQoLLang.Get("error-sort-rollback"));
        }
    }

    public int HandleQuickStore(
        IServerPlayer player,
        QuickStorePacket packet,
        EffectiveServerPolicy policy)
    {
        if (!policy.EnableInventoryManagement) return 0;

        IInventory? destInv = player.InventoryManager.GetInventory(packet.InventoryId);
        if (destInv == null) return 0;

        int movedStacks = TransferMatchingToContainer(player, destInv, packet.StackPerishables, packet.IncludeHotbar);
        return movedStacks;
    }

    public void HandleHotbarRefill(
        IServerPlayer player,
        HotbarRefillPacket packet,
        EffectiveServerPolicy policy)
    {
        if (!policy.AllowHotbarRefill || string.IsNullOrWhiteSpace(packet.ExpectedItemCode)) return;

        IInventory? hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        IInventory? backpack = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (hotbar == null || backpack == null) return;

        if (packet.HotbarSlotIndex < 0 || packet.HotbarSlotIndex >= hotbar.Count) return;
        if (packet.BackpackSlotIndex < 0 || packet.BackpackSlotIndex >= backpack.Count) return;

        if (_lockedSlots.IsSlotLockedOnServer(player.PlayerUID, GlobalConstants.hotBarInvClassName, packet.HotbarSlotIndex)
            || _lockedSlots.IsSlotLockedOnServer(player.PlayerUID, GlobalConstants.backpackInvClassName, packet.BackpackSlotIndex))
        {
            return;
        }

        ItemSlot? hotbarSlot = hotbar[packet.HotbarSlotIndex];
        ItemSlot? backpackSlot = backpack[packet.BackpackSlotIndex];
        if (hotbarSlot == null || backpackSlot == null || backpackSlot.Empty || backpackSlot.Itemstack?.Collectible?.Code == null) return;

        string backpackCode = backpackSlot.Itemstack.Collectible.Code.ToString();
        if (!string.Equals(backpackCode, packet.ExpectedItemCode, StringComparison.OrdinalIgnoreCase)) return;

        if (!packet.IsPreventiveToolSwap)
        {
            if (!hotbarSlot.Empty || !hotbarSlot.CanHold(backpackSlot)) return;

            var op = new ItemStackMoveOperation(
                player.Entity.World,
                EnumMouseButton.Left,
                0,
                EnumMergePriority.DirectMerge,
                backpackSlot.StackSize)
            {
                ActingPlayer = player
            };

            backpackSlot.TryPutInto(hotbarSlot, ref op);
            backpackSlot.MarkDirty();
            hotbarSlot.MarkDirty();
            return;
        }

        if (hotbarSlot.Empty || hotbarSlot.Itemstack?.Collectible?.Code == null) return;
        if (!string.Equals(hotbarSlot.Itemstack.Collectible.Code.ToString(), packet.ExpectedItemCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SwapTwoPlayerSlots(hotbarSlot, backpackSlot);
    }

    public void HandleSwapSlots(IServerPlayer player, SwapSlotsPacket packet)
    {
        IInventory? sourceInv = ResolvePlayerOwnedInventory(player, packet.SourceInventoryClass);
        IInventory? targetInv = ResolvePlayerOwnedInventory(player, packet.TargetInventoryClass);
        if (sourceInv == null || targetInv == null) return;

        if (packet.SourceSlotIndex < 0 || packet.SourceSlotIndex >= sourceInv.Count) return;
        if (packet.TargetSlotIndex < 0 || packet.TargetSlotIndex >= targetInv.Count) return;

        string srcClass = sourceInv.ClassName ?? string.Empty;
        string dstClass = targetInv.ClassName ?? string.Empty;
        if (srcClass == dstClass && packet.SourceSlotIndex == packet.TargetSlotIndex) return;

        if (_lockedSlots.IsSlotLockedOnServer(player.PlayerUID, srcClass, packet.SourceSlotIndex)
            || _lockedSlots.IsSlotLockedOnServer(player.PlayerUID, dstClass, packet.TargetSlotIndex))
        {
            return;
        }

        ItemSlot? sourceSlot = sourceInv[packet.SourceSlotIndex];
        ItemSlot? targetSlot = targetInv[packet.TargetSlotIndex];
        if (!IsValidSwapSlot(sourceSlot) || !IsValidSwapSlot(targetSlot)) return;

        SwapTwoPlayerSlots(sourceSlot!, targetSlot!);
    }

    private static bool IsValidSwapSlot(ItemSlot? slot)
    {
        if (slot == null) return false;
        if (slot is ItemSlotOffhand) return slot.Empty || slot.CanTake();
        return SlotProtectionPolicy.CanSort(slot) && SlotProtectionPolicy.CanSortInto(slot);
    }

    private static IInventory? ResolvePlayerOwnedInventory(IServerPlayer player, string? invClass)
    {
        if (string.IsNullOrWhiteSpace(invClass)) return null;
        if (!SlotProtectionPolicy.IsPlayerBackpackName(invClass) && !SlotProtectionPolicy.IsPlayerHotbarName(invClass))
        {
            return null;
        }
        return player.InventoryManager.GetOwnInventory(invClass);
    }

    private static void SwapTwoPlayerSlots(ItemSlot a, ItemSlot b)
    {
        if (a.Empty && b.Empty) return;
        if (!a.Empty && !b.CanHold(a)) return;
        if (!b.Empty && !a.CanHold(b)) return;
        if ((!a.Empty && !a.CanTake()) || (!b.Empty && !b.CanTake())) return;

        ItemStack? tmp = a.Itemstack;
        a.Itemstack = b.Itemstack;
        b.Itemstack = tmp;
        a.MarkDirty();
        b.MarkDirty();
    }

    public (int MovedStacks, int ContainerCount, List<BlockPos> RemeshPositions) HandleStoreNearby(
        IServerPlayer player,
        StoreNearbyPacket packet,
        EffectiveServerPolicy policy)
    {
        if (!policy.EnableInventoryManagement || !policy.EnableStoreNearby)
        {
            return (0, 0, new List<BlockPos>());
        }

        BlockPos? center = player.Entity?.Pos?.AsBlockPos;
        if (center == null) return (0, 0, new List<BlockPos>());

        IWorldAccessor world = player.Entity!.World;
        int radius = Math.Clamp(policy.StoreNearbyRadius, SeraphQoLServerConfig.MinStoreNearbyRadius, SeraphQoLServerConfig.MaxStoreNearbyRadius);
        List<BlockEntityContainer> containers = FindNearbyAllowedContainers(world, player, center, radius, policy, excludePos: null);

        int totalMoved = 0;
        int touchedContainers = 0;
        var remeshPositions = new List<BlockPos>();

        foreach (BlockEntityContainer container in containers)
        {
            int moved = TransferMatchingToContainer(player, container.Inventory, packet.StackPerishables, packet.IncludeHotbar);
            if (moved <= 0) continue;

            totalMoved += moved;
            touchedContainers++;
            container.MarkDirty(true);

            if (NeedsClientRemesh(container))
            {
                remeshPositions.Add(container.Pos.Copy());
            }
        }

        return (totalMoved, touchedContainers, remeshPositions);
    }

    private int TransferMatchingToContainer(
        IServerPlayer player,
        IInventory destInv,
        bool stackPerishables,
        bool includeHotbar)
    {
        var destCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ItemSlot slot in destInv)
        {
            if (slot.Empty || slot.Itemstack?.Collectible?.Code == null) continue;
            destCodes.Add(slot.Itemstack.Collectible.Code.ToString());
        }

        if (destCodes.Count == 0) return 0;

        List<ItemSlot> sourceSlots = CollectPlayerSourceSlots(player, includeHotbar);
        var snapshots = new List<SortSlotSnapshot>(sourceSlots.Count);
        for (int i = 0; i < sourceSlots.Count; i++)
        {
            ItemSlot slot = sourceSlots[i];
            string invClass = slot.Inventory?.ClassName ?? string.Empty;
            int invSlotId = slot.Inventory?.GetSlotId(slot) ?? i;
            bool isLocked = _lockedSlots.IsSlotLockedOnServer(player.PlayerUID, invClass, invSlotId);
            ItemStack? stack = slot.Itemstack;
            bool isFavorite = stack != null && FavoritesManager.IsFavorite(player, stack);

            snapshots.Add(new SortSlotSnapshot(
                SlotIndex: i,
                InventoryClass: invClass,
                IsSortable: SlotProtectionPolicy.CanSort(slot),
                IsLocked: isLocked,
                IsFavorite: isFavorite,
                ItemCode: stack?.Collectible?.Code?.ToString(),
                DisplayName: string.Empty,
                Category: string.Empty,
                StackSize: stack?.StackSize ?? 0,
                MaxStackSize: stack?.Collectible?.MaxStackSize ?? 64,
                IsPerishable: false,
                FreshnessRatio: 1.0,
                OriginalOrder: i));
        }

        IReadOnlyList<int> eligibleIndices = QuickStorePlanner.FindEligibleSourceIndices(
            snapshots,
            destCodes,
            includeHotbar);

        int movedStacks = 0;
        IWorldAccessor world = player.Entity.World;

        foreach (int idx in eligibleIndices)
        {
            ItemSlot source = sourceSlots[idx];
            if (source.Empty || source.Itemstack == null) continue;

            int beforeSize = source.StackSize;
            MoveIntoInventory(world, player, source, destInv, stackPerishables);
            if (source.Empty || source.StackSize < beforeSize)
            {
                movedStacks++;
            }
        }

        return movedStacks;
    }

    internal static void MoveIntoInventory(
        IWorldAccessor world,
        IServerPlayer player,
        ItemSlot source,
        IInventory destInv,
        bool stackPerishables)
    {
        if (source.Empty || source.Itemstack == null) return;

        // Primero intentar apilar en slots existentes con el mismo item
        foreach (ItemSlot dest in destInv)
        {
            if (source.Empty) return;
            if (dest.Empty || dest.Itemstack == null) continue;
            if (!SameCollectible(source.Itemstack, dest.Itemstack)) continue;
            if (!dest.CanHold(source)) continue;

            if (stackPerishables)
            {
                TryMergeForcePerishable(world, source, dest);
            }
            else
            {
                var op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, source.StackSize)
                {
                    ActingPlayer = player
                };
                source.TryPutInto(dest, ref op);
            }
        }

        // Luego intentar en slots vacios compatibles (dividiendo si el origen supera el limite o la capacidad del slot)
        while (!source.Empty && source.Itemstack != null)
        {
            int collectibleMax = Math.Max(1, source.Itemstack.Collectible?.MaxStackSize ?? 64);
            var moveOp = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, Math.Min(source.StackSize, collectibleMax))
            {
                ActingPlayer = player
            };
            WeightedSlot weighted = destInv.GetBestSuitedSlot(source, moveOp);
            if (weighted.slot == null || !weighted.slot.Empty || !weighted.slot.CanHold(source))
            {
                break;
            }

            int emptyCap = ComputeCappedTransferAmount(0, collectibleMax, weighted.slot.MaxSlotStackSize, source.StackSize);
            if (emptyCap <= 0) break;

            moveOp.RequestedQuantity = emptyCap;
            int before = source.StackSize;
            source.TryPutInto(weighted.slot, ref moveOp);
            if (source.StackSize >= before)
            {
                break;
            }
        }
    }

    private static List<ItemSlot> CollectTargetSlots(
        IServerPlayer player,
        IInventory targetInv,
        bool includeHotbarWithBackpack)
    {
        var slots = new List<ItemSlot>();
        if (SlotProtectionPolicy.IsPlayerBackpack(targetInv) && includeHotbarWithBackpack)
        {
            IInventory? hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
            if (hotbar != null)
            {
                foreach (ItemSlot s in hotbar)
                {
                    if (s is ItemSlotBackpack) continue;
                    slots.Add(s);
                }
            }
        }

        foreach (ItemSlot s in targetInv)
        {
            if (s is ItemSlotBackpack) continue;
            slots.Add(s);
        }

        return slots;
    }

    internal static List<ItemSlot> CollectPlayerSourceSlots(IServerPlayer player, bool includeHotbar)
    {
        var slots = new List<ItemSlot>();
        IInventory? backpack = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (backpack != null)
        {
            foreach (ItemSlot s in backpack)
            {
                if (s is ItemSlotBackpack) continue;
                slots.Add(s);
            }
        }

        if (includeHotbar)
        {
            IInventory? hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
            if (hotbar != null)
            {
                foreach (ItemSlot s in hotbar)
                {
                    if (s is ItemSlotBackpack) continue;
                    slots.Add(s);
                }
            }
        }

        return slots;
    }

    private static ItemStack?[] TakeSnapshot(IReadOnlyList<ItemSlot> slots)
    {
        var snapshot = new ItemStack?[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            snapshot[i] = slots[i].Itemstack?.Clone();
        }
        return snapshot;
    }

    private static void RollbackSnapshot(IReadOnlyList<ItemSlot> slots, ItemStack?[] snapshot, bool[] isProtected)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (isProtected[i]) continue;
            if (slots[i].Itemstack == null && snapshot[i] == null) continue;
            slots[i].Itemstack = snapshot[i]?.Clone();
            slots[i].MarkDirty();
        }
    }

    private static void ApplySortPlan(
        IWorldAccessor world,
        IReadOnlyList<ItemSlot> slots,
        ItemStack?[] snapshot,
        SortPlan plan,
        bool[] isProtected)
    {
        var builtStacks = new ItemStack?[slots.Count];

        foreach (SortSlotAssignment assignment in plan.Assignments)
        {
            if (isProtected[assignment.SlotIndex]) continue;
            if (assignment.ItemCode == null || assignment.StackSize <= 0 || assignment.Sources.Count == 0)
            {
                builtStacks[assignment.SlotIndex] = null;
                continue;
            }

            (int firstSrcIdx, int firstQty) = assignment.Sources[0];
            ItemStack? template = snapshot[firstSrcIdx]?.Clone();
            if (template == null)
            {
                builtStacks[assignment.SlotIndex] = null;
                continue;
            }

            template.StackSize = firstQty;

            for (int s = 1; s < assignment.Sources.Count; s++)
            {
                (int srcIdx, int qty) = assignment.Sources[s];
                ItemStack? part = snapshot[srcIdx];
                if (part == null || qty <= 0) continue;

                BlendPerishableState(world, template, part, qty);
                template.StackSize += qty;
            }

            builtStacks[assignment.SlotIndex] = template;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (isProtected[i]) continue;
            if (slots[i].Itemstack == null && builtStacks[i] == null) continue;
            slots[i].Itemstack = builtStacks[i];
            slots[i].MarkDirty();
        }
    }

    internal static int ComputeCappedTransferAmount(
        int destCurrentSize,
        int collectibleMaxStack,
        int slotMaxStack,
        int sourceSize)
    {
        if (sourceSize <= 0) return 0;
        int effectiveMax = Math.Max(1, Math.Min(collectibleMaxStack > 0 ? collectibleMaxStack : 64, slotMaxStack > 0 ? slotMaxStack : 64));
        int remaining = Math.Max(0, effectiveMax - Math.Max(0, destCurrentSize));
        return Math.Min(remaining, sourceSize);
    }

    private static void TryMergeForcePerishable(IWorldAccessor world, ItemSlot source, ItemSlot dest)
    {
        if (source.Itemstack == null || dest.Itemstack == null) return;
        if (!dest.CanHold(source)) return;

        int collectibleMax = dest.Itemstack.Collectible?.MaxStackSize ?? source.Itemstack.Collectible?.MaxStackSize ?? 64;
        int cappedSpace = ComputeCappedTransferAmount(dest.StackSize, collectibleMax, dest.MaxSlotStackSize, source.StackSize);
        int apiSpace = Math.Max(0, dest.GetRemainingSlotSpace(source.Itemstack));
        int moveQty = Math.Min(cappedSpace, apiSpace);
        if (moveQty <= 0) return;

        BlendPerishableState(world, dest.Itemstack, source.Itemstack, moveQty);

        dest.Itemstack.StackSize += moveQty;
        source.TakeOut(moveQty);
        dest.MarkDirty();
        source.MarkDirty();
    }

    private static void BlendPerishableState(IWorldAccessor world, ItemStack destStack, ItemStack addedStack, int addedQty)
    {
        var destSlot = new DummySlot(destStack);
        var addSlot = new DummySlot(addedStack);
        TransitionState? destState = destStack.Collectible?.UpdateAndGetTransitionState(world, destSlot, EnumTransitionType.Perish);
        TransitionState? addState = addedStack.Collectible?.UpdateAndGetTransitionState(world, addSlot, EnumTransitionType.Perish);
        if (destState == null || addState == null) return;

        int destQty = Math.Max(1, destStack.StackSize);
        float totalQty = destQty + addedQty;
        float blendedHours = ((destState.TransitionedHours * destQty) + (addState.TransitionedHours * addedQty)) / totalQty;
        destStack.Collectible?.SetTransitionState(destStack, EnumTransitionType.Perish, blendedHours);
    }

    private static double ExtractFreshnessRatio(IWorldAccessor world, ItemStack? stack, out bool isPerishable)
    {
        isPerishable = false;
        if (stack?.Collectible == null) return 1.0;

        TransitionState[]? states = stack.Collectible.UpdateAndGetTransitionStates(world, new DummySlot(stack));
        if (states == null) return 1.0;

        foreach (TransitionState state in states)
        {
            if (state.Props?.Type != EnumTransitionType.Perish) continue;
            isPerishable = true;
            double total = state.FreshHours + state.TransitionHours;
            if (total <= 0) return 1.0;
            return Math.Clamp(1.0 - (state.TransitionedHours / total), 0.0, 1.0);
        }

        return 1.0;
    }

    private static bool SameCollectible(ItemStack a, ItemStack b) =>
        a.Collectible?.Code != null
        && b.Collectible?.Code != null
        && a.Collectible.Code.Equals(b.Collectible.Code);

    public AlloyScanResultPacket HandleAlloyScan(
        IServerPlayer player,
        RequestAlloyScanPacket packet,
        EffectiveServerPolicy policy)
    {
        var result = new AlloyScanResultPacket
        {
            FirepitPos = packet.FirepitPos.Copy(),
            SearchRadius = Math.Clamp(packet.SearchRadius, SeraphQoLClientConfig.MinAlloySearchRadius, SeraphQoLClientConfig.MaxAlloySearchRadius)
        };

        IWorldAccessor? world = player.Entity?.World;
        if (world == null) return result;

        var countsByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<BlockEntityContainer> containers = FindNearbyAllowedContainers(
            world,
            player,
            packet.FirepitPos,
            result.SearchRadius,
            policy,
            excludePos: packet.FirepitPos);

        foreach (BlockEntityContainer container in containers)
        {
            foreach (ItemSlot slot in container.Inventory)
            {
                if (slot == null || slot.Empty || slot.Itemstack?.Collectible?.Code == null) continue;
                string code = slot.Itemstack.Collectible.Code.ToString();
                countsByCode[code] = countsByCode.GetValueOrDefault(code) + slot.StackSize;
            }
        }

        foreach ((string code, int count) in countsByCode)
        {
            result.ItemCodes.Add(code);
            result.ItemCounts.Add(count);
        }

        return result;
    }

    public (int MovedItems, int MovedFuel, List<BlockPos> RemeshPositions) HandleAlloyDeposit(
        IServerPlayer player,
        AlloyDepositPacket packet,
        EffectiveServerPolicy policy)
    {
        var remeshPositions = new List<BlockPos>();
        IWorldAccessor? world = player.Entity?.World;
        if (world == null || packet.FirepitPos == null) return (0, 0, remeshPositions);

        if (policy.RequireLandClaimPermission
            && !world.Claims.TryAccess(player, packet.FirepitPos, EnumBlockAccessFlags.Use))
        {
            return (0, 0, remeshPositions);
        }

        if (world.BlockAccessor.GetBlockEntity(packet.FirepitPos) is not BlockEntityFirepit firepit
            || firepit.Inventory is not InventorySmelting smelting)
        {
            return (0, 0, remeshPositions);
        }

        int radius = Math.Clamp(packet.SearchRadius, SeraphQoLClientConfig.MinAlloySearchRadius, SeraphQoLClientConfig.MaxAlloySearchRadius);
        List<BlockEntityContainer> nearbyContainers = packet.IncludeNearby
            ? FindNearbyAllowedContainers(world, player, packet.FirepitPos, radius, policy, excludePos: packet.FirepitPos)
            : new List<BlockEntityContainer>();

        var sourceSlots = new List<(ItemSlot Slot, BlockEntityContainer? Container)>();
        AppendUnlockedPlayerSlots(player, GlobalConstants.backpackInvClassName, sourceSlots);
        AppendUnlockedPlayerSlots(player, GlobalConstants.hotBarInvClassName, sourceSlots);
        foreach (BlockEntityContainer container in nearbyContainers)
        {
            foreach (ItemSlot slot in container.Inventory)
            {
                if (slot != null && !slot.Empty)
                {
                    sourceSlots.Add((slot, container));
                }
            }
        }

        var dirtyContainers = new HashSet<BlockEntityContainer>();
        int movedItems = 0;
        int movedFuel = 0;

        if (packet.DepositItems
            && smelting.HaveCookingContainer
            && smelting[1]?.Itemstack?.Block is BlockSmeltingContainer)
        {
            // 1) Si el crisol tiene pepitas sobrantes o de otro metal, devolver a cofre cercano (si activo) -> inventario -> soltar al suelo
            var maxByMetal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int m = 0; m < packet.MetalCodes.Count; m++)
            {
                string code = packet.MetalCodes[m];
                if (string.IsNullOrWhiteSpace(code)) continue;
                int maxItems = m < packet.MetalMaxItemsAllowed.Count
                    ? Math.Max(0, packet.MetalMaxItemsAllowed[m])
                    : (m < packet.MetalItemsNeeded.Count ? Math.Max(0, packet.MetalItemsNeeded[m]) : 0);
                maxByMetal[code] = maxItems;
            }

            ItemSlot[] cookingSlots = smelting.CookingSlots;
            var crucibleSnapshots = new List<CrucibleSlotItemSnapshot>(cookingSlots.Length);
            for (int s = 0; s < cookingSlots.Length; s++)
            {
                ItemSlot slot = cookingSlots[s];
                if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null) continue;
                CombustibleProperties? comb = slot.Itemstack.Collectible.GetCombustibleProperties(world, slot.Itemstack, null);
                string? smeltedCode = comb?.SmeltedStack?.ResolvedItemstack?.Collectible?.Code?.ToString();
                crucibleSnapshots.Add(new CrucibleSlotItemSnapshot(s, smeltedCode, slot.StackSize));
            }

            CrucibleCleanupPlan cleanup = CrucibleCleanupPlanner.PlanExtractions(crucibleSnapshots, maxByMetal);
            foreach ((int slotIdx, int extractCount) in cleanup.Extractions)
            {
                if (slotIdx < 0 || slotIdx >= cookingSlots.Length || extractCount <= 0) continue;
                ItemSlot cSlot = cookingSlots[slotIdx];
                if (cSlot == null || cSlot.Empty) continue;

                ItemStack? extracted = cSlot.TakeOut(Math.Min(extractCount, cSlot.StackSize));
                cSlot.MarkDirty();
                if (extracted != null && extracted.StackSize > 0)
                {
                    movedItems += extracted.StackSize;
                    StoreOrDropExtractedStack(world, player, extracted, nearbyContainers, packet.IncludeNearby, packet.FirepitPos, dirtyContainers);
                }
            }

            // 2) Depositar pepitas faltantes tras liberar los slots excedentes/incompatibles
            int pairCount = Math.Min(packet.MetalCodes.Count, packet.MetalItemsNeeded.Count);
            for (int m = 0; m < pairCount; m++)
            {
                string metalCode = packet.MetalCodes[m];
                if (string.IsNullOrWhiteSpace(metalCode)) continue;

                int remaining = packet.MetalItemsNeeded[m];
                if (m < packet.MetalMaxItemsAllowed.Count && packet.MetalMaxItemsAllowed[m] >= 0)
                {
                    int kept = cleanup.KeptItemsByMetal.GetValueOrDefault(metalCode);
                    remaining = Math.Max(0, packet.MetalMaxItemsAllowed[m] - kept);
                }
                if (remaining <= 0) continue;

                foreach ((ItemSlot sourceSlot, BlockEntityContainer? container) in sourceSlots)
                {
                    if (remaining <= 0) break;
                    if (sourceSlot.Empty || sourceSlot.Itemstack?.Collectible == null || !sourceSlot.CanTake()) continue;

                    CombustibleProperties? comb = sourceSlot.Itemstack.Collectible.GetCombustibleProperties(world, sourceSlot.Itemstack, null);
                    string? smeltedCode = comb?.SmeltedStack?.ResolvedItemstack?.Collectible?.Code?.ToString();
                    if (!string.Equals(smeltedCode, metalCode, StringComparison.OrdinalIgnoreCase)) continue;

                    int moved = MoveIntoTargetsServer(world, player, sourceSlot, smelting.CookingSlots, remaining);
                    if (moved > 0)
                    {
                        movedItems += moved;
                        remaining -= moved;
                        if (container != null) dirtyContainers.Add(container);
                    }
                }
            }
        }

        if (packet.DepositFuel && !string.IsNullOrWhiteSpace(packet.FuelItemCode) && packet.FuelPiecesNeeded > 0)
        {
            ItemSlot? fuelSlot = smelting[0];
            if (fuelSlot != null)
            {
                // Si hay otro tipo de combustible distinto al elegido, retirarlo usando la misma prioridad
                if (!fuelSlot.Empty
                    && !string.Equals(fuelSlot.Itemstack?.Collectible?.Code?.ToString(), packet.FuelItemCode, StringComparison.OrdinalIgnoreCase))
                {
                    ItemStack? oldFuel = fuelSlot.TakeOut(fuelSlot.StackSize);
                    fuelSlot.MarkDirty();
                    if (oldFuel != null && oldFuel.StackSize > 0)
                    {
                        movedFuel += oldFuel.StackSize;
                        StoreOrDropExtractedStack(world, player, oldFuel, nearbyContainers, packet.IncludeNearby, packet.FirepitPos, dirtyContainers);
                    }
                }

                int existingFuel = !fuelSlot.Empty
                    && string.Equals(fuelSlot.Itemstack?.Collectible?.Code?.ToString(), packet.FuelItemCode, StringComparison.OrdinalIgnoreCase)
                    ? fuelSlot.StackSize
                    : 0;
                int remainingFuel = Math.Max(0, packet.FuelPiecesNeeded - existingFuel);

                foreach ((ItemSlot sourceSlot, BlockEntityContainer? container) in sourceSlots)
                {
                    if (remainingFuel <= 0) break;
                    if (sourceSlot.Empty || sourceSlot.Itemstack?.Collectible?.Code == null || !sourceSlot.CanTake()) continue;
                    if (!string.Equals(sourceSlot.Itemstack.Collectible.Code.ToString(), packet.FuelItemCode, StringComparison.OrdinalIgnoreCase)) continue;

                    int moved = MoveIntoTargetsServer(world, player, sourceSlot, [fuelSlot], remainingFuel);
                    if (moved > 0)
                    {
                        movedFuel += moved;
                        remainingFuel -= moved;
                        if (container != null) dirtyContainers.Add(container);
                    }
                }
            }
        }

        if (movedItems > 0 || movedFuel > 0)
        {
            firepit.MarkDirty(true);
            player.InventoryManager.BroadcastHotbarSlot();
            foreach (BlockEntityContainer container in dirtyContainers)
            {
                container.MarkDirty(true);
                if (NeedsClientRemesh(container))
                {
                    remeshPositions.Add(container.Pos.Copy());
                }
            }
        }

        return (movedItems, movedFuel, remeshPositions);
    }

    public HandbookScanResultPacket HandleHandbookScan(
        IServerPlayer player,
        RequestHandbookScanPacket packet,
        EffectiveServerPolicy policy)
    {
        int radius = Math.Clamp(
            packet.SearchRadius,
            SeraphQoLClientConfig.MinHandbookSearchRadius,
            SeraphQoLClientConfig.MaxHandbookSearchRadius);

        var result = new HandbookScanResultPacket
        {
            SearchRadius = radius
        };

        IWorldAccessor? world = player.Entity?.World;
        BlockPos? center = player.Entity?.Pos?.AsBlockPos;
        if (world == null || center == null || !policy.EnableInventoryManagement || !policy.AllowHandbookNearbyCrafting)
        {
            return result;
        }

        var countsByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maxDurabilityByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        List<BlockEntityContainer> containers = FindNearbyAllowedContainers(
            world,
            player,
            center,
            radius,
            policy,
            excludePos: null);

        foreach (BlockEntityContainer container in containers)
        {
            foreach (ItemSlot slot in container.Inventory)
            {
                if (slot == null || slot.Empty || slot.Itemstack?.Collectible?.Code == null) continue;
                string code = slot.Itemstack.Collectible.Code.ToString();
                countsByCode[code] = countsByCode.GetValueOrDefault(code) + slot.StackSize;
                int remDur = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
                if (remDur > maxDurabilityByCode.GetValueOrDefault(code))
                {
                    maxDurabilityByCode[code] = remDur;
                }
            }
        }

        foreach ((string code, int count) in countsByCode)
        {
            result.ItemCodes.Add(code);
            result.ItemCounts.Add(count);
            result.MaxRemainingDurabilities.Add(maxDurabilityByCode.GetValueOrDefault(code));
        }

        return result;
    }

    public (bool Success, List<BlockPos> RemeshPositions) HandleHandbookFillGrid(
        IServerPlayer player,
        HandbookFillGridPacket packet,
        EffectiveServerPolicy policy)
    {
        var remeshPositions = new List<BlockPos>();
        IWorldAccessor? world = player.Entity?.World;
        BlockPos? center = player.Entity?.Pos?.AsBlockPos;
        if (world == null || center == null || !policy.EnableInventoryManagement || packet.Candidates.Count == 0)
        {
            return (false, remeshPositions);
        }

        IInventory? craftingInv = player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
        if (craftingInv == null) return (false, remeshPositions);

        ItemSlot[] gridSlots = craftingInv.Take(9).ToArray();
        if (gridSlots.Length < 9) return (false, remeshPositions);

        var sourceSlots = new List<(ItemSlot Slot, BlockEntityContainer? Container)>();
        AppendUnlockedPlayerSlots(player, GlobalConstants.backpackInvClassName, sourceSlots);
        AppendUnlockedPlayerSlots(player, GlobalConstants.hotBarInvClassName, sourceSlots);

        var seenInventories = new HashSet<string>(StringComparer.Ordinal);
        IInventory? bpInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        IInventory? hbInv = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        if (bpInv != null) seenInventories.Add(bpInv.InventoryID);
        if (hbInv != null) seenInventories.Add(hbInv.InventoryID);
        seenInventories.Add(craftingInv.InventoryID);

        foreach (InventoryGeneric opened in player.InventoryManager.OpenedInventories.OfType<InventoryGeneric>())
        {
            if (!seenInventories.Add(opened.InventoryID)) continue;
            foreach (ItemSlot slot in opened)
            {
                if (slot != null && !slot.Empty && slot.CanTake())
                {
                    sourceSlots.Add((slot, null));
                }
            }
        }

        List<BlockEntityContainer> nearbyContainers = new();
        if (packet.IncludeNearby && policy.AllowHandbookNearbyCrafting)
        {
            int radius = Math.Clamp(
                packet.SearchRadius,
                SeraphQoLClientConfig.MinHandbookSearchRadius,
                SeraphQoLClientConfig.MaxHandbookSearchRadius);
            nearbyContainers = FindNearbyAllowedContainers(world, player, center, radius, policy, excludePos: null);
            foreach (BlockEntityContainer container in nearbyContainers)
            {
                if (!seenInventories.Add(container.Inventory.InventoryID)) continue;
                foreach (ItemSlot slot in container.Inventory)
                {
                    if (slot != null && !slot.Empty && slot.CanTake())
                    {
                        sourceSlots.Add((slot, container));
                    }
                }
            }
        }

        var recipeSpecs = new List<CraftingRecipeSpec>();
        foreach (HandbookRecipeCandidatePacket cand in packet.Candidates)
        {
            GridRecipe? serverRecipe = FindAuthoritativeGridRecipe(world, cand);
            if (serverRecipe == null) continue;

            CraftingRecipeIngredient?[] resolved = serverRecipe.ResolvedIngredients ?? Array.Empty<CraftingRecipeIngredient?>();
            var ingredients = new CraftingIngredientSpec?[resolved.Length];
            for (int i = 0; i < resolved.Length; i++)
            {
                CraftingRecipeIngredient? ing = resolved[i];
                if (ing == null) continue;
                string? variantCode = i < cand.SlotIngredientCodes.Count && !string.IsNullOrWhiteSpace(cand.SlotIngredientCodes[i])
                    ? cand.SlotIngredientCodes[i]
                    : null;

                ingredients[i] = new CraftingIngredientSpec(
                    Key: variantCode ?? (ing.Code?.ToString() ?? string.Empty),
                    Quantity: Math.Max(1, ing.Quantity),
                    IsTool: ing.IsTool,
                    ToolDurabilityCost: ing.ToolDurabilityCost,
                    Matches: item => MatchesServerIngredient(ing, variantCode, item, world, sourceSlots, gridSlots));
            }

            recipeSpecs.Add(new CraftingRecipeSpec(
                Width: serverRecipe.Width,
                Height: serverRecipe.Height,
                Shapeless: serverRecipe.Shapeless,
                Ingredients: ingredients,
                MatchesCurrentGrid: serverRecipe.Matches(player, world, gridSlots, 3)));
        }

        if (recipeSpecs.Count == 0) return (false, remeshPositions);

        CraftingItemInfo?[] gridInfos = gridSlots.Select(s => ToCraftingItemInfo(s)).ToArray();
        CraftingItemInfo?[] sourceInfos = sourceSlots.Select(s => ToCraftingItemInfo(s.Slot)).ToArray();

        CraftingFillPlan plan = CraftingFillPlanner.BuildPlan(gridInfos, sourceInfos, recipeSpecs, packet.FillMax);
        if (!plan.Success) return (false, remeshPositions);

        var dirtyContainers = new HashSet<BlockEntityContainer>();
        foreach (int clearIdx in plan.SlotsToClear)
        {
            if (clearIdx < 0 || clearIdx >= gridSlots.Length) continue;
            ItemSlot gSlot = gridSlots[clearIdx];
            if (gSlot.Empty || gSlot.Itemstack == null) continue;

            ItemStack? taken = gSlot.TakeOut(gSlot.StackSize);
            gSlot.MarkDirty();
            if (taken != null && taken.StackSize > 0)
            {
                StoreOrDropExtractedStack(world, player, taken, nearbyContainers, packet.IncludeNearby, center, dirtyContainers);
            }
        }

        bool movedAny = false;
        foreach (CraftingTransferStep step in plan.Transfers)
        {
            if (step.SourceSlotIndex < 0 || step.SourceSlotIndex >= sourceSlots.Count) continue;
            if (step.TargetGridIndex < 0 || step.TargetGridIndex >= gridSlots.Length) continue;

            (ItemSlot src, BlockEntityContainer? srcContainer) = sourceSlots[step.SourceSlotIndex];
            ItemSlot dst = gridSlots[step.TargetGridIndex];
            if (src.Empty || !src.CanTake()) continue;

            int moved = MoveIntoTargetsServer(world, player, src, [dst], step.Quantity);
            if (moved > 0)
            {
                movedAny = true;
                if (srcContainer != null) dirtyContainers.Add(srcContainer);
            }
        }

        if (movedAny)
        {
            player.InventoryManager.BroadcastHotbarSlot();
            foreach (BlockEntityContainer container in dirtyContainers)
            {
                container.MarkDirty(true);
                if (NeedsClientRemesh(container))
                {
                    remeshPositions.Add(container.Pos.Copy());
                }
            }
        }

        return (movedAny, remeshPositions);
    }

    private static GridRecipe? FindAuthoritativeGridRecipe(IWorldAccessor world, HandbookRecipeCandidatePacket cand)
    {
        if (world.GridRecipes == null) return null;

        if (cand.RecipeId > 0)
        {
            foreach (GridRecipe recipe in world.GridRecipes)
            {
                if (recipe.Enabled && recipe.RecipeId == cand.RecipeId)
                {
                    return recipe;
                }
            }
        }

        foreach (GridRecipe recipe in world.GridRecipes)
        {
            if (!recipe.Enabled) continue;
            if (recipe.Width != cand.Width || recipe.Height != cand.Height || recipe.Shapeless != cand.Shapeless) continue;
            string? outCode = recipe.Output?.ResolvedItemStack?.Collectible?.Code?.ToString();
            if (!string.IsNullOrEmpty(cand.OutputCode)
                && string.Equals(outCode, cand.OutputCode, StringComparison.OrdinalIgnoreCase))
            {
                return recipe;
            }
        }

        return null;
    }

    private static CraftingItemInfo? ToCraftingItemInfo(ItemSlot? slot)
    {
        ItemStack? stack = slot?.Itemstack;
        if (stack?.Collectible == null || stack.StackSize <= 0) return null;
        string code = stack.Collectible.Code?.ToString() ?? string.Empty;
        string attrToken = stack.Attributes?.ToJsonToken() ?? string.Empty;
        return new CraftingItemInfo(
            ItemCode: code,
            StackSize: stack.StackSize,
            MaxStackSize: Math.Max(1, stack.Collectible.MaxStackSize),
            RemainingDurability: stack.Collectible.GetRemainingDurability(stack),
            MergeKey: $"{code}|{attrToken}");
    }

    private static bool MatchesServerIngredient(
        CraftingRecipeIngredient ingredient,
        string? variantCode,
        CraftingItemInfo info,
        IWorldAccessor world,
        IReadOnlyList<(ItemSlot Slot, BlockEntityContainer? Container)> sourceSlots,
        IReadOnlyList<ItemSlot> gridSlots)
    {
        if (!string.IsNullOrEmpty(variantCode)
            && ingredient.MatchingType != EnumRecipeMatchType.Exact
            && !string.Equals(info.ItemCode, variantCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ItemStack? stack = null;
        for (int i = 0; i < sourceSlots.Count && stack == null; i++)
        {
            ItemStack? s = sourceSlots[i].Slot.Itemstack;
            if (s?.Collectible == null) continue;
            string code = s.Collectible.Code?.ToString() ?? string.Empty;
            if (string.Equals($"{code}|{s.Attributes?.ToJsonToken() ?? string.Empty}", info.MergeKey, StringComparison.Ordinal))
            {
                stack = s;
            }
        }

        for (int i = 0; i < gridSlots.Count && stack == null; i++)
        {
            ItemStack? s = gridSlots[i]?.Itemstack;
            if (s?.Collectible == null) continue;
            string code = s.Collectible.Code?.ToString() ?? string.Empty;
            if (string.Equals($"{code}|{s.Attributes?.ToJsonToken() ?? string.Empty}", info.MergeKey, StringComparison.Ordinal))
            {
                stack = s;
            }
        }

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

    private void StoreOrDropExtractedStack(
        IWorldAccessor world,
        IServerPlayer player,
        ItemStack stack,
        IReadOnlyList<BlockEntityContainer> nearbyContainers,
        bool includeNearby,
        BlockPos firepitPos,
        HashSet<BlockEntityContainer> dirtyContainers)
    {
        if (stack.StackSize <= 0) return;

        // 1. Intentar guardar en cofre cercano si la opcion esta activa
        if (includeNearby)
        {
            foreach (BlockEntityContainer container in nearbyContainers)
            {
                if (stack.StackSize <= 0) return;
                int before = stack.StackSize;
                TryStoreStackIntoInventory(world, stack, container.Inventory, lockedInvClass: null, lockedPlayerUid: null);
                if (stack.StackSize < before)
                {
                    dirtyContainers.Add(container);
                }
            }
        }

        // 2. Intentar guardar en inventario del jugador (mochila primero, luego hotbar)
        foreach (string invClass in new[] { GlobalConstants.backpackInvClassName, GlobalConstants.hotBarInvClassName })
        {
            if (stack.StackSize <= 0) return;
            IInventory? inv = player.InventoryManager.GetOwnInventory(invClass);
            if (inv == null) continue;
            TryStoreStackIntoInventory(world, stack, inv, invClass, player.PlayerUID);
        }

        // 3. Si aun sobra, soltar al suelo junto a la fogata
        if (stack.StackSize > 0)
        {
            world.SpawnItemEntity(
                stack,
                new Vec3d(firepitPos.X + 0.5, firepitPos.Y + 0.9, firepitPos.Z + 0.5),
                new Vec3d(0, 0.08, 0));
        }
    }

    private void TryStoreStackIntoInventory(
        IWorldAccessor world,
        ItemStack stack,
        IInventory destInv,
        string? lockedInvClass,
        string? lockedPlayerUid)
    {
        if (stack.StackSize <= 0 || stack.Collectible == null) return;
        int collectibleMax = Math.Max(1, stack.Collectible.MaxStackSize);

        // Primero apilar sobre stacks existentes iguales
        for (int i = 0; i < destInv.Count && stack.StackSize > 0; i++)
        {
            if (lockedInvClass != null && lockedPlayerUid != null
                && _lockedSlots.IsSlotLockedOnServer(lockedPlayerUid, lockedInvClass, i))
            {
                continue;
            }

            ItemSlot? dest = destInv[i];
            if (dest == null || dest.Empty || dest.Itemstack == null) continue;
            if (dest is ItemSlotBackpack || dest is ItemSlotOffhand) continue;
            if (!SameCollectible(stack, dest.Itemstack)) continue;

            var probe = new DummySlot(stack);
            if (!dest.CanHold(probe)) continue;

            int cap = ComputeCappedTransferAmount(dest.StackSize, collectibleMax, dest.MaxSlotStackSize, stack.StackSize);
            int apiSpace = Math.Max(0, dest.GetRemainingSlotSpace(stack));
            int addQty = Math.Min(cap, apiSpace);
            if (addQty <= 0) continue;

            if (stack.Collectible.UpdateAndGetTransitionStates(world, probe) != null)
            {
                BlendPerishableState(world, dest.Itemstack, stack, addQty);
            }

            dest.Itemstack.StackSize += addQty;
            stack.StackSize -= addQty;
            dest.MarkDirty();
        }

        // Luego usar slots vacios compatibles
        for (int i = 0; i < destInv.Count && stack.StackSize > 0; i++)
        {
            if (lockedInvClass != null && lockedPlayerUid != null
                && _lockedSlots.IsSlotLockedOnServer(lockedPlayerUid, lockedInvClass, i))
            {
                continue;
            }

            ItemSlot? dest = destInv[i];
            if (dest == null || !dest.Empty) continue;
            if (dest is ItemSlotBackpack || dest is ItemSlotOffhand) continue;

            var probe = new DummySlot(stack);
            if (!dest.CanHold(probe)) continue;

            int cap = ComputeCappedTransferAmount(0, collectibleMax, dest.MaxSlotStackSize, stack.StackSize);
            int apiSpace = Math.Max(0, dest.GetRemainingSlotSpace(stack));
            int addQty = Math.Min(cap, apiSpace);
            if (addQty <= 0) continue;

            ItemStack placed = stack.Clone();
            placed.StackSize = addQty;
            dest.Itemstack = placed;
            stack.StackSize -= addQty;
            dest.MarkDirty();
        }
    }

    private void AppendUnlockedPlayerSlots(
        IServerPlayer player,
        string invClass,
        List<(ItemSlot Slot, BlockEntityContainer? Container)> dest)
    {
        IInventory? inv = player.InventoryManager.GetOwnInventory(invClass);
        if (inv == null) return;

        for (int i = 0; i < inv.Count; i++)
        {
            if (_lockedSlots.IsSlotLockedOnServer(player.PlayerUID, invClass, i)) continue;
            ItemSlot? slot = inv[i];
            if (slot == null || slot.Empty || slot is ItemSlotBackpack) continue;
            dest.Add((slot, null));
        }
    }

    private static int MoveIntoTargetsServer(
        IWorldAccessor world,
        IServerPlayer player,
        ItemSlot sourceSlot,
        IEnumerable<ItemSlot> targetSlots,
        int maxAmount)
    {
        if (maxAmount <= 0 || sourceSlot.Empty || sourceSlot.Itemstack == null) return 0;

        int movedTotal = 0;
        int remaining = maxAmount;

        foreach (ItemSlot target in targetSlots)
        {
            if (remaining <= 0 || sourceSlot.Empty || sourceSlot.Itemstack == null) break;
            if (target == null || !target.CanHold(sourceSlot)) continue;

            if (!target.Empty
                && !target.Itemstack!.Equals(world, sourceSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                continue;
            }

            int before = sourceSlot.StackSize;
            var op = new ItemStackMoveOperation(
                world,
                EnumMouseButton.Left,
                0,
                EnumMergePriority.AutoMerge,
                Math.Min(remaining, before))
            {
                ActingPlayer = player
            };

            sourceSlot.TryPutInto(target, ref op);
            int moved = Math.Max(0, before - sourceSlot.StackSize);
            if (moved > 0)
            {
                sourceSlot.MarkDirty();
                target.MarkDirty();
                movedTotal += moved;
                remaining -= moved;
            }
        }

        return movedTotal;
    }

    private static List<BlockEntityContainer> FindNearbyAllowedContainers(
        IWorldAccessor world,
        IServerPlayer player,
        BlockPos center,
        int radius,
        EffectiveServerPolicy policy,
        BlockPos? excludePos)
    {
        BlockPos min = center.AddCopy(-radius, -radius, -radius);
        BlockPos max = center.AddCopy(radius, radius, radius);
        var containers = new List<BlockEntityContainer>();

        world.BlockAccessor.WalkBlocks(min, max, (block, x, y, z) =>
        {
            string? code = block?.Code?.ToString();
            if (!ContainerWhitelistPolicy.IsAllowed(code, policy)) return;

            BlockPos pos = new(x, y, z, center.dimension);
            if (excludePos != null && pos.Equals(excludePos)) return;

            if (policy.RequireLandClaimPermission
                && !world.Claims.TryAccess(player, pos, EnumBlockAccessFlags.Use))
            {
                return;
            }

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityContainer container && container.Inventory != null)
            {
                containers.Add(container);
            }
        });

        containers.Sort((a, b) => a.Pos.DistanceSqTo(center.X, center.Y, center.Z)
            .CompareTo(b.Pos.DistanceSqTo(center.X, center.Y, center.Z)));
        return containers;
    }

    internal static bool NeedsClientRemesh(BlockEntityContainer container) =>
        AccessTools.Method(container.GetType(), "InitMesh") != null;
}
