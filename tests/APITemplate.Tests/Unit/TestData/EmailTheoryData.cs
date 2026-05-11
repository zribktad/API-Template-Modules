using Xunit;

namespace APITemplate.Tests.Unit.TestData;

/// <summary>Shared inputs for Email parametrized tests.</summary>
public static class EmailTheoryData
{
    public static IEnumerable<object?[]> InvalidRawInputs()
    {
        yield return [null];
        yield return [""];
        yield return ["   "];
        yield return ["not-an-email"];
        yield return [$"{new string('a', 316)}@x.co"];
    }

    public static TheoryData<string, string> TrimmingAndNormalizationCases =>
        new() { { "  user@example.com  ", "user@example.com" }, { "A@B.CO", "A@B.CO" } };
}
