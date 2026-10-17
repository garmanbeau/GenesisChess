using System.Diagnostics.CodeAnalysis;

namespace GenesisChess.Engine;

/// <summary>Outcome of applying an action: the new state, or the reason it was rejected.</summary>
public sealed record ActionResult
{
    private ActionResult(GameState? state, string? error)
    {
        State = state;
        Error = error;
    }

    public GameState? State { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(State))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => State is not null;

    public static ActionResult Success(GameState state) => new(state, null);

    public static ActionResult Failure(string error) => new(null, error);
}
