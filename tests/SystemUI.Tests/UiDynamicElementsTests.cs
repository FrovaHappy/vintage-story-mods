using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Xunit;

namespace SystemUI.Tests;

public sealed class UiDynamicElementsTests
{
    private static ElementBounds CreateResolvedBounds()
    {
        RuntimeEnv.GUIScale = 1.0f;
        ElementBounds bounds = ElementBounds.Fixed(10, 10, 400, 120).WithEmptyParent();
        bounds.CalcWorldBounds();
        return bounds;
    }

    [Fact]
    public void DynamicPanelElement_DoesNotInterceptMouseEventsInsideBounds()
    {
        ElementBounds bounds = CreateResolvedBounds();
        Assert.True(bounds.PointInside(50, 50));

        var panel = new UiDynamicPanelElement(null!, bounds, 2, 0.85f);
        Assert.False(panel.IsPositionInside(50, 50));

        var down = new MouseEvent(50, 50, EnumMouseButton.Left, 0);
        panel.OnMouseDown(null!, down);
        Assert.False(down.Handled);

        var up = new MouseEvent(50, 50, EnumMouseButton.Left, 0);
        panel.OnMouseUp(null!, up);
        Assert.False(up.Handled);
    }

    [Fact]
    public void DynamicDividerElement_DoesNotInterceptMouseEventsInsideBounds()
    {
        ElementBounds bounds = CreateResolvedBounds();
        Assert.True(bounds.PointInside(50, 50));

        var divider = new UiDynamicDividerElement(null!, bounds);
        Assert.False(divider.IsPositionInside(50, 50));

        var down = new MouseEvent(50, 50, EnumMouseButton.Left, 0);
        divider.OnMouseDown(null!, down);
        Assert.False(down.Handled);

        var up = new MouseEvent(50, 50, EnumMouseButton.Left, 0);
        divider.OnMouseUp(null!, up);
        Assert.False(up.Handled);
    }
}
