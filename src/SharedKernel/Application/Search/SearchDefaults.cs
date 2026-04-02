namespace SharedKernel.Application.Search;

/// <summary>
/// Shared defaults for full-text search across filter specifications.
/// </summary>
public static class SearchDefaults
{
    /// <summary>
    /// PostgreSQL text search configuration used by all full-text search queries.
    /// </summary>
    public const string TextSearchConfiguration = "english";
}
