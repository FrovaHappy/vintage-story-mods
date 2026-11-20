using Cairo;
using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SeraphQoL.Exploration;

/// <summary>
/// Resalta items en el suelo (EntityItem) con marcadores 2D proyectados y etiquetas opcionales al agacharse.
/// </summary>
public sealed class GroundItemHighlightRenderer : IRenderer
{
    private const int MarkerTexSize = 32;
    private const int MaxLabelCacheEntries = 64;

    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private LoadedTexture _markerTexture;
    private readonly Dictionary<string, LoadedTexture> _labelTextures = new(StringComparer.Ordinal);
    private readonly List<GroundItemIndicator> _cachedIndicators = new();
    private readonly GroundHighlightVisibilityState _visibilityState = new();

    private long _lastScanMs;

    public GroundItemHighlightRenderer(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
        _markerTexture = new LoadedTexture(capi);
    }

    public double RenderOrder => 0.98;
    public int RenderRange => 10;

    public void Reset()
    {
        _visibilityState.Reset();
        _cachedIndicators.Clear();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || _capi.World?.Player?.Entity == null) return;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableGroundItemHighlight) return;

        EntityPlayer playerEntity = _capi.World.Player.Entity;
        long nowMs = _capi.World.ElapsedMilliseconds;
        bool active = _visibilityState.IsVisible(
            cfg.EnableGroundItemHighlight,
            cfg.AlwaysHighlightGroundItems,
            playerEntity.Controls.Sneak,
            nowMs,
            cfg.GroundItemHighlightDurationSeconds);

        if (!active) return;

        if (nowMs - _lastScanMs >= 100)
        {
            _lastScanMs = nowMs;
            RefreshCandidates(cfg);
        }

        if (_cachedIndicators.Count == 0) return;

        EnsureMarkerTexture();
        if (_markerTexture.TextureId <= 0) return;

        double[]? projMat = _capi.Render.PerspectiveProjectionMat;
        double[]? viewMat = _capi.Render.PerspectiveViewMat;
        int fw = _capi.Render.FrameWidth;
        int fh = _capi.Render.FrameHeight;
        if (projMat == null || viewMat == null || fw <= 0 || fh <= 0) return;

        float markerSize = (float)GuiElement.scaled(18.0);
        foreach (GroundItemIndicator ind in _cachedIndicators)
        {
            Vec3d projected = MatrixToolsd.Project(
                new Vec3d(ind.WorldX, ind.WorldY, ind.WorldZ),
                projMat,
                viewMat,
                fw,
                fh);

            if (projected.Z <= 0.0) continue;

            float screenX = (float)projected.X - (markerSize * 0.5f);
            float screenY = (float)(fh - projected.Y) - (markerSize * 0.5f);

            if (screenX < -40 || screenX > fw + 40 || screenY < -40 || screenY > fh + 40) continue;

            _capi.Render.Render2DTexturePremultipliedAlpha(
                _markerTexture.TextureId,
                screenX,
                screenY,
                markerSize,
                markerSize,
                50f,
                new Vec4f(0.92f, 0.72f, 0.48f, ind.Alpha));

            if (ind.ShowLabel)
            {
                LoadedTexture labelTex = GetOrCreateLabelTexture(ind.Label);
                if (labelTex.TextureId > 0)
                {
                    float lx = (float)projected.X - (labelTex.Width * 0.5f);
                    float ly = screenY - labelTex.Height - 2f;
                    _capi.Render.Render2DTexturePremultipliedAlpha(
                        labelTex.TextureId,
                        lx,
                        ly,
                        labelTex.Width,
                        labelTex.Height,
                        51f,
                        new Vec4f(1f, 1f, 1f, ind.Alpha));
                }
            }
        }
    }

    private void RefreshCandidates(SeraphQoLClientConfig cfg)
    {
        _cachedIndicators.Clear();
        EntityPlayer? playerEntity = _capi.World?.Player?.Entity;
        if (playerEntity == null || _capi.World == null) return;

        int radius = Math.Clamp(cfg.GroundItemHighlightRadius, SeraphQoLClientConfig.MinGroundHighlightRadius, SeraphQoLClientConfig.MaxGroundHighlightRadius);
        Vec3d origin = playerEntity.Pos.XYZ;
        Entity[] nearby = _capi.World.GetEntitiesAround(origin, radius, radius, e => e is EntityItem);
        if (nearby == null || nearby.Length == 0) return;

        var candidates = new List<GroundItemCandidate>(nearby.Length);
        foreach (Entity entity in nearby)
        {
            if (entity is not EntityItem itemEntity || itemEntity.Itemstack?.Collectible?.Code == null) continue;
            double dist = origin.DistanceTo(itemEntity.Pos.XYZ);
            candidates.Add(new GroundItemCandidate(
                itemEntity.EntityId,
                itemEntity.Itemstack.Collectible.Code.ToString(),
                itemEntity.Itemstack.GetName(),
                itemEntity.Itemstack.StackSize,
                dist,
                itemEntity.Pos.X,
                itemEntity.Pos.Y + 0.35,
                itemEntity.Pos.Z));
        }

        bool showLabels = cfg.ShowGroundItemNamesOnSneak && (playerEntity.Controls.Sneak || _visibilityState.IsVisible(cfg.EnableGroundItemHighlight, false, false, _capi.World.ElapsedMilliseconds, cfg.GroundItemHighlightDurationSeconds));
        _cachedIndicators.AddRange(GroundItemHighlightPlanner.BuildIndicators(candidates, radius, showLabels));
    }

    private void EnsureMarkerTexture()
    {
        if (_markerTexture.TextureId > 0) return;

        using var surface = new ImageSurface(Format.Argb32, MarkerTexSize, MarkerTexSize);
        using var ctx = new Context(surface);
        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;

        double c = MarkerTexSize * 0.5;
        double r = 10.0;

        ctx.MoveTo(c, c - r);
        ctx.LineTo(c + r, c);
        ctx.LineTo(c, c + r);
        ctx.LineTo(c - r, c);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0.82, 0.60, 0.38, 0.55);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.94, 0.74, 0.50, 0.98);
        ctx.LineWidth = 2.5;
        ctx.Stroke();

        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref _markerTexture);
    }

    private LoadedTexture GetOrCreateLabelTexture(string text)
    {
        if (_labelTextures.TryGetValue(text, out LoadedTexture? existing))
        {
            return existing;
        }

        if (_labelTextures.Count >= MaxLabelCacheEntries)
        {
            foreach (LoadedTexture tex in _labelTextures.Values)
            {
                tex.Dispose();
            }
            _labelTextures.Clear();
        }

        var font = CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold);
        LoadedTexture created = _capi.Gui.TextTexture.GenTextTexture(text, font, new TextBackground
        {
            FillColor = [0.10, 0.07, 0.05, 0.82],
            BorderWidth = 1.2,
            BorderColor = [0.90, 0.70, 0.46, 0.95],
            Radius = 3,
            Padding = 4
        });
        _labelTextures[text] = created;
        return created;
    }

    public void Dispose()
    {
        _markerTexture.Dispose();
        foreach (LoadedTexture tex in _labelTextures.Values)
        {
            tex.Dispose();
        }
        _labelTextures.Clear();
    }
}
