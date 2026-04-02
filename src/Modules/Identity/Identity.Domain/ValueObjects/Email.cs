using ErrorOr;
using Identity.Domain.Errors;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated email address. Must be non-empty, contain '@', and be at most 320 characters.
/// </summary>
public readonly record struct Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    /// <summary>Creates an <see cref="Email"/> after trimming and validating the input.</summary>
    public static ErrorOr<Email> Create(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(trimmed))
            return IdentityDomainErrors.Emails.Empty();

        if (!trimmed.Contains('@'))
            return IdentityDomainErrors.Emails.InvalidFormat();

        if (trimmed.Length > 320)
            return IdentityDomainErrors.Emails.TooLong();

        return new Email(trimmed);
    }

    /// <summary>Factory method for EF Core use only. Bypasses validation as values come from persistence.</summary>
    public static Email FromPersistence(string value) => new(value);

    /// <summary>Returns the canonical form of the email address: trimmed and converted to uppercase invariant.</summary>
    public string Normalize() => Value.Trim().ToUpperInvariant();

    public static implicit operator string(Email email) => email.Value;
}
