namespace ProductCatalog.Features.Product.IdempotentCreate;

/// <summary>
///     FluentValidation validator for <see cref="IdempotentCreateRequest" /> that enforces
///     data-annotation constraints such as non-empty name and maximum length.
/// </summary>
public sealed class IdempotentCreateRequestValidator
    : DataAnnotationsValidator<IdempotentCreateRequest>;
