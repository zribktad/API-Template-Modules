using ErrorOr;
using Identity.Directory.Domain.Services;
using Identity.Errors;
using Identity.ValueObjects;
using Moq;
using Shouldly;
using Xunit;
using IUserRepository = Identity.Directory.Interfaces.IUserRepository;

namespace APITemplate.Tests.Unit.Identity;

public sealed class UserUniquenessCheckerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly UserUniquenessChecker _sut;

    public UserUniquenessCheckerTests()
    {
        _sut = new UserUniquenessChecker(_repository.Object);
    }

    [Fact]
    public async Task EnsureUniqueAsync_WhenEmailAndUsernameFree_ReturnsSuccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string email = "alice@example.com";
        _repository.Setup(r => r.ExistsByEmailAsync(email, ct)).ReturnsAsync(false);
        _repository
            .Setup(r => r.ExistsByUsernameAsync(NormalizedString.Normalize("alice"), ct))
            .ReturnsAsync(false);

        ErrorOr<Success> result = await _sut.EnsureUniqueAsync("alice", email, ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task EnsureUniqueAsync_WhenEmailTaken_ReturnsEmailConflict()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string email = "taken@example.com";
        _repository.Setup(r => r.ExistsByEmailAsync(email, ct)).ReturnsAsync(true);

        ErrorOr<Success> result = await _sut.EnsureUniqueAsync("anyone", email, ct);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    [Fact]
    public async Task EnsureUniqueAsync_WhenEmailTaken_DoesNotQueryUsername()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string email = "taken@example.com";
        _repository.Setup(r => r.ExistsByEmailAsync(email, ct)).ReturnsAsync(true);

        await _sut.EnsureUniqueAsync("irrelevant", email, ct);

        _repository.Verify(
            r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task EnsureUniqueAsync_WhenUsernameTaken_ReturnsUsernameConflict()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string email = "new@example.com";
        _repository.Setup(r => r.ExistsByEmailAsync(email, ct)).ReturnsAsync(false);
        _repository
            .Setup(r => r.ExistsByUsernameAsync(NormalizedString.Normalize("existing"), ct))
            .ReturnsAsync(true);

        ErrorOr<Success> result = await _sut.EnsureUniqueAsync("existing", email, ct);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Users.UsernameAlreadyExists);
    }

    [Fact]
    public async Task EnsureEmailUniqueAsync_UsesEmailValueExactly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string email = "foo@example.com";
        _repository.Setup(r => r.ExistsByEmailAsync(email, ct)).ReturnsAsync(false);

        ErrorOr<Success> result = await _sut.EnsureEmailUniqueAsync(email, ct);

        result.IsError.ShouldBeFalse();
        _repository.Verify(r => r.ExistsByEmailAsync(email, ct), Times.Once);
    }

    [Fact]
    public async Task EnsureUsernameUniqueAsync_NormalizesUsernameBeforeQuery()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string normalized = NormalizedString.Normalize("Mixed.Case");
        _repository.Setup(r => r.ExistsByUsernameAsync(normalized, ct)).ReturnsAsync(false);

        ErrorOr<Success> result = await _sut.EnsureUsernameUniqueAsync("Mixed.Case", ct);

        result.IsError.ShouldBeFalse();
        _repository.Verify(r => r.ExistsByUsernameAsync(normalized, ct), Times.Once);
    }
}
