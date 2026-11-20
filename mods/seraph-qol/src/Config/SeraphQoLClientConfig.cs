using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vintagestory.API.Datastructures;

namespace SeraphQoL.Config;

public enum SortOrder
{
    Category,
    Name,
    Code,
    StackCount
}

/// <summary>
/// Preferencias personales del jugador (cliente) para inventario, botones y overlays.
/// </summary>
public sealed class SeraphQoLClientConfig
{
    public const int MinToolDurabilityThreshold = 1;
    public const int MaxToolDurabilityThreshold = 50;
    public const int MinRefillDelayMs = 0;
    public const int MaxRefillDelayMs = 2000;
    public const int MinRadialOuterRadius = 90;
    public const int MaxRadialOuterRadius = 240;
    public const int MinGroundHighlightRadius = 4;
    public const int MaxGroundHighlightRadius = 48;
    public const double MinGroundHighlightDurationSeconds = 0.0;
    public const double MaxGroundHighlightDurationSeconds = 60.0;
    public const int MinProjectileWaypointTimeoutSeconds = 0;
    public const int MaxProjectileWaypointTimeoutSeconds = 3600;
    public const int MinAlloyUnitsPerNugget = 1;
    public const int MaxAlloyUnitsPerNugget = 20;
    public const int MinAlloySearchRadius = 4;
    public const int MaxAlloySearchRadius = 10;
    public const int MinAnimalIndicatorRange = 2;
    public const int MaxAnimalIndicatorRange = 24;
    public const double MinAnimalIndicatorOpacity = 0.2;
    public const double MaxAnimalIndicatorOpacity = 1.0;
    public const double MinAnimalIndicatorScale = 0.5;
    public const double MaxAnimalIndicatorScale = 2.0;
    public const int MinHealthBarRange = 4;
    public const int MaxHealthBarRange = 32;

    [JsonConverter(typeof(StringEnumConverter))]
    public SortOrder SortOrder { get; set; } = SortOrder.Category;

    public bool SortHotbarWithBackpack { get; set; } = false;
    public bool StackPerishables { get; set; } = false;
    public bool SkipFavoritesWhenSorting { get; set; } = false;

    public bool HideFavoritesOverlay { get; set; } = false;
    public bool HideLockedSlotOverlay { get; set; } = false;
    public bool HideSortButton { get; set; } = false;
    public bool HideQuickStoreButton { get; set; } = false;
    public bool HideStoreNearbyButton { get; set; } = false;
    public bool HideStackPerishablesButton { get; set; } = false;

    // Fase 2: Hotbar Refill
    public bool EnableHotbarRefill { get; set; } = true;
    public bool RefillConsumables { get; set; } = true;
    public bool RefillBlocks { get; set; } = true;
    public bool RefillToolsBeforeBreak { get; set; } = false;
    public int ToolDurabilityThreshold { get; set; } = 5;
    public int RefillDelayMs { get; set; } = 150;
    public bool PreferSmallestStackForRefill { get; set; } = true;
    public HashSet<string> RefillBlacklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Fase 2: Menu Radial de Herramientas (tecla F)
    public bool EnableRadialToolModeMenu { get; set; } = true;
    public bool ToolModeSelectOnKeyRelease { get; set; } = false;
    public int RadialToolModeOuterRadius { get; set; } = 135;

    // Fase 2: Resaltado y Exploracion
    public bool EnableMatchingItemHighlight { get; set; } = true;
    public bool EnableGroundItemHighlight { get; set; } = true;
    public bool AlwaysHighlightGroundItems { get; set; } = false;
    public bool ShowGroundItemNamesOnSneak { get; set; } = true;
    public int GroundItemHighlightRadius { get; set; } = 16;
    public double GroundItemHighlightDurationSeconds { get; set; } = 2.5;
    public bool EnableProjectileTracker { get; set; } = true;
    public int ProjectileWaypointTimeoutSeconds { get; set; } = 300;

    // Fase 3: Calculadora de Aleaciones y Quick-Swap
    public bool EnableAlloyCalculator { get; set; } = true;
    public int AlloyUnitsPerNugget { get; set; } = 5;
    public bool AlloySearchNearbyContainers { get; set; } = false;
    public int AlloySearchRadius { get; set; } = 6;
    public bool EnableQuickSwapRadial { get; set; } = true;
    public bool QuickSwapSelectOnKeyRelease { get; set; } = true;

    // Fase 3: Indicadores en Slots
    public bool EnableSlotIndicators { get; set; } = true;
    public bool ShowSlotFreshnessIndicator { get; set; } = true;
    public bool ShowSlotDurabilityIndicator { get; set; } = true;
    public bool ShowSlotPreparationIndicator { get; set; } = true;
    public bool ShowSlotLiquidIndicator { get; set; } = true;
    public bool ShowSlotCrucibleIndicator { get; set; } = true;
    public bool ShowSlotFoodContainerIndicator { get; set; } = true;
    public bool ShowSlotNightVisionIndicator { get; set; } = true;

    // Fase 3: Indicadores de Sexo/Prenez Animal, Barra de Vida y Particulas de Recursos
    public bool EnableAnimalSexIndicators { get; set; } = true;
    public bool AnimalIndicatorIncludeWild { get; set; } = false;
    public int AnimalIndicatorRange { get; set; } = 12;
    public double AnimalIndicatorOpacity { get; set; } = 0.90;
    public double AnimalIndicatorScale { get; set; } = 1.0;

    public bool EnableEntityHealthBars { get; set; } = true;
    public bool HealthBarShowHostiles { get; set; } = true;
    public bool HealthBarShowPassiveFauna { get; set; } = true;
    public bool HealthBarShowTradersAndNpcs { get; set; } = false;
    public bool HealthBarShowPlayers { get; set; } = false;
    public bool HealthBarOnlyWhenDamaged { get; set; } = false;
    public bool HealthBarRequireSneak { get; set; } = false;
    public int HealthBarRange { get; set; } = 18;

    public bool EnableResourceParticles { get; set; } = true;
    public bool HighlightResinParticles { get; set; } = true;
    public bool HighlightTerraPretaParticles { get; set; } = true;
    public bool HighlightMeteoricIronParticles { get; set; } = true;

    public Dictionary<string, HashSet<int>> LockedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        ToolDurabilityThreshold = Math.Clamp(ToolDurabilityThreshold, MinToolDurabilityThreshold, MaxToolDurabilityThreshold);
        RefillDelayMs = Math.Clamp(RefillDelayMs, MinRefillDelayMs, MaxRefillDelayMs);
        RadialToolModeOuterRadius = Math.Clamp(RadialToolModeOuterRadius, MinRadialOuterRadius, MaxRadialOuterRadius);
        GroundItemHighlightRadius = Math.Clamp(GroundItemHighlightRadius, MinGroundHighlightRadius, MaxGroundHighlightRadius);
        GroundItemHighlightDurationSeconds = Math.Clamp(double.IsNaN(GroundItemHighlightDurationSeconds) ? 2.5 : GroundItemHighlightDurationSeconds, MinGroundHighlightDurationSeconds, MaxGroundHighlightDurationSeconds);
        ProjectileWaypointTimeoutSeconds = Math.Clamp(ProjectileWaypointTimeoutSeconds, MinProjectileWaypointTimeoutSeconds, MaxProjectileWaypointTimeoutSeconds);
        AlloyUnitsPerNugget = Math.Clamp(AlloyUnitsPerNugget, MinAlloyUnitsPerNugget, MaxAlloyUnitsPerNugget);
        AlloySearchRadius = Math.Clamp(AlloySearchRadius, MinAlloySearchRadius, MaxAlloySearchRadius);
        AnimalIndicatorRange = Math.Clamp(AnimalIndicatorRange, MinAnimalIndicatorRange, MaxAnimalIndicatorRange);
        AnimalIndicatorOpacity = Math.Clamp(double.IsNaN(AnimalIndicatorOpacity) ? 0.90 : AnimalIndicatorOpacity, MinAnimalIndicatorOpacity, MaxAnimalIndicatorOpacity);
        AnimalIndicatorScale = Math.Clamp(double.IsNaN(AnimalIndicatorScale) ? 1.0 : AnimalIndicatorScale, MinAnimalIndicatorScale, MaxAnimalIndicatorScale);
        HealthBarRange = Math.Clamp(HealthBarRange, MinHealthBarRange, MaxHealthBarRange);
    }

    public SortOrder CycleSortOrder(int step = 1)
    {
        SortOrder[] values = Enum.GetValues<SortOrder>();
        int idx = Array.IndexOf(values, SortOrder);
        int next = ((idx + step) % values.Length + values.Length) % values.Length;
        SortOrder = values[next];
        return SortOrder;
    }

    public bool IsSlotLocked(string inventoryClass, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(inventoryClass) || slotIndex < 0) return false;
        return LockedSlots.TryGetValue(inventoryClass, out HashSet<int>? set) && set.Contains(slotIndex);
    }

    public bool ToggleLockedSlot(string inventoryClass, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(inventoryClass) || slotIndex < 0) return false;

        if (!LockedSlots.TryGetValue(inventoryClass, out HashSet<int>? set))
        {
            set = new HashSet<int>();
            LockedSlots[inventoryClass] = set;
        }

        bool added = set.Add(slotIndex);
        if (!added)
        {
            set.Remove(slotIndex);
            if (set.Count == 0)
            {
                LockedSlots.Remove(inventoryClass);
            }
        }

        return added;
    }

    public IReadOnlyCollection<int> GetLockedSlots(string inventoryClass)
    {
        if (string.IsNullOrWhiteSpace(inventoryClass)) return Array.Empty<int>();
        return LockedSlots.TryGetValue(inventoryClass, out HashSet<int>? set)
            ? set
            : Array.Empty<int>();
    }

    public bool ApplySetting(string? code, IAttribute? valueAttr)
    {
        if (string.IsNullOrWhiteSpace(code) || valueAttr == null) return false;
        object? raw = valueAttr.GetValue();
        if (raw == null) return false;

        switch (code)
        {
            case nameof(SortOrder):
                if (Enum.TryParse(raw.ToString(), true, out SortOrder parsedOrder))
                {
                    SortOrder = parsedOrder;
                    return true;
                }
                return false;
            case nameof(SortHotbarWithBackpack):
                return ConfigValueParser.TryBool(raw, v => SortHotbarWithBackpack = v);
            case nameof(StackPerishables):
                return ConfigValueParser.TryBool(raw, v => StackPerishables = v);
            case nameof(SkipFavoritesWhenSorting):
                return ConfigValueParser.TryBool(raw, v => SkipFavoritesWhenSorting = v);
            case nameof(HideFavoritesOverlay):
                return ConfigValueParser.TryBool(raw, v => HideFavoritesOverlay = v);
            case nameof(HideLockedSlotOverlay):
                return ConfigValueParser.TryBool(raw, v => HideLockedSlotOverlay = v);
            case nameof(HideSortButton):
                return ConfigValueParser.TryBool(raw, v => HideSortButton = v);
            case nameof(HideQuickStoreButton):
                return ConfigValueParser.TryBool(raw, v => HideQuickStoreButton = v);
            case nameof(HideStoreNearbyButton):
                return ConfigValueParser.TryBool(raw, v => HideStoreNearbyButton = v);
            case nameof(HideStackPerishablesButton):
                return ConfigValueParser.TryBool(raw, v => HideStackPerishablesButton = v);
            case nameof(EnableHotbarRefill):
                return ConfigValueParser.TryBool(raw, v => EnableHotbarRefill = v);
            case nameof(RefillConsumables):
                return ConfigValueParser.TryBool(raw, v => RefillConsumables = v);
            case nameof(RefillBlocks):
                return ConfigValueParser.TryBool(raw, v => RefillBlocks = v);
            case nameof(RefillToolsBeforeBreak):
                return ConfigValueParser.TryBool(raw, v => RefillToolsBeforeBreak = v);
            case nameof(ToolDurabilityThreshold):
                if (ConfigValueParser.TryInt(raw, v => ToolDurabilityThreshold = v)) { Normalize(); return true; }
                return false;
            case nameof(RefillDelayMs):
                if (ConfigValueParser.TryInt(raw, v => RefillDelayMs = v)) { Normalize(); return true; }
                return false;
            case nameof(PreferSmallestStackForRefill):
                return ConfigValueParser.TryBool(raw, v => PreferSmallestStackForRefill = v);
            case nameof(EnableRadialToolModeMenu):
                return ConfigValueParser.TryBool(raw, v => EnableRadialToolModeMenu = v);
            case nameof(ToolModeSelectOnKeyRelease):
                return ConfigValueParser.TryBool(raw, v => ToolModeSelectOnKeyRelease = v);
            case nameof(RadialToolModeOuterRadius):
                if (ConfigValueParser.TryInt(raw, v => RadialToolModeOuterRadius = v)) { Normalize(); return true; }
                return false;
            case nameof(EnableMatchingItemHighlight):
                return ConfigValueParser.TryBool(raw, v => EnableMatchingItemHighlight = v);
            case nameof(EnableGroundItemHighlight):
                return ConfigValueParser.TryBool(raw, v => EnableGroundItemHighlight = v);
            case nameof(AlwaysHighlightGroundItems):
                return ConfigValueParser.TryBool(raw, v => AlwaysHighlightGroundItems = v);
            case nameof(ShowGroundItemNamesOnSneak):
                return ConfigValueParser.TryBool(raw, v => ShowGroundItemNamesOnSneak = v);
            case nameof(GroundItemHighlightRadius):
                if (ConfigValueParser.TryInt(raw, v => GroundItemHighlightRadius = v)) { Normalize(); return true; }
                return false;
            case nameof(GroundItemHighlightDurationSeconds):
                if (ConfigValueParser.TryDouble(raw, v => GroundItemHighlightDurationSeconds = v)) { Normalize(); return true; }
                return false;
            case nameof(EnableProjectileTracker):
                return ConfigValueParser.TryBool(raw, v => EnableProjectileTracker = v);
            case nameof(ProjectileWaypointTimeoutSeconds):
                if (ConfigValueParser.TryInt(raw, v => ProjectileWaypointTimeoutSeconds = v)) { Normalize(); return true; }
                return false;
            case nameof(EnableAlloyCalculator):
                return ConfigValueParser.TryBool(raw, v => EnableAlloyCalculator = v);
            case nameof(AlloyUnitsPerNugget):
                if (ConfigValueParser.TryInt(raw, v => AlloyUnitsPerNugget = v)) { Normalize(); return true; }
                return false;
            case nameof(AlloySearchNearbyContainers):
                return ConfigValueParser.TryBool(raw, v => AlloySearchNearbyContainers = v);
            case nameof(AlloySearchRadius):
                if (ConfigValueParser.TryInt(raw, v => AlloySearchRadius = v)) { Normalize(); return true; }
                return false;
            case nameof(EnableQuickSwapRadial):
                return ConfigValueParser.TryBool(raw, v => EnableQuickSwapRadial = v);
            case nameof(QuickSwapSelectOnKeyRelease):
                return ConfigValueParser.TryBool(raw, v => QuickSwapSelectOnKeyRelease = v);
            case nameof(EnableSlotIndicators):
                return ConfigValueParser.TryBool(raw, v => EnableSlotIndicators = v);
            case nameof(ShowSlotFreshnessIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotFreshnessIndicator = v);
            case nameof(ShowSlotDurabilityIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotDurabilityIndicator = v);
            case nameof(ShowSlotPreparationIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotPreparationIndicator = v);
            case nameof(ShowSlotLiquidIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotLiquidIndicator = v);
            case nameof(ShowSlotCrucibleIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotCrucibleIndicator = v);
            case nameof(ShowSlotFoodContainerIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotFoodContainerIndicator = v);
            case nameof(ShowSlotNightVisionIndicator):
                return ConfigValueParser.TryBool(raw, v => ShowSlotNightVisionIndicator = v);
            case nameof(EnableAnimalSexIndicators):
                return ConfigValueParser.TryBool(raw, v => EnableAnimalSexIndicators = v);
            case nameof(AnimalIndicatorIncludeWild):
                return ConfigValueParser.TryBool(raw, v => AnimalIndicatorIncludeWild = v);
            case nameof(AnimalIndicatorRange):
                if (ConfigValueParser.TryInt(raw, v => AnimalIndicatorRange = v)) { Normalize(); return true; }
                return false;
            case nameof(AnimalIndicatorOpacity):
                if (ConfigValueParser.TryDouble(raw, v => AnimalIndicatorOpacity = v)) { Normalize(); return true; }
                return false;
            case nameof(AnimalIndicatorScale):
                if (ConfigValueParser.TryDouble(raw, v => AnimalIndicatorScale = v)) { Normalize(); return true; }
                return false;
            case nameof(EnableEntityHealthBars):
                return ConfigValueParser.TryBool(raw, v => EnableEntityHealthBars = v);
            case nameof(HealthBarShowHostiles):
                return ConfigValueParser.TryBool(raw, v => HealthBarShowHostiles = v);
            case nameof(HealthBarShowPassiveFauna):
                return ConfigValueParser.TryBool(raw, v => HealthBarShowPassiveFauna = v);
            case nameof(HealthBarShowTradersAndNpcs):
                return ConfigValueParser.TryBool(raw, v => HealthBarShowTradersAndNpcs = v);
            case nameof(HealthBarShowPlayers):
                return ConfigValueParser.TryBool(raw, v => HealthBarShowPlayers = v);
            case nameof(HealthBarOnlyWhenDamaged):
                return ConfigValueParser.TryBool(raw, v => HealthBarOnlyWhenDamaged = v);
            case nameof(HealthBarRequireSneak):
                return ConfigValueParser.TryBool(raw, v => HealthBarRequireSneak = v);
            case nameof(HealthBarRange):
                if (ConfigValueParser.TryInt(raw, v => HealthBarRange = v)) { Normalize(); return true; }
                return false;
            case nameof(EnableResourceParticles):
                return ConfigValueParser.TryBool(raw, v => EnableResourceParticles = v);
            case nameof(HighlightResinParticles):
                return ConfigValueParser.TryBool(raw, v => HighlightResinParticles = v);
            case nameof(HighlightTerraPretaParticles):
                return ConfigValueParser.TryBool(raw, v => HighlightTerraPretaParticles = v);
            case nameof(HighlightMeteoricIronParticles):
                return ConfigValueParser.TryBool(raw, v => HighlightMeteoricIronParticles = v);
            default:
                return false;
        }
    }
}

internal static class ConfigValueParser
{
    public static bool TryBool(object raw, Action<bool> assign)
    {
        switch (raw)
        {
            case bool b:
                assign(b);
                return true;
            case int i:
                assign(i != 0);
                return true;
            case long l:
                assign(l != 0);
                return true;
            default:
                if (bool.TryParse(raw.ToString(), out bool parsed))
                {
                    assign(parsed);
                    return true;
                }
                return false;
        }
    }

    public static bool TryDouble(object raw, Action<double> assign)
    {
        switch (raw)
        {
            case double d:
                assign(d);
                return true;
            case float f:
                assign(f);
                return true;
            case int i:
                assign(i);
                return true;
            case long l:
                assign(l);
                return true;
            default:
                if (double.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    assign(parsed);
                    return true;
                }
                return false;
        }
    }

    public static bool TryInt(object raw, Action<int> assign)
    {
        switch (raw)
        {
            case int i:
                assign(i);
                return true;
            case long l:
                assign((int)l);
                return true;
            case double d:
                assign((int)Math.Round(d));
                return true;
            default:
                if (int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    assign(parsed);
                    return true;
                }
                return false;
        }
    }

    public static bool TryEnum<TEnum>(object raw, out TEnum parsed) where TEnum : struct, Enum =>
        Enum.TryParse(raw.ToString(), true, out parsed);

    public static bool TryEnum<TEnum>(object raw, Action<TEnum> assign) where TEnum : struct, Enum
    {
        if (!TryEnum(raw, out TEnum parsed)) return false;
        assign(parsed);
        return true;
    }
}
