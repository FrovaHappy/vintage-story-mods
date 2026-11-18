using Vintagestory.API.MathTools;
using Xunit;

namespace Comerciantes.Tests;

public sealed class KnownTradersRegistryTests
{
    private static KnownTraderRecord CreateRecord(long entityId, string name, int money) => new()
    {
        EntityId = entityId,
        TraderName = name,
        Archetype = "commodities",
        Position = new BlockPos(100, 110, 200),
        LastSeenTotalDays = 5.0,
        NextRefreshTotalDays = 12.0,
        TraderMoney = money,
        TraderRewardBonusPct = 30.0
    };

    [Fact]
    public void RecordVisit_StoresUpdatesAndIsolatesByPlayer()
    {
        var registry = new KnownTradersRegistry();

        Assert.Empty(registry.GetKnownTraders("player-a"));

        registry.RecordVisit("player-a", CreateRecord(10, "OldName", 20));
        registry.RecordVisit("player-a", CreateRecord(10, "UpdatedName", 35));
        registry.RecordVisit("player-b", CreateRecord(20, "OtherTrader", 40));

        IReadOnlyCollection<KnownTraderRecord> listA = registry.GetKnownTraders("player-a");
        KnownTraderRecord recA = Assert.Single(listA);
        Assert.Equal("UpdatedName", recA.TraderName);
        Assert.Equal(35, recA.TraderMoney);

        KnownTraderRecord recB = Assert.Single(registry.GetKnownTraders("player-b"));
        Assert.Equal(20, recB.EntityId);
    }

    [Fact]
    public void ExportAndImportStateJson_RoundtripsRecords()
    {
        var original = new KnownTradersRegistry();
        original.RecordVisit("player-a", CreateRecord(77, "Marco", 45));

        string json = original.ExportStateJson();

        var restored = new KnownTradersRegistry();
        restored.ImportStateJson(json);

        KnownTraderRecord rec = Assert.Single(restored.GetKnownTraders("player-a"));
        Assert.Equal(77, rec.EntityId);
        Assert.Equal("Marco", rec.TraderName);
        Assert.Equal(45, rec.TraderMoney);
    }

    [Fact]
    public void HasUpdatedList_DetectsExpiredRotationInLastVisitedModeOnly()
    {
        // Live mode never hides grids
        Assert.False(KnownTradersRegistry.HasUpdatedList(10.0, 12.0, 15.0, isLiveSyncMode: true));

        // Snapshot still valid
        Assert.False(KnownTradersRegistry.HasUpdatedList(10.0, 15.0, 12.0, isLiveSyncMode: false));

        // Snapshot rotated (currentDays reached NextRefreshTotalDays)
        Assert.True(KnownTradersRegistry.HasUpdatedList(10.0, 15.0, 15.0, isLiveSyncMode: false));

        // Snapshot rotated (NextRefreshTotalDays behind LastSeenTotalDays)
        Assert.True(KnownTradersRegistry.HasUpdatedList(10.0, 8.0, 10.5, isLiveSyncMode: false));
    }

    [Fact]
    public void FilterAndSortByLastVisited_OrdersNewestVisitFirstAndFiltersByArchetype()
    {
        var list = new List<KnownTraderDto>
        {
            new() { EntityId = 1, TraderName = "Old", Archetype = "luxuries", LastSeenTotalDays = 2.0 },
            new() { EntityId = 2, TraderName = "Newest", Archetype = "artisan", LastSeenTotalDays = 10.0 },
            new() { EntityId = 3, TraderName = "Mid", Archetype = "luxuries", LastSeenTotalDays = 6.0 }
        };

        List<KnownTraderDto> all = KnownTradersRegistry.FilterAndSortByLastVisited(list, null);
        Assert.Equal([2L, 3L, 1L], all.Select(t => t.EntityId).ToArray());

        List<KnownTraderDto> onlyLuxuries = KnownTradersRegistry.FilterAndSortByLastVisited(list, "luxuries");
        Assert.Equal([3L, 1L], onlyLuxuries.Select(t => t.EntityId).ToArray());
    }

    [Fact]
    public void FormatRotationRemaining_FormatsDaysAndHours()
    {
        Assert.Equal("2d 3h", TraderUiHelpers.FormatRotationRemaining(2.125));
        Assert.Equal("6h", TraderUiHelpers.FormatRotationRemaining(0.25));
        Assert.Equal("0h", TraderUiHelpers.FormatRotationRemaining(-1.0));
        Assert.Equal("1d 0h", TraderUiHelpers.FormatRotationRemaining(0.99));
    }
}
