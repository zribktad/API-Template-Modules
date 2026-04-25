using System.Runtime.CompilerServices;
using Moq;
using StackExchange.Redis;

namespace APITemplate.Tests.Unit.Identity.Mocks;

internal sealed class RedisConnectionMultiplexerMockBuilder
{
    private readonly Mock<IConnectionMultiplexer> _multiplexer = new();
    private readonly Mock<ISubscriber> _subscriber = new();

    public RedisConnectionMultiplexerMockBuilder()
    {
        _multiplexer.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(_subscriber.Object);
        _subscriber
            .Setup(s =>
                s.SubscribeAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.CompletedTask);
        _subscriber
            .Setup(s =>
                s.UnsubscribeAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<Action<RedisChannel, RedisValue>?>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.CompletedTask);
        _subscriber
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(0L);
    }

    public RedisConnectionMultiplexerMockBuilder WithConnected(bool isConnected)
    {
        _multiplexer.SetupGet(m => m.IsConnected).Returns(isConnected);
        return this;
    }

    public RedisConnectionMultiplexerMockBuilder WithCapturedSubscribeHandler(
        out Func<Action<RedisChannel, RedisValue>?> getHandler
    )
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        getHandler = () => capturedHandler;

        _subscriber
            .Setup(s =>
                s.SubscribeAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler
            )
            .Returns(Task.CompletedTask);

        return this;
    }

    public RedisConnectionMultiplexerMockBuilder WithPublishThrowing<TException>()
        where TException : Exception
    {
        Exception exception = CreateException<TException>();
        _subscriber
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ThrowsAsync(exception);

        return this;
    }

    public RedisConnectionMultiplexerMockBuilder WithSubscribeThrowing<TException>()
        where TException : Exception
    {
        Exception exception = CreateException<TException>();
        _subscriber
            .Setup(s =>
                s.SubscribeAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ThrowsAsync(exception);

        return this;
    }

    public RedisConnectionMultiplexerMockBuilder WithFirstSubscribeThrowing<TException>()
        where TException : Exception
    {
        Exception exception = CreateException<TException>();
        _subscriber
            .SetupSequence(s =>
                s.SubscribeAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ThrowsAsync(exception)
            .Returns(Task.CompletedTask);

        return this;
    }

    public RedisConnectionMultiplexerMockBuilder WithPublishTask(Task<long> task)
    {
        _subscriber
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(task);

        return this;
    }

    public RedisConnectionMultiplexerMockHarness Build() => new(_multiplexer, _subscriber);

    private static Exception CreateException<TException>()
        where TException : Exception
    {
        if (typeof(TException) == typeof(RedisConnectionException))
            return new RedisConnectionException(ConnectionFailureType.UnableToConnect, "boom");

        if (typeof(TException) == typeof(ObjectDisposedException))
            return new ObjectDisposedException("redis");

        if (typeof(TException) == typeof(OperationCanceledException))
            return new OperationCanceledException();

        return (Exception)Activator.CreateInstance(typeof(TException), "boom")!;
    }
}

internal sealed class RedisConnectionMultiplexerMockHarness(
    Mock<IConnectionMultiplexer> multiplexer,
    Mock<ISubscriber> subscriber
)
{
    public Mock<IConnectionMultiplexer> Multiplexer { get; } = multiplexer;
    public Mock<ISubscriber> Subscriber { get; } = subscriber;
    public IConnectionMultiplexer Object => Multiplexer.Object;

    public void RaiseConnectionRestored()
    {
        ConnectionFailedEventArgs args = (ConnectionFailedEventArgs)
            RuntimeHelpers.GetUninitializedObject(typeof(ConnectionFailedEventArgs));

        Multiplexer.Raise(m => m.ConnectionRestored += null, args);
    }
}
