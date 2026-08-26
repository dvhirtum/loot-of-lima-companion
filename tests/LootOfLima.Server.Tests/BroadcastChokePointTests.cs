using System.Text.RegularExpressions;

namespace LootOfLima.Server.Tests;

/// <summary>
/// Guards the shape of the server rather than its behaviour, so it sits at the root beside the
/// other whole-assembly checks rather than in a per-type folder.
/// </summary>
/// <remarks>
/// This reads the source. That is crude on purpose: the property being guarded is "no other code
/// path can send state", which is a statement about code that does not exist, and no runtime test
/// can observe the absence of a path nobody wrote. Reading the text catches the addition the moment
/// it is made, which is when it is cheap to argue about.
/// </remarks>
public class BroadcastChokePointTests
{
    /// <summary>
    /// Every SignalR fan-out that sends the same bytes to more than one connection. No single set
    /// of bytes is correct for two different seats, so none of these may carry state.
    /// </summary>
    private static readonly string[] FanOut =
    [
        "Clients.All",
        "Clients.AllExcept",
        "Clients.Group",
        "Clients.Groups",
        "Clients.GroupExcept",
        "Clients.Others",
        "Clients.OthersInGroup",
        "Clients.User",
        "Clients.Users",
    ];

    [Fact]
    public void Should_use_no_fan_out_anywhere_when_the_server_source_is_read()
    {
        // Arrange
        // Clients.Caller is deliberately absent from the list: it addresses exactly one connection,
        // which is how a rejected command is answered without telling the table about it.
        (string File, string Sighting)[] sightings =
        [
            .. ServerSources().SelectMany(
                file => FanOut
                    .Where(fanOut => Code(file).Contains(fanOut, StringComparison.Ordinal))
                    .Select(fanOut => (Path.GetFileName(file), fanOut))),
        ];

        // Act
        // Assert
        Assert.Empty(sightings);
    }

    [Fact]
    public void Should_send_state_from_one_file_only_when_the_server_source_is_read()
    {
        // Arrange
        // The projection is the thing being funnelled. If a second file starts calling Project, the
        // hidden-information boundary has stopped being one function guarded in one place.
        string[] projecting =
        [
            .. ServerSources()
                .Where(file => Code(file).Contains("Projector.Project", StringComparison.Ordinal))
                .Select(Path.GetFileName)!,
        ];

        // Act
        // Assert
        Assert.Equal(["GameBroadcaster.cs"], projecting);
    }

    /// <summary>
    /// A file with its comments taken out, so the checks read code rather than prose. The remarks
    /// in this server name the very constructs being barred, in order to explain why they are — and
    /// a check that could not tell an explanation from a use would forbid explaining itself.
    /// </summary>
    /// <remarks>
    /// Stripping only ever removes text, so the worst a mangled strip can do is hide a violation
    /// rather than invent one. There is no <c>//</c> inside a string literal anywhere in this
    /// server; if one ever appears, this turns into a reason to parse properly.
    /// </remarks>
    private static string Code(string file)
    {
        string text = File.ReadAllText(file);
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty,
            RegexOptions.Singleline);
        return Regex.Replace(text, @"//[^\n]*", string.Empty);
    }

    private static IEnumerable<string> ServerSources() =>
        Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot().FullName, "src", "LootOfLima.Server"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal));

    /// <summary>Walks up from the test binaries until the solution file appears.</summary>
    private static DirectoryInfo RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LootOfLima.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException(
            $"Could not find the repository root above '{AppContext.BaseDirectory}'.");
    }
}
