namespace BuildingBlocks.Application.Errors;

public sealed class AppException : Exception, IHasErrorCode, IHasErrorMetadata
{
    public string ErrorCode { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public AppException(
        string message,
        string? errorCode = null,
        Dictionary<string, object>? metadata = null,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? ErrorCatalog.General.Unknown;
        Metadata = metadata ?? new Dictionary<string, object>();
    }
}

