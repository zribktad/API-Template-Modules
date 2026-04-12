namespace Identity.Directory.Features.Role.Shared;

public sealed record RoleResponse(
    Guid Id,
    string Name,
    bool IsImmutable,
    IReadOnlyList<string> Permissions
);