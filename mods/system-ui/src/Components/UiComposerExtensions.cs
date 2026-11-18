using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SystemUI;

/// <summary>
/// Helpers fluidos para componer tarjetas, pestanas, encabezados, grillas y texto enriquecido con iconos.
/// </summary>
public static class UiComposerExtensions
{
    public static GuiComposer AddTabStrip(
        this GuiComposer composer,
        ElementBounds bounds,
        GuiTab[] tabs,
        Action<int> onTabClicked,
        SystemUITheme theme,
        string key = "tabStrip")
    {
        return composer.AddHorizontalTabs(
            tabs,
            bounds,
            onTabClicked,
            theme.TabFont(false),
            theme.TabFont(true),
            key);
    }

    public static GuiComposer AddPanelCard(
        this GuiComposer composer,
        double x,
        double y,
        double width,
        double height,
        SystemUITheme theme)
    {
        return composer.AddInset(
            ElementBounds.Fixed(x, y, width, height),
            theme.CardDepth,
            theme.CardBrightness);
    }

    public static GuiComposer AddDynamicPanelCard(
        this GuiComposer composer,
        double x,
        double y,
        double width,
        double height,
        SystemUITheme theme,
        string? key = null)
    {
        if (!composer.Composed)
        {
            var panel = new UiDynamicPanelElement(
                composer.Api,
                ElementBounds.Fixed(x, y, width, height),
                theme.CardDepth,
                theme.CardBrightness);
            composer.AddInteractiveElement(panel, key);
        }
        return composer;
    }

    public static GuiComposer AddDynamicDivider(
        this GuiComposer composer,
        double x,
        double y,
        double width,
        SystemUITheme theme,
        string? key = null)
    {
        if (!composer.Composed)
        {
            double[] lineColor = [theme.AccentColor[0], theme.AccentColor[1], theme.AccentColor[2], 0.32];
            var divider = new UiDynamicDividerElement(
                composer.Api,
                ElementBounds.Fixed(x, y, width, 2),
                lineColor);
            composer.AddInteractiveElement(divider, key);
        }
        return composer;
    }

    public static GuiComposer AddSectionLabel(
        this GuiComposer composer,
        string text,
        ElementBounds bounds,
        SystemUITheme theme,
        string? key = null)
    {
        return composer.AddStaticText(text, theme.TitleFont(), bounds, key);
    }

    public static GuiComposer AddTrendBadge(
        this GuiComposer composer,
        double deltaPct,
        ElementBounds bounds,
        SystemUITheme theme,
        string? key = null)
    {
        string label = SystemUITheme.FormatTrend(deltaPct, spaced: true, includeNeutralPct: true);
        return composer.AddStaticText(label, theme.TrendFont(deltaPct), bounds, key);
    }

    public static GuiComposer AddActionButton(
        this GuiComposer composer,
        string text,
        ActionConsumable onClick,
        ElementBounds bounds,
        string? key = null)
    {
        return composer.AddSmallButton(text, onClick, bounds, EnumButtonStyle.Normal, key);
    }

    public static GuiComposer AddIconActionButton(
        this GuiComposer composer,
        string text,
        AssetLocation? iconAsset,
        ActionConsumable onClick,
        ElementBounds bounds,
        SystemUITheme theme,
        string? key = null,
        int iconSize = 16,
        bool enabled = true,
        double leftPadding = 12.0)
    {
        if (!composer.Composed)
        {
            int iconColor = ColorUtil.FromRGBADoubles(theme.NeutralColor);
            var button = new UiIconTextButton(
                composer.Api,
                text,
                iconAsset,
                CairoFont.SmallButtonText(),
                CairoFont.ButtonPressedText(),
                onClick,
                bounds,
                EnumButtonStyle.Normal,
                iconSize,
                iconColor,
                leftPadding);
            button.Enabled = enabled;
            composer.AddInteractiveElement(button, key);
        }
        return composer;
    }

    public static GuiComposer AddRichTextBlock(
        this GuiComposer composer,
        RichTextComponentBase[] components,
        ElementBounds bounds,
        string key)
    {
        return composer.AddRichtext(components, bounds, key);
    }

    public static void UpdateRichTextBlock(
        this GuiComposer? composer,
        string key,
        RichTextComponentBase[] components)
    {
        composer?.GetRichtext(key)?.SetNewText(components);
    }

    public static GuiComposer AddSlotGridSection(
        this GuiComposer composer,
        string title,
        IInventory inventory,
        Action<object> sendPacket,
        int columns,
        int rows,
        double x,
        double y,
        SystemUITheme theme,
        string gridKey,
        out ElementBounds gridBounds,
        double titleWidth = 0,
        Func<ICoreClientAPI, IInventory, Action<object>, int, ElementBounds, GuiElement>? customGridFactory = null,
        bool dynamicLabel = false)
    {
        double labelWidth = titleWidth > 0 ? titleWidth : Math.Max(columns * 51, 260);
        ElementBounds labelBounds = ElementBounds.Fixed(x, y, labelWidth, 22);
        if (dynamicLabel)
        {
            composer.AddRichtext(
                new UiRichTextBuilder(composer.Api, theme).Text(title, theme.TitleFont()).Build(),
                labelBounds,
                $"{gridKey}_title");
        }
        else
        {
            composer.AddSectionLabel(title, labelBounds, theme, $"{gridKey}_title");
        }

        gridBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y + 24, columns, rows);
        if (customGridFactory == null)
        {
            return composer.AddItemSlotGrid(inventory, sendPacket, columns, gridBounds, gridKey);
        }

        if (!composer.Composed)
        {
            GuiElement grid = customGridFactory(composer.Api, inventory, sendPacket, columns, gridBounds);
            composer.AddInteractiveElement(grid, gridKey);
            GuiElementItemSlotGridBase.UpdateLastSlotGridFlag(composer);
        }
        return composer;
    }
}
