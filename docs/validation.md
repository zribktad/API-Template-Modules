# How to Add Input Validation

This project uses a hybrid validation approach:

- `DataAnnotationsValidator<T>` for simple per-field rules declared directly on DTOs
- `FluentValidation` for cross-field, conditional, composed, and reusable rules
- `FluentValidationActionFilter` to execute registered validators automatically before controller actions

---

## Overview

The validation pipeline is:

```text
HTTP Request body/query
  → Model binding
  → FluentValidationActionFilter
      → resolves IValidator<T> from DI
      → runs DataAnnotationsValidator<T> and any extra FluentValidation rules
  → If invalid: 400 Bad Request with ValidationProblemDetails
  → If valid: controller action runs
```

Registration is done in DI and MVC setup:

```csharp
services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationActionFilter>();
});

services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
```

There is no per-validator registration and no `AddFluentValidationAutoValidation()` middleware in the current implementation.

---

## Rule of Thumb

Use:

- Data Annotations for required fields, length, ranges, email format, and other single-field rules
- `DataAnnotationsValidator<T>` as the validator base when a request is mostly attribute-driven
- FluentValidation rules only for logic that cannot be expressed cleanly with attributes

This keeps DTO contracts explicit while still allowing richer validation where needed.

---

## Step 1 - Add Data Annotations to the DTO

For simple rules, put validation directly on the request DTO.

```csharp
using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

public sealed record CreateProductRequest(
    [NotEmpty(ErrorMessage = "Product name is required.")]
    [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
    string Name,
    string? Description,
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    decimal Price,
    Guid? CategoryId = null) : IProductRequest;
```

The custom `NotEmptyAttribute` is available in `Application/Common/Validation/NotEmptyAttribute.cs` for strings and collections where plain `[Required]` is not enough.

---

## Step 2 - Bridge Attributes Through a Validator

Create a validator that inherits from `DataAnnotationsValidator<T>`.

```csharp
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.ProductReview.Validation;

public sealed class CreateProductReviewRequestValidator
    : DataAnnotationsValidator<CreateProductReviewRequest>;
```

This base class:

- runs `Validator.TryValidateObject(...)`
- validates constructor parameter attributes for record DTOs
- converts attribute failures into FluentValidation failures

Use this pattern when the request only needs attribute-based validation.

---

## Step 3 - Add Cross-Field or Conditional Rules

When validation depends on multiple fields, extend `DataAnnotationsValidator<T>` and add FluentValidation rules.

```csharp
using APITemplate.Application.Common.Validation;
using FluentValidation;

public abstract class ProductRequestValidatorBase<T> : DataAnnotationsValidator<T>
    where T : class, IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required for products priced above 1000.")
            .When(x => x.Price > 1000);
    }
}
```

Then reuse it:

```csharp
public sealed class CreateProductRequestValidator
    : ProductRequestValidatorBase<CreateProductRequest>;

public sealed class UpdateProductRequestValidator
    : ProductRequestValidatorBase<UpdateProductRequest>;
```

---

## Step 4 - Compose Shared Validators

For filters and reusable request parts, compose validators with `Include(...)`.

```csharp
using FluentValidation;

public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new DateRangeFilterValidator<ProductFilter>());
        Include(new SortableFilterValidator<ProductFilter>(ProductSortFields.Map.AllowedNames));

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);
    }
}
```

Existing reusable validators live under `Application/Common/Validation/`.

---

## Step 5 - Async Validation

When a rule needs I/O, inject a dependency and use `MustAsync`.

```csharp
using FluentValidation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(ICustomerRepository customerRepository)
    {
        RuleFor(x => x.CustomerId)
            .MustAsync(async (id, ct) => await customerRepository.ExistsAsync(id, ct))
            .WithMessage("Customer does not exist.");
    }
}
```

Use this sparingly. Business invariants that depend on broader domain state often belong in the service layer instead.

---

## Service-Level Validation

For business rule violations discovered after request validation, throw the domain `ValidationException`.

```csharp
public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct)
{
    var existing = await _repository.GetByNameAsync(request.Name, ct);
    if (existing is not null)
        throw new ValidationException("A product with the same name already exists.");

    // continue
}
```

`ApiExceptionHandler` converts this into HTTP `400`.

---

## Validation Error Response

Validation failures return HTTP `400 Bad Request` as `ValidationProblemDetails`.

```json
{
  "errors": {
    "Price": [
      "Price must be greater than zero."
    ],
    "Description": [
      "Description is required for products priced above 1000."
    ]
  },
  "title": "One or more validation errors occurred.",
  "status": 400
}
```

---

## Testing Guidance

Use two patterns depending on where the rule lives:

- Attribute-driven validators: instantiate the validator and call `Validate(...)`
- Pure FluentValidation rules: use `FluentValidation.TestHelper`

The existing tests under `tests/APITemplate.Tests/Unit/Validators/` show both styles.

---

## Checklist

- [ ] Add Data Annotation attributes to the DTO for simple field rules
- [ ] Create `<RequestType>Validator.cs` in `Application/Features/<Feature>/Validation/`
- [ ] Inherit from `DataAnnotationsValidator<T>` when attributes should be enforced
- [ ] Add FluentValidation rules only for cross-field, conditional, or composed logic
- [ ] Reuse `PaginationFilterValidator`, `DateRangeFilterValidator<T>`, and `SortableFilterValidator<T>` where applicable
- [ ] Rely on assembly scanning; no per-validator DI registration is needed

---

## Key Files

| File | Purpose |
|------|---------|
| `Application/Common/Validation/DataAnnotationsValidator.cs` | Bridges Data Annotations into FluentValidation |
| `Application/Common/Validation/NotEmptyAttribute.cs` | Custom attribute for non-empty string/collection checks |
| `Application/Common/Validation/PaginationFilterValidator.cs` | Shared pagination validation |
| `Application/Common/Validation/DateRangeFilterValidator.cs` | Shared date-range validation |
| `Application/Common/Validation/SortableFilterValidator.cs` | Shared sort parameter validation |
| `Api/Filters/FluentValidationActionFilter.cs` | Runs validators for controller action arguments |
| `Application/Features/Product/Validation/ProductRequestValidatorBase.cs` | Hybrid validator example |
| `Application/Features/ProductReview/Validation/CreateProductReviewRequestValidator.cs` | Attribute-only validator example |
| `Domain/Exceptions/ValidationException.cs` | Service-layer validation error |
| `Api/ExceptionHandling/ApiExceptionHandler.cs` | Converts validation exceptions to HTTP 400 |
