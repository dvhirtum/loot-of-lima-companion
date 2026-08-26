using System.Security.Cryptography;

namespace LootOfLima.Server.Games;

/// <summary>
/// The short code a player types, or reaches by scanning the QR on the host's screen.
/// </summary>
/// <remarks>
/// Four characters from a 24-letter alphabet: I and O are left out because they are read aloud
/// across a table and mistaken for 1 and 0. That is 331,776 codes, which is not a secret and is not
/// meant to be — the code addresses a game, and the opaque player token is what proves identity.
/// </remarks>
internal static class RoomCodes
{
    internal const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    internal const int Length = 4;

    public static string Next() =>
        string.Create(Length, 0, (span, _) =>
        {
            for (int index = 0; index < span.Length; index++)
            {
                span[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
}
