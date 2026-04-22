using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notifications.Contracts;
using Notifications.Services;
using SharedKernel.Application.Errors;
using Shouldly;
using Xunit;
using NTF = Notifications.Errors.ErrorCatalog;

namespace APITemplate.Tests.Unit.Notifications;

public sealed class MailKitEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WhenUsernameConfiguredWithoutPassword_ThrowsAppException()
    {
        EmailOptions options = new()
        {
            SenderName = "System",
            SenderEmail = "system@example.com",
            SmtpHost = "localhost",
            SmtpPort = 25,
            Username = "mailer",
            Password = null,
        };
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");

        await using MailKitEmailSender sut = new(
            Options.Create(options),
            NullLogger<MailKitEmailSender>.Instance
        );

        AppException ex = await Should.ThrowAsync<AppException>(() =>
            sut.SendAsync(message, TestContext.Current.CancellationToken)
        );

        ex.ErrorCode.ShouldBe(NTF.Smtp.SendFailed);
        ex.Message.ShouldBe("SMTP password is missing.");
    }

    [Fact]
    public async Task SendAsync_WhenSmtpTransportFails_ThrowsAppException()
    {
        EmailOptions options = new()
        {
            SenderName = "System",
            SenderEmail = "system@example.com",
            SmtpHost = "127.0.0.1",
            SmtpPort = 1,
            UseSsl = false,
        };
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");

        await using MailKitEmailSender sut = new(
            Options.Create(options),
            NullLogger<MailKitEmailSender>.Instance
        );

        AppException ex = await Should.ThrowAsync<AppException>(() =>
            sut.SendAsync(message, TestContext.Current.CancellationToken)
        );

        ex.ErrorCode.ShouldBe(NTF.Smtp.SendFailed);
    }
}
