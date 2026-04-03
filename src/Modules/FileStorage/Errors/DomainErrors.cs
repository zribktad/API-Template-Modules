using ErrorOr;

namespace FileStorage.Domain;

public static class DomainErrors
{
    public static class Files
    {
        public static Error FileNotFound(string fileName) =>
            Error.NotFound(
                code: ErrorCatalog.Files.FileNotFound,
                description: $"File '{fileName}' not found."
            );

        public static Error InvalidFileType(string extension) =>
            Error.Validation(
                code: ErrorCatalog.Files.InvalidFileType,
                description: $"File type '{extension}' is not allowed."
            );

        public static Error FileTooLarge(long maxSize) =>
            Error.Validation(
                code: ErrorCatalog.Files.FileTooLarge,
                description: $"File exceeds maximum size of {maxSize} bytes."
            );

        public static Error InvalidPatchDocument(string message) =>
            Error.Validation(
                code: ErrorCatalog.Files.InvalidPatchDocument,
                description: message
            );

        public static Error WebhookInvalidSignature() =>
            Error.Unauthorized(
                code: ErrorCatalog.Files.WebhookInvalidSignature,
                description: "Invalid webhook signature."
            );

        public static Error WebhookMissingHeaders() =>
            Error.Unauthorized(
                code: ErrorCatalog.Files.WebhookMissingHeaders,
                description: "Required webhook headers are missing."
            );
    }
}






