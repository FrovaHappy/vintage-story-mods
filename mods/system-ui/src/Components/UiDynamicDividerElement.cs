using Cairo;
using Vintagestory.API.Client;

namespace SystemUI;

/// <summary>
/// Separador horizontal sutil dibujado como textura dinamica pasiva para listas desplazables.
/// </summary>
public sealed class UiDynamicDividerElement : UiPassiveDynamicElement
{
    private readonly double[] _lineColor;

    public UiDynamicDividerElement(ICoreClientAPI capi, ElementBounds bounds, double[]? lineColor = null)
        : base(capi, bounds)
    {
        _lineColor = lineColor ?? [0.85, 0.64, 0.36, 0.35];
    }

    protected override void DrawDynamicSurface(Context ctx, int width, int height)
    {
        double lineY = Math.Floor(height / 2.0);
        ctx.SetSourceRGBA(0.0, 0.0, 0.0, 0.45);
        ctx.Rectangle(0, Math.Max(0, lineY - 1), width, 1);
        ctx.Fill();

        ctx.SetSourceRGBA(_lineColor[0], _lineColor[1], _lineColor[2], _lineColor[3]);
        ctx.Rectangle(0, lineY, width, 1);
        ctx.Fill();
    }
}
