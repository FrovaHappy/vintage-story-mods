using SeraphQoL.Config;
using SeraphQoL.Inventory;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace SeraphQoL.Client;

/// <summary>
/// Monitorea cambios en la hotbar del cliente para planificar Hotbar Refill tras el retardo configurado.
/// </summary>
public sealed class HotbarRefillMonitor
{
    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private readonly Dictionary<int, RefillSlotSnapshot> _previousHotbar = new();
    private readonly List<PendingRefill> _pending = new();

    private long _lastScanMs;

    private sealed record PendingRefill(long ExecuteAtMs, RefillPlan Plan);

    public HotbarRefillMonitor(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
    }

    public void Reset()
    {
        _previousHotbar.Clear();
        _pending.Clear();
    }

    public void Tick()
    {
        if (!_modSystem.ClientConfig.EnableHotbarRefill || !_modSystem.EffectivePolicy.AllowHotbarRefill)
        {
            _previousHotbar.Clear();
            _pending.Clear();
            return;
        }

        IPlayer? player = _capi.World?.Player;
        if (player?.InventoryManager == null) return;

        long nowMs = _capi.World!.ElapsedMilliseconds;
        FlushDueRefills(nowMs);

        if (nowMs - _lastScanMs < 50) return;
        _lastScanMs = nowMs;

        // No disparar refill mientras el jugador tiene un dialogo de inventario abierto moviendo items manualmente
        if (_capi.Gui.OpenedGuis.Any(g => g is GuiDialogInventory or GuiDialogBlockEntityInventory && g.IsOpened()))
        {
            CaptureSnapshotOnly(player);
            return;
        }

        IInventory? hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        IInventory? backpack = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (hotbar == null || backpack == null) return;

        List<RefillSlotSnapshot> backpackSnapshots = BuildBackpackSnapshots(backpack);
        RefillRuleOptions options = BuildOptions();

        for (int i = 0; i < hotbar.Count; i++)
        {
            ItemSlot? slot = hotbar[i];
            if (slot == null || slot is ItemSlotOffhand or ItemSlotBackpack) continue;

            RefillSlotSnapshot current = CaptureSlot(GlobalConstants.hotBarInvClassName, i, slot);
            if (_previousHotbar.TryGetValue(i, out RefillSlotSnapshot? prev))
            {
                RefillPlan? plan = HotbarRefillPlanner.FindRefillPlan(prev, current, backpackSnapshots, options);
                if (plan != null && !_pending.Any(p => p.Plan.TargetHotbarSlotIndex == i))
                {
                    long executeAt = nowMs + Math.Max(0, _modSystem.ClientConfig.RefillDelayMs);
                    _pending.Add(new PendingRefill(executeAt, plan));
                }
            }

            _previousHotbar[i] = current;
        }
    }

    private void FlushDueRefills(long nowMs)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            PendingRefill item = _pending[i];
            if (nowMs < item.ExecuteAtMs) continue;

            _pending.RemoveAt(i);
            _modSystem.SendHotbarRefillPacket(new HotbarRefillPacket
            {
                HotbarSlotIndex = item.Plan.TargetHotbarSlotIndex,
                BackpackSlotIndex = item.Plan.SourceBackpackSlotIndex,
                ExpectedItemCode = item.Plan.ItemCode,
                IsPreventiveToolSwap = item.Plan.IsPreventiveToolSwap
            });
        }
    }

    private void CaptureSnapshotOnly(IPlayer player)
    {
        IInventory? hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        if (hotbar == null) return;

        for (int i = 0; i < hotbar.Count; i++)
        {
            ItemSlot? slot = hotbar[i];
            if (slot == null || slot is ItemSlotOffhand or ItemSlotBackpack) continue;
            _previousHotbar[i] = CaptureSlot(GlobalConstants.hotBarInvClassName, i, slot);
        }
    }

    private List<RefillSlotSnapshot> BuildBackpackSnapshots(IInventory backpack)
    {
        var list = new List<RefillSlotSnapshot>(backpack.Count);
        for (int i = 0; i < backpack.Count; i++)
        {
            ItemSlot? slot = backpack[i];
            if (slot == null || slot is ItemSlotBackpack || !SlotProtectionPolicy.CanSort(slot)) continue;
            list.Add(CaptureSlot(GlobalConstants.backpackInvClassName, i, slot));
        }
        return list;
    }

    private RefillSlotSnapshot CaptureSlot(string invClass, int index, ItemSlot slot)
    {
        ItemStack? stack = slot.Itemstack;
        bool locked = _modSystem.ClientConfig.IsSlotLocked(invClass, index);
        if (stack?.Collectible?.Code == null)
        {
            return new RefillSlotSnapshot(invClass, index, null, RefillItemKind.None, 0, 0, 0, locked, false);
        }

        int maxDur = stack.Collectible.GetMaxDurability(stack);
        int remDur = maxDur > 0 ? stack.Collectible.GetRemainingDurability(stack) : 0;
        RefillItemKind kind = ClassifyItem(stack, maxDur);

        return new RefillSlotSnapshot(
            invClass,
            index,
            stack.Collectible.Code.ToString(),
            kind,
            stack.StackSize,
            remDur,
            maxDur,
            locked,
            false);
    }

    private static RefillItemKind ClassifyItem(ItemStack stack, int maxDurability)
    {
        if (maxDurability > 0) return RefillItemKind.Tool;
        if (stack.Block != null) return RefillItemKind.Block;
        if (stack.Collectible?.NutritionProps != null) return RefillItemKind.Consumable;
        return RefillItemKind.Other;
    }

    private RefillRuleOptions BuildOptions()
    {
        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        return new RefillRuleOptions(
            cfg.RefillConsumables,
            cfg.RefillBlocks,
            cfg.RefillToolsBeforeBreak,
            cfg.ToolDurabilityThreshold,
            cfg.PreferSmallestStackForRefill,
            cfg.RefillBlacklist);
    }
}
