using GenesisChess.Clock;
using Microsoft.Extensions.Time.Testing;

namespace GenesisChess.Clock.Tests;

public class ChessClockTests
{
    private readonly FakeTimeProvider _time = new();

    private ChessClock Clock(string control) =>
        new(TimeControl.TryParse(control, out var tc) ? tc : throw new ArgumentException(control), _time);

    [Fact]
    public void Clock_does_not_tick_before_start()
    {
        var clock = Clock("3+2");

        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(3), clock.GetRemaining(ClockSide.White));
    }

    [Fact]
    public void Only_the_active_side_loses_time()
    {
        var clock = Clock("3+0");
        clock.Start(ClockSide.White);

        _time.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(160), clock.GetRemaining(ClockSide.White));
        Assert.Equal(TimeSpan.FromMinutes(3), clock.GetRemaining(ClockSide.Black));
    }

    [Fact]
    public void Press_deducts_elapsed_adds_increment_and_switches()
    {
        var clock = Clock("3+2");
        clock.Start(ClockSide.White);

        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.True(clock.Press(ClockSide.White));

        Assert.Equal(TimeSpan.FromSeconds(172), clock.GetRemaining(ClockSide.White));
        Assert.Equal(ClockSide.Black, clock.Active);

        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(175), clock.GetRemaining(ClockSide.Black));
        Assert.Equal(TimeSpan.FromSeconds(172), clock.GetRemaining(ClockSide.White));
    }

    [Fact]
    public void Zero_increment_adds_nothing()
    {
        var clock = Clock("1+0");
        clock.Start(ClockSide.White);

        _time.Advance(TimeSpan.FromSeconds(1));
        clock.Press(ClockSide.White);

        Assert.Equal(TimeSpan.FromSeconds(59), clock.GetRemaining(ClockSide.White));
    }

    [Fact]
    public void Side_flags_exactly_at_zero()
    {
        var clock = Clock("1+0");
        clock.Start(ClockSide.White);

        _time.Advance(TimeSpan.FromSeconds(59.999));
        Assert.False(clock.IsFlagged(ClockSide.White));

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(clock.IsFlagged(ClockSide.White));
        Assert.Equal(TimeSpan.Zero, clock.GetRemaining(ClockSide.White));
    }

    [Fact]
    public void Pressing_after_flag_fails_and_stops_the_clock()
    {
        var clock = Clock("1+5");
        clock.Start(ClockSide.White);

        _time.Advance(TimeSpan.FromSeconds(61));

        Assert.False(clock.Press(ClockSide.White));
        Assert.Equal(ClockStatus.Stopped, clock.Status);
        Assert.Equal(TimeSpan.Zero, clock.GetRemaining(ClockSide.White));
    }

    [Fact]
    public void Pressing_out_of_turn_throws()
    {
        var clock = Clock("3+2");
        clock.Start(ClockSide.White);

        Assert.Throws<InvalidOperationException>(() => clock.Press(ClockSide.Black));
    }

    [Fact]
    public void Stop_freezes_remaining_time()
    {
        var clock = Clock("3+2");
        clock.Start(ClockSide.White);
        _time.Advance(TimeSpan.FromSeconds(30));

        clock.Stop();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromSeconds(150), clock.GetRemaining(ClockSide.White));
        Assert.Null(clock.TimeUntilFlag());
    }

    [Fact]
    public void Time_until_flag_tracks_the_running_side()
    {
        var clock = Clock("1+0");
        clock.Start(ClockSide.White);
        _time.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(TimeSpan.FromSeconds(45), clock.TimeUntilFlag());
    }

    [Fact]
    public void Untimed_clock_never_flags()
    {
        var clock = new ChessClock(TimeControl.Untimed, _time);
        clock.Start(ClockSide.White);

        _time.Advance(TimeSpan.FromDays(3));

        Assert.False(clock.IsFlagged(ClockSide.White));
        Assert.True(clock.Press(ClockSide.White));
        Assert.Null(clock.TimeUntilFlag());
        Assert.True(clock.Snapshot().IsUntimed);
    }

    [Fact]
    public void Snapshot_reports_running_side_and_time()
    {
        var clock = Clock("5+3");
        clock.Start(ClockSide.White);
        _time.Advance(TimeSpan.FromSeconds(1));

        var snapshot = clock.Snapshot();

        Assert.Equal(ClockSide.White, snapshot.Running);
        Assert.Equal(TimeSpan.FromSeconds(299), snapshot.WhiteRemaining);
        Assert.Equal(_time.GetUtcNow(), snapshot.TakenAt);
    }
}

public class TimeControlTests
{
    [Theory]
    [InlineData("3+2", 180, 2)]
    [InlineData("10+0", 600, 0)]
    [InlineData("0.5+0", 30, 0)]
    public void Parses_minutes_plus_seconds(string text, int baseSeconds, int increment)
    {
        Assert.True(TimeControl.TryParse(text, out var tc));
        Assert.Equal(TimeSpan.FromSeconds(baseSeconds), tc.Base);
        Assert.Equal(TimeSpan.FromSeconds(increment), tc.Increment);
        Assert.Equal(text, tc.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("0+2")]
    [InlineData("3+-1")]
    public void Rejects_invalid_text(string text) => Assert.False(TimeControl.TryParse(text, out _));

    [Fact]
    public void Untimed_round_trips()
    {
        Assert.True(TimeControl.TryParse("untimed", out var tc));
        Assert.True(tc.IsUntimed);
        Assert.Equal("Untimed", tc.Category);
    }

    [Theory]
    [InlineData("1+0", "Bullet")]
    [InlineData("3+2", "Blitz")]
    [InlineData("10+5", "Rapid")]
    [InlineData("30+20", "Classical")]
    public void Categorizes_by_estimated_duration(string text, string category)
    {
        TimeControl.TryParse(text, out var tc);
        Assert.Equal(category, tc.Category);
    }

    [Fact]
    public void Presets_are_all_timed() => Assert.All(TimeControl.Presets, tc => Assert.False(tc.IsUntimed));
}
