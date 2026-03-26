using System.Xml.Linq;

namespace APITemplate.Tests.Unit.Build;

internal static class PackageReferencePolicy
{
    public static PolicyEvaluationResult Evaluate(string projectXml, string? centralPackagesXml = null)
    {
        var projectReferences = ParseProjectReferences(projectXml);
        var centralVersions = ParseCentralVersions(centralPackagesXml);
        var resolvedReferences = ResolveVersions(projectReferences, centralVersions);

        var errors = new List<string>();
        foreach (var rule in PackagePolicies.All)
            rule.Validate(resolvedReferences, errors);

        return new PolicyEvaluationResult(errors);
    }

    private static IReadOnlyList<PackageReference> ParseProjectReferences(string projectXml)
    {
        return XDocument.Parse(projectXml)
            .Descendants()
            .Where(node => node.Name.LocalName == "PackageReference")
            .Select(node => new PackageReference(
                (string?)node.Attribute("Include") ?? string.Empty,
                (string?)node.Attribute("Version") ?? string.Empty))
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Include))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> ParseCentralVersions(string? centralPackagesXml)
    {
        if (string.IsNullOrWhiteSpace(centralPackagesXml))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        return XDocument.Parse(centralPackagesXml)
            .Descendants()
            .Where(node => node.Name.LocalName == "PackageVersion")
            .Select(node => new
            {
                Include = (string?)node.Attribute("Include") ?? string.Empty,
                Version = (string?)node.Attribute("Version") ?? string.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Include))
            .ToDictionary(item => item.Include, item => item.Version, StringComparer.Ordinal);
    }

    private static IReadOnlyList<PackageReference> ResolveVersions(
        IReadOnlyList<PackageReference> projectReferences,
        IReadOnlyDictionary<string, string> centralVersions)
    {
        return projectReferences
            .Select(reference => reference with
            {
                Version = string.IsNullOrWhiteSpace(reference.Version) && centralVersions.TryGetValue(reference.Include, out var resolvedVersion)
                    ? resolvedVersion
                    : reference.Version
            })
            .ToList();
    }
}

internal static class PackagePolicies
{
    public static readonly PrefixVersionRule HealthChecks = new(
        Name: "HealthChecks",
        Prefix: "AspNetCore.HealthChecks.",
        VersionSelector: version => version.Major.ToString());

    public static readonly PrefixVersionRule HotChocolate = new(
        Name: "HotChocolate",
        Prefix: "HotChocolate.",
        VersionSelector: version => version.ToString());

    public static readonly PrefixVersionRule Keycloak = new(
        Name: "Keycloak.AuthServices",
        Prefix: "Keycloak.AuthServices.",
        VersionSelector: version => version.ToString());

    public static readonly ExactPairVersionRule Ardalis = new(
        Name: "Ardalis.Specification",
        FirstPackageId: "Ardalis.Specification",
        SecondPackageId: "Ardalis.Specification.EntityFrameworkCore");

    public static readonly RequiredPinnedVersionRule Scalar = new(
        Name: "Scalar.AspNetCore",
        PackageId: "Scalar.AspNetCore");

    public static IReadOnlyList<IPackagePolicyRule> All { get; } =
    [
        HealthChecks,
        HotChocolate,
        Keycloak,
        Ardalis,
        Scalar
    ];
}

internal interface IPackagePolicyRule
{
    void Validate(IReadOnlyList<PackageReference> references, List<string> errors);
}

internal sealed record PrefixVersionRule(
    string Name,
    string Prefix,
    Func<Version, string> VersionSelector) : IPackagePolicyRule
{
    public void Validate(IReadOnlyList<PackageReference> references, List<string> errors)
    {
        var family = references
            .Where(reference => reference.Include.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();

        if (family.Count == 0)
            return;

        var parsed = PackageVersionParsing.Parse(Name, family, errors);
        if (parsed.Count == 0)
            return;

        var distinctVersions = parsed
            .Select(item => VersionSelector(item.Version))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctVersions.Count > 1)
            errors.Add($"{Name} packages must share the same version policy. Found: {string.Join(", ", family.Select(reference => $"{reference.Include}={reference.Version}"))}.");
    }
}

internal sealed record ExactPairVersionRule(
    string Name,
    string FirstPackageId,
    string SecondPackageId) : IPackagePolicyRule
{
    public void Validate(IReadOnlyList<PackageReference> references, List<string> errors)
    {
        var pair = references
            .Where(reference => reference.Include == FirstPackageId || reference.Include == SecondPackageId)
            .ToList();

        if (pair.Count != 2)
        {
            errors.Add($"{Name} package pair must include both {FirstPackageId} and {SecondPackageId}.");
            return;
        }

        var parsed = PackageVersionParsing.Parse(Name, pair, errors);
        if (parsed.Count != 2)
            return;

        if (parsed[0].Version != parsed[1].Version)
            errors.Add($"{Name} packages must share the exact same version. Found: {parsed[0].Reference.Include}={parsed[0].Version}, {parsed[1].Reference.Include}={parsed[1].Version}.");
    }
}

internal sealed record RequiredPinnedVersionRule(
    string Name,
    string PackageId) : IPackagePolicyRule
{
    public void Validate(IReadOnlyList<PackageReference> references, List<string> errors)
    {
        var match = references.SingleOrDefault(reference => reference.Include == PackageId);
        if (match is null)
        {
            errors.Add($"{Name} package reference is required.");
            return;
        }

        if (!Version.TryParse(match.Version, out _))
            errors.Add($"{Name} must declare a parseable pinned version. Found: {match.Version}.");
    }
}

internal static class PackageVersionParsing
{
    public static List<(PackageReference Reference, Version Version)> Parse(
        string familyName,
        IReadOnlyCollection<PackageReference> references,
        List<string> errors)
    {
        var parsed = new List<(PackageReference, Version)>();
        foreach (var reference in references)
        {
            if (!Version.TryParse(reference.Version, out var version))
            {
                errors.Add($"{familyName} package {reference.Include} has an invalid version '{reference.Version}'.");
                continue;
            }

            parsed.Add((reference, version));
        }

        return parsed;
    }
}

internal static class PackagePolicyTestFiles
{
    public const string ProjectXmlWithoutInlineVersions = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <ItemGroup>
            <PackageReference Include="AspNetCore.HealthChecks.Redis" />
            <PackageReference Include="AspNetCore.HealthChecks.NpgSql" />
            <PackageReference Include="HotChocolate.AspNetCore" />
            <PackageReference Include="HotChocolate.Data.EntityFramework" />
            <PackageReference Include="Keycloak.AuthServices.Authentication" />
            <PackageReference Include="Keycloak.AuthServices.Authorization" />
            <PackageReference Include="Ardalis.Specification" />
            <PackageReference Include="Ardalis.Specification.EntityFrameworkCore" />
          </ItemGroup>
        </Project>
        """;

    public const string CentralPackageXmlWithVersionDrift = """
        <Project>
          <ItemGroup>
            <PackageVersion Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />
            <PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="10.0.0" />
            <PackageVersion Include="HotChocolate.AspNetCore" Version="15.1.12" />
            <PackageVersion Include="HotChocolate.Data.EntityFramework" Version="15.1.13" />
            <PackageVersion Include="Keycloak.AuthServices.Authentication" Version="2.8.0" />
            <PackageVersion Include="Keycloak.AuthServices.Authorization" Version="2.7.0" />
            <PackageVersion Include="Ardalis.Specification" Version="9.3.1" />
            <PackageVersion Include="Ardalis.Specification.EntityFrameworkCore" Version="9.3.0" />
          </ItemGroup>
        </Project>
        """;

    public static string GetRepoRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string ReadProjectXml(string repoRoot)
    {
        var projectPaths = new[]
        {
            Path.Combine(repoRoot, "src", "APITemplate.Api", "APITemplate.Api.csproj"),
            Path.Combine(repoRoot, "src", "APITemplate.Application", "APITemplate.Application.csproj"),
            Path.Combine(repoRoot, "src", "APITemplate.Infrastructure", "APITemplate.Infrastructure.csproj")
        };

        var packageReferences = projectPaths
            .Select(path => XDocument.Parse(File.ReadAllText(path)))
            .SelectMany(document => document
                .Descendants()
                .Where(node => node.Name.LocalName == "PackageReference")
                .Select(node => new XElement(node)))
            .ToList();

        var aggregateDocument = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup", packageReferences)));

        return aggregateDocument.ToString();
    }

    public static string ReadCentralPackageXml(string repoRoot)
        => File.ReadAllText(Path.Combine(repoRoot, "Directory.Packages.props"));
}

internal sealed record PackageReference(string Include, string Version);

internal sealed record PolicyEvaluationResult(IReadOnlyList<string> Errors);
