using System.Net.Http;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class UserRequestHandlersTests
{
    private readonly Mock<IUserRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMessageBus> _busMock;
    private readonly Mock<IKeycloakAdminService> _keycloakAdminMock;

    public UserRequestHandlersTests()
    {
        _repositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _busMock = new Mock<IMessageBus>();
        _keycloakAdminMock = new Mock<IKeycloakAdminService>();
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUserResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var expected = new UserResponse(
            Guid.NewGuid(),
            "testuser",
            "test@example.com",
            true,
            UserRole.User,
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

        var result = await GetUserByIdQueryHandler.HandleAsync(
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

        var result = await GetUserByIdQueryHandler.HandleAsync(
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
        var ct = TestContext.Current.CancellationToken;
        var filter = new UserFilter(PageNumber: 1, PageSize: 10);
        var items = new List<UserResponse>
        {
            new(Guid.NewGuid(), "user1", "user1@test.com", true, UserRole.User, DateTime.UtcNow),
            new(
                Guid.NewGuid(),
                "user2",
                "user2@test.com",
                true,
                UserRole.PlatformAdmin,
                DateTime.UtcNow
            ),
        };

        var paged = new PagedResponse<UserResponse>(items, 2, 1, 10);
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

        var result = await GetUsersQueryHandler.HandleAsync(
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

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ReturnsCreatedUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateUserRequest("newuser", "new@example.com");
        var keycloakId = "keycloak-user-id-123";

        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _keycloakAdminMock
            .Setup(k =>
                k.CreateUserAsync(request.Username, request.Email, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(keycloakId);
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser u, CancellationToken _) => u);

        var loggerMock = new Mock<ILogger<CreateUserCommandHandler>>();
        var result = await CreateUserCommandHandler.HandleAsync(
            new CreateUserCommand(request),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            loggerMock.Object,
            _keycloakAdminMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Username.ShouldBe("newuser");
        result.Value.Email.ShouldBe("new@example.com");
        result.Value.Id.ShouldNotBe(Guid.Empty);
        result.Value.IsActive.ShouldBeTrue();
        result.Value.Role.ShouldBe(UserRole.User);

        _keycloakAdminMock.Verify(
            k => k.CreateUserAsync(request.Username, request.Email, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<UserRegisteredNotification>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenKeycloakFails_ThrowsAndDoesNotSaveUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateUserRequest("failuser", "fail@example.com");

        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _keycloakAdminMock
            .Setup(k =>
                k.CreateUserAsync(request.Username, request.Email, It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new HttpRequestException("Keycloak unavailable"));

        var loggerMock = new Mock<ILogger<CreateUserCommandHandler>>();

        await Should.ThrowAsync<HttpRequestException>(() =>
            CreateUserCommandHandler.HandleAsync(
                new CreateUserCommand(request),
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                _busMock.Object,
                loggerMock.Object,
                _keycloakAdminMock.Object,
                ct
            )
        );

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenEmailExists_ReturnsConflictError()
    {
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("existing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<CreateUserCommandHandler>>();
        var result = await CreateUserCommandHandler.HandleAsync(
            new CreateUserCommand(new CreateUserRequest("user", "existing@test.com")),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            loggerMock.Object,
            _keycloakAdminMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    [Fact]
    public async Task CreateAsync_WhenUsernameExists_ReturnsConflictError()
    {
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<CreateUserCommandHandler>>();
        var result = await CreateUserCommandHandler.HandleAsync(
            new CreateUserCommand(new CreateUserRequest("existinguser", "new@test.com")),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            loggerMock.Object,
            _keycloakAdminMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.UsernameAlreadyExists);
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_WhenUserExists_UpdatesFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("updated@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateUserCommandHandler.HandleAsync(
            new UpdateUserCommand(
                user.Id,
                new UpdateUserRequest("updateduser", "updated@test.com")
            ),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        user.Username.ShouldBe("updateduser");
        user.Email.ShouldBe("updated@test.com");
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenSameEmailAndUsername_SkipsUniquenessCheck()
    {
        var user = CreateTestUser();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await UpdateUserCommandHandler.HandleAsync(
            new UpdateUserCommand(user.Id, new UpdateUserRequest(user.Username, user.Email)),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
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
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var result = await UpdateUserCommandHandler.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), new UpdateUserRequest("name", "e@e.com")),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewEmailExists_ReturnsConflictError()
    {
        var user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("taken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await UpdateUserCommandHandler.HandleAsync(
            new UpdateUserCommand(user.Id, new UpdateUserRequest(user.Username, "taken@test.com")),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    // --- ActivateAsync / DeactivateAsync ---

    [Fact]
    public async Task ActivateAsync_SetsIsActiveToTrue()
    {
        var user = CreateTestUser(isActive: false);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(user.Id, IsActive: true),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _keycloakAdminMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        user.IsActive.ShouldBeTrue();
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        var user = CreateTestUser(isActive: true);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(user.Id, IsActive: false),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _keycloakAdminMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        user.IsActive.ShouldBeFalse();
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(Guid.NewGuid(), IsActive: true),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _keycloakAdminMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- ChangeRoleAsync ---

    [Fact]
    public async Task ChangeRoleAsync_ChangesUserRole()
    {
        var user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var loggerMock = new Mock<ILogger<ChangeUserRoleCommandHandler>>();
        var result = await ChangeUserRoleCommandHandler.HandleAsync(
            new ChangeUserRoleCommand(user.Id, new ChangeUserRoleRequest(UserRole.PlatformAdmin)),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            loggerMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        user.Role.ShouldBe(UserRole.PlatformAdmin);
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<UserRoleChangedNotification>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var loggerMock = new Mock<ILogger<ChangeUserRoleCommandHandler>>();
        var result = await ChangeUserRoleCommandHandler.HandleAsync(
            new ChangeUserRoleCommand(
                Guid.NewGuid(),
                new ChangeUserRoleRequest(UserRole.PlatformAdmin)
            ),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            loggerMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDeleteAndCommits()
    {
        var user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await DeleteUserCommandHandler.HandleAsync(
            new DeleteUserCommand(user.Id),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _keycloakAdminMock.Object,
            new Mock<ILogger<DeleteUserCommandHandler>>().Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        _repositoryMock.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var result = await DeleteUserCommandHandler.HandleAsync(
            new DeleteUserCommand(Guid.NewGuid()),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _keycloakAdminMock.Object,
            new Mock<ILogger<DeleteUserCommandHandler>>().Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    // --- Helpers ---

    private static AppUser CreateTestUser(bool isActive = true, UserRole role = UserRole.User)
    {
        return new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            NormalizedUsername = AppUser.NormalizeUsername("testuser"),
            Email = "test@example.com",
            NormalizedEmail = AppUser.NormalizeEmail("test@example.com"),
            IsActive = isActive,
            Role = role,
            TenantId = Guid.NewGuid(),
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
        };
    }
}
