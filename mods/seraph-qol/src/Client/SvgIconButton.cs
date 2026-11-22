using System.Reflection;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SeraphQoL.Client;

/// <summary>
/// Clase base para botones SVG de barra de titulo con resaltado y textura de tooltip cacheada.
/// </summary>
public abstract class SvgToolbarButton : GuiElement
{
    protected static readonly int HoverColor = ColorUtil.ColorFromRgba(255, 255, 255, 255);
    protected static readonly int ShadowColor = ColorUtil.ColorFromRgba(0, 0, 0, 140);

    protected readonly AssetLocation SvgAsset;
    private readonly GuiElementDialogTitleBar? _ownerTitleBar;
    private string _tooltipText;

    protected LoadedTexture NormalTexture;
    protected LoadedTexture HoverTexture;
    protected LoadedTexture ShadowTexture;
    protected LoadedTexture HoverBgTexture;
    private LoadedTexture _tooltipTexture;

    protected SvgToolbarButton(
        ICoreClientAPI capi,
        AssetLocation svgAsset,
        string tooltipText,
        ElementBounds bounds,
        GuiElementDialogTitleBar? ownerTitleBar) : base(capi, bounds)
    {
        SvgAsset = svgAsset;
        _tooltipText = tooltipText;
        _ownerTitleBar = ownerTitleBar;
        NormalTexture = new LoadedTexture(capi);
        HoverTexture = new LoadedTexture(capi);
        ShadowTexture = new LoadedTexture(capi);
        HoverBgTexture = new LoadedTexture(capi);
        _tooltipTexture = new LoadedTexture(capi);
    }

    public bool IsHovered => Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
    internal LoadedTexture TooltipTexture => _tooltipTexture;

    public void UpdateTooltip(string tooltipText)
    {
        if (string.Equals(_tooltipText, tooltipText, StringComparison.Ordinal)) return;
        _tooltipText = tooltipText;
        SvgTextureHelper.BuildTooltipCard(api, _tooltipText, ref _tooltipTexture);
    }

    protected void CancelTitleBarDrag() => SvgTextureHelper.CancelTitleBarDrag(_ownerTitleBar);

    protected abstract int GetNormalColor();
    protected virtual bool ShouldDrawActiveBackdrop => false;
    protected virtual LoadedTexture? GetCustomBackdropTexture() => null;
    protected virtual LoadedTexture GetIconTexture(bool hovered) => hovered ? HoverTexture : NormalTexture;
    protected virtual void ComposeCustomTextures(int size) { }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        base.ComposeElements(ctx, surface);
        int size = Math.Max(8, (int)Bounds.InnerWidth);
        SvgTextureHelper.LoadOrReplace(api, SvgAsset, size, ShadowColor, ref ShadowTexture);
        SvgTextureHelper.LoadOrReplace(api, SvgAsset, size, GetNormalColor(), ref NormalTexture);
        SvgTextureHelper.LoadOrReplace(api, SvgAsset, size, HoverColor, ref HoverTexture);
        SvgTextureHelper.BuildHoverBackdrop(api, size, false, ref HoverBgTexture);
        ComposeCustomTextures(size);
        SvgTextureHelper.BuildTooltipCard(api, _tooltipText, ref _tooltipTexture);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);
        bool hovered = IsHovered;

        LoadedTexture? bgTex = ShouldDrawActiveBackdrop ? GetCustomBackdropTexture() : HoverBgTexture;
        if ((hovered || ShouldDrawActiveBackdrop) && bgTex is { TextureId: > 0 })
        {
            double pad = scaled(2.0);
            api.Render.Render2DTexturePremultipliedAlpha(
                bgTex.TextureId,
                (float)(Bounds.absX - pad),
                (float)(Bounds.absY - pad),
                (float)(Bounds.InnerWidth + pad * 2.0),
                (float)(Bounds.InnerHeight + pad * 2.0),
                48f);
        }

        if (ShadowTexture.TextureId > 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(
                ShadowTexture.TextureId,
                (float)Bounds.absX + 1f,
                (float)Bounds.absY + 1f,
                (float)Bounds.InnerWidth,
                (float)Bounds.InnerHeight,
                49f);
        }

        LoadedTexture active = GetIconTexture(hovered);
        if (active.TextureId > 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(
                active.TextureId,
                (float)Bounds.absX,
                (float)Bounds.absY,
                (float)Bounds.InnerWidth,
                (float)Bounds.InnerHeight,
                50f);
        }
    }

    public override void Dispose()
    {
        NormalTexture.Dispose();
        HoverTexture.Dispose();
        ShadowTexture.Dispose();
        HoverBgTexture.Dispose();
        _tooltipTexture.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Boton SVG de accion simple con soporte opcional de rueda del raton para alternar criterios de ordenamiento.
/// </summary>
public sealed class SvgIconButton : SvgToolbarButton
{
    private static readonly int DefaultNormalColor = ColorUtil.ColorFromRgba(235, 225, 200, 235);

    private readonly ActionConsumable _onClick;
    private readonly Action<int>? _onWheel;

    public SvgIconButton(
        ICoreClientAPI capi,
        AssetLocation svgAsset,
        string tooltipText,
        ActionConsumable onClick,
        ElementBounds bounds,
        Action<int>? onWheel = null,
        GuiElementDialogTitleBar? ownerTitleBar = null)
        : base(capi, svgAsset, tooltipText, bounds, ownerTitleBar)
    {
        _onClick = onClick;
        _onWheel = onWheel;
    }

    protected override int GetNormalColor() => DefaultNormalColor;

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseDownOnElement(capi, args);
        CancelTitleBarDrag();
        if (_onClick())
        {
            capi.Gui.PlaySound("tick");
            args.Handled = true;
        }
    }

    public override void OnMouseWheel(ICoreClientAPI capi, MouseWheelEventArgs args)
    {
        if (_onWheel != null && IsHovered)
        {
            _onWheel(args.delta > 0 ? -1 : 1);
            capi.Gui.PlaySound("tick");
            args.SetHandled(true);
            return;
        }

        base.OnMouseWheel(capi, args);
    }
}

/// <summary>
/// Boton SVG con estado activo/inactivo para modos Favorito, Bloqueo de Slot y Fusion de Perecederos.
/// </summary>
public sealed class SvgIconToggleButton : SvgToolbarButton
{
    private static readonly int ToggleNormalColor = ColorUtil.ColorFromRgba(210, 200, 180, 210);

    private readonly int _activeColor;
    private readonly Action<bool> _onToggle;

    private LoadedTexture _activeTexture;
    private LoadedTexture _activeBgTexture;

    public bool IsActive { get; set; }

    public SvgIconToggleButton(
        ICoreClientAPI capi,
        AssetLocation svgAsset,
        int activeColor,
        bool initialState,
        string tooltipText,
        Action<bool> onToggle,
        ElementBounds bounds,
        GuiElementDialogTitleBar? ownerTitleBar = null)
        : base(capi, svgAsset, tooltipText, bounds, ownerTitleBar)
    {
        _activeColor = activeColor;
        IsActive = initialState;
        _onToggle = onToggle;
        _activeTexture = new LoadedTexture(capi);
        _activeBgTexture = new LoadedTexture(capi);
    }

    protected override int GetNormalColor() => ToggleNormalColor;
    protected override bool ShouldDrawActiveBackdrop => IsActive;
    protected override LoadedTexture? GetCustomBackdropTexture() => _activeBgTexture;
    protected override LoadedTexture GetIconTexture(bool hovered) => IsActive ? _activeTexture : base.GetIconTexture(hovered);

    protected override void ComposeCustomTextures(int size)
    {
        SvgTextureHelper.LoadOrReplace(api, SvgAsset, size, _activeColor, ref _activeTexture);
        SvgTextureHelper.BuildHoverBackdrop(api, size, true, ref _activeBgTexture);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseDownOnElement(capi, args);
        CancelTitleBarDrag();
        IsActive = !IsActive;
        _onToggle(IsActive);
        capi.Gui.PlaySound("toggleswitch");
        args.Handled = true;
    }

    public override void Dispose()
    {
        _activeTexture.Dispose();
        _activeBgTexture.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Capa de renderizado tardio dentro de la fase GUI activa (shader 'gui') para dibujar el tooltip sobre los botones.
/// </summary>
public sealed class SvgTooltipLayerElement : GuiElement
{
    private readonly IReadOnlyList<SvgToolbarButton> _buttons;

    public override double DrawOrder => 0.95;

    public SvgTooltipLayerElement(
        ICoreClientAPI capi,
        IReadOnlyList<SvgToolbarButton> buttons,
        ElementBounds bounds) : base(capi, bounds)
    {
        _buttons = buttons;
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        foreach (SvgToolbarButton button in _buttons)
        {
            if (button.IsHovered)
            {
                SvgTextureHelper.RenderTooltipAbove(api, button.Bounds, button.TooltipTexture);
                break;
            }
        }
    }
}

internal static class SvgTextureHelper
{
    private static readonly FieldInfo? TitleBarMovingField =
        AccessTools.Field(typeof(GuiElementDialogTitleBar), "moving");

    public static void CancelTitleBarDrag(GuiElementDialogTitleBar? titleBar)
    {
        if (titleBar == null) return;
        TitleBarMovingField?.SetValue(titleBar, false);
    }

    public static void LoadOrReplace(
        ICoreClientAPI api,
        AssetLocation svgAsset,
        int size,
        int? color,
        ref LoadedTexture target)
    {
        if (api.Assets.TryGet(svgAsset) == null) return;
        LoadedTexture? loaded = api.Gui.LoadSvg(svgAsset, size, size, size, size, color);
        if (loaded == null) return;
        target.Dispose();
        target = loaded;
    }

    public static void BuildHoverBackdrop(
        ICoreClientAPI api,
        int iconSize,
        bool isActive,
        ref LoadedTexture target)
    {
        int totalSize = Math.Max(12, iconSize + (int)GuiElement.scaled(4.0));
        using var surface = new ImageSurface(Format.Argb32, totalSize, totalSize);
        using var ctx = new Context(surface);
        GuiElement.RoundRectangle(ctx, 0.5, 0.5, totalSize - 1.0, totalSize - 1.0, GuiElement.scaled(3.0));
        ctx.SetSourceRGBA(1.0, 1.0, 1.0, isActive ? 0.20 : 0.14);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(1.0, 1.0, 1.0, isActive ? 0.45 : 0.30);
        ctx.LineWidth = Math.Max(1.0, GuiElement.scaled(1.0));
        ctx.Stroke();
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref target);
    }

    public static void BuildTooltipCard(ICoreClientAPI api, string text, ref LoadedTexture target)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string[] lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        CairoFont font = CairoFont.WhiteSmallText().WithFontSize(13f);
        double padX = GuiElement.scaled(8.0);
        double padY = GuiElement.scaled(5.0);
        double lineGap = GuiElement.scaled(3.0);

        double maxTextWidth = 0.0;
        double singleLineHeight = 0.0;
        foreach (string line in lines)
        {
            TextExtents ext = font.GetTextExtents(line);
            maxTextWidth = Math.Max(maxTextWidth, ext.Width);
            singleLineHeight = Math.Max(singleLineHeight, ext.Height);
        }

        singleLineHeight = Math.Max(singleLineHeight, GuiElement.scaled(13.0));
        int width = Math.Max(40, (int)Math.Ceiling(maxTextWidth + padX * 2.0 + GuiElement.scaled(4.0)));
        int height = Math.Max(22, (int)Math.Ceiling((singleLineHeight * lines.Length) + (lineGap * Math.Max(0, lines.Length - 1)) + padY * 2.0));

        using var surface = new ImageSurface(Format.Argb32, width, height);
        using var ctx = new Context(surface);

        GuiElement.RoundRectangle(ctx, 0.5, 0.5, width - 1.0, height - 1.0, GuiElement.scaled(4.0));
        ctx.SetSourceRGBA(0.08, 0.07, 0.06, 0.94);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.75, 0.64, 0.45, 0.85);
        ctx.LineWidth = Math.Max(1.0, GuiElement.scaled(1.0));
        ctx.Stroke();

        font.SetupContext(ctx);
        ctx.SetSourceRGBA(0.96, 0.94, 0.88, 1.0);
        double baselineY = padY + singleLineHeight * 0.85;
        foreach (string line in lines)
        {
            ctx.MoveTo(padX, baselineY);
            ctx.ShowText(line);
            baselineY += singleLineHeight + lineGap;
        }

        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref target);
    }

    public static void RenderTooltipAbove(ICoreClientAPI api, ElementBounds buttonBounds, LoadedTexture tooltipTex)
    {
        if (tooltipTex.TextureId <= 0) return;

        float margin = (float)GuiElement.scaled(6.0);
        float maxW = Math.Max(margin, api.Render.FrameWidth - tooltipTex.Width - margin);
        float x = Math.Clamp(
            (float)(buttonBounds.absX + buttonBounds.InnerWidth * 0.5 - tooltipTex.Width * 0.5),
            margin,
            maxW);

        float y = (float)(buttonBounds.absY - tooltipTex.Height - margin);
        if (y < margin)
        {
            y = (float)(buttonBounds.absY + buttonBounds.InnerHeight + margin);
        }

        api.Render.Render2DTexturePremultipliedAlpha(tooltipTex.TextureId, x, y, tooltipTex.Width, tooltipTex.Height, 500f);
    }
}
