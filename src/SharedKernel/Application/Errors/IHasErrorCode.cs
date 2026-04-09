namespace SharedKernel.Application.Errors;

public interface IHasErrorCode
{
    string ErrorCode { get; }
}
