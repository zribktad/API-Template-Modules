using APITemplate.Tests.Unit.Infrastructure;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Domain.Interfaces;
using ErrorOr;
using Identity;
using Identity.Common.Email;
using Identity.Directory.Entities;
using Identity.Directory.Features.TenantInvitation;
using Identity.Directory.Features.TenantInvitation.DTOs;
using Identity.Directory.Interfaces;
using Identity.Directory.Options;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = Identity.Common.Events.CacheTags;

namespace APITemplate.Tests.Unit.Identity.TenantInvitations;

[Trait("Category", "Unit")]
public sealed class TenantInvitationCommandHandlersTests
{
    private readonly Mock<ITenantInvitationRepository> _invitationRepository = new();
    private readonly Mock<ITenantRepository> _tenantRepository = new();
    private readonly Mock<ISecureTokenGenerator> _tokens = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork<IdentityDbMarker>> _unitOfWork = new();

    [Fact]
    public async Task Create_WhenPendingInvitationExists_ShouldReturnConflict()
    {
        _invitationRepository
            .Setup(r =>
                r.HasPendingInvitationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        (ErrorOr<TenantInvitationResponse> result, _) =
            await CreateTenantInvitationCommandHandler.HandleAsync(
                new CreateTenantInvitationCommand(
                    new CreateTenantInvitationRequest("user@example.com")
                ),
                _invitationRepository.Object,
                _tenantRepository.Object,
                _unitOfWork.Object,
                _tokens.Object,
                _tenantProvider.Object,
                TimeProvider.System,
                Options.Create(new TenantInvitationOptions { BaseUrl = "https://app.local" }),
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task Accept_WhenTokenNotFound_ShouldReturnError()
    {
        _tokens.Setup(t => t.HashToken("token")).Returns("hashed");
        _invitationRepository
            .Setup(r => r.GetNonRevokedByTokenHashAsync("hashed", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantInvitation?)null);

        (ErrorOr<Success> result, _) = await AcceptTenantInvitationCommandHandler.HandleAsync(
            new AcceptTenantInvitationCommand("token"),
            _invitationRepository.Object,
            _unitOfWork.Object,
            _tokens.Object,
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task Resend_WhenInvitationPending_ShouldRefreshTokenAndEmitNotification()
    {
        Guid tenantId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        TenantInvitation invitation = DomainTestDataFactory.TenantInvitation(
            now: DateTimeOffset.UtcNow
        );
        invitation.Id = invitationId;
        _tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        _invitationRepository
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _tenantRepository
            .Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tenant.Create(tenantId, "default", "Default"));
        _tokens.Setup(t => t.GenerateToken()).Returns("raw");
        _tokens.Setup(t => t.HashToken("raw")).Returns("hashed-raw");

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await ResendTenantInvitationCommandHandler.HandleAsync(
                new ResendTenantInvitationCommand(invitationId),
                _invitationRepository.Object,
                _tenantRepository.Object,
                _unitOfWork.Object,
                _tokens.Object,
                _tenantProvider.Object,
                TimeProvider.System,
                Options.Create(new TenantInvitationOptions { BaseUrl = "https://app.local" }),
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        messages.ShouldContainSingleCacheTag(CacheTags.TenantInvitations);
    }

    [Fact]
    public async Task Revoke_WhenInvitationFound_ShouldUpdateAndEmitCacheTag()
    {
        Guid invitationId = Guid.NewGuid();
        TenantInvitation invitation = DomainTestDataFactory.TenantInvitation();
        invitation.Id = invitationId;
        _invitationRepository
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await RevokeTenantInvitationCommandHandler.HandleAsync(
                new RevokeTenantInvitationCommand(invitationId),
                _invitationRepository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        invitation.Status.ShouldBe(global::Identity.Directory.Enums.InvitationStatus.Revoked);
        messages.ShouldContainSingleCacheTag(CacheTags.TenantInvitations);
    }
}
