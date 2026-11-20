using ProtoBuf;
using SeraphQoL.Config;
using SeraphQoL.Network;
using Vintagestory.API.Datastructures;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class SeraphQoLConfigTests
{
    [Fact]
    public void ClientConfig_HasExpectedMvpDefaults()
    {
        var cfg = new SeraphQoLClientConfig();

        Assert.Equal(SortOrder.Category, cfg.SortOrder);
        Assert.False(cfg.SortHotbarWithBackpack);
        Assert.False(cfg.StackPerishables);
        Assert.False(cfg.SkipFavoritesWhenSorting);
        Assert.False(cfg.HideFavoritesOverlay);
        Assert.False(cfg.HideLockedSlotOverlay);
        Assert.False(cfg.HideSortButton);
        Assert.False(cfg.HideQuickStoreButton);
        Assert.False(cfg.HideStoreNearbyButton);
        Assert.False(cfg.HideStackPerishablesButton);

        // Phase 2 defaults
        Assert.True(cfg.EnableHotbarRefill);
        Assert.True(cfg.RefillConsumables);
        Assert.True(cfg.RefillBlocks);
        Assert.False(cfg.RefillToolsBeforeBreak);
        Assert.Equal(5, cfg.ToolDurabilityThreshold);
        Assert.Equal(150, cfg.RefillDelayMs);
        Assert.True(cfg.PreferSmallestStackForRefill);

        Assert.True(cfg.EnableRadialToolModeMenu);
        Assert.False(cfg.ToolModeSelectOnKeyRelease);
        Assert.Equal(135, cfg.RadialToolModeOuterRadius);

        Assert.True(cfg.EnableMatchingItemHighlight);
        Assert.True(cfg.EnableGroundItemHighlight);
        Assert.False(cfg.AlwaysHighlightGroundItems);
        Assert.True(cfg.ShowGroundItemNamesOnSneak);
        Assert.Equal(16, cfg.GroundItemHighlightRadius);
        Assert.Equal(2.5, cfg.GroundItemHighlightDurationSeconds, 3);
        Assert.True(cfg.EnableProjectileTracker);
        Assert.Equal(300, cfg.ProjectileWaypointTimeoutSeconds);

        // Phase 3 defaults
        Assert.True(cfg.EnableAlloyCalculator);
        Assert.Equal(5, cfg.AlloyUnitsPerNugget);
        Assert.False(cfg.AlloySearchNearbyContainers);
        Assert.Equal(6, cfg.AlloySearchRadius);
        Assert.True(cfg.EnableQuickSwapRadial);
        Assert.True(cfg.QuickSwapSelectOnKeyRelease);
        Assert.True(cfg.EnableSlotIndicators);
        Assert.True(cfg.EnableAnimalSexIndicators);
        Assert.False(cfg.AnimalIndicatorIncludeWild);
        Assert.Equal(12, cfg.AnimalIndicatorRange);
        Assert.Equal(0.90, cfg.AnimalIndicatorOpacity, 3);
        Assert.Equal(1.0, cfg.AnimalIndicatorScale, 3);
        Assert.True(cfg.EnableEntityHealthBars);
        Assert.True(cfg.HealthBarShowHostiles);
        Assert.True(cfg.HealthBarShowPassiveFauna);
        Assert.False(cfg.HealthBarShowTradersAndNpcs);
        Assert.False(cfg.HealthBarShowPlayers);
        Assert.False(cfg.HealthBarOnlyWhenDamaged);
        Assert.False(cfg.HealthBarRequireSneak);
        Assert.Equal(18, cfg.HealthBarRange);
        Assert.True(cfg.EnableResourceParticles);
    }

    [Fact]
    public void ClientConfig_Normalize_ClampsPhase2AndPhase3Bounds()
    {
        var cfg = new SeraphQoLClientConfig
        {
            ToolDurabilityThreshold = 999,
            RefillDelayMs = -50,
            RadialToolModeOuterRadius = 500,
            GroundItemHighlightRadius = 200,
            GroundItemHighlightDurationSeconds = -3,
            ProjectileWaypointTimeoutSeconds = 99999,
            AlloyUnitsPerNugget = 99,
            AlloySearchRadius = 25,
            AnimalIndicatorRange = 999,
            AnimalIndicatorOpacity = 5.0,
            AnimalIndicatorScale = 0.05
        };

        cfg.Normalize();

        Assert.Equal(SeraphQoLClientConfig.MaxToolDurabilityThreshold, cfg.ToolDurabilityThreshold);
        Assert.Equal(0, cfg.RefillDelayMs);
        Assert.Equal(SeraphQoLClientConfig.MaxRadialOuterRadius, cfg.RadialToolModeOuterRadius);
        Assert.Equal(SeraphQoLClientConfig.MaxGroundHighlightRadius, cfg.GroundItemHighlightRadius);
        Assert.Equal(0.0, cfg.GroundItemHighlightDurationSeconds, 3);
        Assert.Equal(SeraphQoLClientConfig.MaxProjectileWaypointTimeoutSeconds, cfg.ProjectileWaypointTimeoutSeconds);
        Assert.Equal(SeraphQoLClientConfig.MaxAlloyUnitsPerNugget, cfg.AlloyUnitsPerNugget);
        Assert.Equal(SeraphQoLClientConfig.MaxAlloySearchRadius, cfg.AlloySearchRadius);
        cfg.AlloySearchRadius = 1;
        cfg.Normalize();
        Assert.Equal(SeraphQoLClientConfig.MinAlloySearchRadius, cfg.AlloySearchRadius);
        Assert.Equal(SeraphQoLClientConfig.MaxAnimalIndicatorRange, cfg.AnimalIndicatorRange);
        Assert.Equal(SeraphQoLClientConfig.MaxAnimalIndicatorOpacity, cfg.AnimalIndicatorOpacity);
        Assert.Equal(SeraphQoLClientConfig.MinAnimalIndicatorScale, cfg.AnimalIndicatorScale);
    }

    [Fact]
    public void ClientConfig_ToggleLockedSlot_TracksIndicesPerInventory()
    {
        var cfg = new SeraphQoLClientConfig();

        Assert.True(cfg.ToggleLockedSlot("backpack", 3));
        Assert.True(cfg.IsSlotLocked("backpack", 3));
        Assert.False(cfg.IsSlotLocked("hotbar", 3));

        Assert.False(cfg.ToggleLockedSlot("backpack", 3));
        Assert.False(cfg.IsSlotLocked("backpack", 3));
    }

    [Fact]
    public void ClientConfig_ApplySetting_UpdatesSortOrderAndBooleans()
    {
        var cfg = new SeraphQoLClientConfig();

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.SortOrder), new StringAttribute("StackCount")));
        Assert.Equal(SortOrder.StackCount, cfg.SortOrder);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.StackPerishables), new BoolAttribute(true)));
        Assert.True(cfg.StackPerishables);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.ToolDurabilityThreshold), new IntAttribute(12)));
        Assert.Equal(12, cfg.ToolDurabilityThreshold);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.RadialToolModeOuterRadius), new IntAttribute(160)));
        Assert.Equal(160, cfg.RadialToolModeOuterRadius);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.GroundItemHighlightRadius), new IntAttribute(24)));
        Assert.Equal(24, cfg.GroundItemHighlightRadius);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.EnableProjectileTracker), new BoolAttribute(false)));
        Assert.False(cfg.EnableProjectileTracker);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.ProjectileWaypointTimeoutSeconds), new IntAttribute(120)));
        Assert.Equal(120, cfg.ProjectileWaypointTimeoutSeconds);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.EnableEntityHealthBars), new BoolAttribute(false)));
        Assert.False(cfg.EnableEntityHealthBars);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.HealthBarShowTradersAndNpcs), new BoolAttribute(true)));
        Assert.True(cfg.HealthBarShowTradersAndNpcs);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLClientConfig.HealthBarRange), new IntAttribute(99)));
        Assert.Equal(SeraphQoLClientConfig.MaxHealthBarRange, cfg.HealthBarRange);

        Assert.False(cfg.ApplySetting("UnknownKey", new BoolAttribute(true)));
    }

    [Fact]
    public void ServerConfig_HasTacticalBalancedFaunaAndStoreNearbyDefaults()
    {
        var cfg = new SeraphQoLServerConfig();

        Assert.True(cfg.EnableFaunaRebalance);
        Assert.Equal(1.5, cfg.SmallPreyHealthMultiplier);
        Assert.Equal(2.0, cfg.MediumLargePreyHealthMultiplier);
        Assert.Equal(2.5, cfg.PredatorHealthMultiplier);

        Assert.Equal(1.75, cfg.MeatDropMultiplier);
        Assert.Equal(1.75, cfg.FatDropMultiplier);
        Assert.Equal(1.5, cfg.HideDropMultiplier);
        Assert.Equal(1.5, cfg.ByproductDropMultiplier);

        Assert.False(cfg.ScaleDomesticatedHealth);
        Assert.True(cfg.ScaleDomesticatedDrops);
        Assert.False(cfg.IncludeAquaticFauna);

        Assert.True(cfg.EnableInventoryManagement);
        Assert.True(cfg.EnableStoreNearby);
        Assert.Equal(6, cfg.StoreNearbyRadius);
        Assert.True(cfg.RequireLandClaimPermission);
        Assert.True(cfg.AllowHotbarRefill);
        Assert.True(cfg.AllowProjectileTracker);
    }

    [Fact]
    public void ServerConfig_Normalize_ClampsOutOfRangeValues()
    {
        var cfg = new SeraphQoLServerConfig
        {
            StoreNearbyRadius = 99,
            SmallPreyHealthMultiplier = 0.0,
            MeatDropMultiplier = -2.0
        };

        cfg.Normalize();

        Assert.Equal(SeraphQoLServerConfig.MaxStoreNearbyRadius, cfg.StoreNearbyRadius);
        Assert.Equal(SeraphQoLServerConfig.MinMultiplier, cfg.SmallPreyHealthMultiplier);
        Assert.Equal(SeraphQoLServerConfig.MinMultiplier, cfg.MeatDropMultiplier);
    }

    [Fact]
    public void EffectiveServerPolicy_FromServerConfig_CopiesSettingsAndNormalizes()
    {
        var server = new SeraphQoLServerConfig
        {
            EnableFaunaRebalance = false,
            PredatorHealthMultiplier = 3.25,
            EnableStoreNearby = false,
            StoreNearbyRadius = 99,
            AllowHotbarRefill = false,
            AllowProjectileTracker = false
        };

        EffectiveServerPolicy effective = EffectiveServerPolicy.FromServerConfig(server);

        Assert.False(effective.EnableFaunaRebalance);
        Assert.Equal(3.25, effective.PredatorHealthMultiplier);
        Assert.False(effective.EnableStoreNearby);
        Assert.Equal(SeraphQoLServerConfig.MaxStoreNearbyRadius, effective.StoreNearbyRadius);
        Assert.False(effective.AllowHotbarRefill);
        Assert.False(effective.AllowProjectileTracker);
    }

    [Fact]
    public void ServerConfig_ApplyPreset_AndManualEditsUpdatePresetState()
    {
        var cfg = new SeraphQoLServerConfig();
        Assert.Equal(FaunaPreset.BalancedTactical, cfg.FaunaPreset);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLServerConfig.FaunaPreset), new StringAttribute("RealisticHunt")));
        Assert.Equal(FaunaPreset.RealisticHunt, cfg.FaunaPreset);
        Assert.Equal(3.5, cfg.PredatorHealthMultiplier);
        Assert.Equal(2.0, cfg.MeatDropMultiplier);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLServerConfig.FaunaPreset), new StringAttribute("Vanilla")));
        Assert.Equal(FaunaPreset.Vanilla, cfg.FaunaPreset);
        Assert.False(cfg.EnableFaunaRebalance);
        Assert.Equal(1.0, cfg.PredatorHealthMultiplier);

        Assert.True(cfg.ApplySetting(nameof(SeraphQoLServerConfig.EnableFaunaRebalance), new BoolAttribute(true)));
        Assert.True(cfg.ApplySetting(nameof(SeraphQoLServerConfig.PredatorHealthMultiplier), new DoubleAttribute(2.8)));
        Assert.Equal(FaunaPreset.Custom, cfg.FaunaPreset);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SortInventoryPacket_RoundTripsSkipFavoritesWhenSortingThroughProtobuf(bool skipFavorites)
    {
        var original = new SortInventoryPacket
        {
            InventoryId = "backpack-test",
            SortOrder = (int)SortOrder.Code,
            SortHotbarWithBackpack = true,
            StackPerishables = true,
            SkipFavoritesWhenSorting = skipFavorites
        };

        using var ms = new MemoryStream();
        Serializer.Serialize(ms, original);
        ms.Position = 0;

        SortInventoryPacket decoded = Serializer.Deserialize<SortInventoryPacket>(ms);
        Assert.Equal(skipFavorites, decoded.SkipFavoritesWhenSorting);
        Assert.Equal(original.InventoryId, decoded.InventoryId);
        Assert.Equal(original.SortOrder, decoded.SortOrder);
    }

    [Fact]
    public void Phase2Packets_RoundTripThroughProtobuf()
    {
        var refill = RoundTrip(new HotbarRefillPacket
        {
            HotbarSlotIndex = 2,
            BackpackSlotIndex = 14,
            ExpectedItemCode = "game:cobblestone-granite",
            IsPreventiveToolSwap = false
        });
        Assert.Equal(2, refill.HotbarSlotIndex);
        Assert.Equal(14, refill.BackpackSlotIndex);
        Assert.Equal("game:cobblestone-granite", refill.ExpectedItemCode);

        var swap = RoundTrip(new SwapSlotsPacket
        {
            SourceInventoryClass = "backpack",
            SourceSlotIndex = 7,
            TargetInventoryClass = "hotbar",
            TargetSlotIndex = 10
        });
        Assert.Equal("backpack", swap.SourceInventoryClass);
        Assert.Equal(10, swap.TargetSlotIndex);

        var wp = RoundTrip(new ProjectileWaypointPacket
        {
            Remove = false,
            WaypointKey = "seraphqol-proj-42",
            X = 12.5,
            Y = 110.2,
            Z = -8.75,
            Title = "Copper Arrow"
        });
        Assert.False(wp.Remove);
        Assert.Equal("seraphqol-proj-42", wp.WaypointKey);
        Assert.Equal("Copper Arrow", wp.Title);

        var scanReq = RoundTrip(new RequestAlloyScanPacket
        {
            FirepitPos = new Vintagestory.API.MathTools.BlockPos(10, 64, -20),
            SearchRadius = 8
        });
        Assert.Equal(10, scanReq.FirepitPos.X);
        Assert.Equal(8, scanReq.SearchRadius);

        var scanRes = RoundTrip(new AlloyScanResultPacket
        {
            FirepitPos = new Vintagestory.API.MathTools.BlockPos(10, 64, -20),
            SearchRadius = 8,
            ItemCodes = ["game:nugget-nativecopper"],
            ItemCounts = [36]
        });
        Assert.Single(scanRes.ItemCodes);
        Assert.Equal(36, scanRes.ItemCounts[0]);

        var depositReq = RoundTrip(new AlloyDepositPacket
        {
            FirepitPos = new Vintagestory.API.MathTools.BlockPos(10, 64, -20),
            DepositItems = true,
            DepositFuel = true,
            IncludeNearby = true,
            SearchRadius = 8,
            MetalCodes = ["game:ingot-copper", "game:ingot-tin"],
            MetalItemsNeeded = [18, 2],
            FuelItemCode = "game:charcoal",
            FuelPiecesNeeded = 3
        });
        Assert.True(depositReq.IncludeNearby);
        Assert.Equal(2, depositReq.MetalCodes.Count);
        Assert.Equal(3, depositReq.FuelPiecesNeeded);

        var depositRes = RoundTrip(new AlloyDepositResultPacket
        {
            FirepitPos = new Vintagestory.API.MathTools.BlockPos(10, 64, -20),
            MovedItems = 20,
            MovedFuel = 3
        });
        Assert.Equal(20, depositRes.MovedItems);
        Assert.Equal(3, depositRes.MovedFuel);
    }

    private static T RoundTrip<T>(T packet)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, packet);
        ms.Position = 0;
        return Serializer.Deserialize<T>(ms);
    }
}
