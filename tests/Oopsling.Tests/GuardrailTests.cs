using System.Reflection;

namespace Oopsling.Tests;

/// <summary>Structural rules from AGENTS.md: neither package touches the network, and the core never references Avalonia.</summary>
public class GuardrailTests
{
    private static readonly Assembly Core = Assembly.Load("Oopsling");
    private static readonly Assembly Window = Assembly.Load("Oopsling.Avalonia");

    // Hosts such as Staypaw and Sweepling fail their own guard tests if a package they take references the network.
    [Fact]
    public void Neither_package_references_a_networking_assembly()
    {
        Assert.Empty(References(Core, "System.Net"));
        Assert.Empty(References(Window, "System.Net"));
    }

    [Fact]
    public void The_core_does_not_reference_Avalonia() => Assert.Empty(References(Core, "Avalonia"));

    // Positive controls: the same check finds the references it exists to catch.
    [Fact]
    public void The_check_finds_network_references() =>
        Assert.NotEmpty(References(Assembly.Load("System.Net.Http"), "System.Net"));

    [Fact]
    public void The_check_finds_Avalonia_in_the_sample() =>
        Assert.NotEmpty(References(Assembly.Load("Oopsling.Sample"), "Avalonia"));

    private static string[] References(Assembly assembly, string prefix) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))];
}
