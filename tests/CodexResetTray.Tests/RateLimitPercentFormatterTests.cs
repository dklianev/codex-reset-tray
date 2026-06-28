using CodexResetTray.Core.Display;

namespace CodexResetTray.Tests;

public sealed class RateLimitPercentFormatterTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(11, 89)]
    [InlineData(100, 0)]
    [InlineData(-20, 100)]
    [InlineData(140, 0)]
    public void RemainingPercent_inverts_and_clamps_used_percent(int usedPercent, int expectedRemaining)
    {
        Assert.Equal(expectedRemaining, RateLimitPercentFormatter.RemainingPercent(usedPercent));
    }

    [Fact]
    public void FormatRemainingPercent_returns_left_copy()
    {
        Assert.Equal("89% left", RateLimitPercentFormatter.FormatRemainingPercent(11));
    }

    [Fact]
    public void FormatRemainingPercentValue_returns_numeric_copy_for_dense_ui()
    {
        Assert.Equal("89%", RateLimitPercentFormatter.FormatRemainingPercentValue(11));
    }

    [Fact]
    public void FormatOptionalRemainingPercent_returns_placeholder_for_missing_data()
    {
        Assert.Equal("--", RateLimitPercentFormatter.FormatOptionalRemainingPercent(null));
    }
}
