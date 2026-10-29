using System.Reflection;

namespace SchoolMs.ArchitectureTests;

/// <summary>
/// Enforces the dependency rules in docs/02-PROJECT-PLAN.md §2.4 at the assembly level.
/// The rules apply automatically to every module added later.
/// </summary>
public class ModuleBoundaryTests
{
    private const string Prefix = "SchoolMs.";
    private const string ModulePrefix = "SchoolMs.Modules.";
    private const string ContractsSuffix = ".Contracts";

    private static readonly Assembly[] AppAssemblies = Directory
        .GetFiles(AppContext.BaseDirectory, $"{Prefix}*.dll")
        .Select(path => Assembly.LoadFrom(path))
        .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal))
        .ToArray();

    public static TheoryData<string> ModuleAssemblies => [.. Names(a => IsModule(a) && !IsContracts(a))];

    public static TheoryData<string> ContractsAssemblies => [.. Names(IsContracts)];

    [Fact]
    public void ApplicationAssemblies_AreDiscovered()
    {
        AppAssemblies.Select(a => a.GetName().Name).ShouldContain("SchoolMs.Api");
    }

    [Fact]
    public void SharedKernel_DependsOnNoOtherApplicationAssembly()
    {
        AppReferences("SchoolMs.SharedKernel").ShouldBeEmpty();
    }

    [Fact]
    public void Infrastructure_DependsOnlyOnSharedKernel()
    {
        AppReferences("SchoolMs.Infrastructure").ShouldBe(["SchoolMs.SharedKernel"], ignoreOrder: true);
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(ModuleAssemblies))]
    public void Module_ReferencesOtherModulesOnlyThroughContracts(string module)
    {
        var illegal = AppReferences(module)
            .Where(r => r.StartsWith(ModulePrefix, StringComparison.Ordinal))
            .Where(r => !r.EndsWith(ContractsSuffix, StringComparison.Ordinal))
            .ToList();

        illegal.ShouldBeEmpty($"{module} must reference other modules only via their .Contracts assembly.");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(ContractsAssemblies))]
    public void Contracts_DependOnlyOnSharedKernelOrOtherContracts(string contracts)
    {
        var illegal = AppReferences(contracts)
            .Where(r => r != "SchoolMs.SharedKernel" && !r.EndsWith(ContractsSuffix, StringComparison.Ordinal))
            .ToList();

        illegal.ShouldBeEmpty($"{contracts} is a public API and must not depend on implementations.");
    }

    private static bool IsModule(Assembly a) => a.GetName().Name!.StartsWith(ModulePrefix, StringComparison.Ordinal);

    private static bool IsContracts(Assembly a) => IsModule(a) && a.GetName().Name!.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    private static IEnumerable<string> Names(Func<Assembly, bool> predicate) =>
        AppAssemblies.Where(predicate).Select(a => a.GetName().Name!);

    private static List<string> AppReferences(string assemblyName) =>
        AppAssemblies.Single(a => a.GetName().Name == assemblyName)
            .GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();
}
