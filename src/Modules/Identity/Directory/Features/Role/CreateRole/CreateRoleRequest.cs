using FluentValidation;

namespace Identity.Directory.Features.Role.CreateRole;

public sealed record CreateRoleRequest(
    string Name,
    List<string> Permissions,
    Guid? TenantId = null
);

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotNull();
    }
}
