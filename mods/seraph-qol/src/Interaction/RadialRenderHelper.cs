using Cairo;
using Vintagestory.API.Client;

namespace SeraphQoL.Interaction;

/// <summary>
/// Helper compartido de renderizado radial Cairo y cache de etiquetas para menus circulares.
/// </summary>
public sealed class RadialRenderHelper : IDisposable
{
    private readonly ICoreClientAPI _capi;
    private readonly Dictionary<string, LoadedTexture> _labelCache = new(StringComparer.Ordinal);
    private LoadedTexture _ringTexture;

    public RadialRenderHelper(ICoreClientAPI capi)
    {
        _capi = capi;
        _ringTexture = new LoadedTexture(capi);
    }

    public int TextureId => _ringTexture.TextureId;
    public int Width => _ringTexture.Width;
    public int Height => _ringTexture.Height;

    public void RebuildRingTexture(
        int outerRadius,
        RadialGeometryConfig geometry,
        IReadOnlyList<RadialToolModeSector> sectors,
        int hoveredSectorIndex)
    {
        int padding = 14;
        int texSize = Math.Max(64, (outerRadius + padding) * 2);
        double cx = texSize * 0.5;
        double cy = texSize * 0.5;

        using var surface = new ImageSurface(Format.Argb32, texSize, texSize);
        using var ctx = new Context(surface);
        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;

        double frameOuterR = geometry.OuterRadius + 5.0;
        double frameInnerR = Math.Max(12.0, geometry.InnerRadius - 4.0);
        double wedgeOuterR = geometry.OuterRadius - 2.0;
        double wedgeInnerR = Math.Min(wedgeOuterR - 4.0, geometry.InnerRadius + 2.0);

        // 1. Dark recessed backing disc + outer bronze bezel ring (Vintage Story frame aesthetic)
        ctx.NewPath();
        ctx.Arc(cx, cy, frameOuterR, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0.10, 0.075, 0.055, 0.90);
        ctx.Fill();

        // Outer beveled rim: dark outer shadow + warm bronze highlight
        ctx.NewPath();
        ctx.Arc(cx, cy, frameOuterR, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0.07, 0.05, 0.04, 0.95);
        ctx.LineWidth = 2.4;
        ctx.Stroke();

        ctx.NewPath();
        ctx.Arc(cx, cy, frameOuterR - 2.0, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0.62, 0.50, 0.36, 0.92);
        ctx.LineWidth = 1.8;
        ctx.Stroke();

        // 2. Individual framed sector wedges separated by uniform parallel dark channels
        const double halfChannelPx = 3.2;
        bool singleSector = sectors.Count <= 1;

        foreach (RadialToolModeSector sector in sectors)
        {
            bool hovered = sector.SectorIndex == hoveredSectorIndex;

            if (singleSector)
            {
                TraceAnnularRing(ctx, cx, cy, wedgeOuterR, wedgeInnerR);
            }
            else
            {
                double outerDelta = Math.Asin(Math.Min(0.45, halfChannelPx / Math.Max(1.0, wedgeOuterR)));
                double innerDelta = Math.Asin(Math.Min(0.45, halfChannelPx / Math.Max(1.0, wedgeInnerR)));

                double sOuter = sector.StartAngleRad + outerDelta;
                double eOuter = sector.EndAngleRad - outerDelta;
                double sInner = sector.StartAngleRad + innerDelta;
                double eInner = sector.EndAngleRad - innerDelta;

                if (eInner <= sInner)
                {
                    sInner = sector.MidAngleRad - 0.02;
                    eInner = sector.MidAngleRad + 0.02;
                }

                TraceGappedWedge(ctx, cx, cy, wedgeOuterR, wedgeInnerR, sOuter, eOuter, sInner, eInner);
            }

            if (!sector.IsEnabled)
            {
                ctx.SetSourceRGBA(0.13, 0.10, 0.08, 0.78);
            }
            else if (hovered)
            {
                ctx.SetSourceRGBA(0.44, 0.32, 0.21, 0.96);
            }
            else if (sector.IsSelected)
            {
                ctx.SetSourceRGBA(0.31, 0.23, 0.16, 0.94);
            }
            else
            {
                ctx.SetSourceRGBA(0.19, 0.15, 0.11, 0.92);
            }
            ctx.FillPreserve();

            if (!sector.IsEnabled)
            {
                ctx.SetSourceRGBA(0.40, 0.33, 0.25, 0.65);
                ctx.LineWidth = 1.6;
            }
            else if (hovered)
            {
                ctx.SetSourceRGBA(0.95, 0.83, 0.60, 1.0);
                ctx.LineWidth = 2.6;
            }
            else if (sector.IsSelected)
            {
                ctx.SetSourceRGBA(0.84, 0.69, 0.48, 0.98);
                ctx.LineWidth = 2.2;
            }
            else
            {
                ctx.SetSourceRGBA(0.65, 0.53, 0.38, 0.92);
                ctx.LineWidth = 1.8;
            }
            ctx.Stroke();

            // Subtle inner dark bevel edge inside each wedge for carved depth
            if (!singleSector && wedgeOuterR - wedgeInnerR > 12.0)
            {
                double insetOuterR = wedgeOuterR - 2.2;
                double insetInnerR = wedgeInnerR + 2.2;
                double insetHalfGap = halfChannelPx + 2.0;
                double oD = Math.Asin(Math.Min(0.45, insetHalfGap / Math.Max(1.0, insetOuterR)));
                double iD = Math.Asin(Math.Min(0.45, insetHalfGap / Math.Max(1.0, insetInnerR)));

                if (sector.EndAngleRad - iD > sector.StartAngleRad + iD)
                {
                    TraceGappedWedge(
                        ctx, cx, cy, insetOuterR, insetInnerR,
                        sector.StartAngleRad + oD, sector.EndAngleRad - oD,
                        sector.StartAngleRad + iD, sector.EndAngleRad - iD);
                    ctx.SetSourceRGBA(hovered ? 0.98 : 0.08, hovered ? 0.88 : 0.06, hovered ? 0.66 : 0.05, hovered ? 0.28 : 0.55);
                    ctx.LineWidth = 1.0;
                    ctx.Stroke();
                }
            }
        }

        // 3. Clean espresso center hub framed by a warm bronze inner ring
        ctx.NewPath();
        ctx.Arc(cx, cy, frameInnerR, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0.14, 0.105, 0.08, 0.92);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.62, 0.50, 0.36, 0.92);
        ctx.LineWidth = 1.8;
        ctx.Stroke();

        ctx.NewPath();
        ctx.Arc(cx, cy, Math.Max(6.0, frameInnerR - 2.2), 0, Math.PI * 2);
        ctx.SetSourceRGBA(0.08, 0.06, 0.045, 0.70);
        ctx.LineWidth = 1.2;
        ctx.Stroke();

        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref _ringTexture);
    }

    private static void TraceAnnularRing(Context ctx, double cx, double cy, double outerR, double innerR)
    {
        ctx.NewPath();
        ctx.Arc(cx, cy, outerR, 0, Math.PI * 2);
        ctx.NewSubPath();
        ctx.ArcNegative(cx, cy, innerR, Math.PI * 2, 0);
        ctx.ClosePath();
    }

    private static void TraceGappedWedge(
        Context ctx,
        double cx,
        double cy,
        double outerR,
        double innerR,
        double startOuterAngle,
        double endOuterAngle,
        double startInnerAngle,
        double endInnerAngle)
    {
        ctx.NewPath();
        ctx.Arc(cx, cy, outerR, startOuterAngle, endOuterAngle);
        ctx.ArcNegative(cx, cy, innerR, endInnerAngle, startInnerAngle);
        ctx.ClosePath();
    }

    public void RenderRing(double centerX, double centerY)
    {
        if (_ringTexture.TextureId <= 0) return;

        float size = _ringTexture.Width;
        _capi.Render.Render2DTexturePremultipliedAlpha(
            _ringTexture.TextureId,
            (float)(centerX - (size * 0.5)),
            (float)(centerY - (size * 0.5)),
            size,
            size,
            50f);
    }

    public void RenderCenterLabel(
        RadialMenuPage page,
        RadialGeometryConfig geometry,
        int hoveredSectorIndex,
        double centerX,
        double centerY)
    {
        RadialToolModeSector? target = hoveredSectorIndex >= 0 && hoveredSectorIndex < page.Sectors.Count
            ? page.Sectors[hoveredSectorIndex]
            : page.Sectors.FirstOrDefault(s => s.IsSelected);

        if (target == null) return;

        string title = string.IsNullOrWhiteSpace(target.Name) ? target.Code : target.Name;
        if (page.TotalPages > 1)
        {
            title = $"{title} ({page.PageIndex + 1}/{page.TotalPages})";
        }

        LoadedTexture titleTex = GetOrCreateLabelTexture(title);
        if (titleTex.TextureId > 0)
        {
            _capi.Render.Render2DTexturePremultipliedAlpha(
                titleTex.TextureId,
                (float)(centerX - (titleTex.Width * 0.5)),
                (float)(centerY + geometry.OuterRadius + GuiElement.scaled(14.0)),
                titleTex.Width,
                titleTex.Height,
                62f);
        }
    }

    public static bool IsHotkeyDown(ICoreClientAPI capi, string hotkeyCode)
    {
        if (!capi.Input.HotKeys.TryGetValue(hotkeyCode, out HotKey? hk) || hk?.CurrentMapping == null)
        {
            return false;
        }

        KeyCombination map = hk.CurrentMapping;
        bool[] keys = capi.Input.KeyboardKeyState;
        int code = map.KeyCode;
        if (code <= 0 || code >= keys.Length || !keys[code])
        {
            return false;
        }

        if (map.SecondKeyCode is int second && second > 0 && second < keys.Length && !keys[second])
        {
            return false;
        }

        if (map.Alt && !IsAnyKeyDown(keys, GlKeys.LAlt, GlKeys.RAlt)) return false;
        if (map.Ctrl && !IsAnyKeyDown(keys, GlKeys.LControl, GlKeys.RControl)) return false;
        if (map.Shift && !IsAnyKeyDown(keys, GlKeys.LShift, GlKeys.RShift)) return false;

        return true;
    }

    private static bool IsAnyKeyDown(bool[] keys, GlKeys left, GlKeys right)
    {
        int l = (int)left;
        int r = (int)right;
        return (l >= 0 && l < keys.Length && keys[l]) || (r >= 0 && r < keys.Length && keys[r]);
    }

    private LoadedTexture GetOrCreateLabelTexture(string text)
    {
        if (_labelCache.TryGetValue(text, out LoadedTexture? existing) && existing.TextureId > 0)
        {
            return existing;
        }

        if (_labelCache.Count >= 48)
        {
            foreach (LoadedTexture tex in _labelCache.Values) tex.Dispose();
            _labelCache.Clear();
        }

        var font = CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold);
        LoadedTexture created = _capi.Gui.TextTexture.GenTextTexture(text, font, new TextBackground
        {
            FillColor = [0.19, 0.15, 0.11, 0.94],
            BorderWidth = 1.8,
            BorderColor = [0.65, 0.53, 0.38, 0.95],
            Radius = 3,
            Padding = 6
        });
        _labelCache[text] = created;
        return created;
    }

    public void Dispose()
    {
        _ringTexture.Dispose();
        foreach (LoadedTexture tex in _labelCache.Values) tex.Dispose();
        _labelCache.Clear();
    }
}
