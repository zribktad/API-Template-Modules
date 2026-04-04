using ErrorOr;

namespace Identity.ValueObjects;

/// <summary>
///     Value object representing a tenant code. Must be non-empty and at most 100 characters.
/// </summary>
public readonly record struct TenantCode
{
    private TenantCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>Creates a <see cref="TenantCode" /> after trimming and validating the input.</summary>
    public static ErrorOr<TenantCode> Create(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(trimmed))
            return IdentityDomainErrors.TenantCodes.Empty();

        if (trimmed.Length > 100)
            return IdentityDomainErrors.TenantCodes.TooLong();

        return new TenantCode(trimmed);
    }

    /// <summary>Factory method for EF Core use only. Bypasses validation as values come from persistence.</summary>
    public static TenantCode FromPersistence(string value)
    {
        return new TenantCode(value);
    }

    public static implicit operator string(TenantCode code)
    {
        return code.Value;
    }
}
