using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Build;

public sealed class PackageReferencePolicyTests
{
    [Fact]
    public void APITemplateCsproj_CompliesWithPackageFamilyVersionPolicy()
    {
        string repoRoot = PackagePolicyTestFiles.GetRepoRoot();
        PolicyEvaluationResult result = PackageReferencePolicy.Evaluate(
            PackagePolicyTestFiles.ReadProjectXml(repoRoot),
            PackagePolicyTestFiles.ReadCentralPackageXml(repoRoot)
        );

        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Evaluate_FailsWhenPackageFamiliesDriftFromPolicy()
    {
        PolicyEvaluationResult result = PackageReferencePolicy.Evaluate(
            PackagePolicyTestFiles.ProjectXmlWithoutInlineVersions,
            PackagePolicyTestFiles.CentralPackageXmlWithVersionDrift
        );

        result.Errors.ShouldContain(error => error.Contains(PackagePolicies.HealthChecks.Name));
        result.Errors.ShouldContain(error => error.Contains(PackagePolicies.HotChocolate.Name));
        result.Errors.ShouldContain(error => error.Contains(PackagePolicies.Keycloak.Name));
        result.Errors.ShouldContain(error => error.Contains(PackagePolicies.Ardalis.Name));
        result.Errors.ShouldContain(error => error.Contains(PackagePolicies.Scalar.Name));
    }
}
