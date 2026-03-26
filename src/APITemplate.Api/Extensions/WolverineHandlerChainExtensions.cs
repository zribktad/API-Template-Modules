using System.Reflection;
using Wolverine.Runtime.Handlers;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Wolverine handler-chain helpers used during bootstrapping to keep Program.cs focused on
/// orchestration rather than reflection-based policy rules.
/// </summary>
public static class WolverineHandlerChainExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when the chain handles a message with a registered validator
    /// and at least one handler returns <c>ErrorOr&lt;T&gt;</c> directly or through Task/ValueTask.
    /// </summary>
    public static bool ShouldApplyErrorOrValidation(
        this HandlerChain chain,
        Assembly validatorAssembly
    ) =>
        chain.MessageType.HasValidatorIn(validatorAssembly)
        && chain.Handlers.Any(h => h.Method.ReturnType.IsErrorOrReturnType());
}
