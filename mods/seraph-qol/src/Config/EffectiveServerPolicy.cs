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
    bool AllowProjectileTracker = true)
{
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
            server.AllowProjectileTracker);
    }
}
