using System.ComponentModel.DataAnnotations;
using ErrorOr;
using SharedKernel.Application.Validation;
using SharedKernelErrorCatalog = SharedKernel.Application.Errors.ErrorCatalog;
using HotChocolate;

namespace SharedKernel.GraphQL.Extensions;

/// <summary>
///     Extension methods that convert <see cref="ErrorOr{T}" /> results to GraphQL-compatible
///     responses. Throws <see cref="GraphQLException" /> on non-NotFound errors, and returns
///     <c>default</c> for NotFound to preserve nullable query semantics.
/// </summary>
public static class ErrorOrGraphQLExtensions
{
    /// <summary>
    ///     Validates <paramref name="model" /> and throws <see cref="GraphQLException" /> with
    ///     code <c>GEN-0400</c> if any failures are found.
    /// </summary>
    public static void ValidateForGraphQL<T>(this IValidator validator, T model) where T : notnull
    {
        IReadOnlyList<ValidationResult> failures = validator.Validate(model);
        if (failures.Count == 0)
        {
            return;
        }

        throw new GraphQLException(
            failures.Select(f =>
                ErrorBuilder
                    .New()
                    .SetMessage(f.ErrorMessage ?? ValidationConstants.ValidationFailedMessage)
                    .SetCode(SharedKernelErrorCatalog.General.ValidationFailed)
                    .SetExtension(ValidationConstants.PropertyNameKey, string.Join(", ", f.MemberNames))
                    .Build()
            )
        );
    }

    /// <summary>
    ///     Unwraps the value on success, or throws <see cref="GraphQLException" /> on error.
    ///     All errors are included in the exception; validation errors forward their
    ///     <c>propertyName</c> metadata as a GraphQL extension.
    /// </summary>
    public static T ToGraphQLResult<T>(this ErrorOr<T> result)
    {
        if (!result.IsError)
        {
            return result.Value;
        }

        throw new GraphQLException(
            result.Errors.Select(e =>
            {
                IErrorBuilder builder = ErrorBuilder
                    .New()
                    .SetMessage(e.Description)
                    .SetCode(e.Code);
                if (e.Metadata?.TryGetValue(ValidationConstants.PropertyNameKey, out object? pn) == true)
                {
                    builder = builder.SetExtension(ValidationConstants.PropertyNameKey, pn);
                }
                return builder.Build();
            })
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
        {
            return result.Value;
        }

        if (result.FirstError.Type == ErrorType.NotFound)
        {
            return default;
        }

        throw new GraphQLException(
            result.Errors.Select(e =>
            {
                IErrorBuilder builder = ErrorBuilder
                    .New()
                    .SetMessage(e.Description)
                    .SetCode(e.Code);
                if (e.Metadata?.TryGetValue(ValidationConstants.PropertyNameKey, out object? pn) == true)
                {
                    builder = builder.SetExtension(ValidationConstants.PropertyNameKey, pn);
                }
                return builder.Build();
            })
        );
    }
}
