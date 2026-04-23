using System.ComponentModel.DataAnnotations;
using ErrorOr;
using ProductCatalog.GraphQL;
using SharedKernel.Application.Validation;
using SharedKernelErrorCatalog = SharedKernel.Application.Errors.ErrorCatalog;

namespace ProductCatalog.Features.Category.GetCategories;

/// <summary>Returns a paginated, filtered, and sorted list of categories.</summary>
public sealed record GetCategoriesQuery(CategoryFilter Filter);

/// <summary>Handles <see cref="GetCategoriesQuery" />.</summary>
public sealed class GetCategoriesQueryHandler
{
    public static ErrorOr<Success> Validate(GetCategoriesQuery query, IValidator validator)
    {
        IReadOnlyList<ValidationResult> failures = validator.Validate(query.Filter);
        if (failures.Count == 0)
            return Result.Success;
        return failures
            .Select(f => Error.Validation(
                SharedKernelErrorCatalog.General.ValidationFailed,
                f.ErrorMessage ?? "Validation failed.",
                new Dictionary<string, object> { ["propertyName"] = string.Join(", ", f.MemberNames) }))
            .ToList<Error>();
    }

    public static async Task<ErrorOr<PagedResponse<CategoryResponse>>> HandleAsync(
        GetCategoriesQuery request,
        ErrorOr<Success> validation,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        if (validation.IsError)
            return validation.Errors;

        return await repository.GetPagedAsync(
            new CategorySpecification(request.Filter),
            request.Filter.PageNumber,
            request.Filter.PageSize,
            ct
        );
    }

    public static CategoryPageResult PostProcess(ErrorOr<PagedResponse<CategoryResponse>> result)
        => new(result.ToGraphQLResult());
}
