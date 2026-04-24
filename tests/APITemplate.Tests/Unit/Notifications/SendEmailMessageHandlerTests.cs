using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Notifications.Contracts;
using Notifications.Handlers;
using Polly;
using SharedKernel.Application.Errors;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

[Trait("Category", "Unit")]
public sealed class SendEmailMessageHandlerTests
{
    private readonly Mock<IEmailSender> _senderMock = new();
    private readonly Mock<ISmtpSendPipelineProvider> _pipelineProviderMock = new();
    private readonly Mock<IFailedEmailStore> _failedEmailStoreMock = new();

    public SendEmailMessageHandlerTests()
    {
        _pipelineProviderMock.Setup(p => p.Get()).Returns(ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task HandleAsync_WhenSendSucceeds_DoesNotStoreFailedEmail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");

        _senderMock.Setup(s => s.SendAsync(message, ct)).Returns(Task.CompletedTask);

        await SendEmailMessageHandler.HandleAsync(
            message,
            _senderMock.Object,
            _pipelineProviderMock.Object,
            _failedEmailStoreMock.Object,
            NullLogger<SendEmailMessageHandler>.Instance,
            ct
        );

        _senderMock.Verify(s => s.SendAsync(message, ct), Times.Once);
        _failedEmailStoreMock.Verify(
            s =>
                s.StoreFailedAsync(
                    It.IsAny<EmailMessage>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSendThrowsAppException_StoresFailedEmail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");

        _senderMock
            .Setup(s => s.SendAsync(message, ct))
            .ThrowsAsync(new AppException("SMTP unavailable", "NTF-0500-SMTP-SEND"));

        await SendEmailMessageHandler.HandleAsync(
            message,
            _senderMock.Object,
            _pipelineProviderMock.Object,
            _failedEmailStoreMock.Object,
            NullLogger<SendEmailMessageHandler>.Instance,
            ct
        );

        _failedEmailStoreMock.Verify(
            s => s.StoreFailedAsync(message, "SMTP unavailable", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSendThrowsAppException_DoesNotRethrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");

        _senderMock
            .Setup(s => s.SendAsync(message, ct))
            .ThrowsAsync(new AppException("SMTP error", "NTF-0500-SMTP-SEND"));

        await Should.NotThrowAsync(() =>
            SendEmailMessageHandler.HandleAsync(
                message,
                _senderMock.Object,
                _pipelineProviderMock.Object,
                _failedEmailStoreMock.Object,
                NullLogger<SendEmailMessageHandler>.Instance,
                ct
            )
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSendThrows_StoresFailedEmail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");
        InvalidOperationException ex = new("SMTP unavailable");

        _senderMock.Setup(s => s.SendAsync(message, ct)).ThrowsAsync(ex);

        await SendEmailMessageHandler.HandleAsync(
            message,
            _senderMock.Object,
            _pipelineProviderMock.Object,
            _failedEmailStoreMock.Object,
            NullLogger<SendEmailMessageHandler>.Instance,
            ct
        );

        _failedEmailStoreMock.Verify(
            s => s.StoreFailedAsync(message, ex.Message, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenCancelled_Rethrows()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        EmailMessage message = new("user@example.com", "Subject", "<p>Body</p>");

        _senderMock
            .Setup(s => s.SendAsync(message, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            SendEmailMessageHandler.HandleAsync(
                message,
                _senderMock.Object,
                _pipelineProviderMock.Object,
                _failedEmailStoreMock.Object,
                NullLogger<SendEmailMessageHandler>.Instance,
                cts.Token
            )
        );
    }
}
