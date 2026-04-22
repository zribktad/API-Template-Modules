using SharedKernel.Application.Errors;
using Shouldly;

namespace APITemplate.Tests.Unit.Helpers;

public static class AppExceptionAssertions
{
    public static async Task<AppException> ShouldThrowAppExceptionAsync(
        this Func<Task> action,
        string expectedErrorCode
    )
    {
        AppException ex = await Should.ThrowAsync<AppException>(action);
        ex.ErrorCode.ShouldBe(expectedErrorCode);
        return ex;
    }
}
