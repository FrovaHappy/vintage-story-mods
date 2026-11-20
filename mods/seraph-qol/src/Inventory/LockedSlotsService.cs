using SeraphQoL.Config;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SeraphQoL.Inventory;

/// <summary>
/// Gestiona slots bloqueados por indice en el cliente y replica el conjunto por jugador en el servidor.
/// </summary>
public sealed class LockedSlotsService
{
    private readonly Dictionary<string, Dictionary<string, HashSet<int>>> _serverLockedByPlayer = new(StringComparer.Ordinal);

    public bool IsLockModeActive { get; set; }

    public void UpdatePlayerLockedSlots(string playerUid, string inventoryClass, IEnumerable<int> slotIndices)
    {
        if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(inventoryClass)) return;

        if (!_serverLockedByPlayer.TryGetValue(playerUid, out var byInv))
        {
            byInv = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            _serverLockedByPlayer[playerUid] = byInv;
        }

        byInv[inventoryClass] = new HashSet<int>(slotIndices.Where(i => i >= 0));
    }

    public bool IsSlotLockedOnServer(string playerUid, string inventoryClass, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(inventoryClass)) return false;
        return _serverLockedByPlayer.TryGetValue(playerUid, out var byInv)
            && byInv.TryGetValue(inventoryClass, out var set)
            && set.Contains(slotIndex);
    }

    public void ClearPlayer(string playerUid) => _serverLockedByPlayer.Remove(playerUid);

    public static void SyncInventoryLocksToServer(ICoreClientAPI capi, SeraphQoLClientConfig config, string inventoryClass)
    {
        capi.Network.GetChannel("seraphqol")?.SendPacket(new UpdateLockedSlotsPacket
        {
            InventoryClass = inventoryClass,
            SlotIndices = config.GetLockedSlots(inventoryClass).ToList()
        });
    }

    public static void SyncAllLocksToServer(ICoreClientAPI capi, SeraphQoLClientConfig config)
    {
        SyncInventoryLocksToServer(capi, config, "backpack");
        SyncInventoryLocksToServer(capi, config, "hotbar");
    }
}
