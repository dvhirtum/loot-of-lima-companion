using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LootOfLima.Rules;

namespace LootOfLima.Server.Serialization;

/// <summary>
/// Refuses, at the serializer, to turn secret game state into JSON.
/// </summary>
/// <remarks>
/// <para>
/// The type system already makes this unreachable: hub methods can only name <c>Contracts</c>
/// types, and <c>Contracts</c> cannot see <c>Rules</c>. This is the belt to that pair of braces —
/// it catches the case where secret state arrives at the serializer as <see cref="object"/>, which
/// no compiler check can see coming, and turns a silent leak into a loud failure.
/// </para>
/// <para>
/// It is a resolver rather than a converter because a converter has to be asked for by a type that
/// is already being serialized. Sitting at the front of the resolver chain means nothing gets
/// contract metadata without passing here first — which is a claim about wiring rather than about
/// this file, so <c>WiringTests</c> asserts it of the built application, at both exits: the hub's
/// JSON protocol and the minimal API's serializer.
/// </para>
/// </remarks>
internal sealed class HiddenStateGuard : IJsonTypeInfoResolver
{
    /// <summary>
    /// The types that carry hidden information. <see cref="GameState"/> holds everything;
    /// <see cref="Deal"/> holds the treasure; <see cref="Seat"/> holds a hand; and
    /// <see cref="HistoryEntry"/> holds whatever its subtype holds.
    /// </summary>
    /// <remarks>
    /// History is barred at the base rather than entry by entry. Three of the subtypes are private
    /// in part — a secret question keeps its range and answer, a token pass keeps the location that
    /// moved, and a failed accusation keeps the pair it named — and listing those three would leave
    /// the guard restating a rule that lives somewhere else, so that adding a fourth private entry
    /// to the engine would silently not be covered here. The rest of the subtypes are public in
    /// content, but none of them is the shape a client is owed: history reaches a viewer as
    /// <c>HistoryEntryView</c>, redacted by type, and an engine entry arriving at the serializer is
    /// a mistake whatever it happens to contain. Nothing in <c>Contracts</c> derives from this, so
    /// barring the base costs nothing a client was entitled to.
    /// </remarks>
    private static readonly Type[] Forbidden =
    [
        typeof(GameState),
        typeof(Deal),
        typeof(Seat),
        typeof(HistoryEntry),
    ];

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (Array.Exists(Forbidden, forbidden => forbidden.IsAssignableFrom(type)))
        {
            throw new InvalidOperationException(
                $"Refusing to serialize {type.Name}: it carries hidden game state. Everything sent to a "
                + "client is the output of the projection, and the projection produces a PlayerView.");
        }

        // Null means "not mine" — the rest of the chain resolves everything this guard allows.
        return null;
    }
}
