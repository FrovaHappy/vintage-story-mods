using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vintagestory.API.Datastructures;

namespace Comerciantes;

public enum DirectorySyncMode
{
    LastVisitedSnapshot,
    LiveKnownTraders
}

public enum SaturationScope
{
    LocalOnly,
    NearbyPartial,
    Global
}

public enum EconomyMode
{
    Stingy,
    Balanced,
    Businessman
}

/// <summary>
/// Configuracion del mod (reglas de servidor/mundo y preferencias de cliente).
/// </summary>
public sealed class ComerciantesConfig
{
    [JsonConverter(typeof(StringEnumConverter))]
    public EconomyMode EconomyMode { get; set; } = EconomyMode.Balanced;

    [JsonConverter(typeof(StringEnumConverter))]
    public DirectorySyncMode DirectorySyncMode { get; set; } = DirectorySyncMode.LastVisitedSnapshot;

    [JsonConverter(typeof(StringEnumConverter))]
    public SaturationScope SaturationScope { get; set; } = SaturationScope.NearbyPartial;

    public double SaturationSpilloverRadius { get; set; } = 1500.0;
    public double SaturationSpilloverFactor { get; set; } = 0.35;
    public double SaturationMinFloor { get; set; } = 0.20;
    public double SaturationS50 { get; set; } = 25.0;
    public double SaturationCurvePower { get; set; } = 1.25;
    public double SaturationRecoveryPerDay { get; set; } = 6.0;

    public double DailyDemandMinPct { get; set; } = 20.0;
    public double DailyDemandMaxPct { get; set; } = 50.0;
    public double MinDurabilityRatio { get; set; } = 0.80;
    public double AntiArbitrageCapFactor { get; set; } = 0.85;
    public double MaxPhysicalTradeDistance { get; set; } = 6.0;

    public bool ShowValuationInInventoryTooltip { get; set; } = false;
    public bool EnableInstantTrade { get; set; } = false;
    public bool EnableDebugLogging { get; set; } = false;

    public bool IsWithinTradeDistance(double distance) =>
        EnableInstantTrade || distance <= MaxPhysicalTradeDistance;

    public double GetBuyPriceMultiplier() => EconomyMode switch
    {
        EconomyMode.Stingy => 1.25,
        _ => 1.00
    };

    public double GetSellPayoutMultiplier() => EconomyMode switch
    {
        EconomyMode.Stingy => 0.75,
        EconomyMode.Businessman => 1.20,
        _ => 1.00
    };

    public bool ApplySetting(string? code, IAttribute? valueAttr)
    {
        if (string.IsNullOrWhiteSpace(code) || valueAttr == null) return false;
        object? raw = valueAttr.GetValue();
        if (raw == null) return false;

        switch (code)
        {
            case nameof(EconomyMode):
                if (Enum.TryParse(raw.ToString(), true, out EconomyMode eco))
                {
                    EconomyMode = eco;
                    return true;
                }
                return false;
            case nameof(DirectorySyncMode):
                if (Enum.TryParse(raw.ToString(), true, out DirectorySyncMode sync))
                {
                    DirectorySyncMode = sync;
                    return true;
                }
                return false;
            case nameof(SaturationScope):
                if (Enum.TryParse(raw.ToString(), true, out SaturationScope scope))
                {
                    SaturationScope = scope;
                    return true;
                }
                return false;
            case nameof(ShowValuationInInventoryTooltip):
                if (TryToBool(raw, out bool showTooltip))
                {
                    ShowValuationInInventoryTooltip = showTooltip;
                    return true;
                }
                return false;
            case nameof(EnableInstantTrade):
                if (TryToBool(raw, out bool instantTrade))
                {
                    EnableInstantTrade = instantTrade;
                    return true;
                }
                return false;
            case nameof(EnableDebugLogging):
                if (TryToBool(raw, out bool debugLog))
                {
                    EnableDebugLogging = debugLog;
                    return true;
                }
                return false;
            case nameof(SaturationSpilloverRadius):
                if (TryToDouble(raw, out double radius)) { SaturationSpilloverRadius = radius; return true; }
                return false;
            case nameof(SaturationSpilloverFactor):
                if (TryToDouble(raw, out double factor)) { SaturationSpilloverFactor = factor; return true; }
                return false;
            case nameof(SaturationMinFloor):
                if (TryToDouble(raw, out double floor)) { SaturationMinFloor = floor; return true; }
                return false;
            case nameof(SaturationS50):
                if (TryToDouble(raw, out double s50)) { SaturationS50 = s50; return true; }
                return false;
            case nameof(SaturationCurvePower):
                if (TryToDouble(raw, out double power)) { SaturationCurvePower = power; return true; }
                return false;
            case nameof(SaturationRecoveryPerDay):
                if (TryToDouble(raw, out double recovery)) { SaturationRecoveryPerDay = recovery; return true; }
                return false;
            case nameof(DailyDemandMinPct):
                if (TryToDouble(raw, out double minPct)) { DailyDemandMinPct = minPct; return true; }
                return false;
            case nameof(DailyDemandMaxPct):
                if (TryToDouble(raw, out double maxPct)) { DailyDemandMaxPct = maxPct; return true; }
                return false;
            case nameof(MinDurabilityRatio):
                if (TryToDouble(raw, out double dur)) { MinDurabilityRatio = dur; return true; }
                return false;
            case nameof(AntiArbitrageCapFactor):
                if (TryToDouble(raw, out double cap)) { AntiArbitrageCapFactor = cap; return true; }
                return false;
            case nameof(MaxPhysicalTradeDistance):
                if (TryToDouble(raw, out double dist)) { MaxPhysicalTradeDistance = dist; return true; }
                return false;
            default:
                return false;
        }
    }

    private static bool TryToBool(object raw, out bool result)
    {
        if (raw is bool b) { result = b; return true; }
        if (bool.TryParse(raw.ToString(), out result)) return true;
        if (TryToDouble(raw, out double num)) { result = Math.Abs(num) > 0.001; return true; }
        return false;
    }

    private static bool TryToDouble(object raw, out double result)
    {
        switch (raw)
        {
            case double d: result = d; return true;
            case float f: result = f; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            default:
                return double.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
