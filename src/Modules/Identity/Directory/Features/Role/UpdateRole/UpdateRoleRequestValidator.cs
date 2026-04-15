using FluentValidation;

namespace Identity.Directory.Features.Role.UpdateRole;

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).NotEmpty().MaximumLength(100);
    }
}
