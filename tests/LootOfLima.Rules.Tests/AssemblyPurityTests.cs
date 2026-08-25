using System.Reflection;

namespace LootOfLima.Rules.Tests;

/// <summary>
/// Not tied to one type: this guards the whole assembly, so it sits at the root rather than in a
/// per-SUT folder.
/// </summary>
public class AssemblyPurityTests
{
    /// <summary>
    /// The rules engine must stay dependency-free so it remains trivially testable and so no
    /// framework concern can leak game state. If this fails, something added a dependency.
    /// </summary>
    [Fact]
    public void Should_reference_only_the_base_class_library_when_the_rules_assembly_is_inspected()
    {
        // Arrange
        Assembly rules = typeof(BoardLayout).Assembly;

        // Act
        string[] referenced = rules
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, "netstandard", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, "mscorlib", StringComparison.Ordinal))
            .ToArray();

        // Assert
        Assert.Empty(referenced);
    }
}
