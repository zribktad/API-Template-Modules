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

        Type genericTypeDefinition = returnType.GetGenericTypeDefinition();
        if (genericTypeDefinition == typeof(Task<>) || genericTypeDefinition == typeof(ValueTask<>))
            return returnType.GetGenericArguments()[0].IsErrorOrReturnType();

        return genericTypeDefinition == typeof(ErrorOr<>);
    }

    internal static bool HasValidatorIn(this Type messageType, Assembly assembly)
    {
        Type validatorType = typeof(IValidator<>).MakeGenericType(messageType);

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition)
            .Any(type => validatorType.IsAssignableFrom(type));
    }
}
