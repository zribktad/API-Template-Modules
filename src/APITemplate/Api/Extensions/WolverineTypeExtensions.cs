using ErrorOr;

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
}
