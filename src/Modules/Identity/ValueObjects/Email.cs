using System.Net.Mail;
using ErrorOr;

namespace Identity.ValueObjects;

/// <summary>
///     Value object representing a validated email address. Must be non-empty, syntactically valid, and be at most 320
///     characters.
/// </summary>
public readonly record struct Email
{
    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; init; } = string.Empty;

    /// <summary>Creates an <see cref="Email" /> after trimming and validating the input.</summary>
    public static ErrorOr<Email> Create(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(trimmed))
            return IdentityDomainErrors.Emails.Empty();

        if (!MailAddress.TryCreate(trimmed, out _))
            return IdentityDomainErrors.Emails.InvalidFormat();

        if (trimmed.Length > 320)
            return IdentityDomainErrors.Emails.TooLong();

        return new Email(trimmed);
    }

    /// <summary>Factory method for EF Core use only. Bypasses validation as values come from persistence.</summary>
    public static Email FromPersistence(string value)
    {
        return new Email(value);
    }

    /// <summary>Returns the canonical form of a raw email string without creating an Email instance.</summary>
    public static string NormalizeRaw(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    /// <summary>Returns the canonical form of the email address: trimmed and converted to uppercase invariant.</summary>
    public string Normalize()
    {
        return Value.ToUpperInvariant();
    }

    public static implicit operator string(Email email)
    {
        return email.Value;
    }
}
