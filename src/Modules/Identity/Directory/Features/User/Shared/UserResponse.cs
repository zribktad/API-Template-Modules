using SharedKernel.Infrastructure.Logging;

namespace Identity.Directory.Features.User;

/// <summary>
///     Read model returned to callers after a user query or creation.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string Username,
    [property: PersonalData] string Email,
    bool IsActive,
    IReadOnlyList<string> Roles,
    ProvisioningStatus ProvisioningStatus,
    DateTime CreatedAtUtc
) : IHasId;
