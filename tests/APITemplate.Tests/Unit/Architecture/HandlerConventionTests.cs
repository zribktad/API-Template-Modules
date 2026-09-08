using System.Reflection;
using BuildingBlocks.Application.Modules;
using Shouldly;
using Wolverine.Attributes;
using Xunit;

namespace APITemplate.Tests.Unit.Architecture;

[Trait("Category", "Unit")]
public class HandlerConventionTests
{
    private static readonly IReadOnlyList<Type> Handlers = AppModuleLoader
        .Discover()
        .SelectMany(m => m.Assemblies)
        .Distinct()
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type is { IsClass: true, IsAbstract: false } && IsHandlerType(type))
        .ToList();

    [Fact]
    public void Handlers_should_be_sealed()
    {
        List<string> violations = Handlers
            .Where(handler => !handler.IsSealed)
            .Select(handler => $"{handler.FullName} — handler must be 'sealed'.")
            .ToList();

        violations.ShouldBeEmpty(
            "Wolverine command/query/event handlers must be sealed classes:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations)
        );
    }

    [Fact]
    public void Handlers_should_not_declare_a_Validate_method()
    {
        const BindingFlags flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Static
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly;
        List<string> violations = new();

        foreach (Type handler in Handlers)
        {
            IEnumerable<MethodInfo> validateMethods = handler
                .GetMethods(flags)
                .Where(method =>
                    method.Name.StartsWith("Validate", StringComparison.Ordinal)
                    && !method.Name.EndsWith("Command", StringComparison.Ordinal)
                    && !method.Name.EndsWith("Query", StringComparison.Ordinal)
                );

            foreach (MethodInfo method in validateMethods)
            {
                // Private helper methods inside handler are allowed if they are simple local validators
                if (
                    !method.IsPublic
                    && !method
                        .GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Wolverine"))
                )
                {
                    continue;
                }

                violations.Add(
                    $"{handler.Name}.{method.Name} — Wolverine special-cases 'Validate*', so its ErrorOr<Success> "
                        + "is never published to subsequent phases. Rename to 'Ensure*'."
                );
            }
        }

        violations.ShouldBeEmpty(
            "Handlers must not declare public Validate* lifecycle methods (use Ensure* guards):"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations)
        );
    }

    private static bool IsHandlerType(Type type) =>
        type.Name.EndsWith("CommandHandler", StringComparison.Ordinal)
        || type.Name.EndsWith("QueryHandler", StringComparison.Ordinal)
        || (
            type.Name.StartsWith("On", StringComparison.Ordinal)
            && type.Name.EndsWith("Handler", StringComparison.Ordinal)
        );
}
