namespace FileStorage.Domain;

public static class ErrorCatalog
{
    public static class Files
    {
        public const string FileNotFound = "EXA-0404-FILE";
        public const string InvalidFileType = "EXA-0400-FILE";
        public const string FileTooLarge = "EXA-0400-SIZE";
        public const string BlobConflict = "EXA-0409-BLOB";
        public const string InvalidPatchDocument = "EXA-0400-PATCH";
        public const string PathTraversal = "EXA-0403-PATH";
        public const string WebhookInvalidSignature = "EXA-0401-WEBHOOK";
        public const string WebhookMissingHeaders = "EXA-0401-WEBHOOK-HDR";
        public const string UploadTokenNotFound = "EXA-0404-UPLOAD";
        public const string CommitAfterTerminalState = "EXA-0410-UPLOAD";
    }
}
