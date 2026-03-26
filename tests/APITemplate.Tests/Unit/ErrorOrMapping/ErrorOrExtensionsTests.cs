using System.Net;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ErrorOrMapping;

public class ErrorOrExtensionsTests
{
    // ── ToActionResult ──────────────────────────────────────────────────────

    [Fact]
    public void ToActionResult_WhenSuccess_Returns200WithValue()
    {
        ErrorOr<string> result = "hello";

        var actionResult = result.ToActionResult(CreateController());

        var ok = actionResult.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBe("hello");
    }

    [Theory]
    [InlineData(ErrorType.NotFound, (int)HttpStatusCode.NotFound, "Not Found")]
    [InlineData(ErrorType.Validation, (int)HttpStatusCode.BadRequest, "Bad Request")]
    [InlineData(ErrorType.Conflict, (int)HttpStatusCode.Conflict, "Conflict")]
    [InlineData(ErrorType.Forbidden, (int)HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(ErrorType.Unauthorized, (int)HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(
        ErrorType.Failure,
        (int)HttpStatusCode.InternalServerError,
        "Internal Server Error"
    )]
    public void ToActionResult_WhenError_ReturnsProblemDetailsWithCorrectStatus(
        ErrorType errorType,
        int expectedStatus,
        string expectedTitle
    )
    {
        var error = errorType switch
        {
            ErrorType.NotFound => Error.NotFound("Error.Code", "Error description."),
            ErrorType.Validation => Error.Validation("Error.Code", "Error description."),
            ErrorType.Conflict => Error.Conflict("Error.Code", "Error description."),
            ErrorType.Forbidden => Error.Forbidden("Error.Code", "Error description."),
            ErrorType.Unauthorized => Error.Unauthorized("Error.Code", "Error description."),
            _ => Error.Failure("Error.Code", "Error description."),
        };
        ErrorOr<string> result = error;

        var actionResult = result.ToActionResult(CreateController());

        var obj = actionResult.Result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(expectedStatus);
        var problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(expectedStatus);
        problem.Title.ShouldBe(expectedTitle);
        problem.Detail.ShouldBe("Error description.");
    }

    [Fact]
    public void ToActionResult_WhenError_SetsErrorCodeExtension()
    {
        var error = Error.NotFound("Products.NotFound", "Product not found.");
        ErrorOr<string> result = error;

        var actionResult = result.ToActionResult(CreateController());

        var obj = actionResult.Result.ShouldBeOfType<ObjectResult>();
        var problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["errorCode"].ShouldBe("Products.NotFound");
    }

    [Fact]
    public void ToActionResult_WhenError_SetsTypeUri()
    {
        var error = Error.NotFound("Products.NotFound", "Product not found.");
        ErrorOr<string> result = error;

        var actionResult = result.ToActionResult(CreateController());

        var problem = ((ObjectResult)actionResult.Result!).Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe("https://api-template.local/errors/Products.NotFound");
    }

    [Fact]
    public void ToActionResult_WhenMultipleValidationErrors_AggregatesDetails()
    {
        var errors = new List<Error>
        {
            Error.Validation("Name", "Name is required."),
            Error.Validation("Price", "Price must be greater than zero."),
        };
        ErrorOr<string> result = errors;

        var actionResult = result.ToActionResult(CreateController());

        var problem = ((ObjectResult)actionResult.Result!).Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("Name is required.");
        problem.Detail.ShouldContain("Price must be greater than zero.");
    }

    [Fact]
    public void ToActionResult_WhenErrorHasMetadata_IncludesMetadataExtension()
    {
        var meta = new Dictionary<string, object> { ["field"] = "Name" };
        var error = Error.NotFound("Products.NotFound", "Not found.", meta);
        ErrorOr<string> result = error;

        var actionResult = result.ToActionResult(CreateController());

        var problem = ((ObjectResult)actionResult.Result!).Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions.ShouldContainKey("metadata");
    }

    // ── ToCreatedResult ─────────────────────────────────────────────────────

    [Fact]
    public void ToCreatedResult_WhenSuccess_Returns201Created()
    {
        ErrorOr<string> result = "created-value";
        var controller = CreateApiController();

        var actionResult = result.ToCreatedResult(controller, v => new { id = v });

        var created = actionResult.Result.ShouldBeOfType<CreatedAtActionResult>();
        created.Value.ShouldBe("created-value");
        created.ActionName.ShouldBe("GetById");
    }

    [Fact]
    public void ToCreatedResult_WhenError_ReturnsProblemDetails()
    {
        ErrorOr<string> result = Error.NotFound("X.NotFound", "Not found.");

        var actionResult = result.ToCreatedResult(CreateApiController(), v => new { id = v });

        var obj = actionResult.Result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    // ── ToNoContentResult ───────────────────────────────────────────────────

    [Fact]
    public void ToNoContentResult_WhenSuccess_Returns204()
    {
        ErrorOr<Success> result = Result.Success;

        var actionResult = result.ToNoContentResult(CreateController());

        actionResult.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public void ToNoContentResult_WhenError_ReturnsProblemDetails()
    {
        ErrorOr<Success> result = Error.NotFound("X.NotFound", "Not found.");

        var actionResult = result.ToNoContentResult(CreateController());

        var obj = actionResult.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    // ── ToBatchResult ───────────────────────────────────────────────────────

    [Fact]
    public void ToBatchResult_WhenSuccessNoFailures_Returns200()
    {
        ErrorOr<BatchResponse> result = new BatchResponse([], 1, 0);

        var actionResult = result.ToBatchResult(CreateApiController());

        var ok = actionResult.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<BatchResponse>();
    }

    [Fact]
    public void ToBatchResult_WhenSuccessWithFailures_Returns422()
    {
        var failures = new List<BatchResultItem> { new(0, Guid.NewGuid(), ["Invalid name."]) };
        ErrorOr<BatchResponse> result = new BatchResponse(failures, 0, 1);

        var actionResult = result.ToBatchResult(CreateApiController());

        var unprocessable = actionResult.Result.ShouldBeOfType<UnprocessableEntityObjectResult>();
        unprocessable.Value.ShouldBeOfType<BatchResponse>();
    }

    [Fact]
    public void ToBatchResult_WhenError_ReturnsProblemDetails()
    {
        ErrorOr<BatchResponse> result = Error.Validation("Items", "Items list cannot be empty.");

        var actionResult = result.ToBatchResult(CreateApiController());

        var obj = actionResult.Result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        obj.Value.ShouldBeOfType<ProblemDetails>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ControllerBase CreateController(string path = "/api/v1/test")
    {
        var controller = new FakeController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Request = { Path = path } },
            ActionDescriptor = new ControllerActionDescriptor(),
            RouteData = new RouteData(),
        };
        return controller;
    }

    private static FakeApiController CreateApiController(string path = "/api/v1/test")
    {
        var controller = new FakeApiController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Request = { Path = path } },
            ActionDescriptor = new ControllerActionDescriptor(),
            RouteData = new RouteData(),
        };
        return controller;
    }

    private class FakeController : ControllerBase;

    private class FakeApiController : ApiControllerBase;
}
