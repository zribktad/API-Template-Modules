using System.Reflection;
using Wolverine.Runtime.Handlers;

namespace APITemplate.Api.Extensions;

public static class WolverineHandlerChainExtensions
{
    public static bool ShouldApplyErrorOrValidation(
        this HandlerChain chain,
        IReadOnlyList<Assembly> validatorAssemblies
    )
    {
        return validatorAssemblies.Any(chain.MessageType.HasValidatorIn)
            && chain.Handlers.Any(handler => handler.Method.ReturnType.IsErrorOrReturnType());
    }
}
