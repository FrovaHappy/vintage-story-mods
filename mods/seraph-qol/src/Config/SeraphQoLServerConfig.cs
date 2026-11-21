using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
/// Presets (prefabs) predefinidos para modificacion de tamano de stack en ConfigLib.
/// </summary>
public enum StackSizePreset
{
    Vanilla,
    P1nksBalanced,
    Double2x,
    Builder4x,
    Custom
}

/// <summary>
/// Presets (prefabs) predefinidos para expansion de capacidad de mochilas en ConfigLib.
/// </summary>
public enum BackpackPreset
{
    Vanilla,
    Balanced15x,
    BiggerBackpacks2x,
    Explorer3x,
    Custom
}

/// <summary>
/// Configuracion autoritativa del servidor para Fauna QoL, tamanos de stack y politicas de inventario/contenedores.
/// </summary>
public sealed class SeraphQoLServerConfig
{
    public const double MinMultiplier = 0.1;
    public const double MaxMultiplier = 20.0;
    public const double MinStackMultiplier = 0.25;
    public const double MaxStackMultiplier = 16.0;
    public const int MinSingleItemMaxStack = 1;
    public const int MaxSingleItemMaxStack = 256;
    public const int MinStackSizeCapValue = 16;
    public const int MaxStackSizeCapValue = 20000;
    public const double MinBackpackMultiplier = 0.5;
    public const double MaxBackpackMultiplier = 8.0;
    public const int MinBackpackSlotsCapValue = 8;
    public const int MaxBackpackSlotsCapValue = 256;
    public const int MinStoreNearbyRadius = 1;
    public const int MaxStoreNearbyRadius = 16;

    // Fauna QoL (Balanceado tactico por defecto)
    [JsonConverter(typeof(StringEnumConverter))]
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

    // Tamanos de Stack (Server Authoritative)
    [JsonConverter(typeof(StringEnumConverter))]
    public StackSizePreset StackSizePreset { get; set; } = StackSizePreset.Vanilla;
    public bool EnableStackSizeModifier { get; set; } = false;
    public double GlobalStackMultiplier { get; set; } = 1.0;
    public double BuildingBlocksStackMultiplier { get; set; } = 1.0;
    public double ResourcesStackMultiplier { get; set; } = 1.0;
    public double FoodStackMultiplier { get; set; } = 1.0;
    public double MiscStackMultiplier { get; set; } = 1.0;
    public bool AllowUnstackableContainersAndFurniture { get; set; } = false;
    public int EmptyContainersMaxStack { get; set; } = 1;
    public int FurnitureUtilityMaxStack { get; set; } = 1;
    public int ClothingMaxStack { get; set; } = 1;
    public int MaxStackSizeCap { get; set; } = 512;

    public HashSet<string> StackSizeBlacklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> StackSizeOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Capacidad de Mochilas y Alforjas (Server Authoritative)
    [JsonConverter(typeof(StringEnumConverter))]
    public BackpackPreset BackpackPreset { get; set; } = BackpackPreset.Vanilla;
    public bool EnableBackpackExpansion { get; set; } = false;
    public double GlobalBackpackMultiplier { get; set; } = 1.0;
    public double StandardBackpacksMultiplier { get; set; } = 1.0;
    public double SpecializedBagsMultiplier { get; set; } = 1.0;
    public double SaddlebagsMultiplier { get; set; } = 1.0;
    public double ModdedBackpacksMultiplier { get; set; } = 1.0;
    public int MaxBackpackSlotsCap { get; set; } = 64;

    public HashSet<string> BackpackBlacklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> BackpackOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

    public bool ApplyStackPreset(StackSizePreset preset)
    {
        StackSizePreset = preset;
        switch (preset)
        {
            case StackSizePreset.Vanilla:
                EnableStackSizeModifier = false;
                GlobalStackMultiplier = 1.0;
                BuildingBlocksStackMultiplier = 1.0;
                ResourcesStackMultiplier = 1.0;
                FoodStackMultiplier = 1.0;
                MiscStackMultiplier = 1.0;
                AllowUnstackableContainersAndFurniture = false;
                EmptyContainersMaxStack = 1;
                FurnitureUtilityMaxStack = 1;
                ClothingMaxStack = 1;
                MaxStackSizeCap = 512;
                return true;

            case StackSizePreset.P1nksBalanced:
                EnableStackSizeModifier = true;
                GlobalStackMultiplier = 1.0;
                BuildingBlocksStackMultiplier = 2.0;
                ResourcesStackMultiplier = 2.0;
                FoodStackMultiplier = 2.0;
                MiscStackMultiplier = 2.0;
                AllowUnstackableContainersAndFurniture = true;
                EmptyContainersMaxStack = 8;
                FurnitureUtilityMaxStack = 16;
                ClothingMaxStack = 16;
                MaxStackSizeCap = 256;
                return true;

            case StackSizePreset.Double2x:
                EnableStackSizeModifier = true;
                GlobalStackMultiplier = 2.0;
                BuildingBlocksStackMultiplier = 1.0;
                ResourcesStackMultiplier = 1.0;
                FoodStackMultiplier = 1.0;
                MiscStackMultiplier = 1.0;
                AllowUnstackableContainersAndFurniture = true;
                EmptyContainersMaxStack = 8;
                FurnitureUtilityMaxStack = 16;
                ClothingMaxStack = 8;
                MaxStackSizeCap = 512;
                return true;

            case StackSizePreset.Builder4x:
                EnableStackSizeModifier = true;
                GlobalStackMultiplier = 1.0;
                BuildingBlocksStackMultiplier = 4.0;
                ResourcesStackMultiplier = 4.0;
                FoodStackMultiplier = 2.0;
                MiscStackMultiplier = 2.0;
                AllowUnstackableContainersAndFurniture = true;
                EmptyContainersMaxStack = 16;
                FurnitureUtilityMaxStack = 64;
                ClothingMaxStack = 16;
                MaxStackSizeCap = 1024;
                return true;

            case StackSizePreset.Custom:
                return true;

            default:
                return false;
        }
    }

    public StackSizePreset DetectMatchingStackPreset()
    {
        foreach (StackSizePreset candidate in new[]
        {
            StackSizePreset.Vanilla,
            StackSizePreset.P1nksBalanced,
            StackSizePreset.Double2x,
            StackSizePreset.Builder4x
        })
        {
            var probe = new SeraphQoLServerConfig();
            probe.ApplyStackPreset(candidate);
            if (MatchesStackValues(probe))
            {
                return candidate;
            }
        }

        return StackSizePreset.Custom;
    }

    public bool ApplyBackpackPreset(BackpackPreset preset)
    {
        BackpackPreset = preset;
        switch (preset)
        {
            case BackpackPreset.Vanilla:
                EnableBackpackExpansion = false;
                GlobalBackpackMultiplier = 1.0;
                StandardBackpacksMultiplier = 1.0;
                SpecializedBagsMultiplier = 1.0;
                SaddlebagsMultiplier = 1.0;
                ModdedBackpacksMultiplier = 1.0;
                MaxBackpackSlotsCap = 64;
                return true;

            case BackpackPreset.Balanced15x:
                EnableBackpackExpansion = true;
                GlobalBackpackMultiplier = 1.0;
                StandardBackpacksMultiplier = 1.5;
                SpecializedBagsMultiplier = 1.5;
                SaddlebagsMultiplier = 1.5;
                ModdedBackpacksMultiplier = 1.5;
                MaxBackpackSlotsCap = 48;
                return true;

            case BackpackPreset.BiggerBackpacks2x:
                EnableBackpackExpansion = true;
                GlobalBackpackMultiplier = 1.0;
                StandardBackpacksMultiplier = 2.0;
                SpecializedBagsMultiplier = 2.0;
                SaddlebagsMultiplier = 2.0;
                ModdedBackpacksMultiplier = 2.0;
                MaxBackpackSlotsCap = 64;
                return true;

            case BackpackPreset.Explorer3x:
                EnableBackpackExpansion = true;
                GlobalBackpackMultiplier = 1.0;
                StandardBackpacksMultiplier = 3.0;
                SpecializedBagsMultiplier = 3.0;
                SaddlebagsMultiplier = 3.0;
                ModdedBackpacksMultiplier = 3.0;
                MaxBackpackSlotsCap = 96;
                return true;

            case BackpackPreset.Custom:
                return true;

            default:
                return false;
        }
    }

    public BackpackPreset DetectMatchingBackpackPreset()
    {
        foreach (BackpackPreset candidate in new[]
        {
            BackpackPreset.Vanilla,
            BackpackPreset.Balanced15x,
            BackpackPreset.BiggerBackpacks2x,
            BackpackPreset.Explorer3x
        })
        {
            var probe = new SeraphQoLServerConfig();
            probe.ApplyBackpackPreset(candidate);
            if (MatchesBackpackValues(probe))
            {
                return candidate;
            }
        }

        return BackpackPreset.Custom;
    }

    public IReadOnlyList<(string Key, object Value)> GetFaunaPresetEntries(bool includePresetKey = false)
    {
        var list = new List<(string Key, object Value)>(12);
        if (includePresetKey)
        {
            list.Add((nameof(FaunaPreset), FaunaPreset.ToString()));
        }

        list.Add((nameof(EnableFaunaRebalance), EnableFaunaRebalance));
        list.Add((nameof(SmallPreyHealthMultiplier), (float)SmallPreyHealthMultiplier));
        list.Add((nameof(MediumLargePreyHealthMultiplier), (float)MediumLargePreyHealthMultiplier));
        list.Add((nameof(PredatorHealthMultiplier), (float)PredatorHealthMultiplier));
        list.Add((nameof(MeatDropMultiplier), (float)MeatDropMultiplier));
        list.Add((nameof(FatDropMultiplier), (float)FatDropMultiplier));
        list.Add((nameof(HideDropMultiplier), (float)HideDropMultiplier));
        list.Add((nameof(ByproductDropMultiplier), (float)ByproductDropMultiplier));
        list.Add((nameof(ScaleDomesticatedHealth), ScaleDomesticatedHealth));
        list.Add((nameof(ScaleDomesticatedDrops), ScaleDomesticatedDrops));
        list.Add((nameof(IncludeAquaticFauna), IncludeAquaticFauna));
        return list;
    }

    public IReadOnlyList<(string Key, object Value)> GetStackPresetEntries(bool includePresetKey = false)
    {
        var list = new List<(string Key, object Value)>(12);
        if (includePresetKey)
        {
            list.Add((nameof(StackSizePreset), StackSizePreset.ToString()));
        }

        list.Add((nameof(EnableStackSizeModifier), EnableStackSizeModifier));
        list.Add((nameof(GlobalStackMultiplier), (float)GlobalStackMultiplier));
        list.Add((nameof(BuildingBlocksStackMultiplier), (float)BuildingBlocksStackMultiplier));
        list.Add((nameof(ResourcesStackMultiplier), (float)ResourcesStackMultiplier));
        list.Add((nameof(FoodStackMultiplier), (float)FoodStackMultiplier));
        list.Add((nameof(MiscStackMultiplier), (float)MiscStackMultiplier));
        list.Add((nameof(AllowUnstackableContainersAndFurniture), AllowUnstackableContainersAndFurniture));
        list.Add((nameof(EmptyContainersMaxStack), EmptyContainersMaxStack));
        list.Add((nameof(FurnitureUtilityMaxStack), FurnitureUtilityMaxStack));
        list.Add((nameof(ClothingMaxStack), ClothingMaxStack));
        list.Add((nameof(MaxStackSizeCap), MaxStackSizeCap));
        return list;
    }

    public IReadOnlyList<(string Key, object Value)> GetBackpackPresetEntries(bool includePresetKey = false)
    {
        var list = new List<(string Key, object Value)>(8);
        if (includePresetKey)
        {
            list.Add((nameof(BackpackPreset), BackpackPreset.ToString()));
        }

        list.Add((nameof(EnableBackpackExpansion), EnableBackpackExpansion));
        list.Add((nameof(GlobalBackpackMultiplier), (float)GlobalBackpackMultiplier));
        list.Add((nameof(StandardBackpacksMultiplier), (float)StandardBackpacksMultiplier));
        list.Add((nameof(SpecializedBagsMultiplier), (float)SpecializedBagsMultiplier));
        list.Add((nameof(SaddlebagsMultiplier), (float)SaddlebagsMultiplier));
        list.Add((nameof(ModdedBackpacksMultiplier), (float)ModdedBackpacksMultiplier));
        list.Add((nameof(MaxBackpackSlotsCap), MaxBackpackSlotsCap));
        return list;
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
        StackSizePreset = source.StackSizePreset;
        EnableStackSizeModifier = source.EnableStackSizeModifier;
        GlobalStackMultiplier = source.GlobalStackMultiplier;
        BuildingBlocksStackMultiplier = source.BuildingBlocksStackMultiplier;
        ResourcesStackMultiplier = source.ResourcesStackMultiplier;
        FoodStackMultiplier = source.FoodStackMultiplier;
        MiscStackMultiplier = source.MiscStackMultiplier;
        AllowUnstackableContainersAndFurniture = source.AllowUnstackableContainersAndFurniture;
        EmptyContainersMaxStack = source.EmptyContainersMaxStack;
        FurnitureUtilityMaxStack = source.FurnitureUtilityMaxStack;
        ClothingMaxStack = source.ClothingMaxStack;
        MaxStackSizeCap = source.MaxStackSizeCap;
        BackpackPreset = source.BackpackPreset;
        EnableBackpackExpansion = source.EnableBackpackExpansion;
        GlobalBackpackMultiplier = source.GlobalBackpackMultiplier;
        StandardBackpacksMultiplier = source.StandardBackpacksMultiplier;
        SpecializedBagsMultiplier = source.SpecializedBagsMultiplier;
        SaddlebagsMultiplier = source.SaddlebagsMultiplier;
        ModdedBackpacksMultiplier = source.ModdedBackpacksMultiplier;
        MaxBackpackSlotsCap = source.MaxBackpackSlotsCap;
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

        GlobalStackMultiplier = ClampStackMultiplier(GlobalStackMultiplier);
        BuildingBlocksStackMultiplier = ClampStackMultiplier(BuildingBlocksStackMultiplier);
        ResourcesStackMultiplier = ClampStackMultiplier(ResourcesStackMultiplier);
        FoodStackMultiplier = ClampStackMultiplier(FoodStackMultiplier);
        MiscStackMultiplier = ClampStackMultiplier(MiscStackMultiplier);
        EmptyContainersMaxStack = Math.Clamp(EmptyContainersMaxStack, MinSingleItemMaxStack, MaxSingleItemMaxStack);
        FurnitureUtilityMaxStack = Math.Clamp(FurnitureUtilityMaxStack, MinSingleItemMaxStack, MaxSingleItemMaxStack);
        ClothingMaxStack = Math.Clamp(ClothingMaxStack, MinSingleItemMaxStack, MaxSingleItemMaxStack);
        MaxStackSizeCap = Math.Clamp(MaxStackSizeCap, MinStackSizeCapValue, MaxStackSizeCapValue);

        GlobalBackpackMultiplier = ClampBackpackMultiplier(GlobalBackpackMultiplier);
        StandardBackpacksMultiplier = ClampBackpackMultiplier(StandardBackpacksMultiplier);
        SpecializedBagsMultiplier = ClampBackpackMultiplier(SpecializedBagsMultiplier);
        SaddlebagsMultiplier = ClampBackpackMultiplier(SaddlebagsMultiplier);
        ModdedBackpacksMultiplier = ClampBackpackMultiplier(ModdedBackpacksMultiplier);
        MaxBackpackSlotsCap = Math.Clamp(MaxBackpackSlotsCap, MinBackpackSlotsCapValue, MaxBackpackSlotsCapValue);
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

        if (string.Equals(code, nameof(StackSizePreset), StringComparison.Ordinal))
        {
            bool parsed = ConfigValueParser.TryEnum<StackSizePreset>(raw, v =>
            {
                if (v == StackSizePreset.Custom) StackSizePreset = StackSizePreset.Custom;
                else ApplyStackPreset(v);
            });
            if (parsed) Normalize();
            return parsed;
        }

        if (string.Equals(code, nameof(BackpackPreset), StringComparison.Ordinal))
        {
            bool parsed = ConfigValueParser.TryEnum<BackpackPreset>(raw, v =>
            {
                if (v == BackpackPreset.Custom) BackpackPreset = BackpackPreset.Custom;
                else ApplyBackpackPreset(v);
            });
            if (parsed) Normalize();
            return parsed;
        }

        bool isFaunaProperty = false;
        bool isStackProperty = false;
        bool isBackpackProperty = false;
        bool applied = code switch
        {
            nameof(EnableFaunaRebalance) => MarkGroup(ConfigValueParser.TryBool(raw, v => EnableFaunaRebalance = v), ref isFaunaProperty),
            nameof(SmallPreyHealthMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => SmallPreyHealthMultiplier = v), ref isFaunaProperty),
            nameof(MediumLargePreyHealthMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => MediumLargePreyHealthMultiplier = v), ref isFaunaProperty),
            nameof(PredatorHealthMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => PredatorHealthMultiplier = v), ref isFaunaProperty),
            nameof(MeatDropMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => MeatDropMultiplier = v), ref isFaunaProperty),
            nameof(FatDropMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => FatDropMultiplier = v), ref isFaunaProperty),
            nameof(HideDropMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => HideDropMultiplier = v), ref isFaunaProperty),
            nameof(ByproductDropMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => ByproductDropMultiplier = v), ref isFaunaProperty),
            nameof(ScaleDomesticatedHealth) => MarkGroup(ConfigValueParser.TryBool(raw, v => ScaleDomesticatedHealth = v), ref isFaunaProperty),
            nameof(ScaleDomesticatedDrops) => MarkGroup(ConfigValueParser.TryBool(raw, v => ScaleDomesticatedDrops = v), ref isFaunaProperty),
            nameof(IncludeAquaticFauna) => MarkGroup(ConfigValueParser.TryBool(raw, v => IncludeAquaticFauna = v), ref isFaunaProperty),
            nameof(EnableInventoryManagement) => ConfigValueParser.TryBool(raw, v => EnableInventoryManagement = v),
            nameof(EnableStoreNearby) => ConfigValueParser.TryBool(raw, v => EnableStoreNearby = v),
            nameof(StoreNearbyRadius) => ConfigValueParser.TryInt(raw, v => StoreNearbyRadius = v),
            nameof(RequireLandClaimPermission) => ConfigValueParser.TryBool(raw, v => RequireLandClaimPermission = v),
            nameof(AllowHotbarRefill) => ConfigValueParser.TryBool(raw, v => AllowHotbarRefill = v),
            nameof(AllowProjectileTracker) => ConfigValueParser.TryBool(raw, v => AllowProjectileTracker = v),
            nameof(EnableStackSizeModifier) => MarkGroup(ConfigValueParser.TryBool(raw, v => EnableStackSizeModifier = v), ref isStackProperty),
            nameof(GlobalStackMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => GlobalStackMultiplier = v), ref isStackProperty),
            nameof(BuildingBlocksStackMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => BuildingBlocksStackMultiplier = v), ref isStackProperty),
            nameof(ResourcesStackMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => ResourcesStackMultiplier = v), ref isStackProperty),
            nameof(FoodStackMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => FoodStackMultiplier = v), ref isStackProperty),
            nameof(MiscStackMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => MiscStackMultiplier = v), ref isStackProperty),
            nameof(AllowUnstackableContainersAndFurniture) => MarkGroup(ConfigValueParser.TryBool(raw, v => AllowUnstackableContainersAndFurniture = v), ref isStackProperty),
            nameof(EmptyContainersMaxStack) => MarkGroup(ConfigValueParser.TryInt(raw, v => EmptyContainersMaxStack = v), ref isStackProperty),
            nameof(FurnitureUtilityMaxStack) => MarkGroup(ConfigValueParser.TryInt(raw, v => FurnitureUtilityMaxStack = v), ref isStackProperty),
            nameof(ClothingMaxStack) => MarkGroup(ConfigValueParser.TryInt(raw, v => ClothingMaxStack = v), ref isStackProperty),
            nameof(MaxStackSizeCap) => MarkGroup(ConfigValueParser.TryInt(raw, v => MaxStackSizeCap = v), ref isStackProperty),
            nameof(EnableBackpackExpansion) => MarkGroup(ConfigValueParser.TryBool(raw, v => EnableBackpackExpansion = v), ref isBackpackProperty),
            nameof(GlobalBackpackMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => GlobalBackpackMultiplier = v), ref isBackpackProperty),
            nameof(StandardBackpacksMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => StandardBackpacksMultiplier = v), ref isBackpackProperty),
            nameof(SpecializedBagsMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => SpecializedBagsMultiplier = v), ref isBackpackProperty),
            nameof(SaddlebagsMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => SaddlebagsMultiplier = v), ref isBackpackProperty),
            nameof(ModdedBackpacksMultiplier) => MarkGroup(ConfigValueParser.TryDouble(raw, v => ModdedBackpacksMultiplier = v), ref isBackpackProperty),
            nameof(MaxBackpackSlotsCap) => MarkGroup(ConfigValueParser.TryInt(raw, v => MaxBackpackSlotsCap = v), ref isBackpackProperty),
            _ => false
        };

        if (applied)
        {
            Normalize();
            if (isFaunaProperty)
            {
                FaunaPreset = DetectMatchingPreset();
            }
            if (isStackProperty)
            {
                StackSizePreset = DetectMatchingStackPreset();
            }
            if (isBackpackProperty)
            {
                BackpackPreset = DetectMatchingBackpackPreset();
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

    private bool MatchesStackValues(SeraphQoLServerConfig other) =>
        EnableStackSizeModifier == other.EnableStackSizeModifier
        && ApproxEqual(GlobalStackMultiplier, other.GlobalStackMultiplier)
        && ApproxEqual(BuildingBlocksStackMultiplier, other.BuildingBlocksStackMultiplier)
        && ApproxEqual(ResourcesStackMultiplier, other.ResourcesStackMultiplier)
        && ApproxEqual(FoodStackMultiplier, other.FoodStackMultiplier)
        && ApproxEqual(MiscStackMultiplier, other.MiscStackMultiplier)
        && AllowUnstackableContainersAndFurniture == other.AllowUnstackableContainersAndFurniture
        && EmptyContainersMaxStack == other.EmptyContainersMaxStack
        && FurnitureUtilityMaxStack == other.FurnitureUtilityMaxStack
        && ClothingMaxStack == other.ClothingMaxStack
        && MaxStackSizeCap == other.MaxStackSizeCap;

    private bool MatchesBackpackValues(SeraphQoLServerConfig other) =>
        EnableBackpackExpansion == other.EnableBackpackExpansion
        && ApproxEqual(GlobalBackpackMultiplier, other.GlobalBackpackMultiplier)
        && ApproxEqual(StandardBackpacksMultiplier, other.StandardBackpacksMultiplier)
        && ApproxEqual(SpecializedBagsMultiplier, other.SpecializedBagsMultiplier)
        && ApproxEqual(SaddlebagsMultiplier, other.SaddlebagsMultiplier)
        && ApproxEqual(ModdedBackpacksMultiplier, other.ModdedBackpacksMultiplier)
        && MaxBackpackSlotsCap == other.MaxBackpackSlotsCap;

    private static bool ApproxEqual(double a, double b) => Math.Abs(a - b) < 0.001;

    private static bool MarkGroup(bool result, ref bool groupFlag)
    {
        groupFlag = result;
        return result;
    }

    private static double ClampMultiplier(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? 1.0
            : Math.Clamp(value, MinMultiplier, MaxMultiplier);

    private static double ClampStackMultiplier(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? 1.0
            : Math.Clamp(value, MinStackMultiplier, MaxStackMultiplier);

    private static double ClampBackpackMultiplier(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? 1.0
            : Math.Clamp(value, MinBackpackMultiplier, MaxBackpackMultiplier);
}
