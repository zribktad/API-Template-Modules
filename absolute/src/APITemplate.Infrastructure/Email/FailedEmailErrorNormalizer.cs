using APITemplate.Domain.Entities;

namespace APITemplate.Infrastructure.Email;

/// <summary>
/// Utility that truncates raw exception messages to the maximum length allowed by
/// <see cref="FailedEmail.LastErrorMaxLength"/> before persisting them.
/// </summary>
internal static class FailedEmailErrorNormalizer
{
    /// <summary>Returns <paramref name="error"/> unchanged if it fits, or truncated to <see cref="FailedEmail.LastErrorMaxLength"/> characters.</summary>
    public static string? Normalize(string? error)
    {
        if (string.IsNullOrEmpty(error) || error.Length <= FailedEmail.LastErrorMaxLength)
        {
            return error;
        }

        return error[..FailedEmail.LastErrorMaxLength];
    }
}
