using Identity.Auth.Entities;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;
using IKeycloakAdminService = Identity.Auth.Security.IKeycloakAdminService;
using IUnitOfWork = SharedKernel.Domain.Interfaces.IUnitOfWork<Identity.IdentityDbMarker>;
using IUserRepository = Identity.Directory.Interfaces.IUserRepository;

namespace APITemplate.Tests.Unit.Identity;

public sealed class ProvisionKeycloakUserHandlerTests
{
    private readonly Mock<IKeycloakAdminService> _keycloakAdmin = new();
    private readonly Mock<ILogger<ProvisionKeycloakUserHandler>> _logger = new();
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task HandleAsync_WhenUserNotFound_SkipsAndReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        ProvisionKeycloakUserEvent @event = new(userId, "alice", "alice@example.com");

        _repository.Setup(r => r.GetByIdAsync<Guid>(userId, ct)).ReturnsAsync((AppUser?)null);

        OutgoingMessages result = await ProvisionKeycloakUserHandler.HandleAsync(
            @event,
            _repository.Object,
            _unitOfWork.Object,
            _keycloakAdmin.Object,
            _logger.Object,
            ct
        );

        result.OfType<UserRegisteredNotification>().ShouldBeEmpty();
        _keycloakAdmin.Verify(
            k =>
                k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyProvisioned_SkipsAndReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        ProvisionKeycloakUserEvent @event = new(userId, "bob", "bob@example.com");

        AppUser alreadyLinked = AppUser.Create(
            "bob",
            Email.FromPersistence("bob@example.com"),
            keycloakUserId: "existing-kc-id"
        );

        _repository.Setup(r => r.GetByIdAsync<Guid>(userId, ct)).ReturnsAsync(alreadyLinked);

        OutgoingMessages result = await ProvisionKeycloakUserHandler.HandleAsync(
            @event,
            _repository.Object,
            _unitOfWork.Object,
            _keycloakAdmin.Object,
            _logger.Object,
            ct
        );

        result.OfType<UserRegisteredNotification>().ShouldBeEmpty();
        _keycloakAdmin.Verify(
            k =>
                k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_LinksKeycloakAndEmitsUserRegisteredNotification()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        const string keycloakId = "kc-123";
        ProvisionKeycloakUserEvent @event = new(userId, "carol", "carol@example.com");

        AppUser user = AppUser.Create(
            "carol",
            Email.FromPersistence("carol@example.com"),
            keycloakUserId: null
        );

        _repository.Setup(r => r.GetByIdAsync<Guid>(userId, ct)).ReturnsAsync(user);
        _keycloakAdmin
            .Setup(k => k.CreateUserAsync(@event.Username, @event.Email, ct))
            .ReturnsAsync(keycloakId);

        AppUser? updatedUser = null;
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<AppUser>(), ct))
            .Callback<AppUser, CancellationToken>((u, _) => updatedUser = u);

        OutgoingMessages result = await ProvisionKeycloakUserHandler.HandleAsync(
            @event,
            _repository.Object,
            _unitOfWork.Object,
            _keycloakAdmin.Object,
            _logger.Object,
            ct
        );

        _keycloakAdmin.Verify(
            k => k.CreateUserAsync(@event.Username, @event.Email, ct),
            Times.Once
        );
        _repository.Verify(r => r.UpdateAsync(It.IsAny<AppUser>(), ct), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(ct), Times.Once);

        updatedUser.ShouldNotBeNull();
        updatedUser!.KeycloakUserId.ShouldBe(keycloakId);

        UserRegisteredNotification notification = result
            .OfType<UserRegisteredNotification>()
            .Single();
        notification.UserId.ShouldBe(user.Id);
        notification.Email.ShouldBe(@event.Email);
        notification.Username.ShouldBe(@event.Username);
    }

    [Fact]
    public async Task HandleAsync_WhenKeycloakFails_Throws_SoWolverineRetries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        ProvisionKeycloakUserEvent @event = new(userId, "dave", "dave@example.com");

        AppUser user = AppUser.Create(
            "dave",
            Email.FromPersistence("dave@example.com"),
            keycloakUserId: null
        );

        _repository.Setup(r => r.GetByIdAsync<Guid>(userId, ct)).ReturnsAsync(user);
        _keycloakAdmin
            .Setup(k => k.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), ct))
            .ThrowsAsync(new HttpRequestException("Keycloak unavailable"));

        await Should.ThrowAsync<HttpRequestException>(() =>
            ProvisionKeycloakUserHandler.HandleAsync(
                @event,
                _repository.Object,
                _unitOfWork.Object,
                _keycloakAdmin.Object,
                _logger.Object,
                ct
            )
        );

        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
