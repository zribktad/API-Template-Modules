namespace APITemplate.Api;

/// <summary>
///     Single source of truth for the GraphQL endpoint path, shared by the endpoint mapping and the
///     REST exception handler's bypass so they cannot drift.
/// </summary>
public static class GraphQLPathConstants
{
    public const string BasePath = "/graphql";
}
