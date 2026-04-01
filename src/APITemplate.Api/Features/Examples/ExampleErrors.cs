using ErrorOr;

namespace APITemplate.Api.Features.Examples;

/// <summary>
/// Error factory methods for example/showcase features.
/// </summary>
public static class ExampleErrors
{
    public static Error InvalidPatchDocument(string message) =>
        Error.Validation(code: "EXA-0400-PATCH", description: $"Invalid patch document: {message}");
}
