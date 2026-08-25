using System.Reflection;
using System.Xml.Linq;

namespace LootOfLima.Rules.Tests;

/// <summary>
/// Not tied to one type: these guard whole assemblies, so they sit at the root rather than in a
/// per-SUT folder.
/// </summary>
/// <remarks>
/// The checks read the project files rather than the compiled assemblies. Reflection is not enough:
/// <see cref="Assembly.GetReferencedAssemblies"/> reports only the references Roslyn actually
/// emitted, and Roslyn omits any reference whose types are never used. A dependency added but not
/// yet touched would ship at runtime while leaving a reflection-based test green, which is the
/// opposite of what these invariants are for.
/// </remarks>
public class AssemblyPurityTests
{
    /// <summary>
    /// The rules engine must stay dependency-free so it remains trivially testable and so no
    /// framework concern can reach into game state.
    /// </summary>
    [Fact]
    public void Should_declare_no_dependency_at_all_when_the_rules_project_is_read()
    {
        // Arrange
        XDocument project = LoadProject("src/LootOfLima.Rules/LootOfLima.Rules.csproj");

        // Act
        string[] dependencies =
        [
            .. project.Descendants("PackageReference").Concat(project.Descendants("ProjectReference"))
                .Select(reference => reference.Attribute("Include")?.Value ?? "(unnamed)"),
        ];

        // Assert
        Assert.Empty(dependencies);
    }

    /// <summary>
    /// The first layer of the hidden-information design: because Contracts cannot see Rules,
    /// GameState, Deal, Seat and SecretQuestionEntry are simply unreachable from any hub signature,
    /// so no future hub method can return one by accident.
    /// </summary>
    [Fact]
    public void Should_declare_no_reference_to_the_rules_engine_when_the_contracts_project_is_read()
    {
        // Arrange
        XDocument project = LoadProject("src/LootOfLima.Contracts/LootOfLima.Contracts.csproj");

        // Act
        string[] references =
        [
            .. project.Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
                .Where(include => include.Contains("LootOfLima.Rules", StringComparison.Ordinal)),
        ];

        // Assert
        Assert.Empty(references);
    }

    /// <summary>
    /// The purity check above reads a file, so it would pass just as happily against a path that
    /// no longer exists. This is what stops a rename from turning it into a no-op.
    /// </summary>
    [Fact]
    public void Should_find_the_projects_when_the_repository_is_walked_from_the_test_assembly()
    {
        // Arrange
        DirectoryInfo root = RepositoryRoot();

        // Act
        bool rules = File.Exists(Path.Combine(root.FullName, "src/LootOfLima.Rules/LootOfLima.Rules.csproj"));
        bool contracts = File.Exists(Path.Combine(root.FullName, "src/LootOfLima.Contracts/LootOfLima.Contracts.csproj"));

        // Assert
        Assert.True(rules);
        Assert.True(contracts);
    }

    private static XDocument LoadProject(string relativePath) =>
        XDocument.Load(Path.Combine(RepositoryRoot().FullName, relativePath));

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
