using System.ComponentModel.DataAnnotations;
using ErrorOr;
using SharedKernel.Application.Validation;

namespace ProductCatalog.GraphQL;

/// <summary>
///     Extension methods that convert <see cref="ErrorOr{T}" /> results to GraphQL-compatible
///     responses. Throws <see cref="GraphQLException" /> on non-NotFound errors, and returns
///     <c>default</c> for NotFound to preserve nullable query semantics.
/// </summary>
public static class ErrorOrGraphQLExtensions
{
    /// <summary>
    ///     Validates <paramref name="model" /> using the shared <see cref="IValidator" /> and throws
    ///     <see cref="GraphQLException" /> with code <c>GEN-0400</c> if any failures are found.
    /// </summary>
    public static void ValidateForGraphQL(this IValidator validator, object model)
    {
        IReadOnlyList<ValidationResult> failures = validator.Validate(model);
        if (failures.Count == 0)
            return;

        IError[] errors = failures
            .Select(f =>
                ErrorBuilder
                    .New()
                    .SetMessage(f.ErrorMessage ?? "Validation failed.")
                    .SetCode(SharedKernel.Application.Errors.ErrorCatalog.General.ValidationFailed)
                    .SetExtension("propertyName", string.Join(", ", f.MemberNames))
                    .Build()
            )
            .ToArray();

        throw new GraphQLException(errors);
    }

    /// <summary>
    ///     Unwraps the value on success, or throws <see cref="GraphQLException" /> on error.
    /// </summary>
    public static T ToGraphQLResult<T>(this ErrorOr<T> result)
    {
        if (!result.IsError)
            return result.Value;

        Error firstError = result.FirstError;
        throw new GraphQLException(
            ErrorBuilder.New().SetMessage(firstError.Description).SetCode(firstError.Code).Build()
        );
    }

    /// <summary>
    ///     Unwraps the value on success, returns <c>default</c> for NotFound errors
    ///     (preserving nullable query semantics), or throws <see cref="GraphQLException" />
    ///     for other error types.
    /// </summary>
    public static T? ToGraphQLNullableResult<T>(this ErrorOr<T> result)
    {
        if (!result.IsError)
            return result.Value;

        if (result.FirstError.Type == ErrorType.NotFound)
            return default;

        Error firstError = result.FirstError;
        throw new GraphQLException(
            ErrorBuilder.New().SetMessage(firstError.Description).SetCode(firstError.Code).Build()
        );
    }
}
