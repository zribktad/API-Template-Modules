using Microsoft.Extensions.Compliance.Classification;

namespace APITemplate.Infrastructure.Logging;

/// <summary>
/// Defines the project-specific <see cref="DataClassification"/> taxonomy used to classify
/// log parameters for compliance-aware redaction in the Microsoft.Extensions.Compliance pipeline.
/// </summary>
public static class LogDataClassifications
{
    private const string TaxonomyName = "APITemplate";

    /// <summary>Classification for personally identifiable information such as email addresses and names.</summary>
    public static DataClassification Personal => new(TaxonomyName, nameof(Personal));

    /// <summary>Classification for sensitive business data that must not appear in plain-text logs.</summary>
    public static DataClassification Sensitive => new(TaxonomyName, nameof(Sensitive));

    /// <summary>Classification for data that carries no privacy concern and may be logged as-is.</summary>
    public static DataClassification Public => new(TaxonomyName, nameof(Public));
}

/// <summary>
/// Marks a log parameter or property as personally identifiable information, causing it to be
/// redacted by the configured <see cref="LogDataClassifications.Personal"/> classification policy.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    public PersonalDataAttribute()
        : base(LogDataClassifications.Personal) { }
}

/// <summary>
/// Marks a log parameter or property as sensitive business data, causing it to be
/// redacted by the configured <see cref="LogDataClassifications.Sensitive"/> classification policy.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : DataClassificationAttribute
{
    public SensitiveDataAttribute()
        : base(LogDataClassifications.Sensitive) { }
}
