using System.Globalization;
using Vintagestory.API.Client;

namespace SystemUI;

/// <summary>
/// Paleta, tipografias y metricas estandar compartidas por las ventanas construidas sobre System UI.
/// </summary>
public sealed class SystemUITheme
{
    public double DialogPadding => GuiStyle.ElementToDialogPadding;
    public double SectionGap { get; init; } = 10;
    public double RowHeight { get; init; } = 28;
    public double TabHeight { get; init; } = 28;
    public float PanelAlpha { get; init; } = 0.94f;
    public int CardDepth { get; init; } = 2;
    public float CardBrightness { get; init; } = 0.85f;

    public double[] PositiveColor { get; init; } = [0.45, 0.88, 0.45, 1.0];
    public double[] NegativeColor { get; init; } = [0.94, 0.42, 0.36, 1.0];
    public double[] WarningColor { get; init; } = [0.96, 0.78, 0.32, 1.0];
    public double[] HighlightColor { get; init; } = [0.98, 0.87, 0.48, 1.0];
    public double[] AccentColor { get; init; } = [0.85, 0.64, 0.36, 1.0];
    public double[] NeutralColor { get; init; } = [0.90, 0.90, 0.88, 1.0];
    public double[] MutedColor { get; init; } = [0.70, 0.70, 0.68, 1.0];

    public CairoFont TitleFont() => CairoFont.WhiteSmallishText().WithWeight(Cairo.FontWeight.Bold);
    public CairoFont BodyFont() => CairoFont.WhiteSmallText();
    public CairoFont DetailFont() => CairoFont.WhiteDetailText();

    public CairoFont Font(double[] color, bool detail = false, bool bold = false)
    {
        CairoFont font = (detail ? CairoFont.WhiteDetailText() : CairoFont.WhiteSmallText()).WithColor(color);
        return bold ? font.WithWeight(Cairo.FontWeight.Bold) : font;
    }

    public CairoFont PositiveFont(bool detail = false, bool bold = false) => Font(PositiveColor, detail, bold);
    public CairoFont NegativeFont(bool detail = false, bool bold = false) => Font(NegativeColor, detail, bold);
    public CairoFont WarningFont(bool detail = false, bool bold = false) => Font(WarningColor, detail, bold);
    public CairoFont HighlightFont(bool detail = false, bool bold = true) => Font(HighlightColor, detail, bold);
    public CairoFont MutedFont(bool detail = true) => Font(MutedColor, detail, false);

    public CairoFont TrendFont(double deltaPct, bool detail = true)
    {
        double[] color = deltaPct > 0.5
            ? PositiveColor
            : deltaPct < -0.5
                ? NegativeColor
                : NeutralColor;

        return Font(color, detail, true);
    }

    public CairoFont TabFont(bool selected)
    {
        return selected
            ? CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold)
            : CairoFont.WhiteSmallText().WithColor(MutedColor);
    }

    public static string FormatTrend(double deltaPct, bool spaced = false, bool includeNeutralPct = true)
    {
        string sep = spaced ? " " : string.Empty;
        if (deltaPct > 0.5)
        {
            return $"▲{sep}+{deltaPct.ToString("0.#", CultureInfo.InvariantCulture)}%";
        }
        if (deltaPct < -0.5)
        {
            return $"▼{sep}{deltaPct.ToString("0.#", CultureInfo.InvariantCulture)}%";
        }
        return includeNeutralPct
            ? $"={sep}{deltaPct.ToString("0.#", CultureInfo.InvariantCulture)}%"
            : "=";
    }
}
