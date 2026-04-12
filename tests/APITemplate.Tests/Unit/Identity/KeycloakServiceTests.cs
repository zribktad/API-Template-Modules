using System.Net;
using System.Text;
using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.Keycloak;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class KeycloakServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandler = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

    private KeycloakService CreateSut()
    {
        HttpClient client = new(_httpMessageHandler.Object);
        _httpClientFactory
            .Setup(x => x.CreateClient(AuthConstants.HttpClients.KeycloakToken))
            .Returns(client);

        return new KeycloakService(
            _httpClientFactory.Object,
            Options.Create(
                new KeycloakOptions
                {
                    AuthServerUrl = "https://keycloak.example.com",
                    Realm = "example-realm",
                    Resource = "example-client",
                    Credentials = new KeycloakCredentialsOptions { Secret = "example-secret" },
                }
            ),
            NullLogger<KeycloakService>.Instance
        );
    }

    [Fact]
    public async Task RefreshSessionAsync_WhenResponseIsValid_ReturnsTokensAndPostsRefreshGrant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? requestBody = null;
        Uri? requestUri = null;
        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns<HttpRequestMessage, CancellationToken>(
                async (request, _) =>
                {
                    requestUri = request.RequestUri;
                    requestBody = await request.Content!.ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":300}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    };
                }
            );

        KeycloakService sut = CreateSut();
        KeycloakRefreshResult result = await sut.RefreshSessionAsync("refresh-token", ct);

        result.Status.ShouldBe(KeycloakRefreshStatus.Success);
        result.TokenResponse.ShouldNotBeNull();
        result.TokenResponse.AccessToken.ShouldBe("new-access");
        result.TokenResponse.RefreshToken.ShouldBe("new-refresh");
        result.TokenResponse.ExpiresIn.ShouldBe(300);
        requestUri.ShouldBe(
            new Uri(
                "https://keycloak.example.com/realms/example-realm/protocol/openid-connect/token"
            )
        );
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("grant_type=refresh_token");
        requestBody.ShouldContain("client_id=example-client");
        requestBody.ShouldContain("client_secret=example-secret");
        requestBody.ShouldContain("refresh_token=refresh-token");
    }

    [Fact]
    public async Task RefreshSessionAsync_WhenStatusIsNonSuccess_ReturnsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        KeycloakService sut = CreateSut();
        KeycloakRefreshResult result = await sut.RefreshSessionAsync("refresh-token", ct);

        result.Status.ShouldBe(KeycloakRefreshStatus.ProviderError);
        result.TokenResponse.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshSessionAsync_WhenPayloadIsInvalid_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"\",\"refresh_token\":\"new-refresh\",\"expires_in\":0}",
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );

        KeycloakService sut = CreateSut();
        KeycloakRefreshResult result = await sut.RefreshSessionAsync("refresh-token", ct);

        result.Status.ShouldBe(KeycloakRefreshStatus.ProviderError);
        result.TokenResponse.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshSessionAsync_WhenRequestIsCanceled_ThrowsOperationCanceled()
    {
        CancellationTokenSource cts = new();
        cts.Cancel();

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        KeycloakService sut = CreateSut();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            sut.RefreshSessionAsync("refresh-token", cts.Token)
        );
    }
}
