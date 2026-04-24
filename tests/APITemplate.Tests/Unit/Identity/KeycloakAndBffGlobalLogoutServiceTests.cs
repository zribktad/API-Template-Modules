using System.Net.Http;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class KeycloakAndBffGlobalLogoutServiceTests
{
    [Fact]
    public async Task SignOutEverywhereAsync_CallsKeycloakLogoutThenBffRevoke()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string kcId = Guid.NewGuid().ToString();
        var keycloak = new Mock<IKeycloakAdminService>(MockBehavior.Strict);
        var revocation = new Mock<IBffSessionRevocationService>(MockBehavior.Strict);

        var sequence = new MockSequence();
        keycloak
            .InSequence(sequence)
            .Setup(k => k.LogoutAllUserSessionsAsync(kcId, ct))
            .Returns(Task.CompletedTask);
        revocation
            .InSequence(sequence)
            .Setup(s =>
                s.RevokeAllSessionsForSubjectAsync(
                    kcId,
                    BffSessionRevocationReason.CredentialRotation,
                    ct
                )
            )
            .Returns(Task.CompletedTask);

        var sut = new KeycloakAndBffGlobalLogoutService(
            keycloak.Object,
            revocation.Object,
            NullLogger<KeycloakAndBffGlobalLogoutService>.Instance
        );

        await sut.SignOutEverywhereAsync(kcId, BffSessionRevocationReason.CredentialRotation, ct);

        keycloak.Verify(k => k.LogoutAllUserSessionsAsync(kcId, ct), Times.Once);
        revocation.Verify(
            s =>
                s.RevokeAllSessionsForSubjectAsync(
                    kcId,
                    BffSessionRevocationReason.CredentialRotation,
                    ct
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SignOutEverywhereAsync_WhenKeycloakLogoutThrows_StillRevokesBffSessions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string kcId = Guid.NewGuid().ToString();
        var keycloak = new Mock<IKeycloakAdminService>();
        var revocation = new Mock<IBffSessionRevocationService>(MockBehavior.Strict);

        keycloak
            .Setup(k => k.LogoutAllUserSessionsAsync(kcId, ct))
            .ThrowsAsync(new HttpRequestException("Keycloak unavailable"));
        revocation
            .Setup(s =>
                s.RevokeAllSessionsForSubjectAsync(
                    kcId,
                    BffSessionRevocationReason.CredentialRotation,
                    ct
                )
            )
            .Returns(Task.CompletedTask);

        var sut = new KeycloakAndBffGlobalLogoutService(
            keycloak.Object,
            revocation.Object,
            NullLogger<KeycloakAndBffGlobalLogoutService>.Instance
        );

        await sut.SignOutEverywhereAsync(kcId, BffSessionRevocationReason.CredentialRotation, ct);

        revocation.Verify(
            s =>
                s.RevokeAllSessionsForSubjectAsync(
                    kcId,
                    BffSessionRevocationReason.CredentialRotation,
                    ct
                ),
            Times.Once
        );
    }
}
