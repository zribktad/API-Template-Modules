using Microsoft.Extensions.Options;
using Moq;
using Notifications.Contracts;
using Notifications.Features;
using SharedKernel.Application.Errors;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

[Trait("Category", "Unit")]
public sealed class NotificationEmailHandlerTests
{
    private readonly Mock<IEmailTemplateRenderer> _templateRenderer = new();

    [Fact]
    public async Task TenantInvitationHandleAsync_WhenRenderFails_ThrowsAppException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TenantInvitationCreatedNotification @event = new(
            Guid.NewGuid(),
            "user@example.com",
            "Tenant",
            "token",
            "https://app/invite",
            24
        );
        _templateRenderer
            .Setup(r => r.RenderAsync(EmailTemplateNames.TenantInvitation, It.IsAny<object>(), ct))
            .ThrowsAsync(new AppException("Missing template", "NTF-0500-TEMPLATE-NOT-FOUND"));

        AppException ex = await Should.ThrowAsync<AppException>(() =>
            TenantInvitationEmailHandler.HandleAsync(@event, _templateRenderer.Object, ct)
        );

        ex.ErrorCode.ShouldBe("NTF-0500-TEMPLATE-NOT-FOUND");
    }

    [Fact]
    public async Task UserRegisteredHandleAsync_WhenRenderFails_ThrowsAppException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UserRegisteredNotification @event = new(Guid.NewGuid(), "user@example.com", "user");
        IOptions<EmailOptions> options = Options.Create(
            new EmailOptions { BaseUrl = "https://app" }
        );
        _templateRenderer
            .Setup(r => r.RenderAsync(EmailTemplateNames.UserRegistration, It.IsAny<object>(), ct))
            .ThrowsAsync(new AppException("Template parse failed", "NTF-0500-TEMPLATE-PARSE"));

        AppException ex = await Should.ThrowAsync<AppException>(() =>
            UserRegisteredEmailHandler.HandleAsync(@event, _templateRenderer.Object, options, ct)
        );

        ex.ErrorCode.ShouldBe("NTF-0500-TEMPLATE-PARSE");
    }

    [Fact]
    public async Task UserRoleChangedHandleAsync_WhenRenderFails_ThrowsAppException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UserRoleChangedNotification @event = new(
            Guid.NewGuid(),
            "user@example.com",
            "user",
            "Viewer",
            "Admin"
        );
        _templateRenderer
            .Setup(r => r.RenderAsync(EmailTemplateNames.UserRoleChanged, It.IsAny<object>(), ct))
            .ThrowsAsync(new AppException("Missing template", "NTF-0500-TEMPLATE-NOT-FOUND"));

        AppException ex = await Should.ThrowAsync<AppException>(() =>
            UserRoleChangedEmailHandler.HandleAsync(@event, _templateRenderer.Object, ct)
        );

        ex.ErrorCode.ShouldBe("NTF-0500-TEMPLATE-NOT-FOUND");
    }

    [Fact]
    public async Task TenantInvitationHandleAsync_WhenRenderSucceeds_EmitsEmailMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TenantInvitationCreatedNotification @event = new(
            Guid.NewGuid(),
            "user@example.com",
            "Tenant",
            "token",
            "https://app/invite",
            24
        );
        _templateRenderer
            .Setup(r => r.RenderAsync(EmailTemplateNames.TenantInvitation, It.IsAny<object>(), ct))
            .ReturnsAsync("<p>Hello</p>");

        OutgoingMessages result = await TenantInvitationEmailHandler.HandleAsync(
            @event,
            _templateRenderer.Object,
            ct
        );

        result.OfType<EmailMessage>().Single().HtmlBody.ShouldBe("<p>Hello</p>");
    }
}
