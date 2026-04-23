using ErrorOr;
using Identity.Auth.Entities;
using Identity.Directory.Domain.Services;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.Errors;
using Moq;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = Identity.Events.CacheTags;
using CreateUserCommand = Identity.Directory.Features.User.CreateUserCommand;
using CreateUserCommandHandler = Identity.Directory.Features.User.CreateUserCommandHandler;
using CreateUserRequest = Identity.Directory.Features.User.CreateUserRequest;
using ErrorCatalog = Identity.Errors.ErrorCatalog;
using IdentityUnitOfWork = SharedKernel.Domain.Interfaces.IUnitOfWork<Identity.IdentityDbMarker>;
using IUserRepository = Identity.Directory.Interfaces.IUserRepository;
using UserResponse = Identity.Directory.Features.User.UserResponse;

namespace APITemplate.Tests.Unit.Identity;

public sealed class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IUserUniquenessChecker> _uniqueness = new();
    private readonly Mock<IdentityUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task HandleAsync_Success_CreatesUserWithNullKeycloakIdAndEmitsProvisionEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("alice", "alice@example.com");
        CreateUserCommand command = new(request);

        _uniqueness
            .Setup(u => u.EnsureUniqueAsync(request.Username, It.IsAny<string>(), ct))
            .ReturnsAsync(Result.Success);

        AppUser? addedUser = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<AppUser>(), ct))
            .Callback<AppUser, CancellationToken>((u, _) => addedUser = u)
            .ReturnsAsync((AppUser u, CancellationToken _) => u);

        ErrorOr<Success> validation = await CreateUserCommandHandler.ValidateAsync(
            command,
            _uniqueness.Object,
            ct
        );
        (ErrorOr<UserResponse> response, OutgoingMessages messages) =
            await CreateUserCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                validation,
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
    public async Task ValidateAsync_WhenEmailAlreadyExists_ReturnsConflictError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("bob", "taken@example.com");

        _uniqueness
            .Setup(u => u.EnsureUniqueAsync(request.Username, It.IsAny<string>(), ct))
            .ReturnsAsync(DomainErrors.Users.EmailAlreadyExists(request.Email));

        ErrorOr<Success> result = await CreateUserCommandHandler.ValidateAsync(
            new CreateUserCommand(request),
            _uniqueness.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    [Fact]
    public async Task ValidateAsync_WhenUsernameAlreadyExists_ReturnsConflictError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CreateUserRequest request = new("existinguser", "new@example.com");

        _uniqueness
            .Setup(u => u.EnsureUniqueAsync(request.Username, It.IsAny<string>(), ct))
            .ReturnsAsync(DomainErrors.Users.UsernameAlreadyExists(request.Username));

        ErrorOr<Success> result = await CreateUserCommandHandler.ValidateAsync(
            new CreateUserCommand(request),
            _uniqueness.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.UsernameAlreadyExists);
    }
}
