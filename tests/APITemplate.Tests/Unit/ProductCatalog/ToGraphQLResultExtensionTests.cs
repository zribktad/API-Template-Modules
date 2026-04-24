using ErrorOr;
using HotChocolate;
using ProductCatalog.GraphQL;
using SharedKernel.Application.Errors;
using Shouldly;
using Xunit;
using DomainError = ErrorOr.Error;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class ToGraphQLResultExtensionTests
{
    [Fact]
    public void ToGraphQLResult_Success_ReturnsValue()
    {
        ErrorOr<string> result = "ok";

        string value = result.ToGraphQLResult();

        value.ShouldBe("ok");
    }

    [Fact]
    public void ToGraphQLResult_SingleError_ThrowsGraphQLException()
    {
        ErrorOr<string> result = DomainError.Validation("GEN-0400", "Invalid value.");

        GraphQLException ex = Should.Throw<GraphQLException>(() => result.ToGraphQLResult());

        ex.Errors.Count.ShouldBe(1);
        ex.Errors[0].Code.ShouldBe("GEN-0400");
        ex.Errors[0].Message.ShouldBe("Invalid value.");
    }

    [Fact]
    public void ToGraphQLResult_MultipleErrors_ThrowsAllErrors()
    {
        ErrorOr<string> result = new List<DomainError>
        {
            DomainError.Validation("GEN-0400", "First error."),
            DomainError.Validation("GEN-0400", "Second error."),
        };

        GraphQLException ex = Should.Throw<GraphQLException>(() => result.ToGraphQLResult());

        ex.Errors.Count.ShouldBe(2);
        ex.Errors.ShouldAllBe(e => e.Code == "GEN-0400");
    }

    [Fact]
    public void ToGraphQLResult_ErrorWithPropertyNameMetadata_ForwardsExtension()
    {
        ErrorOr<string> result = DomainError.Validation(
            ErrorCatalog.General.ValidationFailed,
            "MinPrice must be >= 0.",
            new Dictionary<string, object> { ["propertyName"] = "MinPrice" }
        );

        GraphQLException ex = Should.Throw<GraphQLException>(() => result.ToGraphQLResult());

        IError error = ex.Errors[0];
        error.Extensions.ShouldNotBeNull();
        error.Extensions!.ContainsKey("propertyName").ShouldBeTrue();
        error.Extensions["propertyName"].ShouldBe("MinPrice");
    }

    [Fact]
    public void ToGraphQLResult_ErrorWithoutMetadata_DoesNotSetPropertyNameExtension()
    {
        ErrorOr<string> result = DomainError.Failure("GEN-0500", "Unexpected error.");

        GraphQLException ex = Should.Throw<GraphQLException>(() => result.ToGraphQLResult());

        IError error = ex.Errors[0];
        bool hasPropertyName = error.Extensions?.ContainsKey("propertyName") == true;
        hasPropertyName.ShouldBeFalse();
    }
}
