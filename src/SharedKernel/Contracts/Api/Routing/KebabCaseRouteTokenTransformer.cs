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

        s = InsertHyphenAfterLowercaseOrDigitBeforeUppercase().Replace(s, "$1-$2");
        s = InsertHyphenBetweenAcronymAndWord().Replace(s, "$1-$2");
        return s.ToLowerInvariant();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex InsertHyphenAfterLowercaseOrDigitBeforeUppercase();

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex InsertHyphenBetweenAcronymAndWord();
}
