namespace GenesisChess.Clock;

/// <summary>The two players as the clock sees them. Kept separate from the engine's colors on purpose.</summary>
public enum ClockSide
{
    White,
    Black,
}

public enum ClockStatus
{
    NotStarted,
    Running,
    Stopped,
}

/// <summary>A point-in-time reading of the clock, suitable for sending to clients.</summary>
public sealed record ClockSnapshot(
    TimeSpan WhiteRemaining,
    TimeSpan BlackRemaining,
    ClockSide? Running,
    DateTimeOffset TakenAt,
    bool IsUntimed)
{
    public TimeSpan RemainingFor(ClockSide side) => side == ClockSide.White ? WhiteRemaining : BlackRemaining;
}

/// <summary>
/// A two-sided chess clock with Fischer increment. Not thread-safe: the owner serializes access.
/// Time comes from <see cref="TimeProvider"/> so it can be faked in tests.
/// </summary>
public sealed class ChessClock
{
    private readonly TimeProvider _time;
    private readonly TimeSpan[] _remaining;
    private long _turnStartedAt;

    public ChessClock(TimeControl timeControl, TimeProvider? timeProvider = null)
    {
        TimeControl = timeControl;
        _time = timeProvider ?? TimeProvider.System;
        _remaining = [timeControl.Base, timeControl.Base];
    }

    public TimeControl TimeControl { get; }

    public ClockStatus Status { get; private set; } = ClockStatus.NotStarted;

    /// <summary>The side whose time is running, or null when the clock is not running.</summary>
    public ClockSide? Active { get; private set; }

    /// <summary>Starts running <paramref name="side"/>'s time.</summary>
    public void Start(ClockSide side)
    {
        if (Status != ClockStatus.NotStarted)
        {
            throw new InvalidOperationException("The clock has already been started.");
        }

        Status = ClockStatus.Running;
        Active = side;
        _turnStartedAt = _time.GetTimestamp();
    }

    /// <summary>
    /// Ends <paramref name="side"/>'s turn: deducts elapsed time, adds the increment and starts the
    /// opponent's time. Returns false (and stops the clock) if the side had already run out.
    /// </summary>
    public bool Press(ClockSide side)
    {
        if (Status != ClockStatus.Running || Active != side)
        {
            throw new InvalidOperationException($"It is not {side}'s time to press the clock.");
        }

        if (TimeControl.IsUntimed)
        {
            Active = Other(side);
            return true;
        }

        Settle();
        if (_remaining[(int)side] <= TimeSpan.Zero)
        {
            _remaining[(int)side] = TimeSpan.Zero;
            Status = ClockStatus.Stopped;
            Active = null;
            return false;
        }

        _remaining[(int)side] += TimeControl.Increment;
        Active = Other(side);
        return true;
    }

    /// <summary>Freezes both clocks, e.g. when the game ends.</summary>
    public void Stop()
    {
        if (Status == ClockStatus.Running && !TimeControl.IsUntimed)
        {
            Settle();
        }

        Status = ClockStatus.Stopped;
        Active = null;
    }

    public TimeSpan GetRemaining(ClockSide side)
    {
        if (TimeControl.IsUntimed)
        {
            return TimeSpan.MaxValue;
        }

        var remaining = _remaining[(int)side];
        if (Status == ClockStatus.Running && Active == side)
        {
            remaining -= _time.GetElapsedTime(_turnStartedAt);
        }

        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>Whether <paramref name="side"/> has no time left.</summary>
    public bool IsFlagged(ClockSide side) => !TimeControl.IsUntimed && GetRemaining(side) <= TimeSpan.Zero;

    /// <summary>How long until the running side flags, or null when nothing is running.</summary>
    public TimeSpan? TimeUntilFlag() =>
        Status == ClockStatus.Running && !TimeControl.IsUntimed && Active is { } side ? GetRemaining(side) : null;

    public ClockSnapshot Snapshot() => new(
        GetRemaining(ClockSide.White),
        GetRemaining(ClockSide.Black),
        Status == ClockStatus.Running ? Active : null,
        _time.GetUtcNow(),
        TimeControl.IsUntimed);

    private void Settle()
    {
        var side = (int)Active!.Value;
        _remaining[side] -= _time.GetElapsedTime(_turnStartedAt);
        _turnStartedAt = _time.GetTimestamp();
    }

    private static ClockSide Other(ClockSide side) => side == ClockSide.White ? ClockSide.Black : ClockSide.White;
}
