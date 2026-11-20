using SeraphQoL.Inventory;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace SeraphQoL.Client;

/// <summary>
/// Servicio cliente para equipar o restaurar rapidamente la fuente de luz mas brillante o el mejor escudo en offhand/hotbar.
/// </summary>
public sealed class EquipLightClientService
{
    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;

    private EquipLightSwapState? _lastLightOffhandSwap;
    private EquipLightSwapState? _lastLightHotbarSwap;
    private EquipLightSwapState? _lastShieldOffhandSwap;
    private EquipLightSwapState? _lastShieldHotbarSwap;

    public EquipLightClientService(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
    }

    public bool TryToggleLight(bool toOffhand)
    {
        return TryToggleEquipment(
            toOffhand,
            ref toOffhand ? ref _lastLightOffhandSwap : ref _lastLightHotbarSwap,
            GatherLightCandidates,
            (candidates, targetClass, targetIndex, currentCode, previous) =>
                (EquipLightPlanner.PlanEquip(candidates, targetClass, targetIndex, currentCode, previous),
                 candidates.Select(c => (c.InventoryClass, c.SlotIndex, c.ItemCode))));
    }

    public bool TryToggleShield(bool toOffhand)
    {
        return TryToggleEquipment(
            toOffhand,
            ref toOffhand ? ref _lastShieldOffhandSwap : ref _lastShieldHotbarSwap,
            GatherShieldCandidates,
            (candidates, targetClass, targetIndex, currentCode, previous) =>
                (EquipShieldPlanner.PlanEquip(candidates, targetClass, targetIndex, currentCode, previous),
                 candidates.Select(c => (c.InventoryClass, c.SlotIndex, c.ItemCode))));
    }

    private bool TryToggleEquipment<TCandidate>(
        bool toOffhand,
        ref EquipLightSwapState? previousSwap,
        System.Func<IInventory, IInventory, List<TCandidate>> gatherCandidates,
        System.Func<IReadOnlyList<TCandidate>, string, int, string?, EquipLightSwapState?, (EquipLightDecision? Decision, IEnumerable<(string InventoryClass, int SlotIndex, string ItemCode)> Keys)> plan)
    {
        IPlayer? player = _capi.World?.Player;
        if (player?.InventoryManager == null) return false;

        IInventory? hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        IInventory? backpack = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (hotbar == null || backpack == null) return false;

        ItemSlot? targetSlot = toOffhand
            ? player.InventoryManager.OffhandHotbarSlot
            : player.InventoryManager.ActiveHotbarSlot;
        if (targetSlot?.Inventory == null) return false;

        string targetClass = targetSlot.Inventory.ClassName ?? GlobalConstants.hotBarInvClassName;
        int targetIndex = targetSlot.Inventory.GetSlotId(targetSlot);
        if (targetIndex < 0) return false;

        if (_modSystem.ClientConfig.IsSlotLocked(targetClass, targetIndex)) return false;

        List<TCandidate> candidates = gatherCandidates(backpack, hotbar);
        string? currentTargetCode = targetSlot.Itemstack?.Collectible?.Code?.ToString();

        (EquipLightDecision? decision, IEnumerable<(string InventoryClass, int SlotIndex, string ItemCode)> keys) =
            plan(candidates, targetClass, targetIndex, currentTargetCode, previousSwap);

        if (decision == null) return false;

        if (decision.IsRestore)
        {
            previousSwap = null;
        }
        else
        {
            (string InventoryClass, int SlotIndex, string ItemCode) picked = keys.FirstOrDefault(c =>
                string.Equals(c.InventoryClass, decision.SourceInventoryClass, StringComparison.OrdinalIgnoreCase)
                && c.SlotIndex == decision.SourceSlotIndex);

            if (!string.IsNullOrWhiteSpace(picked.ItemCode))
            {
                previousSwap = new EquipLightSwapState(
                    decision.SourceInventoryClass,
                    decision.SourceSlotIndex,
                    decision.TargetInventoryClass,
                    decision.TargetSlotIndex,
                    picked.ItemCode);
            }
        }

        _modSystem.SendSwapSlotsPacket(new SwapSlotsPacket
        {
            SourceInventoryClass = decision.SourceInventoryClass,
            SourceSlotIndex = decision.SourceSlotIndex,
            TargetInventoryClass = decision.TargetInventoryClass,
            TargetSlotIndex = decision.TargetSlotIndex
        });
        _capi.Gui.PlaySound("tick");
        return true;
    }

    private List<LightSourceCandidate> GatherLightCandidates(IInventory backpack, IInventory hotbar)
    {
        var list = new List<LightSourceCandidate>();
        CollectLightFromInventory(backpack, GlobalConstants.backpackInvClassName, list);
        CollectLightFromInventory(hotbar, GlobalConstants.hotBarInvClassName, list);
        return list;
    }

    private void CollectLightFromInventory(IInventory inv, string invClass, ICollection<LightSourceCandidate> output)
    {
        if (_capi.World?.BlockAccessor == null) return;

        for (int i = 0; i < inv.Count; i++)
        {
            ItemSlot? slot = inv[i];
            if (slot == null || slot is ItemSlotBackpack || slot.Empty || slot.Itemstack?.Collectible?.Code == null) continue;

            byte[]? hsv = slot.Itemstack.Collectible.GetLightHsv(_capi.World.BlockAccessor, null, slot.Itemstack);
            if (hsv == null || hsv.Length < 3 || hsv[2] <= 0) continue;

            output.Add(new LightSourceCandidate(
                invClass,
                i,
                slot.Itemstack.Collectible.Code.ToString(),
                hsv[2],
                hsv[0],
                hsv[1],
                _modSystem.ClientConfig.IsSlotLocked(invClass, i)));
        }
    }

    private List<ShieldCandidate> GatherShieldCandidates(IInventory backpack, IInventory hotbar)
    {
        var list = new List<ShieldCandidate>();
        CollectShieldsFromInventory(backpack, GlobalConstants.backpackInvClassName, list);
        CollectShieldsFromInventory(hotbar, GlobalConstants.hotBarInvClassName, list);
        return list;
    }

    private void CollectShieldsFromInventory(IInventory inv, string invClass, ICollection<ShieldCandidate> output)
    {
        for (int i = 0; i < inv.Count; i++)
        {
            ItemSlot? slot = inv[i];
            if (slot == null || slot is ItemSlotBackpack || slot.Empty || slot.Itemstack?.Collectible?.Code == null) continue;

            ItemStack stack = slot.Itemstack;
            CollectibleObject col = stack.Collectible;
            JsonObject? shieldAttr = col.Attributes?["shield"];
            if (col is not ItemShield && (shieldAttr == null || !shieldAttr.Exists)) continue;

            float dmgAbs = shieldAttr?["damageAbsorption"].AsFloat(0f) ?? 0f;
            float projAbs = shieldAttr?["projectileDamageAbsorption"].AsFloat(0f) ?? 0f;
            int maxDur = col.GetMaxDurability(stack);
            int remDur = maxDur > 0 ? col.GetRemainingDurability(stack) : 0;

            output.Add(new ShieldCandidate(
                invClass,
                i,
                col.Code.ToString(),
                dmgAbs,
                projAbs,
                remDur,
                maxDur,
                _modSystem.ClientConfig.IsSlotLocked(invClass, i)));
        }
    }
}
