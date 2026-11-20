namespace SeraphQoL.Config;

/// <summary>
/// Politica efectiva del servidor construida a partir de SeraphQoLServerConfig y sincronizada al cliente.
/// </summary>
public sealed record EffectiveServerPolicy(
    bool EnableFaunaRebalance,
    double SmallPreyHealthMultiplier,
    double MediumLargePreyHealthMultiplier,
    double PredatorHealthMultiplier,
    double MeatDropMultiplier,
    double FatDropMultiplier,
    double HideDropMultiplier,
    double ByproductDropMultiplier,
    bool ScaleDomesticatedHealth,
    bool ScaleDomesticatedDrops,
    bool IncludeAquaticFauna,
    IReadOnlySet<string> FaunaWhitelist,
    IReadOnlySet<string> FaunaBlacklist,
    bool EnableInventoryManagement,
    bool EnableStoreNearby,
    int StoreNearbyRadius,
    bool RequireLandClaimPermission,
    IReadOnlySet<string> AdditionalContainerWhitelist,
    IReadOnlySet<string> ContainerBlacklist,
    bool AllowHotbarRefill = true,
    bool AllowProjectileTracker = true,
    bool EnableStackSizeModifier = false,
    double GlobalStackMultiplier = 1.0,
    double BuildingBlocksStackMultiplier = 1.0,
    double ResourcesStackMultiplier = 1.0,
    double FoodStackMultiplier = 1.0,
    double MiscStackMultiplier = 1.0,
    bool AllowUnstackableContainersAndFurniture = false,
    int EmptyContainersMaxStack = 1,
    int FurnitureUtilityMaxStack = 1,
    int ClothingMaxStack = 1,
    int MaxStackSizeCap = 512,
    IReadOnlySet<string>? StackSizeBlacklist = null,
    IReadOnlyDictionary<string, int>? StackSizeOverrides = null)
{
    public IReadOnlySet<string> StackSizeBlacklist { get; init; } =
        StackSizeBlacklist ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> StackSizeOverrides { get; init; } =
        StackSizeOverrides ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public static EffectiveServerPolicy FromServerConfig(SeraphQoLServerConfig server)
    {
        server.Normalize();
        return new EffectiveServerPolicy(
            server.EnableFaunaRebalance,
            server.SmallPreyHealthMultiplier,
            server.MediumLargePreyHealthMultiplier,
            server.PredatorHealthMultiplier,
            server.MeatDropMultiplier,
            server.FatDropMultiplier,
            server.HideDropMultiplier,
            server.ByproductDropMultiplier,
            server.ScaleDomesticatedHealth,
            server.ScaleDomesticatedDrops,
            server.IncludeAquaticFauna,
            new HashSet<string>(server.FaunaWhitelist, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(server.FaunaBlacklist, StringComparer.OrdinalIgnoreCase),
            server.EnableInventoryManagement,
            server.EnableStoreNearby,
            server.StoreNearbyRadius,
            server.RequireLandClaimPermission,
            new HashSet<string>(server.AdditionalContainerWhitelist, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(server.ContainerBlacklist, StringComparer.OrdinalIgnoreCase),
            server.AllowHotbarRefill,
            server.AllowProjectileTracker,
            server.EnableStackSizeModifier,
            server.GlobalStackMultiplier,
            server.BuildingBlocksStackMultiplier,
            server.ResourcesStackMultiplier,
            server.FoodStackMultiplier,
            server.MiscStackMultiplier,
            server.AllowUnstackableContainersAndFurniture,
            server.EmptyContainersMaxStack,
            server.FurnitureUtilityMaxStack,
            server.ClothingMaxStack,
            server.MaxStackSizeCap,
            new HashSet<string>(server.StackSizeBlacklist, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(server.StackSizeOverrides, StringComparer.OrdinalIgnoreCase));
    }
}
