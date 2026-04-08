using ErrorOr;
using Identity.Entities;
using Identity.Features.User.Events;
using Moq;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = Identity.Events.CacheTags;
using CreateUserCommand = Identity.Features.User.CreateUserCommand;
using CreateUserCommandHandler = Identity.Features.User.CreateUserCommandHandler;
using CreateUserRequest = Identity.Features.User.DTOs.CreateUserRequest;
using ErrorCatalog = Identity.Errors.ErrorCatalog;
using IdentityUnitOfWork = SharedKernel.Domain.Interfaces.IUnitOfWork<Identity.IdentityDbMarker>;
using IUserRepository = Identity.Interfaces.IUserRepository;
using UserResponse = Identity.Features.User.DTOs.UserResponse;

namespace APITemplate.Tests.Unit.Identity;

public sealed class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IdentityUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task HandleAsync_Success_CreatesUserWithNullKeycloakIdAndEmitsProvisionEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("alice", "alice@example.com");

        _repository.Setup(r => r.ExistsByEmailAsync(request.Email, ct)).ReturnsAsync(false);
        _repository
            .Setup(r => r.ExistsByUsernameAsync(AppUser.NormalizeUsername(request.Username), ct))
            .ReturnsAsync(false);

        AppUser? addedUser = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<AppUser>(), ct))
            .Callback<AppUser, CancellationToken>((u, _) => addedUser = u)
            .ReturnsAsync((AppUser u, CancellationToken _) => u);

        (ErrorOr<UserResponse> response, OutgoingMessages messages) =
            await CreateUserCommandHandler.HandleAsync(
                new CreateUserCommand(request),
                _repository.Object,
                _unitOfWork.Object,
                ct
            );

        response.IsError.ShouldBeFalse();
        response.Value.Username.ShouldBe(request.Username);
        response.Value.Email.ShouldBe(request.Email);

        _repository.Verify(r => r.AddAsync(It.IsAny<AppUser>(), ct), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(ct), Times.Once);

        addedUser.ShouldNotBeNull();
        addedUser!.KeycloakUserId.ShouldBeNull();

        ProvisionKeycloakUserEvent provisionEvent = messages
            .OfType<ProvisionKeycloakUserEvent>()
            .Single();
        provisionEvent.UserId.ShouldBe(addedUser.Id);
        provisionEvent.Username.ShouldBe(request.Username);
        provisionEvent.Email.ShouldBe(request.Email);

        messages.OfType<UserRegisteredNotification>().ShouldBeEmpty();

        CacheInvalidationNotification cacheEvent = messages
            .OfType<CacheInvalidationNotification>()
            .Single();
        cacheEvent.CacheTag.ShouldBe(CacheTags.Users);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyExists_ReturnsConflictError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("bob", "taken@example.com");

        _repository.Setup(r => r.ExistsByEmailAsync(request.Email, ct)).ReturnsAsync(true);

        (ErrorOr<UserResponse> response, OutgoingMessages messages) =
            await CreateUserCommandHandler.HandleAsync(
                new CreateUserCommand(request),
                _repository.Object,
                _unitOfWork.Object,
                ct
            );

        response.IsError.ShouldBeTrue();
        response.FirstError.Type.ShouldBe(ErrorType.Conflict);
        response.FirstError.Code.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);

        _repository.Verify(
            r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        messages.OfType<ProvisionKeycloakUserEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenUsernameAlreadyExists_ReturnsConflictError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("existinguser", "new@example.com");

        _repository.Setup(r => r.ExistsByEmailAsync(request.Email, ct)).ReturnsAsync(false);
        _repository
            .Setup(r => r.ExistsByUsernameAsync(AppUser.NormalizeUsername(request.Username), ct))
            .ReturnsAsync(true);

        (ErrorOr<UserResponse> response, OutgoingMessages messages) =
            await CreateUserCommandHandler.HandleAsync(
                new CreateUserCommand(request),
                _repository.Object,
                _unitOfWork.Object,
                ct
            );

        response.IsError.ShouldBeTrue();
        response.FirstError.Type.ShouldBe(ErrorType.Conflict);
        response.FirstError.Code.ShouldBe(ErrorCatalog.Users.UsernameAlreadyExists);

        _repository.Verify(
            r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        messages.OfType<ProvisionKeycloakUserEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenInvalidEmail_ReturnsValidationError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("charlie", "not-an-email");

        (ErrorOr<UserResponse> response, OutgoingMessages messages) =
            await CreateUserCommandHandler.HandleAsync(
                new CreateUserCommand(request),
                _repository.Object,
                _unitOfWork.Object,
                ct
            );

        response.IsError.ShouldBeTrue();
        response.FirstError.Type.ShouldBe(ErrorType.Validation);

        _repository.Verify(
            r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _repository.Verify(
            r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        messages.OfType<ProvisionKeycloakUserEvent>().ShouldBeEmpty();
    }
}
