using GenesisChess.Clock;

namespace GenesisChess.Clock.Tests;

public class LowTimeThresholdTests
{
    private static TimeControl Parse(string control) =>
        TimeControl.TryParse(control, out var tc) ? tc : throw new ArgumentException(control);

    [Theory]
    [InlineData("1+0", 10)]   // 6 s raw, raised to the 10 s floor
    [InlineData("2+1", 16)]   // (120 + 40) / 10
    [InlineData("3+0", 18)]
    [InlineData("3+2", 26)]   // (180 + 80) / 10
    [InlineData("5+0", 30)]
    [InlineData("5+3", 42)]   // (300 + 120) / 10
    [InlineData("10+0", 60)]
    [InlineData("15+0", 60)]  // 90 s raw, capped at 60 s
    [InlineData("15+10", 60)]
    [InlineData("30+20", 60)]
    [InlineData("0.25+0", 10)]
    public void Threshold_scales_with_time_control_within_bounds(string control, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Parse(control).LowTimeThreshold);
    }

    [Fact]
    public void Untimed_is_never_low()
    {
        Assert.Equal(TimeSpan.Zero, TimeControl.Untimed.LowTimeThreshold);
        Assert.False(TimeControl.Untimed.IsLowTime(TimeSpan.Zero));
    }

    [Fact]
    public void Low_time_is_strictly_below_the_threshold()
    {
        var tc = Parse("5+0"); // 30 s

        Assert.False(tc.IsLowTime(TimeSpan.FromSeconds(31)));
        Assert.False(tc.IsLowTime(TimeSpan.FromSeconds(30)));
        Assert.True(tc.IsLowTime(TimeSpan.FromSeconds(29.9)));
        Assert.True(tc.IsLowTime(TimeSpan.Zero));
    }

    [Fact]
    public void Threshold_is_always_shorter_than_the_starting_time_for_presets()
    {
        foreach (var tc in TimeControl.Presets)
        {
            Assert.False(tc.IsLowTime(tc.Base), tc.ToString());
        }
    }
}
