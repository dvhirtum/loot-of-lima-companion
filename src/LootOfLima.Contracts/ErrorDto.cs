namespace LootOfLima.Contracts;

/// <summary>
/// A rejected command, sent back to the one caller who made it.
/// </summary>
/// <remarks>
/// Errors are addressed, never broadcast: "you cannot ask that yet" is between the server and the
/// player who tried. A rejection sent to the table would say who is trying what and when, which is
/// information nobody is entitled to.
/// </remarks>
/// <param name="Code">A stable machine-readable code, so the client can phrase its own message.</param>
/// <param name="Message">The engine's own words, for a developer reading a log.</param>
public sealed record ErrorDto(string Code, string Message);
