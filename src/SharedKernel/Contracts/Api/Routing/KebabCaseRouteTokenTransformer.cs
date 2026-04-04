using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.Contracts.Api.Routing;

/// <summary>
/// Transforms route token values (e.g. <c>[controller]</c>) to kebab-case for outbound URLs and inbound matching.
/// </summary>
public sealed partial class KebabCaseRouteTokenTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value is not string s || string.IsNullOrEmpty(s))
        {
            return null;
        }

        return InsertHyphensBetweenWords().Replace(s, "$1-$2").ToLowerInvariant();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex InsertHyphensBetweenWords();
}
