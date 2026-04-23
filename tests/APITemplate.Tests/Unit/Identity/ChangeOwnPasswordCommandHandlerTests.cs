using ErrorOr;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Identity.Directory.Entities;
using Identity.Directory.Features.Account;
using Identity.Directory.Features.User;
using Identity.Directory.Interfaces;
using Identity.Errors;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class ChangeOwnPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IKeycloakAdminService> _keycloakAdmin = new();
    private readonly Mock<IKeycloakAndBffGlobalLogoutService> _globalLogout = new();

    [Fact]
    public async Task HandleAsync_WhenUserNotFound_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string kcId = Guid.NewGuid().ToString();
        _repository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<UserByKeycloakUserIdSpecification>(), ct))
            .ReturnsAsync((AppUser?)null);

        ChangeOwnPasswordCommand command = new(
            kcId,
            "alice",
            new ChangePasswordRequest("old-old", "new1new1")
        );

        ErrorOr<Success> result = await ChangeOwnPasswordCommandHandler.HandleAsync(
            command,
            _repository.Object,
            _keycloakAdmin.Object,
            _globalLogout.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.NotFoundByKeycloakId);
        _keycloakAdmin.Verify(
            k => k.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), ct),
            Times.Never
        );
        _keycloakAdmin.Verify(
            k =>
                k.SetUserPasswordAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    ct
                ),
            Times.Never
        );
        _globalLogout.Verify(
            g =>
                g.SignOutEverywhereAsync(
                    It.IsAny<string>(),
                    It.IsAny<BffSessionRevocationReason>(),
                    ct
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentPasswordInvalid_ReturnsValidationError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string kcId = Guid.NewGuid().ToString();
        AppUser user = AppUser.Create("alice", "alice@example.com", kcId);
        _repository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<UserByKeycloakUserIdSpecification>(), ct))
            .ReturnsAsync(user);

        _keycloakAdmin
            .Setup(k => k.ValidateCredentialsAsync("alice", "wrong", ct))
            .ReturnsAsync(false);

        ChangeOwnPasswordCommand command = new(
            kcId,
            "alice",
            new ChangePasswordRequest("wrong", "newnewnew")
        );

        ErrorOr<Success> result = await ChangeOwnPasswordCommandHandler.HandleAsync(
            command,
            _repository.Object,
            _keycloakAdmin.Object,
            _globalLogout.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        _keycloakAdmin.Verify(
            k => k.SetUserPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), false, ct),
            Times.Never
        );
        _globalLogout.Verify(
            g =>
                g.SignOutEverywhereAsync(
                    It.IsAny<string>(),
                    It.IsAny<BffSessionRevocationReason>(),
                    ct
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenValid_UpdatesPasswordAndRevokesSessions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string kcId = Guid.NewGuid().ToString();
        AppUser user = AppUser.Create("alice", "alice@example.com", kcId);
        _repository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<UserByKeycloakUserIdSpecification>(), ct))
            .ReturnsAsync(user);

        _keycloakAdmin
            .Setup(k => k.ValidateCredentialsAsync("alice", "old-old", ct))
            .ReturnsAsync(true);

        ChangeOwnPasswordCommand command = new(
            kcId,
            "alice",
            new ChangePasswordRequest("old-old", "new1new1")
        );

        ErrorOr<Success> result = await ChangeOwnPasswordCommandHandler.HandleAsync(
            command,
            _repository.Object,
            _keycloakAdmin.Object,
            _globalLogout.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        _keycloakAdmin.Verify(k => k.SetUserPasswordAsync(kcId, "new1new1", false, ct), Times.Once);
        _globalLogout.Verify(
            g => g.SignOutEverywhereAsync(kcId, BffSessionRevocationReason.CredentialRotation, ct),
            Times.Once
        );
    }
}
