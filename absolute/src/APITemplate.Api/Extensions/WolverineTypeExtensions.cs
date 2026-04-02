using System.Reflection;
using ErrorOr;
using FluentValidation;

namespace APITemplate.Api.Extensions;

internal static class WolverineTypeExtensions
{
    internal static bool IsErrorOrReturnType(this Type returnType)
    {
        if (!returnType.IsGenericType)
            return false;

        var genericTypeDefinition = returnType.GetGenericTypeDefinition();

        if (genericTypeDefinition == typeof(Task<>) || genericTypeDefinition == typeof(ValueTask<>))
            return returnType.GetGenericArguments()[0].IsErrorOrReturnType();

        return genericTypeDefinition == typeof(ErrorOr<>);
    }

    internal static bool HasValidatorIn(this Type messageType, Assembly assembly)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(messageType);
        return assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition)
            .Any(type => validatorType.IsAssignableFrom(type));
    }
}
