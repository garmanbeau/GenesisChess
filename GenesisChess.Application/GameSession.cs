using GenesisChess.Clock;
using GenesisChess.Engine;

namespace GenesisChess.Application;

/// <summary>
/// One live game: rules state, seats, draw offers and the clock. All mutations are serialized by a
/// lock and announced through <see cref="Changed"/> after the lock is released.
/// </summary>
/// <remarks>
/// Clock policy: nothing ticks until White's first action, which starts Black's time. From then on
/// every action presses the clock (deduct elapsed, add increment). A timer fires when the side to
/// move runs out so a flag is caught even if nobody acts.
/// </remarks>
public sealed class GameSession : IDisposable
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ChessClock _clock;
    private readonly List<MoveRecord> _moves = [];
    private ITimer? _flagTimer;
    private string? _whiteToken;
    private string? _blackToken;
    private GameState _state;
    private GameOutcome? _outcome;
    private Color? _drawOfferedBy;

    internal GameSession(string id, TimeControl timeControl, TimeProvider time, GameState? initialState = null)
    {
        Id = id;
        TimeControl = timeControl;
        _time = time;
        _state = initialState ?? GameState.Initial();
        _clock = new ChessClock(timeControl, time);
        CreatedAt = LastActivity = time.GetUtcNow();
    }

    /// <summary>Raised after any change. Handlers run on the caller's thread and must not block.</summary>
    public event Action<GameSession>? Changed;

    public string Id { get; }

    public TimeControl TimeControl { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastActivity { get; private set; }

    public GameStatus Status
    {
        get
        {
            lock (_gate)
            {
                return StatusUnsafe;
            }
        }
    }

    private GameStatus StatusUnsafe => _outcome is not null
        ? GameStatus.Finished
        : _whiteToken is not null && _blackToken is not null ? GameStatus.InProgress : GameStatus.WaitingForOpponent;

    /// <summary>
    /// Resolves a viewer: an existing token keeps its seat, otherwise the next open seat is taken,
    /// otherwise the viewer spectates.
    /// </summary>
    public JoinResult Join(string? token)
    {
        JoinResult result;
        lock (_gate)
        {
            var seat = SeatOfUnsafe(token);
            if (seat != Seat.Spectator)
            {
                return new JoinResult(seat, token);
            }

            if (_outcome is not null || (_whiteToken is not null && _blackToken is not null))
            {
                return new JoinResult(Seat.Spectator, null);
            }

            var open = _whiteToken is null ? Seat.White : Seat.Black;
            result = new JoinResult(open, ClaimUnsafe(open));
        }

        RaiseChanged();
        return result;
    }

    internal string Claim(Seat seat)
    {
        lock (_gate)
        {
            return ClaimUnsafe(seat);
        }
    }

    public Seat SeatOf(string? token)
    {
        lock (_gate)
        {
            return SeatOfUnsafe(token);
        }
    }

    public CommandResult Submit(string? token, GameAction action) => Mutate(token, color =>
    {
        if (StatusUnsafe != GameStatus.InProgress)
        {
            return CommandResult.Fail(StatusUnsafe == GameStatus.Finished ? "The game is over." : "Waiting for an opponent to join.");
        }

        if (color != _state.SideToMove)
        {
            return CommandResult.Fail("It is not your turn.");
        }

        if (_clock.IsFlagged(ToClock(color)))
        {
            FinishOnTime(color);
            return CommandResult.Fail("Your time ran out.");
        }

        var before = _state;
        var result = Rules.Apply(before, action);
        if (!result.IsSuccess)
        {
            return CommandResult.Fail(result.Error);
        }

        _state = result.State;
        if (_clock.Status == ClockStatus.NotStarted)
        {
            _clock.Start(ToClock(color.Opponent())); // White's first action is free.
        }
        else
        {
            _clock.Press(ToClock(color));
        }

        _moves.Add(new MoveRecord(_moves.Count + 1, color, action, Notation.ToSan(before, action)));
        if (_drawOfferedBy == color.Opponent())
        {
            _drawOfferedBy = null; // Playing on declines a pending offer.
        }

        if (Outcomes.Evaluate(_state) is { } outcome)
        {
            Finish(outcome);
        }
        else
        {
            ScheduleFlagCheck();
        }

        return CommandResult.Ok;
    });

    public CommandResult Resign(string? token) => Mutate(token, color =>
    {
        if (_outcome is not null)
        {
            return CommandResult.Fail("The game is over.");
        }

        Finish(GameOutcome.WinFor(color.Opponent(), OutcomeReason.Resignation));
        return CommandResult.Ok;
    });

    /// <summary>Offers a draw, or accepts the opponent's pending offer.</summary>
    public CommandResult OfferDraw(string? token) => Mutate(token, color =>
    {
        if (StatusUnsafe != GameStatus.InProgress)
        {
            return CommandResult.Fail("Draws can only be offered during a game.");
        }

        if (_drawOfferedBy == color.Opponent())
        {
            Finish(GameOutcome.Draw(OutcomeReason.Agreement));
        }
        else
        {
            _drawOfferedBy = color;
        }

        return CommandResult.Ok;
    });

    public CommandResult DeclineDraw(string? token) => Mutate(token, color =>
    {
        if (_drawOfferedBy != color.Opponent())
        {
            return CommandResult.Fail("There is no draw offer to decline.");
        }

        _drawOfferedBy = null;
        return CommandResult.Ok;
    });

    /// <summary>A consistent snapshot of the game from the perspective of <paramref name="token"/>'s holder.</summary>
    public GameView GetView(string? token)
    {
        lock (_gate)
        {
            var seat = SeatOfUnsafe(token);
            var view = new GameView
            {
                Id = Id,
                TimeControl = TimeControl,
                Status = StatusUnsafe,
                Seat = seat,
                State = _state,
                Outcome = _outcome,
                InCheck = Attacks.IsInCheck(_state.Board, _state.SideToMove),
                Moves = [.. _moves],
                WhiteSeated = _whiteToken is not null,
                BlackSeated = _blackToken is not null,
                DrawOfferedBy = _drawOfferedBy,
                Clock = _clock.Snapshot(),
            };

            if (view.IsYourTurn)
            {
                view = view with
                {
                    LegalMoves = [.. MoveGenerator.LegalMoves(_state)],
                    LegalPlacements = [.. MoveGenerator.LegalPlacements(_state)],
                };
            }

            return view;
        }
    }

    /// <summary>Ends the game if the side to move has run out of time. Returns true if it did.</summary>
    public bool CheckFlag()
    {
        lock (_gate)
        {
            if (!CheckFlagUnsafe())
            {
                return false;
            }
        }

        RaiseChanged();
        return true;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _flagTimer?.Dispose();
            _flagTimer = null;
        }
    }

    private bool CheckFlagUnsafe()
    {
        if (_outcome is not null || _clock.Active is not { } active || !_clock.IsFlagged(active))
        {
            return false;
        }

        FinishOnTime(FromClock(active));
        return true;
    }

    private void OnFlagTimer()
    {
        bool flagged;
        lock (_gate)
        {
            flagged = CheckFlagUnsafe();
            if (!flagged && _outcome is null)
            {
                ScheduleFlagCheck(); // Fired early (timer granularity); try again.
            }
        }

        if (flagged)
        {
            RaiseChanged();
        }
    }

    private void ScheduleFlagCheck()
    {
        _flagTimer?.Dispose();
        _flagTimer = _clock.TimeUntilFlag() is { } due
            ? _time.CreateTimer(_ => OnFlagTimer(), null, due + TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan)
            : null;
    }

    private void FinishOnTime(Color flagged)
    {
        var winner = flagged.Opponent();
        Finish(Outcomes.CanEverMate(_state, winner)
            ? GameOutcome.WinFor(winner, OutcomeReason.Timeout)
            : GameOutcome.Draw(OutcomeReason.TimeoutVsInsufficientMaterial));
    }

    private CommandResult Mutate(string? token, Func<Color, CommandResult> command)
    {
        CommandResult result;
        bool changed;
        lock (_gate)
        {
            var color = SeatOfUnsafe(token) switch
            {
                Seat.White => Color.White,
                Seat.Black => Color.Black,
                _ => (Color?)null,
            };

            if (color is null)
            {
                return CommandResult.Fail("Spectators cannot play.");
            }

            var outcomeBefore = _outcome;
            result = command(color.Value);

            // A failed command can still end the game (e.g. moving after the flag fell).
            changed = result.Succeeded || !Equals(outcomeBefore, _outcome);
            if (result.Succeeded)
            {
                LastActivity = _time.GetUtcNow();
            }
        }

        if (changed)
        {
            RaiseChanged();
        }

        return result;
    }

    private void Finish(GameOutcome outcome)
    {
        _outcome = outcome;
        _drawOfferedBy = null;
        _clock.Stop();
        _flagTimer?.Dispose();
        _flagTimer = null;
    }

    private static ClockSide ToClock(Color color) => color == Color.White ? ClockSide.White : ClockSide.Black;

    private static Color FromClock(ClockSide side) => side == ClockSide.White ? Color.White : Color.Black;

    private string ClaimUnsafe(Seat seat)
    {
        var token = Identifiers.NewPlayerToken();
        switch (seat)
        {
            case Seat.White when _whiteToken is null:
                _whiteToken = token;
                break;
            case Seat.Black when _blackToken is null:
                _blackToken = token;
                break;
            default:
                throw new InvalidOperationException($"Seat {seat} is not available.");
        }

        LastActivity = _time.GetUtcNow();
        return token;
    }

    private Seat SeatOfUnsafe(string? token) =>
        Identifiers.TokensMatch(_whiteToken, token) ? Seat.White
        : Identifiers.TokensMatch(_blackToken, token) ? Seat.Black
        : Seat.Spectator;

    private void RaiseChanged()
    {
        if (Changed is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action<GameSession>>())
        {
            try
            {
                handler(this);
            }
#pragma warning disable CA1031 // One broken subscriber (e.g. a dead circuit) must not starve the others.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }
}
