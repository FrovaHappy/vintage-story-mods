using ProtoBuf;
using SeraphQoL.Config;
using Vintagestory.API.MathTools;

namespace SeraphQoL.Network;

[ProtoContract]
public sealed class ServerPolicySyncPacket
{
    [ProtoMember(1)] public bool EnableFaunaRebalance { get; set; }
    [ProtoMember(2)] public double SmallPreyHealthMultiplier { get; set; }
    [ProtoMember(3)] public double MediumLargePreyHealthMultiplier { get; set; }
    [ProtoMember(4)] public double PredatorHealthMultiplier { get; set; }
    [ProtoMember(5)] public double MeatDropMultiplier { get; set; }
    [ProtoMember(6)] public double FatDropMultiplier { get; set; }
    [ProtoMember(7)] public double HideDropMultiplier { get; set; }
    [ProtoMember(8)] public double ByproductDropMultiplier { get; set; }
    [ProtoMember(9)] public bool ScaleDomesticatedHealth { get; set; }
    [ProtoMember(10)] public bool ScaleDomesticatedDrops { get; set; }
    [ProtoMember(11)] public bool EnableInventoryManagement { get; set; }
    [ProtoMember(12)] public bool EnableStoreNearby { get; set; }
    [ProtoMember(13)] public int StoreNearbyRadius { get; set; }
    [ProtoMember(14)] public bool RequireLandClaimPermission { get; set; }
    [ProtoMember(15)] public bool AllowHotbarRefill { get; set; }
    [ProtoMember(16)] public bool AllowProjectileTracker { get; set; }

    public static ServerPolicySyncPacket FromPolicy(EffectiveServerPolicy policy) => new()
    {
        EnableFaunaRebalance = policy.EnableFaunaRebalance,
        SmallPreyHealthMultiplier = policy.SmallPreyHealthMultiplier,
        MediumLargePreyHealthMultiplier = policy.MediumLargePreyHealthMultiplier,
        PredatorHealthMultiplier = policy.PredatorHealthMultiplier,
        MeatDropMultiplier = policy.MeatDropMultiplier,
        FatDropMultiplier = policy.FatDropMultiplier,
        HideDropMultiplier = policy.HideDropMultiplier,
        ByproductDropMultiplier = policy.ByproductDropMultiplier,
        ScaleDomesticatedHealth = policy.ScaleDomesticatedHealth,
        ScaleDomesticatedDrops = policy.ScaleDomesticatedDrops,
        EnableInventoryManagement = policy.EnableInventoryManagement,
        EnableStoreNearby = policy.EnableStoreNearby,
        StoreNearbyRadius = policy.StoreNearbyRadius,
        RequireLandClaimPermission = policy.RequireLandClaimPermission,
        AllowHotbarRefill = policy.AllowHotbarRefill,
        AllowProjectileTracker = policy.AllowProjectileTracker
    };

    public EffectiveServerPolicy ToDisplayPolicy() => new(
        EnableFaunaRebalance,
        SmallPreyHealthMultiplier,
        MediumLargePreyHealthMultiplier,
        PredatorHealthMultiplier,
        MeatDropMultiplier,
        FatDropMultiplier,
        HideDropMultiplier,
        ByproductDropMultiplier,
        ScaleDomesticatedHealth,
        ScaleDomesticatedDrops,
        IncludeAquaticFauna: false,
        FaunaWhitelist: new HashSet<string>(),
        FaunaBlacklist: new HashSet<string>(),
        EnableInventoryManagement,
        EnableStoreNearby,
        StoreNearbyRadius,
        RequireLandClaimPermission,
        AdditionalContainerWhitelist: new HashSet<string>(),
        ContainerBlacklist: new HashSet<string>(),
        AllowHotbarRefill,
        AllowProjectileTracker);
}

[ProtoContract]
public sealed class SortInventoryPacket
{
    [ProtoMember(1)] public string InventoryId { get; set; } = string.Empty;
    [ProtoMember(2)] public int SortOrder { get; set; }
    [ProtoMember(3)] public bool SortHotbarWithBackpack { get; set; }
    [ProtoMember(4)] public bool StackPerishables { get; set; }
    [ProtoMember(5)] public bool SkipFavoritesWhenSorting { get; set; }
}

[ProtoContract]
public sealed class QuickStorePacket
{
    [ProtoMember(1)] public string InventoryId { get; set; } = string.Empty;
    [ProtoMember(2)] public bool StackPerishables { get; set; }
    [ProtoMember(3)] public bool IncludeHotbar { get; set; }
}

[ProtoContract]
public sealed class StoreNearbyPacket
{
    [ProtoMember(1)] public bool StackPerishables { get; set; }
    [ProtoMember(2)] public bool IncludeHotbar { get; set; }
}

[ProtoContract]
public sealed class UpdateFavoritePacket
{
    [ProtoMember(1)] public string ItemCode { get; set; } = string.Empty;
    [ProtoMember(2)] public bool IsFavorite { get; set; }
}

[ProtoContract]
public sealed class UpdateLockedSlotsPacket
{
    [ProtoMember(1)] public string InventoryClass { get; set; } = string.Empty;
    [ProtoMember(2)] public List<int> SlotIndices { get; set; } = new();
}

[ProtoContract]
public sealed class RemeshContainersPacket
{
    [ProtoMember(1)] public List<BlockPos> Positions { get; set; } = new();
}

[ProtoContract]
public sealed class HotbarRefillPacket
{
    [ProtoMember(1)] public int HotbarSlotIndex { get; set; }
    [ProtoMember(2)] public int BackpackSlotIndex { get; set; }
    [ProtoMember(3)] public string ExpectedItemCode { get; set; } = string.Empty;
    [ProtoMember(4)] public bool IsPreventiveToolSwap { get; set; }
}

[ProtoContract]
public sealed class SwapSlotsPacket
{
    [ProtoMember(1)] public string SourceInventoryClass { get; set; } = string.Empty;
    [ProtoMember(2)] public int SourceSlotIndex { get; set; }
    [ProtoMember(3)] public string TargetInventoryClass { get; set; } = string.Empty;
    [ProtoMember(4)] public int TargetSlotIndex { get; set; }
}

[ProtoContract]
public sealed class ProjectileWaypointPacket
{
    [ProtoMember(1)] public bool Remove { get; set; }
    [ProtoMember(2)] public string WaypointKey { get; set; } = string.Empty;
    [ProtoMember(3)] public double X { get; set; }
    [ProtoMember(4)] public double Y { get; set; }
    [ProtoMember(5)] public double Z { get; set; }
    [ProtoMember(6)] public string Title { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class RequestAlloyScanPacket
{
    [ProtoMember(1)] public BlockPos FirepitPos { get; set; } = new(0);
    [ProtoMember(2)] public int SearchRadius { get; set; } = 6;
}

[ProtoContract]
public sealed class AlloyScanResultPacket
{
    [ProtoMember(1)] public BlockPos FirepitPos { get; set; } = new(0);
    [ProtoMember(2)] public int SearchRadius { get; set; }
    [ProtoMember(3)] public List<string> ItemCodes { get; set; } = new();
    [ProtoMember(4)] public List<int> ItemCounts { get; set; } = new();
}

[ProtoContract]
public sealed class AlloyDepositPacket
{
    [ProtoMember(1)] public BlockPos FirepitPos { get; set; } = new(0);
    [ProtoMember(2)] public bool DepositItems { get; set; }
    [ProtoMember(3)] public bool DepositFuel { get; set; }
    [ProtoMember(4)] public bool IncludeNearby { get; set; }
    [ProtoMember(5)] public int SearchRadius { get; set; } = 6;
    [ProtoMember(6)] public List<string> MetalCodes { get; set; } = new();
    [ProtoMember(7)] public List<int> MetalItemsNeeded { get; set; } = new();
    [ProtoMember(8)] public string FuelItemCode { get; set; } = string.Empty;
    [ProtoMember(9)] public int FuelPiecesNeeded { get; set; }
    [ProtoMember(10)] public List<int> MetalMaxItemsAllowed { get; set; } = new();
}

[ProtoContract]
public sealed class AlloyDepositResultPacket
{
    [ProtoMember(1)] public BlockPos FirepitPos { get; set; } = new(0);
    [ProtoMember(2)] public int MovedItems { get; set; }
    [ProtoMember(3)] public int MovedFuel { get; set; }
}
