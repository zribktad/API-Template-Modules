using APITemplate.Tests.Unit.Helpers;
using ErrorOr;
using Notifications.Contracts;
using Notifications.Services;
using Shouldly;
using Xunit;
using NTF = Notifications.Errors.ErrorCatalog;

namespace APITemplate.Tests.Unit.Notifications;

public sealed class FluidEmailTemplateRendererTests
{
    private static readonly string UnknownTemplateId =
        $"{EmailTemplateNames.UserRegistration}.fixture-unknown-template";

    private readonly FluidEmailTemplateRenderer _sut = new();

    [Fact]
    public async Task RenderAsync_UserRegistration_SubstitutesModel()
    {
        var model = new
        {
            Username = "Ada",
            Email = "ada@example.com",
            LoginUrl = "https://app/login",
        };

        ErrorOr<string> result = await _sut.RenderAsync(
            EmailTemplateNames.UserRegistration,
            model,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.ShouldContain("Ada");
        result.Value.ShouldContain("ada@example.com");
        result.Value.ShouldContain("https://app/login");
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplate_ReturnsNotFoundError()
    {
        ErrorOr<string> result = await _sut.RenderAsync(
            UnknownTemplateId,
            new { },
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(NTF.Templates.NotFound);
    }
}
