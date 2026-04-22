using APITemplate.Tests.Unit.Helpers;
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

        string html = await _sut.RenderAsync(
            EmailTemplateNames.UserRegistration,
            model,
            TestContext.Current.CancellationToken
        );

        html.ShouldContain("Ada");
        html.ShouldContain("ada@example.com");
        html.ShouldContain("https://app/login");
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplate_Throws()
    {
        await (
            (Func<Task>)(
                () =>
                    _sut.RenderAsync(
                        UnknownTemplateId,
                        new { },
                        TestContext.Current.CancellationToken
                    )
            )
        ).ShouldThrowAppExceptionAsync(NTF.Templates.NotFound);
    }
}
