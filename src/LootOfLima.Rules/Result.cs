namespace LootOfLima.Rules;

/// <summary>
/// The outcome of a command: either a value or a <see cref="GameError"/>, never both and never
/// neither. Rule violations are values, not exceptions — only programming errors throw.
/// </summary>
public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    private Result(TValue? value, GameError? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>The failure, or <c>null</c> when the command succeeded.</summary>
    public GameError? Error { get; }

    /// <summary>
    /// True when the command produced a value.
    /// </summary>
    /// <remarks>
    /// Checks the value rather than only the error, because no constructor runs for
    /// <c>default(Result&lt;T&gt;)</c>: it has no error and no value, and reporting that as success
    /// would hand the caller a null.
    /// </remarks>
    public bool IsSuccess => Error is null && _value is not null;

    /// <summary>The new value. Throws when the command failed, so check <see cref="IsSuccess"/> first.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            Error is null
                ? "This result was never produced by a command, so it holds neither a value nor an error."
                : $"The command failed ({Error}), so it produced no value.");

    public static Result<TValue> Success(TValue value) =>
        value is not null
            ? new Result<TValue>(value, null)
            : throw new ArgumentNullException(nameof(value), "A successful command must produce a value.");

    public static Result<TValue> Failure(GameError error) => new(default, error);

    /// <summary>Lets a guard return its error directly, without naming the result type again.</summary>
    public static implicit operator Result<TValue>(GameError error) => Failure(error);
}
