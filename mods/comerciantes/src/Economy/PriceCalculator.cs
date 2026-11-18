using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Comerciantes;

public sealed record ItemValuationResult(
    double BaseValue,
    double AffinityMod,
    double DemandMod,
    double SaturationMod,
    double ConditionMod,
    int MinLotSize,
    int LotGearPayout,
    double DeltaPct,
    bool IsTradeable,
    bool IsOverstocked = false);

/// <summary>
/// Calcula el precio final, lote minimo entero y tendencia de variacion de un item.
/// </summary>
public sealed class PriceCalculator
{
    private readonly ItemValuationRegistry _valuations;
    private readonly TraderAffinityMatrix _affinities;
    private readonly WorldSaturationRegistry _saturation;

    public PriceCalculator(
        ItemValuationRegistry valuations,
        TraderAffinityMatrix affinities,
        WorldSaturationRegistry saturation)
    {
        _valuations = valuations;
        _affinities = affinities;
        _saturation = saturation;
    }

    public static int GetEffectiveBuyPrice(int rawPrice, ComerciantesConfig config) =>
        Math.Max(1, (int)Math.Round(rawPrice * config.GetBuyPriceMultiplier(), MidpointRounding.AwayFromZero));

    public static double ComputeTraderDemandBonusPct(long traderEntityId, double nextRefreshTargetDays, ComerciantesConfig config)
    {
        double minPct = Math.Min(config.DailyDemandMinPct, config.DailyDemandMaxPct);
        double maxPct = Math.Max(config.DailyDemandMinPct, config.DailyDemandMaxPct);
        if (Math.Abs(maxPct - minPct) < 0.01) return Math.Round(minPct, 0);

        long cycleSeed = (long)Math.Floor(nextRefreshTargetDays / 7.0);
        unchecked
        {
            ulong hash = (ulong)traderEntityId * 6364136223846793005UL + (ulong)cycleSeed * 1442695040888963407UL + 0x9E3779B97F4A7C15UL;
            hash ^= hash >> 30;
            hash *= 0xBF58476D1CE4E5B9UL;
            hash ^= hash >> 27;
            double t = (hash & 0xFFFFUL) / 65535.0;
            return Math.Round(minPct + t * (maxPct - minPct), 0);
        }
    }

    public ItemValuationResult Evaluate(
        ItemStack stack,
        long traderEntityId,
        string traderArchetype,
        BlockPos traderPos,
        double currentTotalDays,
        ISet<string> dailyWantedCodes,
        IReadOnlyDictionary<string, int> traderSellingPrices,
        ComerciantesConfig config,
        double traderDemandMultiplier = 1.40)
    {
        double baseValue = _valuations.GetBaseValue(stack);
        double conditionMod = _valuations.GetConditionMultiplier(stack, config);
        if (baseValue <= 0.0 || conditionMod <= 0.0)
        {
            return new ItemValuationResult(0, 1, 1, 1, conditionMod, 0, 0, 0, false);
        }

        string itemCode = stack.Collectible.Code.ToString();
        double affinityMod = _affinities.GetAffinityMultiplier(traderArchetype, stack.Collectible);
        double demandMod = dailyWantedCodes.Contains(itemCode) ? traderDemandMultiplier : 1.0;

        double satVolume = _saturation.GetCurrentVolume(traderEntityId, traderPos, currentTotalDays, itemCode, config);
        double saturationMod = _saturation.EvaluateMultiplier(satVolume, config);
        double economySellMult = config.GetSellPayoutMultiplier();

        double unitPrice = baseValue * affinityMod * demandMod * saturationMod * conditionMod * economySellMult;

        if (traderSellingPrices.TryGetValue(itemCode, out int retailPrice) && retailPrice > 0)
        {
            double maxAllowed = Math.Floor(retailPrice * config.AntiArbitrageCapFactor);
            if (maxAllowed >= 1.0)
            {
                unitPrice = Math.Min(unitPrice, maxAllowed);
            }
        }

        if (!TryComputeStandardizedLot(unitPrice, stack.Collectible.MaxStackSize, out int minLotSize, out int lotGearPayout))
        {
            bool isOverstocked = saturationMod < 0.999;
            return new ItemValuationResult(baseValue, affinityMod, demandMod, saturationMod, conditionMod, minLotSize, 0, -100, false, isOverstocked);
        }

        double effectiveUnit = (double)lotGearPayout / minLotSize;
        double deltaPct = ((effectiveUnit - baseValue) / baseValue) * 100.0;

        return new ItemValuationResult(
            baseValue,
            affinityMod,
            demandMod,
            saturationMod,
            conditionMod,
            minLotSize,
            lotGearPayout,
            deltaPct,
            true);
    }

    public static bool TryComputeStandardizedLot(
        double unitPrice,
        int maxStackSize,
        out int lotSize,
        out int lotGearPayout)
    {
        lotSize = 0;
        lotGearPayout = 0;
        if (unitPrice <= 0.0) return false;

        if (maxStackSize <= 1)
        {
            if (unitPrice < 0.5)
            {
                lotSize = 1;
                return false;
            }
            lotSize = 1;
            lotGearPayout = Math.Max(1, (int)Math.Round(unitPrice, MidpointRounding.AwayFromZero));
            return true;
        }

        if (unitPrice >= 1.0)
        {
            lotSize = 1;
            lotGearPayout = Math.Max(1, (int)Math.Round(unitPrice, MidpointRounding.AwayFromZero));
            return true;
        }

        int maxAllowedLot = Math.Min(64, maxStackSize);
        int rawUnits = Math.Max(1, (int)Math.Ceiling((1.0 / unitPrice) - 1e-6));
        lotSize = Math.Max(4, ((rawUnits + 3) / 4) * 4);
        if (lotSize > maxAllowedLot)
        {
            return false;
        }

        lotGearPayout = Math.Max(1, (int)Math.Floor((lotSize * unitPrice) + 1e-6));
        return true;
    }

    public SellSlotValuationEntry EvaluateSellSlot(
        int sellSlotIndex,
        ItemStack stack,
        long traderEntityId,
        string traderArchetype,
        BlockPos traderPos,
        double currentTotalDays,
        ISet<string> dailyWantedCodes,
        IReadOnlyDictionary<string, int> traderSellingPrices,
        ComerciantesConfig config,
        double traderDemandMultiplier = 1.40,
        IDictionary<string, double>? simulatedAddedGev = null)
    {
        ItemValuationResult firstEval = Evaluate(
            stack,
            traderEntityId,
            traderArchetype,
            traderPos,
            currentTotalDays,
            dailyWantedCodes,
            traderSellingPrices,
            config,
            traderDemandMultiplier);

        string itemCode = stack.Collectible.Code.ToString();
        string displayName = stack.GetName();

        if (!firstEval.IsTradeable)
        {
            return new SellSlotValuationEntry
            {
                SellSlotIndex = sellSlotIndex,
                ItemCode = itemCode,
                DisplayName = displayName,
                QuantityInSlot = stack.StackSize,
                IsTradeable = false,
                IsOverstocked = firstEval.IsOverstocked
            };
        }

        double baseSat = _saturation.GetCurrentVolume(traderEntityId, traderPos, currentTotalDays, itemCode, config);
        double extraSat = simulatedAddedGev != null && simulatedAddedGev.TryGetValue(itemCode, out double curExtra)
            ? curExtra
            : 0.0;

        double maxAllowed = double.MaxValue;
        if (traderSellingPrices.TryGetValue(itemCode, out int retailPrice) && retailPrice > 0)
        {
            double cap = Math.Floor(retailPrice * config.AntiArbitrageCapFactor);
            if (cap >= 1.0) maxAllowed = cap;
        }

        double economySellMult = config.GetSellPayoutMultiplier();
        double startSatMod = _saturation.EvaluateMultiplier(baseSat + extraSat, config);
        double startUnitPrice = Math.Min(maxAllowed, firstEval.BaseValue * firstEval.AffinityMod * firstEval.DemandMod * startSatMod * firstEval.ConditionMod * economySellMult);
        int maxStackSize = stack.Collectible.MaxStackSize;

        if (!TryComputeStandardizedLot(startUnitPrice, maxStackSize, out int currentLotSize, out int startLotPayout))
        {
            return new SellSlotValuationEntry
            {
                SellSlotIndex = sellSlotIndex,
                ItemCode = itemCode,
                DisplayName = displayName,
                QuantityInSlot = stack.StackSize,
                IsTradeable = false,
                IsOverstocked = startSatMod < 0.999
            };
        }

        int totalGears = 0;
        int consumedUnits = 0;
        int consumedLots = 0;
        int remaining = stack.StackSize;

        while (remaining > 0)
        {
            double satMod = _saturation.EvaluateMultiplier(baseSat + extraSat, config);
            double unitPrice = Math.Min(maxAllowed, firstEval.BaseValue * firstEval.AffinityMod * firstEval.DemandMod * satMod * firstEval.ConditionMod * economySellMult);
            if (!TryComputeStandardizedLot(unitPrice, maxStackSize, out int lotSize, out int lotPayout) || remaining < lotSize)
            {
                break;
            }

            totalGears += lotPayout;
            consumedUnits += lotSize;
            consumedLots++;
            remaining -= lotSize;
            extraSat += firstEval.BaseValue * lotSize;
        }

        if (simulatedAddedGev != null)
        {
            simulatedAddedGev[itemCode] = extraSat;
        }

        double effectiveUnit = consumedUnits > 0
            ? (double)totalGears / consumedUnits
            : (double)startLotPayout / currentLotSize;
        double effectiveDeltaPct = ((effectiveUnit - firstEval.BaseValue) / firstEval.BaseValue) * 100.0;

        return new SellSlotValuationEntry
        {
            SellSlotIndex = sellSlotIndex,
            ItemCode = itemCode,
            DisplayName = displayName,
            QuantityInSlot = stack.StackSize,
            MinLotSize = firstEval.MinLotSize,
            LotGearPayout = startLotPayout,
            ConsumedLots = consumedLots,
            TotalPayoutGears = totalGears,
            DeltaPct = effectiveDeltaPct,
            IsTradeable = true,
            BaseValue = firstEval.BaseValue,
            AffinityMod = firstEval.AffinityMod,
            DemandMod = firstEval.DemandMod,
            SaturationMod = startSatMod,
            ConsumedUnits = consumedUnits,
            CurrentLotSize = currentLotSize
        };
    }
}
