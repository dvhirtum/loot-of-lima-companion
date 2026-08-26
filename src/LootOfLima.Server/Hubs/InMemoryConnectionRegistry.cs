using System.Collections.Concurrent;

namespace LootOfLima.Server.Hubs;

/// <inheritdoc cref="IConnectionRegistry"/>
internal sealed class InMemoryConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, Connection> _byConnectionId = new(StringComparer.Ordinal);

    public void Attach(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _byConnectionId[connection.ConnectionId] = connection;
    }

    public void Detach(string connectionId) => _byConnectionId.TryRemove(connectionId, out _);

    public IReadOnlyList<Connection> InGame(string gameId) =>
    [
        .. _byConnectionId.Values.Where(
            connection => string.Equals(connection.GameId, gameId, StringComparison.Ordinal)),
    ];

    public Connection? Find(string connectionId) =>
        _byConnectionId.TryGetValue(connectionId, out Connection? connection) ? connection : null;
}
