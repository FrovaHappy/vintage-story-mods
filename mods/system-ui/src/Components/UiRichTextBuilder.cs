using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SystemUI;

/// <summary>
/// Constructor fluido para RichTextComponentBase[] que permite mezclar texto coloreado,
/// iconos de items reales (ej. engranaje oxidado) e iconos SVG en una misma linea.
/// </summary>
public sealed class UiRichTextBuilder
{
    private readonly ICoreClientAPI _capi;
    private readonly SystemUITheme _theme;
    private readonly List<RichTextComponentBase> _components = [];

    public UiRichTextBuilder(ICoreClientAPI capi, SystemUITheme theme)
    {
        _capi = capi;
        _theme = theme;
    }

    public UiRichTextBuilder Text(string text, CairoFont? font = null)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _components.Add(new RichTextComponent(_capi, text, font ?? _theme.DetailFont()));
        }
        return this;
    }

    public UiRichTextBuilder Bold(string text, bool detail = true) =>
        Text(text, _theme.Font(_theme.NeutralColor, detail, bold: true));

    public UiRichTextBuilder Positive(string text, bool detail = true, bool bold = true) =>
        Text(text, _theme.PositiveFont(detail, bold));

    public UiRichTextBuilder Negative(string text, bool detail = true, bool bold = true) =>
        Text(text, _theme.NegativeFont(detail, bold));

    public UiRichTextBuilder Warning(string text, bool detail = true, bool bold = true) =>
        Text(text, _theme.WarningFont(detail, bold));

    public UiRichTextBuilder Highlight(string text, bool detail = true, bool bold = true) =>
        Text(text, _theme.HighlightFont(detail, bold));

    public UiRichTextBuilder Muted(string text, bool detail = true) =>
        Text(text, _theme.MutedFont(detail));

    public UiRichTextBuilder Color(string text, double[] rgba, bool detail = true, bool bold = false) =>
        Text(text, _theme.Font(rgba, detail, bold));

    public UiRichTextBuilder Trend(double deltaPct, bool detail = true) =>
        Text(SystemUITheme.FormatTrend(deltaPct, spaced: false, includeNeutralPct: true), _theme.TrendFont(deltaPct, detail));

    public UiRichTextBuilder ItemIcon(ItemStack? stack, double size = 20, double rightPadding = 4)
    {
        if (stack?.Collectible != null)
        {
            _components.Add(new ItemstackTextComponent(_capi, stack, size, rightPadding, EnumFloat.Inline, null));
        }
        return this;
    }

    public UiRichTextBuilder VectorIcon(string iconName, string iconPath, CairoFont? font = null)
    {
        _components.Add(new IconComponent(_capi, iconName, iconPath, font ?? _theme.DetailFont()));
        return this;
    }

    public UiRichTextBuilder NewLine() => Text("\n");

    public RichTextComponentBase[] Build() => _components.ToArray();
}
