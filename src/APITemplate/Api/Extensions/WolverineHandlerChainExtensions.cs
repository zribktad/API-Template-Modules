using Wolverine.Runtime.Handlers;

namespace APITemplate.Api.Extensions;

public static class WolverineHandlerChainExtensions
{
    public static bool ShouldApplyDataAnnotationsValidation(this HandlerChain chain)
    {
        return chain.Handlers.Any(handler => handler.Method.ReturnType.IsErrorOrReturnType());
    }
}
