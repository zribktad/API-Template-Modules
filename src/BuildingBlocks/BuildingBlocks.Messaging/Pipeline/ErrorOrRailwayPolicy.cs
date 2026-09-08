using ErrorOr;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace BuildingBlocks.Messaging.Pipeline;

/// <summary>
///     Wolverine handler policy that automatically inserts ErrorOrUnwrapFrame after every before-phase
///     method returning ErrorOr&lt;T&gt;, eliminating repetitive manual IsError guards.
/// </summary>
public sealed class ErrorOrRailwayPolicy : IHandlerPolicy
{
    public void Apply(
        IReadOnlyList<HandlerChain> chains,
        GenerationRules rules,
        IServiceContainer container
    )
    {
        foreach (HandlerChain chain in chains)
        {
            Type? responseErrorOrType = TryGetErrorOrResponseType(chain);
            if (responseErrorOrType is null)
            {
                continue;
            }

            chain.ApplyImpliedMiddlewareFromHandlers(rules);

            List<MethodCall> beforeCalls = chain
                .Middleware.OfType<MethodCall>()
                .Where(call => ClosesErrorOr(call.ReturnVariable?.VariableType))
                .ToList();

            foreach (MethodCall call in beforeCalls)
            {
                Variable beforeResult = call.ReturnVariable!;
                Type unwrappedType = beforeResult.VariableType.GetGenericArguments()[0];
                int index = chain.Middleware.IndexOf(call);
                chain.Middleware.Insert(
                    index + 1,
                    new ErrorOrUnwrapFrame(beforeResult, unwrappedType, responseErrorOrType)
                );
            }
        }
    }

    private static Type? TryGetErrorOrResponseType(HandlerChain chain)
    {
        foreach (MethodCall handler in chain.HandlerCalls())
        {
            Type? returnType = handler.ReturnType;
            if (returnType is null)
            {
                continue;
            }

            if (ClosesErrorOr(returnType))
            {
                return returnType;
            }

            if (IsValueTuple(returnType))
            {
                Type firstItem = returnType.GetGenericArguments()[0];
                if (ClosesErrorOr(firstItem))
                {
                    return firstItem;
                }
            }
        }

        return null;
    }

    private static bool ClosesErrorOr(Type? type) =>
        type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(ErrorOr<>);

    private static bool IsValueTuple(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTuple<,>);
}
