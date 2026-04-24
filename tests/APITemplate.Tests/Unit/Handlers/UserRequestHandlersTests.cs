using ErrorOr;
using Identity.Auth.Entities;
using Identity.Directory.Domain.Services;
using Identity.Directory.Entities;
using Identity.Directory.Enums;
using Identity.Directory.Features.User;
using Moq;
using SharedKernel.Contracts.Events;
using SharedKernel.Domain.Common;
using Shouldly;
using Wolverine;
using Xunit;
using ErrorCatalog = Identity.Errors.ErrorCatalog;
using IdentityUnitOfWork = SharedKernel.Domain.Interfaces.IUnitOfWork<Identity.IdentityDbMarker>;
using IUserRepository = Identity.Directory.Interfaces.IUserRepository;

namespace APITemplate.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class UserRequestHandlersTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IdentityUnitOfWork> _unitOfWorkMock = new();
    private IUserUniquenessChecker Uniqueness => new UserUniquenessChecker(_repositoryMock.Object);

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUserResponse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UserResponse expected = new(
            Guid.NewGuid(),
            "testuser",
            "test@example.com",
            true,
            UserRole.User,
            ProvisioningStatus.Completed,
            DateTime.UtcNow
        );

        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        ErrorOr<UserResponse> result = await GetUserByIdQueryHandler.HandleAsync(
            new GetUserByIdQuery(expected.Id),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(expected.Id);
        result.Value.Username.ShouldBe("testuser");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((UserResponse?)null);

        ErrorOr<UserResponse> result = await GetUserByIdQueryHandler.HandleAsync(
            new GetUserByIdQuery(Guid.NewGuid()),
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- GetPagedAsync ---

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResponse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UserFilter filter = new(PageNumber: 1, PageSize: 10);
        List<UserResponse> items =
        [
            new(
                Guid.NewGuid(),
                "user1",
                "user1@test.com",
                true,
                UserRole.User,
                ProvisioningStatus.Completed,
                DateTime.UtcNow
            ),
            new(
                Guid.NewGuid(),
                "user2",
                "user2@test.com",
                true,
                UserRole.PlatformAdmin,
                ProvisioningStatus.Completed,
                DateTime.UtcNow
            ),
        ];

        PagedResponse<UserResponse> paged = new(items, 2, 1, 10);
        _repositoryMock
            .Setup(r =>
                r.GetPagedAsync(
                    It.IsAny<UserFilterSpecification>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(paged);

        ErrorOr<PagedResponse<UserResponse>> result = await GetUsersQueryHandler.HandleAsync(
            new GetUsersQuery(filter),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Items.Count().ShouldBe(2);
        result.Value.TotalCount.ShouldBe(2);
        result.Value.PageNumber.ShouldBe(1);
        result.Value.PageSize.ShouldBe(10);
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_WhenUserExists_UpdatesFields()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppUser user = CreateTestUser();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("updated@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        UpdateUserCommand command = new(
            user.Id,
            new UpdateUserRequest("updateduser", "updated@test.com")
        );

        ErrorOr<AppUser> validation = await UpdateUserCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            Uniqueness,
            ct
        );
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await UpdateUserCommandHandler.HandleAsync(
                command,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                validation,
                ct
            );

        result.IsError.ShouldBeFalse();
        user.Username.Value.ShouldBe("updateduser");
        user.Email.Value.ShouldBe("updated@test.com");
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        messages.OfType<CacheInvalidationNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UpdateAsync_WhenSameEmailAndUsername_SkipsUniquenessCheck()
    {
        AppUser user = CreateTestUser();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        UpdateUserCommand command = new(
            user.Id,
            new UpdateUserRequest(user.Username.Value, user.Email.Value)
        );

        ErrorOr<AppUser> validation = await UpdateUserCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            Uniqueness,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await UpdateUserCommandHandler.HandleAsync(
                command,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                validation,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        _repositoryMock.Verify(
            r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _repositoryMock.Verify(
            r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        messages.OfType<CacheInvalidationNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UpdateAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        UpdateUserCommand command = new(Guid.NewGuid(), new UpdateUserRequest("name", "e@e.com"));

        ErrorOr<AppUser> validation = await UpdateUserCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            Uniqueness,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, _) = await UpdateUserCommandHandler.HandleAsync(
            command,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            validation,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewEmailExists_ReturnsConflictError()
    {
        AppUser user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("taken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        UpdateUserCommand command = new(
            user.Id,
            new UpdateUserRequest(user.Username.Value, "taken@test.com")
        );

        ErrorOr<AppUser> validation = await UpdateUserCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            Uniqueness,
            TestContext.Current.CancellationToken
        );

        validation.IsError.ShouldBeTrue();
        validation.FirstError.Type.ShouldBe(ErrorType.Conflict);
        validation.FirstError.Code.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    // --- ActivateAsync / DeactivateAsync ---

    [Fact]
    public async Task ActivateAsync_SetsIsActiveToTrue()
    {
        AppUser user = CreateTestUser(isActive: false);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        SetUserActiveCommand command = new(user.Id, IsActive: true);

        ErrorOr<AppUser> validation = await SetUserActiveCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await SetUserActiveCommandHandler.HandleAsync(
                command,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                validation,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        user.IsActive.ShouldBeTrue();
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        messages.OfType<CacheInvalidationNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        AppUser user = CreateTestUser(isActive: true);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        SetUserActiveCommand command = new(user.Id, IsActive: false);

        ErrorOr<AppUser> validation = await SetUserActiveCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await SetUserActiveCommandHandler.HandleAsync(
                command,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                validation,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        user.IsActive.ShouldBeFalse();
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        messages.OfType<CacheInvalidationNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ActivateAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        SetUserActiveCommand command = new(Guid.NewGuid(), IsActive: true);

        ErrorOr<AppUser> validation = await SetUserActiveCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, _) = await SetUserActiveCommandHandler.HandleAsync(
            command,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            validation,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- ChangeRoleAsync ---

    [Fact]
    public async Task ChangeRoleAsync_ChangesUserRole()
    {
        AppUser user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ChangeUserRoleCommand command = new(
            user.Id,
            new ChangeUserRoleRequest(UserRole.PlatformAdmin)
        );

        ErrorOr<AppUser> validation = await ChangeUserRoleCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await ChangeUserRoleCommandHandler.HandleAsync(
                command,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                validation,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        user.Role.ShouldBe(UserRole.PlatformAdmin);
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        messages.OfType<UserRoleChangedNotification>().ShouldHaveSingleItem();
        messages.OfType<CacheInvalidationNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        ChangeUserRoleCommand command = new(
            Guid.NewGuid(),
            new ChangeUserRoleRequest(UserRole.PlatformAdmin)
        );

        ErrorOr<AppUser> validation = await ChangeUserRoleCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, _) = await ChangeUserRoleCommandHandler.HandleAsync(
            command,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            validation,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDeleteAndCommits()
    {
        AppUser user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        DeleteUserCommand command = new(user.Id);

        ErrorOr<AppUser> validation = await DeleteUserCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await DeleteUserCommandHandler.HandleAsync(
                command,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                validation,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        _repositoryMock.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        messages.OfType<CacheInvalidationNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DeleteAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        DeleteUserCommand command = new(Guid.NewGuid());

        ErrorOr<AppUser> validation = await DeleteUserCommandHandler.ValidateAsync(
            command,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );
        (ErrorOr<Success> result, _) = await DeleteUserCommandHandler.HandleAsync(
            command,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            validation,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- Helpers ---

    private static AppUser CreateTestUser(bool isActive = true, UserRole role = UserRole.User)
    {
        return AppUser.Create(
            username: "testuser",
            email: "test@example.com",
            keycloakUserId: "keycloak-test-id",
            isActive: isActive
        );
    }
}
