using APITemplate.Application.Common.DTOs;

namespace APITemplate.Application.Common.Validation;

/// <summary>
/// Validates <see cref="PaginationFilter"/> instances by running all Data Annotation attributes
/// declared on the record's properties and constructor parameters.
/// </summary>
public sealed class PaginationFilterValidator : DataAnnotationsValidator<PaginationFilter>;
