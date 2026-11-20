using SeraphQoL.Exploration;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class ExplorationAndHighlightTests
{
    [Fact]
    public void MatchingItemHighlightPolicy_MatchesByCodeOnlyWhenEnabled()
    {
        Assert.True(MatchingItemHighlightPolicy.ShouldHighlightSlot("game:stick", "game:stick", featureEnabled: true));
        Assert.False(MatchingItemHighlightPolicy.ShouldHighlightSlot("game:stick", "game:stone", featureEnabled: true));
        Assert.False(MatchingItemHighlightPolicy.ShouldHighlightSlot("game:stick", "game:stick", featureEnabled: false));
        Assert.False(MatchingItemHighlightPolicy.ShouldHighlightSlot(null, "game:stick", featureEnabled: true));
    }

    [Fact]
    public void GroundItemHighlightPlanner_FiltersRadiusFormatsLabelsAndComputesAlpha()
    {
        var candidates = new List<GroundItemCandidate>
        {
            new(1, "game:stick", "Stick", 12, 4.0, 1, 64, 1),
            new(2, "game:torch-basic-lit", "Torch", 1, 16.0, 10, 64, 10),
            new(3, "game:flint", "Flint", 2, 20.0, 20, 64, 1)
        };

        IReadOnlyList<GroundItemIndicator> indicators = GroundItemHighlightPlanner.BuildIndicators(
            candidates, maxRadius: 16, showLabels: true);

        Assert.Equal(2, indicators.Count);
        Assert.Equal(2L, indicators[0].EntityId); // furthest first for painter's order
        Assert.Equal("Torch", indicators[0].Label);
        Assert.True(indicators[0].Alpha >= 0.25f && indicators[0].Alpha < indicators[1].Alpha);

        Assert.Equal(1L, indicators[1].EntityId);
        Assert.Equal("Stick x12", indicators[1].Label);
        Assert.True(indicators[1].ShowLabel);
    }

    [Fact]
    public void GroundHighlightVisibilityState_ShowsWhileSneakingAndDelaysHideOnStandUp()
    {
        var state = new GroundHighlightVisibilityState();

        Assert.False(state.IsVisible(true, false, isSneaking: false, nowMs: 1000, standUpDelaySeconds: 2.5));
        Assert.True(state.IsVisible(true, false, isSneaking: true, nowMs: 1500, standUpDelaySeconds: 2.5));
        // Stand up at t=2000 -> remains visible until 2000 + 2500 = 4500
        Assert.True(state.IsVisible(true, false, isSneaking: false, nowMs: 2000, standUpDelaySeconds: 2.5));
        Assert.True(state.IsVisible(true, false, isSneaking: false, nowMs: 4499, standUpDelaySeconds: 2.5));
        Assert.False(state.IsVisible(true, false, isSneaking: false, nowMs: 4500, standUpDelaySeconds: 2.5));
    }

    [Fact]
    public void ProjectileTrackerState_TracksLandedOnceAndClearsOnRemoval()
    {
        var tracker = new ProjectileTrackerState();
        tracker.Register(101L, "player-1", "Copper Arrow");

        Assert.True(tracker.TryMarkLanded(101L, out TrackedProjectileRecord? record));
        Assert.NotNull(record);
        Assert.Equal("seraphqol-proj-101", record!.WaypointKey);
        Assert.Equal("Copper Arrow", ProjectileWaypointFormatter.FormatTitle(record.ItemName));

        // Second landed check should not re-emit Add packet
        Assert.False(tracker.TryMarkLanded(101L, out _));

        Assert.True(tracker.TryRemove(101L, out TrackedProjectileRecord? removed));
        Assert.Equal("player-1", removed!.OwnerPlayerUid);
        Assert.False(tracker.TryRemove(101L, out _));
    }

    [Fact]
    public void ProjectileTrackerState_PreservesRecordOnChunkUnloadAndExpiresAfterTimeout()
    {
        var tracker = new ProjectileTrackerState();
        tracker.Register(202L, "player-1", "Flint Arrow", createdAtMs: 1000L);
        Assert.True(tracker.TryMarkLanded(202L, out _));

        // Chunk unload should mark unloaded without removing waypoint immediately
        Assert.True(tracker.TryMarkChunkUnloaded(202L));
        Assert.Equal(1, tracker.Count);

        // Before 5 minutes (300_000 ms), nothing expires
        Assert.Empty(tracker.CollectExpired(nowMs: 250_000L, timeoutSeconds: 300));

        // After 5 minutes, expired record is removed and returned
        IReadOnlyList<TrackedProjectileRecord> expired = tracker.CollectExpired(nowMs: 301_000L, timeoutSeconds: 300);
        Assert.Single(expired);
        Assert.Equal("seraphqol-proj-202", expired[0].WaypointKey);
        Assert.Equal(0, tracker.Count);
    }
}
