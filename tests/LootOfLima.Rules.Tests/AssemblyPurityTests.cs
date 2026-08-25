namespace LootOfLima.Rules.Tests;

public class AssemblyPurityTests
{
    /// <summary>
    /// The rules engine must stay dependency-free so it remains trivially testable and so no
    /// framework concern can leak game state. If this fails, something added a dependency.
    /// </summary>
    [Fact]
    public void Rules_assembly_references_only_the_base_class_library()
    {
        string[] referenced = typeof(BoardLayout).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, "netstandard", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, "mscorlib", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(referenced);
    }
}
