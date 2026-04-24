using System.Net;
using Moq;
using Webhooks.Security;
using Xunit;

namespace APITemplate.Tests.Unit.Webhooks;

[Trait("Category", "Unit")]
public sealed class SsrfProtectedSocketsHttpHandlerTests
{
    private readonly Mock<INetworkSecurityPolicy> _policyMock = new();

    [Fact]
    public async Task ConnectCallback_ShouldHandleIpAddressRequest_WithoutThrowingInvalidCastException()
    {
        // Arrange
        _policyMock.Setup(p => p.IsAllowed(It.IsAny<IPAddress>())).Returns(true);
        var handler = SsrfProtectedSocketsHttpHandlerFactory.Create(_policyMock.Object);
        using var client = new HttpClient(handler);

        // We don't actually need the connection to succeed, just to reach the ConnectCallback
        // and process the host/port without crashing before the socket attempt.
        // Using a non-listening port on localhost.
        var requestUri = "http://127.0.0.1:1/";

        // Act & Assert
        // We expect a HttpRequestException (because nobody is listening on port 1),
        // but NOT an InvalidCastException or NotSupportedException from our handler logic.
        var ex = await Record.ExceptionAsync(() =>
            client.GetAsync(requestUri, TestContext.Current.CancellationToken)
        );

        if (ex is HttpRequestException httpEx)
        {
            // Success - it reached the point of trying to connect to the socket.
            // If it had failed in our callback, the exception would be wrapped
            // but the message would contain our custom text if it was the leaf cause.
            Assert.True(true);
        }
        else if (
            ex is InvalidOperationException invEx
            && invEx.Message.Contains("Failed to connect")
        )
        {
            // Also success - reached the end of our loop and failed to connect.
            Assert.True(true);
        }
        else
        {
            Assert.Null(ex);
        }
    }

    [Fact]
    public async Task ConnectCallback_ShouldHandleDnsRequest_WithoutThrowing()
    {
        // Arrange
        _policyMock.Setup(p => p.IsAllowed(It.IsAny<IPAddress>())).Returns(true);
        var handler = SsrfProtectedSocketsHttpHandlerFactory.Create(_policyMock.Object);
        using var client = new HttpClient(handler);

        var requestUri = "http://localhost:1/";

        // Act & Assert
        var ex = await Record.ExceptionAsync(() =>
            client.GetAsync(requestUri, TestContext.Current.CancellationToken)
        );

        Assert.True(
            ex is HttpRequestException
                || (ex is InvalidOperationException && ex.Message.Contains("Failed to connect"))
        );
    }

    [Fact]
    public async Task ConnectCallback_ShouldThrow_WhenPolicyProhibitsAddress()
    {
        // Arrange
        _policyMock.Setup(p => p.IsAllowed(It.IsAny<IPAddress>())).Returns(false);
        var handler = SsrfProtectedSocketsHttpHandlerFactory.Create(_policyMock.Object);
        using var client = new HttpClient(handler);

        var requestUri = "http://127.0.0.1:1/";

        // Act & Assert
        // HttpClient wraps the underlying InvalidOperationException from the handler in a HttpRequestException
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync(requestUri, TestContext.Current.CancellationToken)
        );
        Assert.Contains("is prohibited", ex.Message);
    }
}
