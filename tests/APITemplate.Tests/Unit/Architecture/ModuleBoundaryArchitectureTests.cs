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

public sealed class ModuleBoundaryArchitectureTests
{
    [Fact]
    public void Modules_ShouldNotDependOnOtherModules_ExceptExplicitTemporaryExceptions()
    {
        List<string> failures = new();

        foreach (ModuleDefinition module in ModuleArchitecture.Modules)
        {
            foreach (string forbiddenRootNamespace in ModuleArchitecture.GetForbiddenModuleDependencies(
                module.Name
            ))
            {
                bool result = Types
                    .InAssembly(module.Assembly)
                    .ShouldNot()
                    .HaveDependencyOn(forbiddenRootNamespace)
                    .GetResult();

                if (!result)
                {
                    failures.Add($"{module.Name} must not depend on {forbiddenRootNamespace}.");
                }
            }
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ProductCatalog_TemporaryExceptionToReviews_ShouldRemainExplicitUntilRefactored()
    {
        bool result = Types
            .InAssembly(ModuleArchitecture.GetModule("ProductCatalog").Assembly)
            .Should()
            .HaveDependencyOn(ModuleArchitecture.Reviews.RootNamespace)
            .GetResult();

        result.ShouldBeTrue(
            "ProductCatalog -> Reviews is the only temporary cross-module exception. "
                + "If this dependency is removed, delete the whitelist entry and this test."
        );
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOnAnyModule()
    {
        List<string> failures = new();

        foreach (string moduleRootNamespace in ModuleArchitecture.Modules.Select(x => x.RootNamespace))
        {
            bool result = Types
                .InAssembly(ModuleArchitecture.SharedKernelAssembly)
                .ShouldNot()
                .HaveDependencyOn(moduleRootNamespace)
                .GetResult();

            if (!result)
                failures.Add($"SharedKernel must not depend on module {moduleRootNamespace}.");
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
                .GetResult();

            if (!result)
                failures.Add($"{module.Name}.Features must not depend on {ModuleArchitecture.ApiRootNamespace}.");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ModuleProjectReferences_ShouldOnlyContainTheExplicitTemporaryException()
    {
        string repoRoot = GetRepoRoot();
        string modulesRoot = Path.Combine(repoRoot, "src", "Modules");

        List<string> interModuleReferences = Directory
            .GetFiles(modulesRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .SelectMany(ParseInterModuleProjectReferences)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        interModuleReferences.ShouldBe(
            ["ProductCatalog -> Reviews"],
            "Only the temporary ProductCatalog -> Reviews module reference should exist."
        );
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
            .Where(include =>
                include.Contains("..\\", StringComparison.Ordinal)
                && include.Contains("\\Modules\\", StringComparison.Ordinal)
            )
            .Select(include => Path.GetFileNameWithoutExtension(include))
            .Where(targetModule => !string.Equals(sourceModule, targetModule, StringComparison.Ordinal))
            .Select(targetModule => $"{sourceModule} -> {targetModule}");
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
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

        public static Assembly SharedKernelAssembly => typeof(SharedKernel.Application.Errors.AppException)
            .Assembly;

        private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _allowedModuleDependencies =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [ProductCatalog.Name] = new HashSet<string>(StringComparer.Ordinal)
                {
                    Reviews.RootNamespace,
                },
            };

        public static ModuleDefinition GetModule(string name)
        {
            return Modules.Single(module => string.Equals(module.Name, name, StringComparison.Ordinal));
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
                .Select(module => module.RootNamespace)
                .Where(rootNamespace => !allowedDependencies.Contains(rootNamespace))
                .ToList();
        }
    }
}
