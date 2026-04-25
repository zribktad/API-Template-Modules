using System.Reflection;
using System.Xml.Linq;
using APITemplate.Api;
using BackgroundJobs;
using Chatting;
using FileStorage;
using Identity;
using NetArchTest.Rules;
using Notifications;
using ProductCatalog;
using Reviews;
using Shouldly;
using Webhooks;
using Xunit;

namespace APITemplate.Tests.Unit.Architecture;

[Trait("Category", "Unit")]
[Trait("Category", "Unit.Component")]
public sealed class ModuleBoundaryArchitectureTests
{
    [Fact]
    public void Modules_ShouldNotDependOnOtherModules_ExceptExplicitTemporaryExceptions()
    {
        List<string> failures = new();

        foreach (ModuleDefinition module in ModuleArchitecture.Modules)
        {
            foreach (
                string forbiddenModuleName in ModuleArchitecture.GetForbiddenModuleDependencies(
                    module.Name
                )
            )
            {
                if (
                    ModuleArchitecture.HasDirectAssemblyReference(
                        module.Assembly,
                        forbiddenModuleName
                    )
                )
                    failures.Add($"{module.Name} must not depend on {forbiddenModuleName}.");
            }
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ProductCatalog_TemporaryExceptionToReviews_ShouldRemainExplicitUntilRefactored()
    {
        string repoRoot = GetRepoRoot();
        List<string> interModuleReferences = GetInterModuleProjectReferences(repoRoot);

        interModuleReferences.ShouldContain(
            "ProductCatalog -> Reviews",
            "ProductCatalog -> Reviews is the only temporary cross-module exception. "
                + "If this dependency is removed, delete the whitelist entry and this test."
        );
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOnAnyModule()
    {
        List<string> failures = new();

        foreach (string moduleName in ModuleArchitecture.Modules.Select(x => x.Name))
        {
            if (
                ModuleArchitecture.HasDirectAssemblyReference(
                    ModuleArchitecture.SharedKernelAssembly,
                    moduleName
                )
            )
                failures.Add($"SharedKernel must not depend on module {moduleName}.");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ModuleFeatureTypes_ShouldNotDependOnApiAssembly()
    {
        List<string> failures = new();

        foreach (ModuleDefinition module in ModuleArchitecture.Modules)
        {
            bool result = Types
                .InAssembly(module.Assembly)
                .That()
                .ResideInNamespace($"{module.RootNamespace}.Features")
                .ShouldNot()
                .HaveDependencyOn(ModuleArchitecture.ApiRootNamespace)
                .GetResult()
                .IsSuccessful;

            if (!result)
                failures.Add(
                    $"{module.Name}.Features must not depend on {ModuleArchitecture.ApiRootNamespace}."
                );
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ModuleProjectReferences_ShouldOnlyContainTheExplicitTemporaryException()
    {
        string repoRoot = GetRepoRoot();
        List<string> interModuleReferences = GetInterModuleProjectReferences(repoRoot);

        interModuleReferences.ShouldBe(
            ["ProductCatalog -> Reviews"],
            "Only the temporary ProductCatalog -> Reviews module reference should exist."
        );
    }

    [Fact]
    public void ParseInterModuleProjectReferences_ShouldNormalizeWindowsStyleSeparators()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string sourceProject = Path.Combine(
            tempRoot,
            "src",
            "Modules",
            "ProductCatalog",
            "ProductCatalog.csproj"
        );

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourceProject)!);
            Directory.CreateDirectory(Path.Combine(tempRoot, "src", "Modules", "Reviews"));

            File.WriteAllText(
                sourceProject,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="..\Reviews\Reviews.csproj" />
                  </ItemGroup>
                </Project>
                """
            );

            ParseInterModuleProjectReferences(sourceProject)
                .ShouldBe(["ProductCatalog -> Reviews"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static List<string> GetInterModuleProjectReferences(string repoRoot)
    {
        string modulesRoot = Path.Combine(repoRoot, "src", "Modules");

        return Directory
            .GetFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(ParseInterModuleProjectReferences)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> ParseInterModuleProjectReferences(string projectPath)
    {
        XDocument document = XDocument.Parse(File.ReadAllText(projectPath));
        string sourceModule = Path.GetFileNameWithoutExtension(projectPath);

        return document
            .Descendants()
            .Where(node => node.Name.LocalName == "ProjectReference")
            .Select(node => (string?)node.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .Select(include => NormalizeProjectReferencePath(projectPath, include))
            .Where(include =>
                include.Contains(
                    $"{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            )
            .Select(include => Path.GetFileNameWithoutExtension(include))
            .Where(targetModule =>
                !string.Equals(sourceModule, targetModule, StringComparison.Ordinal)
            )
            .Select(targetModule => $"{sourceModule} -> {targetModule}");
    }

    private static string NormalizeProjectReferencePath(string projectPath, string include)
    {
        string normalizedInclude = include
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(projectPath)!, normalizedInclude)
        );
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")
        );
    }

    private sealed record ModuleDefinition(string Name, string RootNamespace, Assembly Assembly);

    private static class ModuleArchitecture
    {
        public const string ApiRootNamespace = "APITemplate.Api";

        public static readonly ModuleDefinition BackgroundJobs = new(
            nameof(BackgroundJobs),
            nameof(BackgroundJobs),
            typeof(BackgroundJobsModule).Assembly
        );

        public static readonly ModuleDefinition Chatting = new(
            "Chatting",
            "Chatting",
            typeof(global::Chatting.ChattingModule).Assembly
        );

        public static readonly ModuleDefinition FileStorage = new(
            nameof(FileStorage),
            nameof(FileStorage),
            typeof(FileStorageModule).Assembly
        );

        public static readonly ModuleDefinition Identity = new(
            nameof(Identity),
            nameof(Identity),
            typeof(IdentityModule).Assembly
        );

        public static readonly ModuleDefinition Notifications = new(
            nameof(Notifications),
            nameof(Notifications),
            typeof(NotificationsModule).Assembly
        );

        public static readonly ModuleDefinition ProductCatalog = new(
            nameof(ProductCatalog),
            nameof(ProductCatalog),
            typeof(ProductCatalogModule).Assembly
        );

        public static readonly ModuleDefinition Reviews = new(
            nameof(Reviews),
            nameof(Reviews),
            typeof(ReviewsModule).Assembly
        );

        public static readonly ModuleDefinition Webhooks = new(
            nameof(Webhooks),
            nameof(Webhooks),
            typeof(WebhooksModule).Assembly
        );

        public static readonly IReadOnlyList<ModuleDefinition> Modules =
        [
            BackgroundJobs,
            Chatting,
            FileStorage,
            Identity,
            Notifications,
            ProductCatalog,
            Reviews,
            Webhooks,
        ];

        public static Assembly SharedKernelAssembly =>
            typeof(SharedKernel.Application.Errors.AppException).Assembly;

        private static readonly IReadOnlyDictionary<
            string,
            IReadOnlySet<string>
        > _allowedModuleDependencies = new Dictionary<string, IReadOnlySet<string>>(
            StringComparer.Ordinal
        )
        {
            [ProductCatalog.Name] = new HashSet<string>(StringComparer.Ordinal)
            {
                Reviews.RootNamespace,
            },
        };

        public static ModuleDefinition GetModule(string name)
        {
            return Modules.Single(module =>
                string.Equals(module.Name, name, StringComparison.Ordinal)
            );
        }

        public static bool HasDirectAssemblyReference(
            Assembly assembly,
            string referencedAssemblyName
        )
        {
            return assembly
                .GetReferencedAssemblies()
                .Any(reference =>
                    string.Equals(reference.Name, referencedAssemblyName, StringComparison.Ordinal)
                );
        }

        public static IReadOnlyList<string> GetForbiddenModuleDependencies(string moduleName)
        {
            IReadOnlySet<string> allowedDependencies = _allowedModuleDependencies.TryGetValue(
                moduleName,
                out IReadOnlySet<string>? allowed
            )
                ? allowed
                : new HashSet<string>(StringComparer.Ordinal);

            return Modules
                .Where(module => !string.Equals(module.Name, moduleName, StringComparison.Ordinal))
                .Select(module => module.Name)
                .Where(moduleDependency => !allowedDependencies.Contains(moduleDependency))
                .ToList();
        }
    }
}
