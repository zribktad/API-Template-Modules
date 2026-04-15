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
        var validator = new CreateRoleRequestValidator();

        var results = validator.Validate(request);

        results.IsValid.ShouldBeFalse();
        results.Errors.ShouldContain(r => r.ErrorMessage.Contains("must not be empty"));
    }

    [Fact]
    public void UpdateRoleRequest_WhenNameMissing_FailsValidation()
    {
        var request = new UpdateRoleRequest("", new List<string> { "Users.Read" });
        var validator = new UpdateRoleRequestValidator();

        var results = validator.Validate(request);

        results.IsValid.ShouldBeFalse();
        results.Errors.ShouldContain(r => r.PropertyName == "Name");
    }

    [Fact]
    public void CreateRoleRequest_WhenPayloadValid_PassesValidation()
    {
        var request = new CreateRoleRequest(
            "Tenant Admin",
            new List<string> { "Users.Read", "Roles.Update" }
        );
        var validator = new CreateRoleRequestValidator();

        var results = validator.Validate(request);

        results.IsValid.ShouldBeTrue();
        results.Errors.ShouldBeEmpty();
    }
}
