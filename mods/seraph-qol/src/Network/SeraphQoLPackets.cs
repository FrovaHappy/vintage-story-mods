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
    [ProtoMember(31)] public bool EnableBackpackExpansion { get; set; }
    [ProtoMember(32)] public double GlobalBackpackMultiplier { get; set; } = 1.0;
    [ProtoMember(33)] public double StandardBackpacksMultiplier { get; set; } = 1.0;
    [ProtoMember(34)] public double SpecializedBagsMultiplier { get; set; } = 1.0;
    [ProtoMember(35)] public double SaddlebagsMultiplier { get; set; } = 1.0;
    [ProtoMember(36)] public double ModdedBackpacksMultiplier { get; set; } = 1.0;
    [ProtoMember(37)] public int MaxBackpackSlotsCap { get; set; } = 64;
    [ProtoMember(38)] public List<string> BackpackBlacklist { get; set; } = new();
    [ProtoMember(39)] public List<string> BackpackOverridePatterns { get; set; } = new();
    [ProtoMember(40)] public List<int> BackpackOverrideValues { get; set; } = new();
    [ProtoMember(41)] public bool AllowHandbookNearbyCrafting { get; set; } = true;

    public static ServerPolicySyncPacket FromPolicy(EffectiveServerPolicy policy)
    {
        (List<string> overridePatterns, List<int> overrideValues) = SplitOverrideDictionary(policy.StackSizeOverrides);
        (List<string> backpackPatterns, List<int> backpackValues) = SplitOverrideDictionary(policy.BackpackOverrides);

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
            StackSizeOverrideValues = overrideValues,
            EnableBackpackExpansion = policy.EnableBackpackExpansion,
            GlobalBackpackMultiplier = policy.GlobalBackpackMultiplier,
            StandardBackpacksMultiplier = policy.StandardBackpacksMultiplier,
            SpecializedBagsMultiplier = policy.SpecializedBagsMultiplier,
            SaddlebagsMultiplier = policy.SaddlebagsMultiplier,
            ModdedBackpacksMultiplier = policy.ModdedBackpacksMultiplier,
            MaxBackpackSlotsCap = policy.MaxBackpackSlotsCap,
            BackpackBlacklist = policy.BackpackBlacklist.ToList(),
            BackpackOverridePatterns = backpackPatterns,
            BackpackOverrideValues = backpackValues,
            AllowHandbookNearbyCrafting = policy.AllowHandbookNearbyCrafting
        };
    }

    public EffectiveServerPolicy ToDisplayPolicy()
    {
        Dictionary<string, int> overrides = MergeOverrideDictionary(StackSizeOverridePatterns, StackSizeOverrideValues);
        Dictionary<string, int> backpackOverrides = MergeOverrideDictionary(BackpackOverridePatterns, BackpackOverrideValues);

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
            overrides,
            EnableBackpackExpansion,
            GlobalBackpackMultiplier,
            StandardBackpacksMultiplier,
            SpecializedBagsMultiplier,
            SaddlebagsMultiplier,
            ModdedBackpacksMultiplier,
            MaxBackpackSlotsCap,
            new HashSet<string>(BackpackBlacklist, StringComparer.OrdinalIgnoreCase),
            backpackOverrides,
            AllowHandbookNearbyCrafting);
    }

    private static (List<string> Patterns, List<int> Values) SplitOverrideDictionary(IReadOnlyDictionary<string, int> source)
    {
        var patterns = new List<string>(source.Count);
        var values = new List<int>(source.Count);
        foreach ((string pattern, int value) in source)
        {
            patterns.Add(pattern);
            values.Add(value);
        }
        return (patterns, values);
    }

    private static Dictionary<string, int> MergeOverrideDictionary(IReadOnlyList<string> patterns, IReadOnlyList<int> values)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int count = Math.Min(patterns.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            string pattern = patterns[i];
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                result[pattern] = values[i];
            }
        }
        return result;
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

[ProtoContract]
public sealed class RequestHandbookScanPacket
{
    [ProtoMember(1)] public int SearchRadius { get; set; } = 6;
}

[ProtoContract]
public sealed class HandbookScanResultPacket
{
    [ProtoMember(1)] public int SearchRadius { get; set; }
    [ProtoMember(2)] public List<string> ItemCodes { get; set; } = new();
    [ProtoMember(3)] public List<int> ItemCounts { get; set; } = new();
    [ProtoMember(4)] public List<int> MaxRemainingDurabilities { get; set; } = new();
}

[ProtoContract]
public sealed class HandbookRecipeCandidatePacket
{
    [ProtoMember(1)] public int RecipeId { get; set; }
    [ProtoMember(2)] public string OutputCode { get; set; } = string.Empty;
    [ProtoMember(3)] public int Width { get; set; }
    [ProtoMember(4)] public int Height { get; set; }
    [ProtoMember(5)] public bool Shapeless { get; set; }
    [ProtoMember(6)] public List<string> SlotIngredientCodes { get; set; } = new();
}

[ProtoContract]
public sealed class HandbookFillGridPacket
{
    [ProtoMember(1)] public bool FillMax { get; set; }
    [ProtoMember(2)] public bool IncludeNearby { get; set; }
    [ProtoMember(3)] public int SearchRadius { get; set; } = 6;
    [ProtoMember(4)] public List<HandbookRecipeCandidatePacket> Candidates { get; set; } = new();
}

[ProtoContract]
public sealed class HandbookFillResultPacket
{
    [ProtoMember(1)] public bool Success { get; set; }
}
