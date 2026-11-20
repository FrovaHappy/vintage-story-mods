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
    [ProtoMember(17)] public bool EnableStackSizeModifier { get; set; }
    [ProtoMember(18)] public double GlobalStackMultiplier { get; set; } = 1.0;
    [ProtoMember(19)] public double BuildingBlocksStackMultiplier { get; set; } = 1.0;
    [ProtoMember(20)] public double ResourcesStackMultiplier { get; set; } = 1.0;
    [ProtoMember(21)] public double FoodStackMultiplier { get; set; } = 1.0;
    [ProtoMember(22)] public double MiscStackMultiplier { get; set; } = 1.0;
    [ProtoMember(23)] public bool AllowUnstackableContainersAndFurniture { get; set; }
    [ProtoMember(24)] public int EmptyContainersMaxStack { get; set; } = 1;
    [ProtoMember(25)] public int FurnitureUtilityMaxStack { get; set; } = 1;
    [ProtoMember(26)] public int ClothingMaxStack { get; set; } = 1;
    [ProtoMember(27)] public int MaxStackSizeCap { get; set; } = 512;
    [ProtoMember(28)] public List<string> StackSizeBlacklist { get; set; } = new();
    [ProtoMember(29)] public List<string> StackSizeOverridePatterns { get; set; } = new();
    [ProtoMember(30)] public List<int> StackSizeOverrideValues { get; set; } = new();

    public static ServerPolicySyncPacket FromPolicy(EffectiveServerPolicy policy)
    {
        var overridePatterns = new List<string>(policy.StackSizeOverrides.Count);
        var overrideValues = new List<int>(policy.StackSizeOverrides.Count);
        foreach ((string pattern, int value) in policy.StackSizeOverrides)
        {
            overridePatterns.Add(pattern);
            overrideValues.Add(value);
        }

        return new ServerPolicySyncPacket
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
            AllowProjectileTracker = policy.AllowProjectileTracker,
            EnableStackSizeModifier = policy.EnableStackSizeModifier,
            GlobalStackMultiplier = policy.GlobalStackMultiplier,
            BuildingBlocksStackMultiplier = policy.BuildingBlocksStackMultiplier,
            ResourcesStackMultiplier = policy.ResourcesStackMultiplier,
            FoodStackMultiplier = policy.FoodStackMultiplier,
            MiscStackMultiplier = policy.MiscStackMultiplier,
            AllowUnstackableContainersAndFurniture = policy.AllowUnstackableContainersAndFurniture,
            EmptyContainersMaxStack = policy.EmptyContainersMaxStack,
            FurnitureUtilityMaxStack = policy.FurnitureUtilityMaxStack,
            ClothingMaxStack = policy.ClothingMaxStack,
            MaxStackSizeCap = policy.MaxStackSizeCap,
            StackSizeBlacklist = policy.StackSizeBlacklist.ToList(),
            StackSizeOverridePatterns = overridePatterns,
            StackSizeOverrideValues = overrideValues
        };
    }

    public EffectiveServerPolicy ToDisplayPolicy()
    {
        var overrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int count = Math.Min(StackSizeOverridePatterns.Count, StackSizeOverrideValues.Count);
        for (int i = 0; i < count; i++)
        {
            string pattern = StackSizeOverridePatterns[i];
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                overrides[pattern] = StackSizeOverrideValues[i];
            }
        }

        return new EffectiveServerPolicy(
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
            AllowProjectileTracker,
            EnableStackSizeModifier,
            GlobalStackMultiplier,
            BuildingBlocksStackMultiplier,
            ResourcesStackMultiplier,
            FoodStackMultiplier,
            MiscStackMultiplier,
            AllowUnstackableContainersAndFurniture,
            EmptyContainersMaxStack,
            FurnitureUtilityMaxStack,
            ClothingMaxStack,
            MaxStackSizeCap,
            new HashSet<string>(StackSizeBlacklist, StringComparer.OrdinalIgnoreCase),
            overrides);
    }
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
