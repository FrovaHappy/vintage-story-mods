using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SystemUI;

/// <summary>
/// Boton de accion de System UI con soporte opcional para un icono SVG rasterizado a la izquierda del texto.
/// </summary>
public sealed class UiIconTextButton : GuiElementTextButton
{
    private const float IconRenderZ = 52f;

    private readonly AssetLocation? _iconAsset;
    private readonly int _iconSize;
    private readonly int _iconColor;
    private readonly double _leftPadding;
    private LoadedTexture? _iconTexture;

    public UiIconTextButton(
        ICoreClientAPI capi,
        string text,
        AssetLocation? iconAsset,
        CairoFont font,
        CairoFont hoverFont,
        ActionConsumable onClick,
        ElementBounds bounds,
        EnumButtonStyle style = EnumButtonStyle.Normal,
        int iconSize = 16,
        int iconColor = -1,
        double leftPadding = 12.0)
        : base(capi, text, font, hoverFont, onClick, bounds, style)
    {
        _iconAsset = iconAsset;
        _iconSize = Math.Max(8, iconSize);
        _iconColor = iconColor;
        _leftPadding = leftPadding;
    }

    public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
    {
        base.ComposeElements(ctxStatic, surfaceStatic);

        _iconTexture?.Dispose();
        _iconTexture = null;

        if (_iconAsset != null && api.Assets.TryGet(_iconAsset) != null)
        {
            int scaledPx = (int)Math.Round(scaled(_iconSize));
            _iconTexture = api.Gui.LoadSvg(_iconAsset, scaledPx, scaledPx, scaledPx, scaledPx, _iconColor);
        }
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);

        if (Visible && _iconTexture is { TextureId: > 0 })
        {
            float x = (float)(Bounds.renderX + scaled(_leftPadding));
            float y = (float)(Bounds.renderY + (Bounds.InnerHeight - _iconTexture.Height) / 2.0);
            Vec4f? tint = Enabled ? null : new Vec4f(1f, 1f, 1f, 0.45f);
            api.Render.Render2DTexturePremultipliedAlpha(
                _iconTexture.TextureId,
                x,
                y,
                _iconTexture.Width,
                _iconTexture.Height,
                IconRenderZ,
                tint);
        }
    }

    public override void Dispose()
    {
        _iconTexture?.Dispose();
        _iconTexture = null;
        base.Dispose();
    }
}
