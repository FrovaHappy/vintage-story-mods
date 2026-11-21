using Newtonsoft.Json.Linq;
using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SeraphQoL.Backpacks;

/// <summary>
/// Adaptador de infraestructura que captura los quantitySlots base, aplica capacidades configuradas
/// sobre CollectibleObject.Attributes y sincroniza slots de mochilas existentes soltando excedentes al suelo.
/// </summary>
public sealed class BackpackAssetsService
{
    private readonly BackpackCapacityCalculator _calculator = new();
    private readonly Dictionary<string, int> _baseQuantitySlots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ItemStack>> _pendingDropsByPlayerUid = new(StringComparer.Ordinal);

    public void ApplyToCollectibles(ICoreAPI api, EffectiveServerPolicy policy)
    {
        if (api.World?.Collectibles == null) return;

        int updatedCount = 0;
        foreach (CollectibleObject collectible in api.World.Collectibles)
        {
            string? code = collectible?.Code?.ToString();
            if (collectible == null || collectible.Id == 0 || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (collectible.Attributes?.Token is not JObject rootObj
                || rootObj["backpack"] is not JObject backpackObj
                || backpackObj["quantitySlots"] is not JValue quantityVal
                || (quantityVal.Type != JTokenType.Integer && quantityVal.Type != JTokenType.Float))
            {
                continue;
            }

            int currentSlots = Math.Max(1, quantityVal.Value<int>());
            if (!_baseQuantitySlots.TryGetValue(code, out int baseSlots))
            {
                baseSlots = currentSlots;
                _baseQuantitySlots[code] = baseSlots;
            }

            BackpackCollectibleDescriptor descriptor = BuildDescriptor(collectible, code, baseSlots, backpackObj);
            int targetSlots = _calculator.CalculateTargetSlots(descriptor, policy);

            if (currentSlots != targetSlots)
            {
                backpackObj["quantitySlots"] = targetSlots;
                updatedCount++;
            }
        }

        if (policy.EnableBackpackExpansion || updatedCount > 0)
        {
            api.Logger.Notification(
                "[SeraphQoL] Backpack capacity policy applied ({0}): {1} bag(s) updated.",
                policy.EnableBackpackExpansion ? "enabled" : "vanilla",
                updatedCount);
        }
    }

    public static void ReloadOnlinePlayerBackpacks(ICoreAPI api)
    {
        if (api is ICoreServerAPI sapi)
        {
            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                ReloadPlayerBackpackInventory(sapi.World, player, markBagSlotsDirty: true);
            }
        }
        else if (api is ICoreClientAPI capi && capi.World?.Player != null)
        {
            ReloadPlayerBackpackInventory(capi.World, capi.World.Player, markBagSlotsDirty: false);
        }
    }

    public void HandleHeldBagSlotsSync(
        ItemStack? bagStack,
        int targetSlots,
        InventoryBase? parentInv,
        IWorldAccessor? world)
    {
        if (bagStack == null || world == null || targetSlots <= 0)
        {
            return;
        }

        List<ItemStack> overflow = ExtractOverflowAndSyncHeldBag(bagStack, targetSlots, world);
        if (overflow.Count == 0 || world.Side != EnumAppSide.Server)
        {
            return;
        }

        if (parentInv is InventoryBasePlayer playerInv && playerInv.Player != null)
        {
            if (playerInv.Player.Entity != null)
            {
                DropStacksAndNotifyPlayer(world, playerInv.Player, overflow);
            }
            else
            {
                QueuePendingDrops(playerInv.Player.PlayerUID, overflow);
            }
            return;
        }

        if (parentInv?.Pos != null)
        {
            Vec3d dropPos = parentInv.Pos.ToVec3d().Add(0.5, 0.8, 0.5);
            SpawnDroppedStacks(world, dropPos, overflow);
        }
    }

    public void FlushPendingDrops(ICoreServerAPI sapi, IServerPlayer player)
    {
        if (player.Entity == null || string.IsNullOrEmpty(player.PlayerUID))
        {
            return;
        }

        if (!_pendingDropsByPlayerUid.Remove(player.PlayerUID, out List<ItemStack>? pending) || pending.Count == 0)
        {
            return;
        }

        DropStacksAndNotifyPlayer(sapi.World, player, pending);
    }

    public void ClearPendingDrops(string? playerUid)
    {
        if (!string.IsNullOrEmpty(playerUid))
        {
            _pendingDropsByPlayerUid.Remove(playerUid);
        }
    }

    public static List<ItemStack> ExtractOverflowAndSyncHeldBag(
        ItemStack? bagStack,
        int targetSlots,
        IWorldAccessor? world)
    {
        if (bagStack?.Attributes == null || targetSlots <= 0)
        {
            return [];
        }

        ITreeAttribute? backpackTree = bagStack.Attributes.GetTreeAttribute("backpack");
        if (backpackTree == null)
        {
            return [];
        }

        ITreeAttribute? slotsTree = backpackTree.GetTreeAttribute("slots");
        if (slotsTree == null)
        {
            slotsTree = new TreeAttribute();
            backpackTree["slots"] = slotsTree;
        }

        return ExtractOverflowAndSyncSlots(slotsTree, targetSlots, world);
    }

    public static List<ItemStack> ExtractOverflowAndSyncSlots(
        ITreeAttribute? slotsTree,
        int targetSlots,
        IWorldAccessor? world = null)
    {
        if (slotsTree == null || targetSlots <= 0)
        {
            return [];
        }

        for (int i = 0; i < targetSlots; i++)
        {
            string key = $"slot-{i}";
            if (!slotsTree.HasAttribute(key))
            {
                slotsTree[key] = new ItemstackAttribute(null);
            }
        }

        var overflow = new List<ItemStack>();
        List<string>? keysToRemove = null;

        foreach (KeyValuePair<string, IAttribute> entry in slotsTree)
        {
            if (!BackpackCapacityCalculator.TryParseSlotKeyIndex(entry.Key, out int slotIndex)
                || slotIndex < targetSlots)
            {
                continue;
            }

            if (entry.Value?.GetValue() is ItemStack stack && stack.StackSize > 0)
            {
                ItemStack copy = stack.Clone();
                if (world != null)
                {
                    copy.ResolveBlockOrItem(world);
                }
                overflow.Add(copy);
            }

            keysToRemove ??= new List<string>();
            keysToRemove.Add(entry.Key);
        }

        if (keysToRemove != null)
        {
            foreach (string key in keysToRemove)
            {
                slotsTree.RemoveAttribute(key);
            }
        }

        return overflow;
    }

    private void QueuePendingDrops(string? playerUid, List<ItemStack> stacks)
    {
        if (string.IsNullOrEmpty(playerUid) || stacks.Count == 0)
        {
            return;
        }

        if (!_pendingDropsByPlayerUid.TryGetValue(playerUid, out List<ItemStack>? list))
        {
            list = new List<ItemStack>(stacks.Count);
            _pendingDropsByPlayerUid[playerUid] = list;
        }

        list.AddRange(stacks);
    }

    private static void DropStacksAndNotifyPlayer(IWorldAccessor world, IPlayer player, IReadOnlyList<ItemStack> stacks)
    {
        if (player.Entity == null || stacks.Count == 0)
        {
            return;
        }

        Vec3d dropPos = player.Entity.Pos.XYZ.Add(0, 0.5, 0);
        SpawnDroppedStacks(world, dropPos, stacks);

        if (player is IServerPlayer serverPlayer)
        {
            serverPlayer.SendMessage(
                GlobalConstants.GeneralChatGroup,
                SeraphQoLLang.Get("warning-backpack-shrink-dropped", stacks.Count),
                EnumChatType.Notification);
        }
    }

    private static void SpawnDroppedStacks(IWorldAccessor world, Vec3d dropPos, IReadOnlyList<ItemStack> stacks)
    {
        foreach (ItemStack stack in stacks)
        {
            if (stack.StackSize > 0)
            {
                world.SpawnItemEntity(stack, dropPos);
            }
        }
    }

    private static void ReloadPlayerBackpackInventory(IWorldAccessor world, IPlayer player, bool markBagSlotsDirty)
    {
        if (player.InventoryManager?.GetOwnInventory(GlobalConstants.backpackInvClassName) is not InventoryBase bpInv)
        {
            return;
        }

        bpInv.AfterBlocksLoaded(world);

        if (markBagSlotsDirty)
        {
            for (int i = 0; i < 4 && i < bpInv.Count; i++)
            {
                if (!bpInv[i].Empty)
                {
                    bpInv.MarkSlotDirty(i);
                }
            }
        }
    }

    private static BackpackCollectibleDescriptor BuildDescriptor(
        CollectibleObject collectible,
        string code,
        int baseSlots,
        JObject backpackObj)
    {
        string className = collectible.Class ?? collectible.GetType().Name;

        int? rawFlags = backpackObj["storageFlags"]?.Value<int?>();
        bool hasRestrictedFlags = rawFlags.HasValue
            && (EnumItemStorageFlags)rawFlags.Value != EnumItemStorageFlags.Backpack
            && (EnumItemStorageFlags)rawFlags.Value != EnumItemStorageFlags.General;

        bool hasStorageTags = backpackObj["storageTags"] is JArray arr && arr.Count > 0;

        return new BackpackCollectibleDescriptor(
            Code: code,
            ClassName: className,
            BaseQuantitySlots: baseSlots,
            HasRestrictedStorageFlags: hasRestrictedFlags,
            HasStorageTags: hasStorageTags);
    }
}
