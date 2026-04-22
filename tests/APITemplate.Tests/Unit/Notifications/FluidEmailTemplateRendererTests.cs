using Notifications.Contracts;
using Notifications.Services;
using SharedKernel.Application.Errors;
using Shouldly;
using Xunit;

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
        await Should.ThrowAsync<AppException>(() =>
            _sut.RenderAsync(UnknownTemplateId, new { }, TestContext.Current.CancellationToken)
        );
    }
}
