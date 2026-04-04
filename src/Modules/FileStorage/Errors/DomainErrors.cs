namespace FileStorage.Domain;

public static class DomainErrors
{
    public static class Files
    {
        public static Error FileNotFound(string fileName)
        {
            return Error.NotFound(ErrorCatalog.Files.FileNotFound, $"File '{fileName}' not found.");
        }

        public static Error InvalidFileType(string extension)
        {
            return Error.Validation(
                ErrorCatalog.Files.InvalidFileType,
                $"File type '{extension}' is not allowed."
            );
        }

        public static Error FileTooLarge(long maxSize)
        {
            return Error.Validation(
                ErrorCatalog.Files.FileTooLarge,
                $"File exceeds maximum size of {maxSize} bytes."
            );
        }

        public static Error InvalidPatchDocument(string message)
        {
            return Error.Validation(ErrorCatalog.Files.InvalidPatchDocument, message);
        }

        public static Error WebhookInvalidSignature()
        {
            return Error.Unauthorized(
                ErrorCatalog.Files.WebhookInvalidSignature,
                "Invalid webhook signature."
            );
        }

        public static Error WebhookMissingHeaders()
        {
            return Error.Unauthorized(
                ErrorCatalog.Files.WebhookMissingHeaders,
                "Required webhook headers are missing."
            );
        }
    }
}
