using Notifications.Contracts;
using Notifications.Services;
using SharedKernel.Application.Errors;
using Shouldly;
using Xunit;
using NTF = Notifications.Errors.ErrorCatalog;

namespace APITemplate.Tests.Unit.Notifications;

[Trait("Category", "Unit")]
public sealed class FluidEmailTemplateRendererTests
{
    private sealed record UserRegistrationModel(string Username, string Email, string LoginUrl);

    private static readonly string UnknownTemplateId =
        $"{EmailTemplateNames.UserRegistration}.fixture-unknown-template";

    private readonly FluidEmailTemplateRenderer _sut = new();

    [Fact]
    public async Task RenderAsync_UserRegistration_SubstitutesModel()
    {
        UserRegistrationModel model = new("Ada", "ada@example.com", "https://app/login");

        string result = await _sut.RenderAsync(
            EmailTemplateNames.UserRegistration,
            model,
            TestContext.Current.CancellationToken
        );

        result.ShouldContain("Ada");
        result.ShouldContain("ada@example.com");
        result.ShouldContain("https://app/login");
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplate_ThrowsAppException()
    {
        AppException ex = await Should.ThrowAsync<AppException>(() =>
            _sut.RenderAsync(UnknownTemplateId, new { }, TestContext.Current.CancellationToken)
        );

        ex.ErrorCode.ShouldBe(NTF.Templates.NotFound);
    }
}
