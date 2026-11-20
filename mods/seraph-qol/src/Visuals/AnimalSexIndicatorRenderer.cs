using Cairo;
using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SeraphQoL.Visuals;

/// <summary>
/// Renderiza indicadores 3D billboard unificados sobre entidades con estilo retro de Vintage Story:
/// barra de vida configurable por tipo de entidad y cuadrados separados encima (sexo, prenez y generacion).
/// </summary>
public sealed class AnimalSexIndicatorRenderer : IRenderer
{
    private const string ShaderName = "seraphqol_animal_sex";
    private const int MaxBadgeCacheEntries = 160;
    private const int SquareSizePx = 36;
    private const int SquareGapPx = 5;
    private const int HealthBarWidthPx = 148;
    private const int HealthBarHeightPx = 22;
    private const int RowGapPx = 5;
    private const int CanvasPadPx = 4;

    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private readonly Matrixf _model = new();
    private readonly Vec4f _tint = new(1f, 1f, 1f, 1f);
    private readonly Dictionary<string, LoadedTexture> _hudCache = new(StringComparer.Ordinal);

    private MeshRef? _quadMesh;
    private IShaderProgram? _shader;
    private bool _resourcesInitialized;
    private bool _disposed;

    public AnimalSexIndicatorRenderer(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
        capi.Event.ReloadShader += ReloadShader;
    }

    public double RenderOrder => 0.90;
    public int RenderRange => 36;

    public void Initialize()
    {
        if (_disposed || _resourcesInitialized) return;
        _resourcesInitialized = true;

        MeshData quadData = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0f, 1f, 1f);
        for (int i = 1; i < quadData.Uv.Length; i += 2)
        {
            quadData.Uv[i] = 1f - quadData.Uv[i];
        }
        _quadMesh = _capi.Render.UploadMesh(quadData);
        ReloadShader();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (_disposed || stage != EnumRenderStage.Opaque) return;

        EntityPlayer? playerEntity = _capi.World?.Player?.Entity;
        if (playerEntity == null) return;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        bool isSneaking = playerEntity.Controls.Sneak;
        bool showAnyAnimalSquares = AnimalIndicatorGeometry.ShouldShowForPlayer(cfg.EnableAnimalSexIndicators, isSneaking);
        bool showAnyHealthBars = cfg.EnableEntityHealthBars && (!cfg.HealthBarRequireSneak || isSneaking);

        if (!showAnyAnimalSquares && !showAnyHealthBars)
        {
            return;
        }

        if (!_resourcesInitialized) Initialize();
        if (_shader == null || _shader.Disposed || _quadMesh == null) return;

        Vec3d camPos = playerEntity.CameraPos;
        int playerDim = playerEntity.Pos.Dimension;

        double animalRange = Math.Clamp(cfg.AnimalIndicatorRange, SeraphQoLClientConfig.MinAnimalIndicatorRange, SeraphQoLClientConfig.MaxAnimalIndicatorRange);
        double healthRange = Math.Clamp(cfg.HealthBarRange, SeraphQoLClientConfig.MinHealthBarRange, SeraphQoLClientConfig.MaxHealthBarRange);
        double maxSearchRange = Math.Max(showAnyAnimalSquares ? animalRange : 0.0, showAnyHealthBars ? healthRange : 0.0);
        double maxSearchRangeSq = maxSearchRange * maxSearchRange;

        float unitPerPx = (float)(0.00476 * Math.Clamp(cfg.AnimalIndicatorScale, 0.4, 2.5));
        float baseOpacity = (float)Math.Clamp(cfg.AnimalIndicatorOpacity, 0.1, 1.0);
        float[] viewMat = _capi.Render.CameraMatrixOriginf;

        var healthFilter = new EntityHealthBarFilter(
            cfg.EnableEntityHealthBars,
            cfg.HealthBarShowHostiles,
            cfg.HealthBarShowPassiveFauna,
            cfg.HealthBarShowTradersAndNpcs,
            cfg.HealthBarShowPlayers,
            cfg.HealthBarOnlyWhenDamaged,
            cfg.HealthBarRequireSneak);

        _capi.Render.GlDisableCullFace();
        _capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);

        _shader.Use();
        _shader.UniformMatrix("projectionMatrix", _capi.Render.CurrentProjectionMatrix);
        _shader.UniformMatrix("viewMatrix", viewMat);

        try
        {
            foreach (Entity entity in _capi.World!.LoadedEntities.Values)
            {
                if (entity == null || !entity.IsRendered || !entity.Alive) continue;
                if (entity.EntityId == playerEntity.EntityId || entity.Pos.Dimension != playerDim) continue;

                double worldX = entity.Pos.X;
                double worldY = entity.Pos.InternalY + (entity.SelectionBox?.Y2 ?? entity.CollisionBox?.Y2 ?? 1.0) + 0.42;
                double worldZ = entity.Pos.Z;

                double dxCam = camPos.X - worldX;
                double dyCam = camPos.Y - worldY;
                double dzCam = camPos.Z - worldZ;
                double distSq = (dxCam * dxCam) + (dyCam * dyCam) + (dzCam * dzCam);
                if (distSq > maxSearchRangeSq) continue;

                double dist = Math.Sqrt(distSq);

                IReadOnlyList<AnimalSquareBadge> squares = Array.Empty<AnimalSquareBadge>();
                if (showAnyAnimalSquares && dist <= animalRange && entity is not EntityPlayer)
                {
                    int generation = entity.WatchedAttributes.GetInt("generation", 0);
                    string? variantGender = null;
                    entity.Properties?.Variant?.TryGetValue("gender", out variantGender);
                    bool isPregnant = entity.WatchedAttributes.GetTreeAttribute("multiply")?.GetBool("isPregnant") ?? false;

                    AnimalBiologicalState bio = AnimalSexClassifier.Resolve(
                        entity.Code?.ToString(),
                        variantGender,
                        generation,
                        entity.Alive,
                        isPregnant,
                        cfg.AnimalIndicatorIncludeWild);

                    if (bio != AnimalBiologicalState.None)
                    {
                        squares = AnimalIndicatorFormatter.BuildSquareTokens(bio, generation);
                    }
                }

                bool showBar = false;
                float curHp = 0f;
                float maxHp = 0f;
                if (showAnyHealthBars && dist <= healthRange && TryReadHealth(entity, out curHp, out maxHp))
                {
                    string code = entity.Code?.ToString() ?? string.Empty;
                    string className = entity.GetType().Name;
                    bool isPlayer = entity is EntityPlayer;
                    bool isTraderOrNpc = entity is EntityTrader
                        || className.Contains("Trader", StringComparison.OrdinalIgnoreCase)
                        || className.Contains("Humanoid", StringComparison.OrdinalIgnoreCase);

                    var desc = new EntityHudDescriptor(
                        code,
                        className,
                        isPlayer,
                        isTraderOrNpc,
                        entity.Alive,
                        curHp,
                        maxHp,
                        IsHostileAi: false);

                    EntityHudCategory category = EntityHudClassifier.Classify(desc);
                    showBar = EntityHudClassifier.ShouldShowHealthBar(category, curHp, maxHp, isSneaking, healthFilter);
                }

                if (squares.Count == 0 && !showBar) continue;

                double activeMaxRange = showBar && squares.Count > 0
                    ? Math.Max(animalRange, healthRange)
                    : (showBar ? healthRange : animalRange);

                float alpha = AnimalIndicatorGeometry.ComputeDistanceAlpha(dist, activeMaxRange) * baseOpacity;
                if (alpha <= 0.02f) continue;

                LoadedTexture tex = GetOrCreateCompositeTexture(squares, showBar, curHp, maxHp);
                if (tex.TextureId <= 0) continue;

                float scaleW = tex.Width * unitPerPx;
                float scaleH = tex.Height * unitPerPx;

                float[] m = _model.Values;
                m[0] = viewMat[0] * scaleW;
                m[1] = viewMat[4] * scaleW;
                m[2] = viewMat[8] * scaleW;
                m[3] = 0f;

                m[4] = viewMat[1] * scaleH;
                m[5] = viewMat[5] * scaleH;
                m[6] = viewMat[9] * scaleH;
                m[7] = 0f;

                m[8] = viewMat[2];
                m[9] = viewMat[6];
                m[10] = viewMat[10];
                m[11] = 0f;

                m[12] = (float)(worldX - camPos.X);
                m[13] = (float)(worldY - camPos.Y);
                m[14] = (float)(worldZ - camPos.Z);
                m[15] = 1f;

                _tint.Set(1f, 1f, 1f, alpha);
                _shader.BindTexture2D("iconTexture", tex.TextureId, 0);
                _shader.UniformMatrix("modelMatrix", m);
                _shader.Uniform("iconTint", _tint);
                _capi.Render.RenderMesh(_quadMesh);
            }
        }
        finally
        {
            _shader.Stop();
            _capi.Render.GlToggleBlend(false);
            _capi.Render.GlEnableCullFace();
        }
    }

    private static bool TryReadHealth(Entity entity, out float currentHealth, out float maxHealth)
    {
        EntityBehaviorHealth? bh = entity.GetBehavior<EntityBehaviorHealth>();
        if (bh != null && bh.MaxHealth > 0f)
        {
            currentHealth = bh.Health;
            maxHealth = bh.MaxHealth;
            return true;
        }

        ITreeAttribute? healthTree = entity.WatchedAttributes?.GetTreeAttribute("health");
        if (healthTree != null)
        {
            float max = healthTree.GetFloat("maxhealth", 0f);
            if (max > 0f)
            {
                currentHealth = healthTree.GetFloat("currenthealth", max);
                maxHealth = max;
                return true;
            }
        }

        currentHealth = 0f;
        maxHealth = 0f;
        return false;
    }

    private LoadedTexture GetOrCreateCompositeTexture(
        IReadOnlyList<AnimalSquareBadge> squares,
        bool showBar,
        float currentHealth,
        float maxHealth)
    {
        string squaresKey = squares.Count == 0
            ? string.Empty
            : string.Join("|", squares.Select(s => $"{(int)s.Kind}:{s.Text}"));

        int fillBucket = showBar && maxHealth > 0f
            ? Math.Clamp((int)Math.Round((currentHealth / maxHealth) * 50f), 0, 50)
            : -1;
        string hpText = showBar ? EntityHudClassifier.FormatHealthText(currentHealth, maxHealth) : string.Empty;
        string cacheKey = $"{squaresKey}#{fillBucket}#{hpText}";

        if (_hudCache.TryGetValue(cacheKey, out LoadedTexture? cached) && cached.TextureId > 0)
        {
            return cached;
        }

        if (_hudCache.Count >= MaxBadgeCacheEntries)
        {
            foreach (LoadedTexture tex in _hudCache.Values) tex.Dispose();
            _hudCache.Clear();
        }

        int squaresRowW = squares.Count > 0
            ? (squares.Count * SquareSizePx) + ((squares.Count - 1) * SquareGapPx)
            : 0;
        int contentW = Math.Max(squaresRowW, showBar ? HealthBarWidthPx : 0);
        int texW = Math.Max(32, contentW + (CanvasPadPx * 2));

        int texH = CanvasPadPx * 2;
        if (squares.Count > 0) texH += SquareSizePx;
        if (showBar) texH += (squares.Count > 0 ? RowGapPx : 0) + HealthBarHeightPx;

        using var surface = new ImageSurface(Format.Argb32, texW, texH);
        using var ctx = new Context(surface);
        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;

        double yCursor = CanvasPadPx;

        // 1. Fila superior: cada item en su propio cuadrado retro biselado (encima de la barra)
        if (squares.Count > 0)
        {
            double xCursor = (texW - squaresRowW) * 0.5;
            foreach (AnimalSquareBadge item in squares)
            {
                DrawRetroBox(ctx, xCursor, yCursor, SquareSizePx, SquareSizePx);
                DrawSquareBadgeContent(ctx, xCursor, yCursor, SquareSizePx, item);
                xCursor += SquareSizePx + SquareGapPx;
            }
            yCursor += SquareSizePx + RowGapPx;
        }

        // 2. Fila inferior: barra de vida con el mismo estilo retro
        if (showBar)
        {
            double barX = (texW - HealthBarWidthPx) * 0.5;
            DrawHealthBar(ctx, barX, yCursor, HealthBarWidthPx, HealthBarHeightPx, currentHealth, maxHealth, hpText);
        }

        var created = new LoadedTexture(_capi);
        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref created);
        _hudCache[cacheKey] = created;
        return created;
    }

    private static void DrawRetroBox(Context ctx, double x, double y, double w, double h)
    {
        // Sombra exterior oscura + relleno espresso + marco bronce biselado (estilo Radial / UI VS)
        ctx.Rectangle(x, y, w, h);
        ctx.SetSourceRGBA(0.18, 0.14, 0.10, 0.94);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.06, 0.045, 0.035, 0.96);
        ctx.LineWidth = 2.4;
        ctx.Stroke();

        ctx.Rectangle(x + 1.5, y + 1.5, w - 3.0, h - 3.0);
        ctx.SetSourceRGBA(0.66, 0.54, 0.39, 0.96);
        ctx.LineWidth = 1.6;
        ctx.Stroke();

        ctx.Rectangle(x + 2.8, y + 2.8, w - 5.6, h - 5.6);
        ctx.SetSourceRGBA(0.08, 0.06, 0.045, 0.60);
        ctx.LineWidth = 1.0;
        ctx.Stroke();
    }

    private static void DrawSquareBadgeContent(Context ctx, double x, double y, double size, AnimalSquareBadge item)
    {
        (double r, double g, double b) = item.Kind switch
        {
            AnimalSquareKind.SexMale => (0.44, 0.80, 1.00),
            AnimalSquareKind.SexFemale => (0.98, 0.56, 0.76),
            AnimalSquareKind.Pregnant => (1.00, 0.84, 0.36),
            _ => (1.00, 1.00, 1.00)
        };

        double fontSize = item.Kind == AnimalSquareKind.Generation
            ? (item.Text.Length >= 3 ? 13.5 : item.Text.Length == 2 ? 16.0 : 18.0)
            : 21.0;

        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(fontSize);
        TextExtents ext = ctx.TextExtents(item.Text);
        double tx = x + ((size - ext.Width) * 0.5) - ext.XBearing;
        double ty = y + ((size - ext.Height) * 0.5) - ext.YBearing;

        ctx.MoveTo(tx + 1.0, ty + 1.0);
        ctx.SetSourceRGBA(0.05, 0.03, 0.02, 0.90);
        ctx.ShowText(item.Text);

        ctx.MoveTo(tx, ty);
        ctx.SetSourceRGBA(r, g, b, 1.0);
        ctx.ShowText(item.Text);
    }

    private static void DrawHealthBar(
        Context ctx,
        double x,
        double y,
        double w,
        double h,
        float currentHealth,
        float maxHealth,
        string hpText)
    {
        DrawRetroBox(ctx, x, y, w, h);

        double pad = 3.5;
        double trackX = x + pad;
        double trackY = y + pad;
        double trackW = w - (pad * 2.0);
        double trackH = h - (pad * 2.0);

        ctx.Rectangle(trackX, trackY, trackW, trackH);
        ctx.SetSourceRGBA(0.09, 0.07, 0.05, 0.96);
        ctx.Fill();

        float ratio = maxHealth > 0f ? Math.Clamp(currentHealth / maxHealth, 0f, 1f) : 0f;
        if (ratio > 0f)
        {
            double fillW = Math.Max(2.0, trackW * ratio);
            (double fr, double fg, double fb) = ResolveHealthBarRgb(ratio);

            ctx.Rectangle(trackX, trackY, fillW, trackH);
            ctx.SetSourceRGBA(fr, fg, fb, 0.96);
            ctx.Fill();

            // Sutil brillo superior en la barra de relleno
            ctx.Rectangle(trackX, trackY, fillW, Math.Min(2.5, trackH * 0.25));
            ctx.SetSourceRGBA(Math.Min(1.0, fr + 0.18), Math.Min(1.0, fg + 0.18), Math.Min(1.0, fb + 0.18), 0.70);
            ctx.Fill();
        }

        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(12.5);
        TextExtents ext = ctx.TextExtents(hpText);
        double tx = x + ((w - ext.Width) * 0.5) - ext.XBearing;
        double ty = y + ((h - ext.Height) * 0.5) - ext.YBearing;

        ctx.MoveTo(tx + 1.0, ty + 1.0);
        ctx.SetSourceRGBA(0.04, 0.03, 0.02, 0.95);
        ctx.ShowText(hpText);

        ctx.MoveTo(tx, ty);
        ctx.SetSourceRGBA(1.0, 1.0, 1.0, 1.0);
        ctx.ShowText(hpText);
    }

    private static (double R, double G, double B) ResolveHealthBarRgb(float ratio)
    {
        if (ratio >= 0.55f) return (0.28, 0.74, 0.34);
        if (ratio >= 0.25f) return (0.86, 0.64, 0.20);
        return (0.84, 0.24, 0.20);
    }

    private bool ReloadShader()
    {
        if (_disposed) return true;

        IShaderProgram? previous = _shader;
        IShaderProgram prog = _capi.Shader.NewShaderProgram();
        prog.AssetDomain = SeraphQoLLang.Domain;
        _capi.Shader.RegisterFileShaderProgram(ShaderName, prog);

        if (!prog.Compile())
        {
            _capi.Logger.Warning("[SeraphQoL] Failed to compile shader {0}", ShaderName);
            prog.Dispose();
            return true;
        }

        _shader = prog;
        if (previous != null && !ReferenceEquals(previous, prog))
        {
            previous.Dispose();
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _capi.Event.ReloadShader -= ReloadShader;
        _shader?.Dispose();
        _shader = null;
        _quadMesh?.Dispose();
        _quadMesh = null;
        foreach (LoadedTexture tex in _hudCache.Values) tex.Dispose();
        _hudCache.Clear();
    }
}
