using Ardalis.Specification;

namespace APITemplate.Tests.Unit.Helpers;

internal static class SpecificationTestExtensions
{
    public static Func<T, bool> CompileSingleFilter<T>(this ISpecification<T> spec) =>
        spec.WhereExpressions.Single().Filter.Compile();

    public static List<Func<T, bool>> CompileFilters<T>(this ISpecification<T> spec) =>
        spec.WhereExpressions.Select(e => e.Filter.Compile()).ToList();
}
