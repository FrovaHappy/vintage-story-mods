using Vintagestory.API.MathTools;
using Xunit;

namespace Comerciantes.Tests;

public sealed class WorldSaturationRegistryTests
{
    [Fact]
    public void EvaluateMultiplier_FollowsCurveAndMinFloor()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig
        {
            SaturationS50 = 25.0,
            SaturationCurvePower = 1.0,
            SaturationMinFloor = 0.20
        };

        Assert.Equal(1.0, registry.EvaluateMultiplier(0.0, config), 4);
        Assert.Equal(0.5, registry.EvaluateMultiplier(25.0, config), 4);
        Assert.Equal(0.20, registry.EvaluateMultiplier(10000.0, config), 4);
    }

    [Fact]
    public void EvaluateMultiplier_DefaultConfig_UsesTightenedFloorAndCurve()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig();

        // New defaults: S50 = 15.0, CurvePower = 1.6, MinFloor = 0.10
        Assert.Equal(1.0, registry.EvaluateMultiplier(0.0, config), 4);
        Assert.Equal(0.5, registry.EvaluateMultiplier(15.0, config), 4);
        Assert.Equal(0.10, registry.EvaluateMultiplier(500.0, config), 4);
    }

    [Fact]
    public void RecordSale_LocalOnly_DoesNotAffectOtherTraders()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig { SaturationScope = SaturationScope.LocalOnly };
        var pos1 = new BlockPos(100, 110, 100);
        var pos2 = new BlockPos(110, 110, 100);

        registry.RecordSale(1, pos1, 10.0, "game:ingot-copper", 20.0, config);

        Assert.Equal(20.0, registry.GetCurrentVolume(1, pos1, 10.0, "game:ingot-copper", config), 4);
        Assert.Equal(0.0, registry.GetCurrentVolume(2, pos2, 10.0, "game:ingot-copper", config), 4);
    }

    [Fact]
    public void RecordSale_NearbyPartial_SpillsOverWithinRadiusOnly()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig
        {
            SaturationScope = SaturationScope.NearbyPartial,
            SaturationSpilloverRadius = 1000.0,
            SaturationSpilloverFactor = 0.40
        };

        var originPos = new BlockPos(0, 100, 0);
        var nearPos = new BlockPos(500, 100, 0);   // 500 blocks -> weight = 0.40 * (1 - 0.5) = 0.20
        var farPos = new BlockPos(1500, 100, 0);   // beyond 1000 blocks

        // Register near and far traders in the registry first
        _ = registry.GetCurrentVolume(2, nearPos, 10.0, "game:charcoal", config);
        _ = registry.GetCurrentVolume(3, farPos, 10.0, "game:charcoal", config);

        registry.RecordSale(1, originPos, 10.0, "game:charcoal", 50.0, config);

        Assert.Equal(50.0, registry.GetCurrentVolume(1, originPos, 10.0, "game:charcoal", config), 4);
        Assert.Equal(10.0, registry.GetCurrentVolume(2, nearPos, 10.0, "game:charcoal", config), 4);
        Assert.Equal(0.0, registry.GetCurrentVolume(3, farPos, 10.0, "game:charcoal", config), 4);
    }

    [Fact]
    public void RecordSale_Global_SharesVolumeAcrossTraders()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig { SaturationScope = SaturationScope.Global };

        registry.RecordSale(1, new BlockPos(0, 0, 0), 5.0, "game:fat", 12.0, config);

        Assert.Equal(12.0, registry.GetCurrentVolume(999, new BlockPos(5000, 0, 5000), 5.0, "game:fat", config), 4);
    }

    [Fact]
    public void GetCurrentVolume_Global_AppliesDailyRecovery()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig
        {
            SaturationScope = SaturationScope.Global,
            SaturationRecoveryPerDay = 6.0
        };
        var pos = new BlockPos(0, 100, 0);

        registry.RecordSale(1, pos, 10.0, "game:charcoal", 20.0, config);

        // 2 days later -> recovers 12.0, leaving 8.0
        Assert.Equal(8.0, registry.GetCurrentVolume(999, pos, 12.0, "game:charcoal", config), 4);
        // 5 days later -> recovers another 18.0, clamped to 0.0
        Assert.Equal(0.0, registry.GetCurrentVolume(999, pos, 15.0, "game:charcoal", config), 4);
    }

    [Fact]
    public void GetCurrentVolume_AppliesDailyRecovery()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig
        {
            SaturationScope = SaturationScope.LocalOnly,
            SaturationRecoveryPerDay = 6.0
        };
        var pos = new BlockPos(0, 100, 0);

        registry.RecordSale(1, pos, 10.0, "game:charcoal", 20.0, config);

        // 2 days later -> recovers 12.0, leaving 8.0
        Assert.Equal(8.0, registry.GetCurrentVolume(1, pos, 12.0, "game:charcoal", config), 4);
        // 5 days later -> recovers another 18.0, clamped to 0.0
        Assert.Equal(0.0, registry.GetCurrentVolume(1, pos, 15.0, "game:charcoal", config), 4);
    }

    [Fact]
    public void RecordSale_IgnoresNonPositiveDelta()
    {
        var registry = new WorldSaturationRegistry();
        var config = new ComerciantesConfig { SaturationScope = SaturationScope.LocalOnly };
        var pos = new BlockPos(0, 100, 0);

        registry.RecordSale(1, pos, 10.0, "game:charcoal", 0.0, config);
        registry.RecordSale(1, pos, 10.0, "game:charcoal", -5.0, config);

        Assert.Equal(0.0, registry.GetCurrentVolume(1, pos, 10.0, "game:charcoal", config), 4);
    }

    [Fact]
    public void ExportAndImportStateJson_PersistsLocalAndGlobalVolumes()
    {
        var original = new WorldSaturationRegistry();
        var localCfg = new ComerciantesConfig { SaturationScope = SaturationScope.LocalOnly };
        var globalCfg = new ComerciantesConfig { SaturationScope = SaturationScope.Global };
        var pos = new BlockPos(10, 100, 20);

        original.RecordSale(5, pos, 12.0, "game:ingot-copper", 15.0, localCfg);
        original.RecordSale(5, pos, 12.0, "game:fat", 22.5, globalCfg);

        string json = original.ExportStateJson();

        var restored = new WorldSaturationRegistry();
        restored.ImportStateJson(json);

        Assert.Equal(15.0, restored.GetCurrentVolume(5, pos, 12.0, "game:ingot-copper", localCfg), 4);
        Assert.Equal(22.5, restored.GetCurrentVolume(99, pos, 12.0, "game:fat", globalCfg), 4);
    }

    [Fact]
    public void ImportStateJson_SupportsLegacyTraderDictionaryFormat()
    {
        const string legacyJson = """
            {
              "7": {
                "LastPos": { "X": 1, "Y": 64, "Z": 2 },
                "LastUpdatedTotalDays": 4.0,
                "VolumesByGroup": { "game:charcoal": 18.0 }
              }
            }
            """;

        var restored = new WorldSaturationRegistry();
        restored.ImportStateJson(legacyJson);

        var cfg = new ComerciantesConfig { SaturationScope = SaturationScope.LocalOnly };
        Assert.Equal(18.0, restored.GetCurrentVolume(7, new BlockPos(1, 64, 2), 4.0, "game:charcoal", cfg), 4);
    }
}
