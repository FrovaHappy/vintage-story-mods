using SeraphQoL.Interaction;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class ToolModeRadialPlannerTests
{
    private static readonly RadialGeometryConfig DefaultGeometry = new(OuterRadius: 130, InnerRadius: 48, DeadZoneRadius: 34);

    [Fact]
    public void BuildSectors_DistributesModesEvenlyStartingAtTopClockwise()
    {
        var items = new List<ToolModeDescriptor>
        {
            new(0, "1x1", "1x1", "Single voxel", IsSelected: true, IsEnabled: true),
            new(1, "2x2", "2x2", "2x2 cube", IsSelected: false, IsEnabled: true),
            new(2, "4x4", "4x4", "4x4 cube", IsSelected: false, IsEnabled: true),
            new(3, "8x8", "8x8", "8x8 cube", IsSelected: false, IsEnabled: true)
        };

        RadialMenuPage page = ToolModeRadialPlanner.BuildSectors(items, pageIndex: 0, maxSectorsPerPage: 10);

        Assert.Equal(1, page.TotalPages);
        Assert.Equal(4, page.Sectors.Count);
        Assert.True(page.Sectors[0].IsSelected);
        Assert.Equal(0, page.Sectors[0].NativeModeIndex);

        // Sector 0 should be centered near top (-PI/2) or starting at top
        (double x0, double y0) = ToolModeRadialPlanner.ComputeIconCenter(page.Sectors[0], DefaultGeometry);
        (double x1, double y1) = ToolModeRadialPlanner.ComputeIconCenter(page.Sectors[1], DefaultGeometry);
        Assert.True(y0 < 0); // top
        Assert.True(x1 > 0); // right
    }

    [Fact]
    public void HitTestSector_DetectsDeadZoneAndDirectionOutsideRing()
    {
        var items = Enumerable.Range(0, 4)
            .Select(i => new ToolModeDescriptor(i, $"m{i}", $"Mode {i}", "", i == 0, true))
            .ToList();

        RadialMenuPage page = ToolModeRadialPlanner.BuildSectors(items);

        // Inside dead zone -> -1
        Assert.Equal(-1, ToolModeRadialPlanner.HitTestSector(5, -5, page.Sectors, DefaultGeometry));

        // Top (0, -80) -> Sector 0
        Assert.Equal(0, ToolModeRadialPlanner.HitTestSector(0, -80, page.Sectors, DefaultGeometry));

        // Right (160, 0) even outside OuterRadius -> Sector 1
        Assert.Equal(1, ToolModeRadialPlanner.HitTestSector(160, 0, page.Sectors, DefaultGeometry));

        // Bottom (0, 90) -> Sector 2
        Assert.Equal(2, ToolModeRadialPlanner.HitTestSector(0, 90, page.Sectors, DefaultGeometry));

        // Left (-90, 0) -> Sector 3
        Assert.Equal(3, ToolModeRadialPlanner.HitTestSector(-90, 0, page.Sectors, DefaultGeometry));
    }

    [Fact]
    public void BuildSectors_PaginatesWhenExceedingMaxSectorsPerPage()
    {
        var items = Enumerable.Range(0, 14)
            .Select(i => new ToolModeDescriptor(i, $"m{i}", $"Mode {i}", "", i == 11, true))
            .ToList();

        RadialMenuPage page0 = ToolModeRadialPlanner.BuildSectors(items, pageIndex: 0, maxSectorsPerPage: 10);
        RadialMenuPage page1 = ToolModeRadialPlanner.BuildSectors(items, pageIndex: 1, maxSectorsPerPage: 10);

        Assert.Equal(2, page0.TotalPages);
        Assert.Equal(10, page0.Sectors.Count);
        Assert.Equal(4, page1.Sectors.Count);
        Assert.Equal(11, page1.Sectors[1].NativeModeIndex);
        Assert.True(page1.Sectors[1].IsSelected);
    }

    [Fact]
    public void RadialHoldReleaseTracker_DoesNotCloseWhenTappedQuicklyAndMouseMoves()
    {
        var tracker = new RadialHoldReleaseTracker(holdThresholdMs: 180);
        tracker.Reset(openedAtMs: 1000);

        // User tapped Alt+F and released F quickly before 180ms while mouse was still near center
        Assert.False(tracker.ShouldSelectOnRelease(true, isHotkeyDown: false, nowMs: 1080, hoveredSectorIndex: -1));

        // At 260ms user moves mouse slightly out of center onto sector 2 -> must NOT close!
        Assert.False(tracker.ShouldSelectOnRelease(true, isHotkeyDown: false, nowMs: 1260, hoveredSectorIndex: 2));
    }

    [Fact]
    public void RadialHoldReleaseTracker_SelectsOnlyWhenHeldPastThresholdThenReleasedOnSector()
    {
        var tracker = new RadialHoldReleaseTracker(holdThresholdMs: 180);
        tracker.Reset(openedAtMs: 1000);

        // Key remains held past 180ms
        Assert.False(tracker.ShouldSelectOnRelease(true, isHotkeyDown: true, nowMs: 1120, hoveredSectorIndex: -1));
        Assert.False(tracker.ShouldSelectOnRelease(true, isHotkeyDown: true, nowMs: 1220, hoveredSectorIndex: 1));

        // Key released while hovering sector 1 -> triggers selection
        Assert.True(tracker.ShouldSelectOnRelease(true, isHotkeyDown: false, nowMs: 1250, hoveredSectorIndex: 1));
    }
}
