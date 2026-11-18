using Xunit;

namespace SystemUI.Tests;

public sealed class SystemUIThemeTests
{
    [Theory]
    [InlineData(25.4, false, true, "▲+25.4%")]
    [InlineData(25.4, true, true, "▲ +25.4%")]
    [InlineData(-12.5, false, true, "▼-12.5%")]
    [InlineData(-12.5, true, true, "▼ -12.5%")]
    [InlineData(0.2, false, true, "=0.2%")]
    [InlineData(0.2, true, true, "= 0.2%")]
    [InlineData(0.2, true, false, "=")]
    [InlineData(-0.4, false, false, "=")]
    public void FormatTrend_FormatsPositiveNegativeAndNeutral(
        double deltaPct,
        bool spaced,
        bool includeNeutralPct,
        string expected)
    {
        Assert.Equal(expected, SystemUITheme.FormatTrend(deltaPct, spaced, includeNeutralPct));
    }
}
