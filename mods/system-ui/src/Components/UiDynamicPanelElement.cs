using Cairo;
using Vintagestory.API.Client;

namespace SystemUI;

/// <summary>
/// Tarjeta/panel con relieve dibujada como textura dinamica pasiva para soportar scroll con recorte.
/// </summary>
public sealed class UiDynamicPanelElement : UiPassiveDynamicElement
{
    private readonly int _depth;
    private readonly float _brightness;

    public UiDynamicPanelElement(ICoreClientAPI capi, ElementBounds bounds, int depth, float brightness)
        : base(capi, bounds)
    {
        _depth = depth;
        _brightness = brightness;
    }

    protected override void DrawDynamicSurface(Context ctx, int width, int height)
    {
        double radius = GuiStyle.ElementBGRadius;
        RoundRectangle(ctx, 0, 0, width, height, radius);
        ctx.SetSourceRGBA(
            GuiStyle.DialogDefaultBgColor[0] * _brightness,
            GuiStyle.DialogDefaultBgColor[1] * _brightness,
            GuiStyle.DialogDefaultBgColor[2] * _brightness,
            GuiStyle.DialogDefaultBgColor[3]);
        ctx.Fill();

        EmbossRoundRectangleElement(ctx, 0, 0, width, height, true, _depth, (int)radius);
    }
}
