using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SeraphQoL.Visuals;

/// <summary>
/// Adjunta y actualiza AdvancedParticleProperties nativos directamente sobre bloques clave (patron Particles Plus).
/// </summary>
public sealed class ResourceParticlesService
{
    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private readonly Dictionary<int, AdvancedParticleProperties[]?> _originalParticles = new();
    private bool _capturedOriginals;

    public ResourceParticlesService(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
    }

    public void ApplyConfiguredParticles()
    {
        if (_capi.World == null) return;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        var options = new ResourceParticlesOptions(
            Resin: cfg.EnableResourceParticles && cfg.HighlightResinParticles,
            TerraPreta: cfg.EnableResourceParticles && cfg.HighlightTerraPretaParticles,
            MeteoricIron: cfg.EnableResourceParticles && cfg.HighlightMeteoricIronParticles);

        if (!_capturedOriginals)
        {
            CaptureOriginals();
            _capturedOriginals = true;
        }

        foreach ((int blockId, AdvancedParticleProperties[]? orig) in _originalParticles)
        {
            Block? blk = _capi.World.GetBlock(blockId);
            if (blk != null)
            {
                blk.ParticleProperties = orig ?? Array.Empty<AdvancedParticleProperties>();
            }
        }

        if (!cfg.EnableResourceParticles) return;

        if (options.Resin) AttachToWildcard("log-resin-*", CreateResinParticles());
        if (options.TerraPreta) AttachToWildcard("soil-compost-*", CreateSoilSmokeParticles(hue: 17f, sat: 195f, val: 42f));
        if (options.MeteoricIron) AttachToWildcard("meteorite-*", CreateMeteoricSparks());
    }

    private void CaptureOriginals()
    {
        foreach (string wildcard in new[] { "log-resin-*", "soil-compost-*", "meteorite-*" })
        {
            Block[]? found = _capi.World.SearchBlocks(new AssetLocation("game", wildcard));
            if (found == null) continue;
            foreach (Block blk in found)
            {
                if (blk == null || blk.Id == 0) continue;
                _originalParticles.TryAdd(blk.Id, blk.ParticleProperties);
            }
        }
    }

    private void AttachToWildcard(string wildcard, AdvancedParticleProperties[] extra)
    {
        Block[]? blocks = _capi.World.SearchBlocks(new AssetLocation("game", wildcard));
        if (blocks == null) return;

        foreach (Block blk in blocks)
        {
            if (blk == null || blk.Id == 0) continue;
            AdvancedParticleProperties[]? orig = _originalParticles.TryGetValue(blk.Id, out AdvancedParticleProperties[]? saved)
                ? saved
                : blk.ParticleProperties;

            blk.ParticleProperties = (orig != null && orig.Length > 0)
                ? orig.Concat(extra).ToArray()
                : extra;
        }
    }

    private static AdvancedParticleProperties[] CreateResinParticles()
    {
        (float xOff, float zOff)[] sides =
        [
            (0f, 0.60f),
            (0f, -0.60f),
            (-0.60f, 0f),
            (0.60f, 0f)
        ];

        var list = new AdvancedParticleProperties[sides.Length];
        for (int i = 0; i < sides.Length; i++)
        {
            (float x, float z) = sides[i];
            list[i] = new AdvancedParticleProperties
            {
                HsvaColor =
                [
                    NatFloat.createUniform(22f, 3f),
                    NatFloat.createUniform(236f, 0f),
                    NatFloat.createUniform(252f, 0f),
                    NatFloat.createUniform(199f, 50f)
                ],
                PosOffset =
                [
                    NatFloat.createUniform(x, MathF.Abs(x) > 0.1f ? 0f : 0.2f),
                    NatFloat.createUniform(-0.5f, 0f),
                    NatFloat.createUniform(z, MathF.Abs(z) > 0.1f ? 0f : 0.2f)
                ],
                GravityEffect = NatFloat.createUniform(0.5f, 0f),
                LifeLength = NatFloat.createUniform(1.2f, 0f),
                Quantity = NatFloat.createUniform(0.05f, 0f),
                Size = NatFloat.createUniform(1.2f, 0.2f),
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 1.0f),
                Velocity =
                [
                    NatFloat.createUniform(0f, 0.5f),
                    NatFloat.createUniform(0f, 0.5f),
                    NatFloat.createUniform(0f, 0.5f)
                ],
                ParticleModel = EnumParticleModel.Cube,
                VertexFlags = 8192,
                TerrainCollision = true,
                WindAffectednes = 1.0f
            };
        }

        return list;
    }

    private static AdvancedParticleProperties[] CreateSoilSmokeParticles(float hue, float sat, float val)
    {
        var p = new AdvancedParticleProperties
        {
            HsvaColor =
            [
                NatFloat.createUniform(hue, 3f),
                NatFloat.createUniform(sat, 10f),
                NatFloat.createUniform(val, 12f),
                NatFloat.createUniform(175f, 40f)
            ],
            PosOffset =
            [
                NatFloat.createUniform(0f, 0.45f),
                NatFloat.createUniform(0.55f, 0.10f),
                NatFloat.createUniform(0f, 0.45f)
            ],
            OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -16f),
            GravityEffect = NatFloat.createUniform(-0.025f, 0.008f),
            LifeLength = NatFloat.createUniform(0.75f, 0.15f),
            Quantity = NatFloat.createUniform(0.10f, 0.05f),
            Size = NatFloat.createUniform(0.32f, 0.08f),
            SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, 1.1f),
            Velocity =
            [
                NatFloat.createUniform(0f, 0.04f),
                NatFloat.createUniform(0.05f, 0.08f),
                NatFloat.createUniform(0f, 0.04f)
            ],
            ParticleModel = EnumParticleModel.Quad,
            VertexFlags = 0,
            TerrainCollision = false,
            WindAffectednes = 0.8f
        };

        return [p];
    }

    private static AdvancedParticleProperties[] CreateMeteoricSparks()
    {
        // El hierro meteorico suele quedar enterrado 2-3 bloques: velocidad ascendente sin colision
        // para superar los 4 bloques de altura y permanecer visible desde la superficie.
        var p = new AdvancedParticleProperties
        {
            HsvaColor =
            [
                NatFloat.createUniform(15f, 5f),
                NatFloat.createUniform(185f, 30f),
                NatFloat.createUniform(205f, 35f),
                NatFloat.createUniform(225f, 35f)
            ],
            PosOffset =
            [
                NatFloat.createUniform(0f, 0.45f),
                NatFloat.createUniform(0.8f, 1.2f),
                NatFloat.createUniform(0f, 0.45f)
            ],
            OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -18f),
            GravityEffect = NatFloat.createUniform(0f, 0f),
            LifeLength = NatFloat.createUniform(0.85f, 0.25f),
            Quantity = NatFloat.createUniform(0.55f, 0.25f),
            Size = NatFloat.createUniform(0.40f, 0.12f),
            Velocity =
            [
                NatFloat.createUniform(0f, 0.25f),
                NatFloat.createUniform(5.5f, 1.5f),
                NatFloat.createUniform(0f, 0.25f)
            ],
            ParticleModel = EnumParticleModel.Quad,
            VertexFlags = 110,
            TerrainCollision = false,
            DieInLiquid = false,
            WindAffectednes = 0.1f
        };

        return [p];
    }
}
