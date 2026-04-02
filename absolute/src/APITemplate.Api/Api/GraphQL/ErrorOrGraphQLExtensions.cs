using ErrorOr;
using HotChocolate;

namespace APITemplate.Api.GraphQL;

/// <summary>
/// Extension methods that convert <see cref="ErrorOr{T}"/> results to GraphQL-compatible
/// responses. Throws <see cref="GraphQLException"/> on non-NotFound errors, and returns
/// <c>default</c> for NotFound to preserve nullable query semantics.
/// </summary>
public static class ErrorOrGraphQLExtensions
{
    /// <summary>
    /// Unwraps the value on success, or throws <see cref="GraphQLException"/> on error.
    /// </summary>
    public static T ToGraphQLResult<T>(this ErrorOr<T> result)
    {
        if (!result.IsError)
            return result.Value;

        var firstError = result.FirstError;
        throw new GraphQLException(
            ErrorBuilder.New().SetMessage(firstError.Description).SetCode(firstError.Code).Build()
        );
    }

    /// <summary>
    /// Unwraps the value on success, returns <c>default</c> for NotFound errors
    /// (preserving nullable query semantics), or throws <see cref="GraphQLException"/>
    /// for other error types.
    /// </summary>
    public static T? ToGraphQLNullableResult<T>(this ErrorOr<T> result)
    {
        if (!result.IsError)
            return result.Value;

        if (result.FirstError.Type == ErrorType.NotFound)
            return default;

        var firstError = result.FirstError;
        throw new GraphQLException(
            ErrorBuilder.New().SetMessage(firstError.Description).SetCode(firstError.Code).Build()
        );
    }
}
