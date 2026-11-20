using Vintagestory.API.Datastructures;

namespace SeraphQoL.Config;

/// <summary>
/// Presets (prefabs) predefinidos para balanceo rapido de Fauna QoL en ConfigLib.
/// </summary>
public enum FaunaPreset
{
    BalancedTactical,
    Vanilla,
    RealisticHunt,
    AbundantResources,
    HardcorePredators,
    Custom
}

/// <summary>
/// Configuracion autoritativa del servidor para Fauna QoL y politicas de inventario/contenedores.
/// </summary>
public sealed class SeraphQoLServerConfig
{
    public const double MinMultiplier = 0.1;
    public const double MaxMultiplier = 20.0;
    public const int MinStoreNearbyRadius = 1;
    public const int MaxStoreNearbyRadius = 16;

    // Fauna QoL (Balanceado tactico por defecto)
    public FaunaPreset FaunaPreset { get; set; } = FaunaPreset.BalancedTactical;
    public bool EnableFaunaRebalance { get; set; } = true;
    public double SmallPreyHealthMultiplier { get; set; } = 1.5;
    public double MediumLargePreyHealthMultiplier { get; set; } = 2.0;
    public double PredatorHealthMultiplier { get; set; } = 2.5;

    public double MeatDropMultiplier { get; set; } = 1.75;
    public double FatDropMultiplier { get; set; } = 1.75;
    public double HideDropMultiplier { get; set; } = 1.5;
    public double ByproductDropMultiplier { get; set; } = 1.5;

    public bool ScaleDomesticatedHealth { get; set; } = false;
    public bool ScaleDomesticatedDrops { get; set; } = true;
    public bool IncludeAquaticFauna { get; set; } = false;

    public HashSet<string> FaunaWhitelist { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FaunaBlacklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Inventario y Contenedores
    public bool EnableInventoryManagement { get; set; } = true;
    public bool EnableStoreNearby { get; set; } = true;
    public int StoreNearbyRadius { get; set; } = 6;
    public bool RequireLandClaimPermission { get; set; } = true;

    // Fase 2: Refill y Exploracion
    public bool AllowHotbarRefill { get; set; } = true;
    public bool AllowProjectileTracker { get; set; } = true;

    public HashSet<string> AdditionalContainerWhitelist { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ContainerBlacklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ApplyPreset(FaunaPreset preset)
    {
        FaunaPreset = preset;
        switch (preset)
        {
            case FaunaPreset.BalancedTactical:
                EnableFaunaRebalance = true;
                SmallPreyHealthMultiplier = 1.5;
                MediumLargePreyHealthMultiplier = 2.0;
                PredatorHealthMultiplier = 2.5;
                MeatDropMultiplier = 1.75;
                FatDropMultiplier = 1.75;
                HideDropMultiplier = 1.5;
                ByproductDropMultiplier = 1.5;
                ScaleDomesticatedHealth = false;
                ScaleDomesticatedDrops = true;
                IncludeAquaticFauna = false;
                return true;

            case FaunaPreset.Vanilla:
                EnableFaunaRebalance = false;
                SmallPreyHealthMultiplier = 1.0;
                MediumLargePreyHealthMultiplier = 1.0;
                PredatorHealthMultiplier = 1.0;
                MeatDropMultiplier = 1.0;
                FatDropMultiplier = 1.0;
                HideDropMultiplier = 1.0;
                ByproductDropMultiplier = 1.0;
                ScaleDomesticatedHealth = false;
                ScaleDomesticatedDrops = false;
                IncludeAquaticFauna = false;
                return true;

            case FaunaPreset.RealisticHunt:
                EnableFaunaRebalance = true;
                SmallPreyHealthMultiplier = 1.25;
                MediumLargePreyHealthMultiplier = 2.5;
                PredatorHealthMultiplier = 3.5;
                MeatDropMultiplier = 2.0;
                FatDropMultiplier = 2.0;
                HideDropMultiplier = 1.75;
                ByproductDropMultiplier = 1.5;
                ScaleDomesticatedHealth = false;
                ScaleDomesticatedDrops = true;
                IncludeAquaticFauna = false;
                return true;

            case FaunaPreset.AbundantResources:
                EnableFaunaRebalance = true;
                SmallPreyHealthMultiplier = 1.25;
                MediumLargePreyHealthMultiplier = 1.5;
                PredatorHealthMultiplier = 1.75;
                MeatDropMultiplier = 2.5;
                FatDropMultiplier = 2.5;
                HideDropMultiplier = 2.0;
                ByproductDropMultiplier = 2.0;
                ScaleDomesticatedHealth = false;
                ScaleDomesticatedDrops = true;
                IncludeAquaticFauna = false;
                return true;

            case FaunaPreset.HardcorePredators:
                EnableFaunaRebalance = true;
                SmallPreyHealthMultiplier = 1.5;
                MediumLargePreyHealthMultiplier = 2.5;
                PredatorHealthMultiplier = 4.0;
                MeatDropMultiplier = 2.0;
                FatDropMultiplier = 2.0;
                HideDropMultiplier = 2.0;
                ByproductDropMultiplier = 1.75;
                ScaleDomesticatedHealth = false;
                ScaleDomesticatedDrops = true;
                IncludeAquaticFauna = false;
                return true;

            case FaunaPreset.Custom:
                return true;

            default:
                return false;
        }
    }

    public FaunaPreset DetectMatchingPreset()
    {
        foreach (FaunaPreset candidate in new[]
        {
            FaunaPreset.BalancedTactical,
            FaunaPreset.Vanilla,
            FaunaPreset.RealisticHunt,
            FaunaPreset.AbundantResources,
            FaunaPreset.HardcorePredators
        })
        {
            var probe = new SeraphQoLServerConfig();
            probe.ApplyPreset(candidate);
            if (MatchesFaunaValues(probe))
            {
                return candidate;
            }
        }

        return FaunaPreset.Custom;
    }

    public void SyncConfigSettingsFrom(SeraphQoLServerConfig source)
    {
        FaunaPreset = source.FaunaPreset;
        EnableFaunaRebalance = source.EnableFaunaRebalance;
        SmallPreyHealthMultiplier = source.SmallPreyHealthMultiplier;
        MediumLargePreyHealthMultiplier = source.MediumLargePreyHealthMultiplier;
        PredatorHealthMultiplier = source.PredatorHealthMultiplier;
        MeatDropMultiplier = source.MeatDropMultiplier;
        FatDropMultiplier = source.FatDropMultiplier;
        HideDropMultiplier = source.HideDropMultiplier;
        ByproductDropMultiplier = source.ByproductDropMultiplier;
        ScaleDomesticatedHealth = source.ScaleDomesticatedHealth;
        ScaleDomesticatedDrops = source.ScaleDomesticatedDrops;
        IncludeAquaticFauna = source.IncludeAquaticFauna;
        EnableInventoryManagement = source.EnableInventoryManagement;
        EnableStoreNearby = source.EnableStoreNearby;
        StoreNearbyRadius = source.StoreNearbyRadius;
        RequireLandClaimPermission = source.RequireLandClaimPermission;
        AllowHotbarRefill = source.AllowHotbarRefill;
        AllowProjectileTracker = source.AllowProjectileTracker;
        Normalize();
    }

    public void Normalize()
    {
        SmallPreyHealthMultiplier = ClampMultiplier(SmallPreyHealthMultiplier);
        MediumLargePreyHealthMultiplier = ClampMultiplier(MediumLargePreyHealthMultiplier);
        PredatorHealthMultiplier = ClampMultiplier(PredatorHealthMultiplier);
        MeatDropMultiplier = ClampMultiplier(MeatDropMultiplier);
        FatDropMultiplier = ClampMultiplier(FatDropMultiplier);
        HideDropMultiplier = ClampMultiplier(HideDropMultiplier);
        ByproductDropMultiplier = ClampMultiplier(ByproductDropMultiplier);
        StoreNearbyRadius = Math.Clamp(StoreNearbyRadius, MinStoreNearbyRadius, MaxStoreNearbyRadius);
    }

    public bool ApplySetting(string? code, IAttribute? valueAttr)
    {
        if (string.IsNullOrWhiteSpace(code) || valueAttr == null) return false;
        object? raw = valueAttr.GetValue();
        if (raw == null) return false;

        if (string.Equals(code, nameof(FaunaPreset), StringComparison.Ordinal))
        {
            bool parsed = ConfigValueParser.TryEnum<FaunaPreset>(raw, v =>
            {
                if (v == FaunaPreset.Custom) FaunaPreset = FaunaPreset.Custom;
                else ApplyPreset(v);
            });
            if (parsed) Normalize();
            return parsed;
        }

        bool isFaunaProperty = false;
        bool applied = code switch
        {
            nameof(EnableFaunaRebalance) => MarkFauna(ConfigValueParser.TryBool(raw, v => EnableFaunaRebalance = v), ref isFaunaProperty),
            nameof(SmallPreyHealthMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => SmallPreyHealthMultiplier = v), ref isFaunaProperty),
            nameof(MediumLargePreyHealthMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => MediumLargePreyHealthMultiplier = v), ref isFaunaProperty),
            nameof(PredatorHealthMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => PredatorHealthMultiplier = v), ref isFaunaProperty),
            nameof(MeatDropMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => MeatDropMultiplier = v), ref isFaunaProperty),
            nameof(FatDropMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => FatDropMultiplier = v), ref isFaunaProperty),
            nameof(HideDropMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => HideDropMultiplier = v), ref isFaunaProperty),
            nameof(ByproductDropMultiplier) => MarkFauna(ConfigValueParser.TryDouble(raw, v => ByproductDropMultiplier = v), ref isFaunaProperty),
            nameof(ScaleDomesticatedHealth) => MarkFauna(ConfigValueParser.TryBool(raw, v => ScaleDomesticatedHealth = v), ref isFaunaProperty),
            nameof(ScaleDomesticatedDrops) => MarkFauna(ConfigValueParser.TryBool(raw, v => ScaleDomesticatedDrops = v), ref isFaunaProperty),
            nameof(IncludeAquaticFauna) => MarkFauna(ConfigValueParser.TryBool(raw, v => IncludeAquaticFauna = v), ref isFaunaProperty),
            nameof(EnableInventoryManagement) => ConfigValueParser.TryBool(raw, v => EnableInventoryManagement = v),
            nameof(EnableStoreNearby) => ConfigValueParser.TryBool(raw, v => EnableStoreNearby = v),
            nameof(StoreNearbyRadius) => ConfigValueParser.TryInt(raw, v => StoreNearbyRadius = v),
            nameof(RequireLandClaimPermission) => ConfigValueParser.TryBool(raw, v => RequireLandClaimPermission = v),
            nameof(AllowHotbarRefill) => ConfigValueParser.TryBool(raw, v => AllowHotbarRefill = v),
            nameof(AllowProjectileTracker) => ConfigValueParser.TryBool(raw, v => AllowProjectileTracker = v),
            _ => false
        };

        if (applied)
        {
            Normalize();
            if (isFaunaProperty)
            {
                FaunaPreset = DetectMatchingPreset();
            }
        }

        return applied;
    }

    private bool MatchesFaunaValues(SeraphQoLServerConfig other) =>
        EnableFaunaRebalance == other.EnableFaunaRebalance
        && ApproxEqual(SmallPreyHealthMultiplier, other.SmallPreyHealthMultiplier)
        && ApproxEqual(MediumLargePreyHealthMultiplier, other.MediumLargePreyHealthMultiplier)
        && ApproxEqual(PredatorHealthMultiplier, other.PredatorHealthMultiplier)
        && ApproxEqual(MeatDropMultiplier, other.MeatDropMultiplier)
        && ApproxEqual(FatDropMultiplier, other.FatDropMultiplier)
        && ApproxEqual(HideDropMultiplier, other.HideDropMultiplier)
        && ApproxEqual(ByproductDropMultiplier, other.ByproductDropMultiplier)
        && ScaleDomesticatedHealth == other.ScaleDomesticatedHealth
        && ScaleDomesticatedDrops == other.ScaleDomesticatedDrops
        && IncludeAquaticFauna == other.IncludeAquaticFauna;

    private static bool ApproxEqual(double a, double b) => Math.Abs(a - b) < 0.001;

    private static bool MarkFauna(bool result, ref bool isFaunaProperty)
    {
        isFaunaProperty = result;
        return result;
    }

    private static double ClampMultiplier(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? 1.0
            : Math.Clamp(value, MinMultiplier, MaxMultiplier);
}
