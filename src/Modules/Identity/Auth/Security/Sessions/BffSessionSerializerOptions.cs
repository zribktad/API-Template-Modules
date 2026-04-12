using System.Text.Json;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Shared JSON serializer options for BFF session serialization across stores and coordinators.
/// </summary>
internal static class BffSessionSerializerOptions
{
    public static readonly JsonSerializerOptions Instance = new(JsonSerializerDefaults.Web);
}
