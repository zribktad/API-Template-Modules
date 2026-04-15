using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Features.Role.CreateRole;
using Identity.Directory.Features.Role.UpdateRole;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Boundary;

public sealed class BoundaryRequestValidationTests
{
    [Fact]
    public void CreateRoleRequest_WhenPermissionsContainWhitespace_FailsValidation()
    {
        var request = new CreateRoleRequest("Tenant Admin", new List<string> { "Users.Read", " " });

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.ErrorMessage == "Permissions must not contain empty values.");
    }

    [Fact]
    public void UpdateRoleRequest_WhenNameMissing_FailsValidation()
    {
        var request = new UpdateRoleRequest("", new List<string> { "Users.Read" });

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Name"));
    }

    [Fact]
    public void CreateRoleRequest_WhenPayloadValid_PassesValidation()
    {
        var request = new CreateRoleRequest(
            "Tenant Admin",
            new List<string> { "Users.Read", "Roles.Update" }
        );

        bool isValid = DataAnnotationsTestHelper.TryValidateAllProperties(request, out var results);

        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }
}
