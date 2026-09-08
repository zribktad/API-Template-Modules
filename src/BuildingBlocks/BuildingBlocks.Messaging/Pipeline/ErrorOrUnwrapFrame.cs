using ErrorOr;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;
using Wolverine.Runtime;

namespace BuildingBlocks.Messaging.Pipeline;

/// <summary>
///     Generated middleware frame that unwraps ErrorOr before-phase results and short-circuits on failure.
/// </summary>
internal sealed class ErrorOrUnwrapFrame : AsyncFrame
{
    private readonly Variable _beforeResult;
    private readonly Type _responseErrorOrType;
    private readonly Variable? _unwrapped;
    private Variable? _context;

    public ErrorOrUnwrapFrame(Variable beforeResult, Type unwrappedType, Type responseErrorOrType)
    {
        _beforeResult = beforeResult;
        _responseErrorOrType = responseErrorOrType;

        if (unwrappedType != typeof(Success))
        {
            _unwrapped = new Variable(unwrappedType, this);
        }
    }

    public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
    {
        _context = chain.FindVariable(typeof(MessageContext));
        yield return _beforeResult;
        yield return _context;
    }

    public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
    {
        string before = _beforeResult.Usage;
        string context = _context!.Usage;
        string responseType = _responseErrorOrType.FullNameInCode();

        writer.Write($"BLOCK:if ({before}.IsError)");
        writer.Write(
            $"await {context}.EnqueueCascadingAsync(({responseType}){before}.Errors).ConfigureAwait(false);"
        );
        writer.Write("return;");
        writer.FinishBlock();

        if (_unwrapped is not null)
        {
            writer.Write($"var {_unwrapped.Usage} = {before}.Value;");
        }

        Next?.GenerateCode(method, writer);
    }
}
