using System.ComponentModel.DataAnnotations;

namespace Identity.Directory.Features.Account;

/// <summary>Payload for changing the signed-in user's Keycloak password.</summary>
public sealed record ChangePasswordRequest(
    [NotEmpty] [MaxLength(256)] string CurrentPassword,
    [NotEmpty] [MinLength(4)] [MaxLength(256)] string NewPassword
);
