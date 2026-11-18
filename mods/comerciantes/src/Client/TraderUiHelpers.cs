using SystemUI;
using Vintagestory.API.Common;

namespace Comerciantes;

/// <summary>
/// Utilidades visuales y de formateo compartidas entre las ventanas de comercio y directorio.
/// </summary>
internal static class TraderUiHelpers
{
    public static ItemStack? ResolveStack(IWorldAccessor world, byte[]? stackBytes, string itemCode, int stackSize)
    {
        if (stackBytes != null && stackBytes.Length > 0)
        {
            ItemStack stack = new(stackBytes);
            if (stack.ResolveBlockOrItem(world))
            {
                stack.StackSize = Math.Max(1, stackSize);
                return stack;
            }
        }

        if (string.IsNullOrEmpty(itemCode)) return null;
        AssetLocation loc = new(itemCode);
        CollectibleObject? obj = (CollectibleObject?)world.GetItem(loc) ?? world.GetBlock(loc);
        return obj != null ? new ItemStack(obj, Math.Max(1, stackSize)) : null;
    }

    public static string FormatOfferTooltip(
        StockOfferEntry offer,
        string priceKey,
        string stockKey,
        string outOfStockKey)
    {
        string stockLine = offer.Stock > 0
            ? ComerciantesLang.Get(stockKey, offer.Stock)
            : ComerciantesLang.Get(outOfStockKey);
        return ComerciantesLang.Get(priceKey, offer.PriceGears, offer.StackSize, stockLine);
    }

    public static string FormatWantedTooltip(WantedItemEntry wanted, string tooltipKey)
    {
        string arrow = SystemUITheme.FormatTrend(wanted.DeltaPct, spaced: true, includeNeutralPct: false);
        string lot = wanted.MinLotSize > 1
            ? ComerciantesLang.Get("tooltip-wanted-lot-multi", wanted.MinLotSize, wanted.LotGearPayout)
            : ComerciantesLang.Get("tooltip-wanted-lot-single", wanted.LotGearPayout);
        return ComerciantesLang.Get(tooltipKey, lot, arrow, $"{wanted.SaturationPct:0}");
    }

    public static string FormatRotationRemaining(double daysLeft)
    {
        double clamped = Math.Max(0.0, daysLeft);
        int days = (int)Math.Floor(clamped);
        int hours = (int)Math.Round((clamped - days) * 24.0, MidpointRounding.AwayFromZero);
        if (hours >= 24)
        {
            days++;
            hours = 0;
        }
        return days > 0 ? $"{days}d {hours}h" : $"{hours}h";
    }

    public static string FormatLastSeenAgo(double currentTotalDays, double lastSeenTotalDays)
    {
        double elapsed = Math.Max(0.0, currentTotalDays - lastSeenTotalDays);
        if (elapsed < 1.0)
        {
            int hours = (int)Math.Floor(elapsed * 24.0);
            return hours <= 0
                ? ComerciantesLang.Get("gui-dir-seen-just-now")
                : ComerciantesLang.Get("gui-dir-seen-hours", hours);
        }

        int days = (int)Math.Floor(elapsed);
        return ComerciantesLang.Get("gui-dir-seen-days", days);
    }
}
