namespace SharedKernel.Application.Errors;

public interface IHasErrorMetadata
{
    IReadOnlyDictionary<string, object> Metadata { get; }
}
