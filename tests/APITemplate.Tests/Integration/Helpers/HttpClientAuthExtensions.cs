namespace APITemplate.Tests.Integration.Helpers;

internal static class HttpClientAuthExtensions
{
    internal static HttpClient AsAdmin(
        this HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null
    )
    {
        IntegrationAuthHelper.Authenticate(client, userId: userId, tenantId: tenantId);
        return client;
    }

    internal static HttpClient AsUser(
        this HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null,
        string[]? permissions = null
    )
    {
        IntegrationAuthHelper.AuthenticateAsUser(
            client,
            userId: userId,
            tenantId: tenantId,
            permissions: permissions
        );
        return client;
    }

    internal static HttpClient AsTenantAdmin(
        this HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null,
        string[]? permissions = null
    )
    {
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(
            client,
            userId: userId,
            tenantId: tenantId,
            permissions: permissions
        );
        return client;
    }

    internal static HttpClient WithAuth(
        this HttpClient client,
        string role,
        Guid? userId = null,
        Guid? tenantId = null,
        string? username = null,
        string? email = null,
        string[]? permissions = null,
        string? subject = null
    )
    {
        IntegrationAuthHelper.Authenticate(
            client,
            userId: userId,
            tenantId: tenantId,
            username: username,
            role: role,
            permissions: permissions,
            email: email,
            subject: subject
        );
        return client;
    }

    internal static HttpClient WithoutAuth(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}
