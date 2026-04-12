using Identity;
using Identity.Auth.Entities;
using Identity.Directory.Entities;
using Identity.Directory.Features.TenantInvitation.Specifications;
using Identity.Directory.Features.User;
using Identity.Directory.Interfaces;
using Identity.Directory.Security;
using Identity.Persistence;
using Identity.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class ResolveAppUserAccessHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly string KeycloakSub = "kc-sub-1";
    private const string UserEmail = "user@example.com";

    [Fact]
    public async Task HandleAsync_WhenUserExistsByKeycloakId_ReturnsAllowed()
    {
        AppUser existing = AppUser.Create(
            "user",
            Email.FromPersistence(UserEmail),
            KeycloakSub,
            TenantId
        );

        Mock<IUserRepository> users = new();
        users
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByKeycloakUserIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existing);

        UserAccessResolution result = await InvokeAsync(users);

        result.IsAllowed.ShouldBeTrue();
        result.User.ShouldBe(existing);
    }

    [Fact]
    public async Task HandleAsync_WhenNoUserAndNoInvitations_ReturnsNoInvitation()
    {
        Mock<IUserRepository> users = new();
        users
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByKeycloakUserIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AppUser?)null);
        users
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserUnlinkedByNormalizedEmailSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AppUser?)null);

        Mock<ITenantInvitationRepository> invites = new();
        invites
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<AcceptedInvitationByNormalizedEmailSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((TenantInvitation?)null);
        invites
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<LatestInvitationByNormalizedEmailSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((TenantInvitation?)null);

        UserAccessResolution result = await InvokeAsync(users, invites);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorCode.ShouldBe(UserAccessErrorCodes.NoInvitation);
    }

    [Fact]
    public async Task HandleAsync_WhenLatestInvitationPendingAndNotExpired_ReturnsPending()
    {
        Mock<IUserRepository> users = new();
        users
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByKeycloakUserIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AppUser?)null);
        users
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserUnlinkedByNormalizedEmailSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AppUser?)null);

        TenantInvitation pending = TenantInvitation.Create(
            Email.FromPersistence(UserEmail),
            "hash",
            expiryHours: 48,
            TimeProvider.System
        );
        pending.TenantId = TenantId;

        Mock<ITenantInvitationRepository> invites = new();
        invites
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<AcceptedInvitationByNormalizedEmailSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((TenantInvitation?)null);
        invites
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<LatestInvitationByNormalizedEmailSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(pending);

        UserAccessResolution result = await InvokeAsync(users, invites);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorCode.ShouldBe(UserAccessErrorCodes.PendingInvitation);
    }

    private static Task<UserAccessResolution> InvokeAsync(
        Mock<IUserRepository> users,
        Mock<ITenantInvitationRepository>? invites = null
    )
    {
        invites ??= new Mock<ITenantInvitationRepository>();
        Mock<IUnitOfWork<IdentityDbMarker>> uow = new();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return ResolveAppUserAccessHandler.HandleAsync(
            new ResolveAppUserAccessQuery(KeycloakSub, UserEmail, "user"),
            users.Object,
            invites.Object,
            uow.Object,
            TimeProvider.System,
            NullLogger<ResolveAppUserAccessHandler>.Instance,
            TestContext.Current.CancellationToken
        );
    }
}
