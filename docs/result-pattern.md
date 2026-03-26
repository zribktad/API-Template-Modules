# Result Pattern (Phase 2 Plan)

This project currently uses exceptions for business failures and `IExceptionHandler` for HTTP translation.

`Result<T>` is planned as a selective next step for expected business outcomes, while unexpected failures continue to use exceptions.

## When to use `Result<T>`

- Validation errors expected from business rules
- Not found scenarios that are part of normal control flow
- Conflict/forbidden outcomes expected by domain policy

## When to use exceptions

- Infrastructure failures (database outage, network, serialization)
- Programming/runtime defects
- Any unexpected condition that should bubble to global exception handling

## Rollout approach

1. Keep `ApiExceptionHandler` as fallback for unknown errors.
2. Pilot `Result<T>` in `ProductReviews` (`create/get/delete` paths).
3. Validate readability, test impact, and API contract consistency.
4. Expand to other services only after the pilot stabilizes.
