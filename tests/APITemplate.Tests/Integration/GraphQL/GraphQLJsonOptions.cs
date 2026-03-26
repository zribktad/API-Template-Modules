using System.Text.Json;

namespace APITemplate.Tests.Integration.GraphQL;

internal static class GraphQLJsonOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
